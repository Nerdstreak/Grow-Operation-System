import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { HomeAssistantSettingsDto, SensorMetricType, SettingsOverviewDto, TentDto, UpdateTentRequest, UpdateTentSensorRequest } from '../types'
import { V1Alert, V1Button, V1Empty, V1Field, V1Page, V1Section, V1Switch, V1Tabs, toNullableString } from '../components/v1'
import { classNames } from '../utils'

type GroupKey = 'tent' | 'reservoir' | 'hardware'
type SensorDraft = { metricType: SensorMetricType; haEntityId: string; displayLabel: string; isActive: boolean }
type TentMappingDraft = { cameraEntityId: string; sensors: SensorDraft[] }
type EntityDefinition = { metricType: SensorMetricType; label: string; group: GroupKey; placeholder: string; unit?: string }

type SavingState = 'ha' | `tent-${number}` | null

const groups: Array<{ key: GroupKey; label: string }> = [
  { key: 'tent', label: 'Zelt' },
  { key: 'reservoir', label: 'RDWC/DWC' },
  { key: 'hardware', label: 'Technik' },
]

const definitions: EntityDefinition[] = [
  { metricType: 'AirTemperature', label: 'Luft', group: 'tent', placeholder: 'sensor.zelt_temperatur', unit: '°C' },
  { metricType: 'Humidity', label: 'RLF', group: 'tent', placeholder: 'sensor.zelt_luftfeuchte', unit: '%' },
  { metricType: 'Vpd', label: 'VPD', group: 'tent', placeholder: 'sensor.zelt_vpd', unit: 'kPa' },
  { metricType: 'Ppfd', label: 'PPFD', group: 'tent', placeholder: 'sensor.lampe_ppfd', unit: 'µmol/m²/s' },
  { metricType: 'Co2', label: 'CO₂', group: 'tent', placeholder: 'sensor.zelt_co2', unit: 'ppm' },
  { metricType: 'LightStatus', label: 'Licht', group: 'tent', placeholder: 'switch.licht' },
  { metricType: 'ReservoirPh', label: 'pH', group: 'reservoir', placeholder: 'sensor.rdwc_ph' },
  { metricType: 'ReservoirEc', label: 'EC', group: 'reservoir', placeholder: 'sensor.rdwc_ec', unit: 'mS/cm' },
  { metricType: 'ReservoirWaterTemp', label: 'Wasser °C', group: 'reservoir', placeholder: 'sensor.rdwc_wassertemperatur', unit: '°C' },
  { metricType: 'ReservoirLevel', label: 'Wasserstand', group: 'reservoir', placeholder: 'sensor.rdwc_wasserstand', unit: 'L/cm' },
  { metricType: 'ReservoirOrp', label: 'ORP', group: 'reservoir', placeholder: 'sensor.rdwc_orp', unit: 'mV' },
  { metricType: 'ReservoirDissolvedOxygen', label: 'DO', group: 'reservoir', placeholder: 'sensor.rdwc_do', unit: 'mg/L' },
  { metricType: 'PumpCirculation', label: 'Umwälzpumpe', group: 'hardware', placeholder: 'switch.rdwc_pumpe' },
  { metricType: 'PumpAir', label: 'Luftpumpe', group: 'hardware', placeholder: 'switch.luftpumpe' },
  { metricType: 'Chiller', label: 'Chiller', group: 'hardware', placeholder: 'climate.chiller' },
  { metricType: 'UpsStatus', label: 'USV', group: 'hardware', placeholder: 'sensor.usv_status' },
]

function HomeAssistantPage() {
  const [ha, setHa] = useState<HomeAssistantSettingsDto>({ baseUrl: '', accessToken: '', enabled: false })
  const [tents, setTents] = useState<TentDto[]>([])
  const [drafts, setDrafts] = useState<Record<number, TentMappingDraft>>({})
  const [selectedTentId, setSelectedTentId] = useState<number | null>(null)
  const [activeGroup, setActiveGroup] = useState<GroupKey>('tent')
  const [showToken, setShowToken] = useState(false)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState<SavingState>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const overview = await apiFetch<SettingsOverviewDto>('/api/settings', { signal: controller.signal })
        setHa(overview.homeAssistant)
        setTents(overview.tents)
        setDrafts(Object.fromEntries(overview.tents.map((tent) => [tent.id, createTentDraft(tent)])))
        setSelectedTentId((current) => current ?? overview.tents[0]?.id ?? null)
      } catch (caught) {
        if (!controller.signal.aborted) setError(formatApiError(caught, 'Home Assistant konnte nicht geladen werden.'))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  const selectedTent = useMemo(() => tents.find((tent) => tent.id === selectedTentId) ?? tents[0] ?? null, [selectedTentId, tents])
  const selectedDraft = selectedTent ? drafts[selectedTent.id] : null
  const mappedCount = useMemo(() => (Object.values(drafts) as TentMappingDraft[]).reduce((sum, draft) => sum + (draft.cameraEntityId.trim() ? 1 : 0) + draft.sensors.filter((sensor) => sensor.isActive && sensor.haEntityId.trim()).length, 0), [drafts])

  async function saveConnection(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving('ha')
    setError(null)
    setMessage(null)
    try {
      const saved = await apiFetch<HomeAssistantSettingsDto>('/api/settings/home-assistant', { method: 'PUT', body: JSON.stringify({ baseUrl: toNullableString(ha.baseUrl), accessToken: toNullableString(ha.accessToken), enabled: ha.enabled }) })
      setHa(saved)
      setMessage('Verbindung gespeichert.')
    } catch (caught) {
      setError(formatApiError(caught, 'Home Assistant konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function saveSelectedTent() {
    if (!selectedTent || !selectedDraft) return
    setSaving(`tent-${selectedTent.id}`)
    setError(null)
    setMessage(null)
    try {
      const saved = await apiFetch<TentDto>(`/api/settings/tents/${selectedTent.id}`, { method: 'PUT', body: JSON.stringify(toUpdateTentRequest(selectedTent, selectedDraft)) })
      setTents((current) => current.map((tent) => tent.id === saved.id ? saved : tent))
      setDrafts((current) => ({ ...current, [saved.id]: createTentDraft(saved) }))
      setMessage(`${saved.name} gespeichert.`)
    } catch (caught) {
      setError(formatApiError(caught, 'Entity-Mapping konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  function updateCamera(value: string) {
    if (!selectedTent) return
    setDrafts((current) => ({ ...current, [selectedTent.id]: { ...current[selectedTent.id], cameraEntityId: value } }))
  }

  function updateSensor(metricType: SensorMetricType, patch: Partial<SensorDraft>) {
    if (!selectedTent) return
    setDrafts((current) => ({ ...current, [selectedTent.id]: { ...current[selectedTent.id], sensors: current[selectedTent.id].sensors.map((sensor) => sensor.metricType === metricType ? { ...sensor, ...patch } : sensor) } }))
  }

  return (
    <V1Page eyebrow="Home Assistant" title="Entitäten" action={<div className="v1-ha-status"><strong>{mappedCount}</strong><span>gemappt</span></div>}>
      {error && <V1Alert message={error} tone="warn" />}
      {message && <V1Alert message={message} tone="ok" />}
      {loading ? <V1Empty title="Lade Home Assistant..." /> : (
        <>
          <V1Section title="Verbindung">
            <form className="v1-ha-connect-form" onSubmit={(event) => void saveConnection(event)}>
              <V1Field label="URL"><input value={ha.baseUrl ?? ''} onChange={(event) => setHa((current) => ({ ...current, baseUrl: event.target.value }))} placeholder="http://homeassistant.local:8123" /></V1Field>
              <V1Field label="Token"><div className="v1-inline-input"><input type={showToken ? 'text' : 'password'} value={ha.accessToken ?? ''} onChange={(event) => setHa((current) => ({ ...current, accessToken: event.target.value }))} autoComplete="off" /><V1Button onClick={() => setShowToken((current) => !current)}>{showToken ? 'Verbergen' : 'Anzeigen'}</V1Button></div></V1Field>
              <V1Switch label="Aktiv" checked={ha.enabled} onChange={(checked) => setHa((current) => ({ ...current, enabled: checked }))} />
              <V1Button type="submit" variant="primary" disabled={saving === 'ha'}>{saving === 'ha' ? 'Speichert...' : 'Speichern'}</V1Button>
            </form>
          </V1Section>

          {tents.length === 0 ? <V1Empty title="Kein Zelt angelegt" action={<Link to="/zelte" className="v1-button is-primary">Zelt anlegen</Link>} /> : selectedTent && selectedDraft && (
            <section className="v1-ha-layout">
              <V1Tabs label="Zelt" active={selectedTent.id} onChange={(id) => setSelectedTentId(id)} items={tents.map((tent) => ({ value: tent.id, label: tent.name, meta: formatTentSize(tent) }))} />
              <V1Section title={selectedTent.name} action={<V1Button variant="primary" disabled={saving === `tent-${selectedTent.id}`} onClick={() => void saveSelectedTent()}>{saving === `tent-${selectedTent.id}` ? 'Speichert...' : 'Speichern'}</V1Button>}>
                <V1Field label="Kamera Entity" wide><input value={selectedDraft.cameraEntityId} onChange={(event) => updateCamera(event.target.value)} placeholder="camera.hauptzelt" /></V1Field>
                <V1Tabs label="Entity-Gruppe" active={activeGroup} onChange={(group) => setActiveGroup(group)} items={groups.map((group) => ({ value: group.key, label: group.label }))} />
                <div className="v1-entity-list">
                  {definitions.filter((definition) => definition.group === activeGroup).map((definition) => {
                    const sensor = selectedDraft.sensors.find((item) => item.metricType === definition.metricType) ?? createSensorDraft(definition)
                    return <EntityRow key={definition.metricType} definition={definition} sensor={sensor} onChange={(patch) => updateSensor(definition.metricType, patch)} />
                  })}
                </div>
              </V1Section>
            </section>
          )}
        </>
      )}
    </V1Page>
  )
}

function EntityRow({ definition, sensor, onChange }: { definition: EntityDefinition; sensor: SensorDraft; onChange: (patch: Partial<SensorDraft>) => void }) {
  return <div className={classNames('v1-entity-row', sensor.isActive && 'active')}><label><input type="checkbox" checked={sensor.isActive} onChange={(event) => onChange({ isActive: event.target.checked })} /><strong>{definition.label}</strong>{definition.unit && <span>{definition.unit}</span>}</label><input value={sensor.haEntityId} onChange={(event) => onChange({ haEntityId: event.target.value })} placeholder={definition.placeholder} /></div>
}

function createTentDraft(tent: TentDto): TentMappingDraft { return { cameraEntityId: tent.cameraEntityId ?? '', sensors: definitions.map((definition) => { const existing = tent.sensors.find((sensor) => sensor.metricType === definition.metricType); return { metricType: definition.metricType, haEntityId: existing?.haEntityId ?? '', displayLabel: existing?.displayLabel ?? definition.label, isActive: existing?.isActive ?? false } }) } }
function createSensorDraft(definition: EntityDefinition): SensorDraft { return { metricType: definition.metricType, haEntityId: '', displayLabel: definition.label, isActive: false } }
function toUpdateTentRequest(tent: TentDto, draft: TentMappingDraft): UpdateTentRequest { const sensors: UpdateTentSensorRequest[] = draft.sensors.map((sensor) => ({ id: tent.sensors.find((existing) => existing.metricType === sensor.metricType)?.id ?? 0, metricType: sensor.metricType, haEntityId: toNullableString(sensor.haEntityId), displayLabel: toNullableString(sensor.displayLabel), isActive: sensor.isActive && sensor.haEntityId.trim().length > 0 })); return { name: tent.name, status: tent.status, kind: tent.kind, tentType: tent.tentType, notes: tent.notes, displayOrder: tent.displayOrder, accentColor: tent.accentColor, widthCm: tent.widthCm, depthCm: tent.depthCm, tentHeightCm: tent.tentHeightCm, lightType: tent.lightType, lightWatt: tent.lightWatt, lightController: tent.lightController, lightControllerEntityId: tent.lightControllerEntityId, exhaustFanCount: tent.exhaustFanCount, exhaustM3h: tent.exhaustM3h, circulationFanCount: tent.circulationFanCount, hvacController: tent.hvacController, hvacControllerEntityId: tent.hvacControllerEntityId, co2Available: tent.co2Available, cameraEntityId: toNullableString(draft.cameraEntityId), sensors } }
function formatTentSize(tent: TentDto) { return !tent.widthCm && !tent.depthCm && !tent.tentHeightCm ? 'Größe offen' : `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm` }
function formatApiError(caught: unknown, fallback: string) { return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback }

export default HomeAssistantPage
