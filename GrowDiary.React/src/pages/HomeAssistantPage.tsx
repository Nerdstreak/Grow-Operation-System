import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { HomeAssistantSettingsDto, SensorMetricType, SettingsOverviewDto, TentDto, UpdateTentRequest, UpdateTentSensorRequest } from '../types'
import { classNames } from '../utils'

type SavingState = 'ha' | `tent-${number}` | null
type SensorDraft = { metricType: SensorMetricType; haEntityId: string; displayLabel: string; isActive: boolean }
type TentMappingDraft = { cameraEntityId: string; sensors: SensorDraft[] }
type EntityDefinition = { metricType: SensorMetricType; label: string; group: 'tent' | 'reservoir' | 'hardware'; placeholder: string }

const entityDefinitions: EntityDefinition[] = [
  { metricType: 'AirTemperature', label: 'Lufttemperatur', group: 'tent', placeholder: 'sensor.zelt_temperatur' },
  { metricType: 'Humidity', label: 'Luftfeuchte', group: 'tent', placeholder: 'sensor.zelt_luftfeuchte' },
  { metricType: 'Vpd', label: 'VPD', group: 'tent', placeholder: 'sensor.zelt_vpd' },
  { metricType: 'Ppfd', label: 'PPFD', group: 'tent', placeholder: 'sensor.lampe_ppfd' },
  { metricType: 'Co2', label: 'CO₂', group: 'tent', placeholder: 'sensor.zelt_co2' },
  { metricType: 'LightStatus', label: 'Licht', group: 'tent', placeholder: 'switch.licht' },
  { metricType: 'ReservoirPh', label: 'pH', group: 'reservoir', placeholder: 'sensor.rdwc_ph' },
  { metricType: 'ReservoirEc', label: 'EC', group: 'reservoir', placeholder: 'sensor.rdwc_ec' },
  { metricType: 'ReservoirOrp', label: 'ORP', group: 'reservoir', placeholder: 'sensor.rdwc_orp' },
  { metricType: 'ReservoirDissolvedOxygen', label: 'DO', group: 'reservoir', placeholder: 'sensor.rdwc_do' },
  { metricType: 'ReservoirWaterTemp', label: 'Wassertemp.', group: 'reservoir', placeholder: 'sensor.rdwc_wassertemperatur' },
  { metricType: 'ReservoirLevel', label: 'Wasserstand', group: 'reservoir', placeholder: 'sensor.rdwc_wasserstand' },
  { metricType: 'PumpCirculation', label: 'Umwälzpumpe', group: 'hardware', placeholder: 'switch.rdwc_pumpe' },
  { metricType: 'PumpAir', label: 'Luftpumpe', group: 'hardware', placeholder: 'switch.luftpumpe' },
  { metricType: 'Chiller', label: 'Chiller', group: 'hardware', placeholder: 'climate.chiller' },
  { metricType: 'UpsStatus', label: 'USV', group: 'hardware', placeholder: 'sensor.usv_status' },
]

function HomeAssistantPage() {
  const [ha, setHa] = useState<HomeAssistantSettingsDto>({ baseUrl: '', accessToken: '', enabled: false })
  const [tents, setTents] = useState<TentDto[]>([])
  const [drafts, setDrafts] = useState<Record<number, TentMappingDraft>>({})
  const [showToken, setShowToken] = useState(false)
  const [selectedTentId, setSelectedTentId] = useState<number | null>(null)
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
      } catch (caught) {
        if (!controller.signal.aborted) setError(formatApiError(caught, 'Home Assistant konnte nicht geladen werden.'))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  const mappedCount = useMemo(() => Object.values(drafts).reduce((sum, draft) => sum + draft.sensors.filter((sensor) => sensor.isActive && sensor.haEntityId.trim()).length + (draft.cameraEntityId.trim() ? 1 : 0), 0), [drafts])
  const selectedTent = useMemo(() => tents.find((tent) => tent.id === selectedTentId) ?? tents[0] ?? null, [selectedTentId, tents])

  async function saveHomeAssistant(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving('ha')
    setError(null)
    setMessage(null)
    try {
      const saved = await apiFetch<HomeAssistantSettingsDto>('/api/settings/home-assistant', {
        method: 'PUT',
        body: JSON.stringify({ baseUrl: toNullableString(ha.baseUrl), accessToken: toNullableString(ha.accessToken), enabled: ha.enabled }),
      })
      setHa(saved)
      setMessage('Gespeichert.')
    } catch (caught) {
      setError(formatApiError(caught, 'Home Assistant konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function saveTentMapping(tent: TentDto) {
    const draft = drafts[tent.id]
    if (!draft) return
    setSaving(`tent-${tent.id}`)
    setError(null)
    setMessage(null)
    try {
      const saved = await apiFetch<TentDto>(`/api/settings/tents/${tent.id}`, { method: 'PUT', body: JSON.stringify(toUpdateTentRequest(tent, draft)) })
      setTents((current) => current.map((item) => (item.id === saved.id ? saved : item)))
      setDrafts((current) => ({ ...current, [saved.id]: createTentDraft(saved) }))
      setMessage(`${saved.name} gespeichert.`)
    } catch (caught) {
      setError(formatApiError(caught, `${tent.name} konnte nicht gespeichert werden.`))
    } finally {
      setSaving(null)
    }
  }

  function updateSensor(tentId: number, metricType: SensorMetricType, patch: Partial<SensorDraft>) {
    setDrafts((current) => {
      const draft = current[tentId]
      if (!draft) return current
      return { ...current, [tentId]: { ...draft, sensors: draft.sensors.map((sensor) => (sensor.metricType === metricType ? { ...sensor, ...patch } : sensor)) } }
    })
  }

  function updateCamera(tentId: number, value: string) {
    setDrafts((current) => {
      const draft = current[tentId]
      if (!draft) return current
      return { ...current, [tentId]: { ...draft, cameraEntityId: value } }
    })
  }

  return (
    <main className="page-scroll app-page ha-rebuild-page">
      <section className="control-header">
        <div>
          <span className="control-kicker">Home Assistant</span>
          <h1>Entitäten</h1>
        </div>
        <div className="ha-count"><strong>{mappedCount}</strong><span>aktiv</span></div>
      </section>

      {error && <div className="inline-error"><strong>Fehler</strong><span>{error}</span></div>}
      {message && <div className="inline-ok">{message}</div>}

      {loading ? (
        <div className="empty-hint tight">Lädt...</div>
      ) : (
        <>
          <section className="ha-connect-panel ops-card">
            <form onSubmit={(event) => void saveHomeAssistant(event)}>
              <label className="field compact-field">
                <span>URL</span>
                <input value={ha.baseUrl ?? ''} onChange={(event) => setHa((current) => ({ ...current, baseUrl: event.target.value }))} placeholder="http://homeassistant.local:8123" />
              </label>
              <label className="field compact-field">
                <span>Token</span>
                <div className="inline-input-button">
                  <input type={showToken ? 'text' : 'password'} value={ha.accessToken ?? ''} onChange={(event) => setHa((current) => ({ ...current, accessToken: event.target.value }))} autoComplete="off" />
                  <button type="button" className="btn" onClick={() => setShowToken((current) => !current)}>{showToken ? 'Verbergen' : 'Anzeigen'}</button>
                </div>
              </label>
              <label className="compact-check">
                <input type="checkbox" checked={ha.enabled} onChange={(event) => setHa((current) => ({ ...current, enabled: event.target.checked }))} />
                <span>aktiv</span>
              </label>
              <button type="submit" className="btn btn-primary" disabled={saving === 'ha'}>{saving === 'ha' ? 'Speichert...' : 'Speichern'}</button>
            </form>
          </section>

          {tents.length === 0 ? (
            <div className="empty-hint tight"><Link to="/zelte" className="btn btn-primary">Zelt anlegen</Link></div>
          ) : (
            <section className="ha-mapping-stack">
              <div className="ha-tent-switcher" aria-label="Zelt auswählen">
                {tents.map((tent) => (
                  <button
                    key={tent.id}
                    type="button"
                    className={classNames('ha-tent-tab', selectedTent?.id === tent.id && 'active')}
                    onClick={() => setSelectedTentId(tent.id)}
                  >
                    <strong>{tent.name}</strong>
                    <span>{formatTentSize(tent)}</span>
                  </button>
                ))}
              </div>

              {selectedTent && drafts[selectedTent.id] && (
                <article className="ops-card ha-map-card">
                  <header className="ops-card-header">
                    <div><h2>{selectedTent.name}</h2><span>{formatTentSize(selectedTent)}</span></div>
                    <button type="button" className="btn btn-primary" disabled={saving === `tent-${selectedTent.id}`} onClick={() => void saveTentMapping(selectedTent)}>
                      {saving === `tent-${selectedTent.id}` ? 'Speichert...' : 'Speichern'}
                    </button>
                  </header>

                  <label className="entity-row camera-entity-row">
                    <span>Kamera</span>
                    <input value={drafts[selectedTent.id].cameraEntityId} onChange={(event) => updateCamera(selectedTent.id, event.target.value)} placeholder="camera.zelt" />
                  </label>

                  <EntityGroup title="Zelt" definitions={entityDefinitions.filter((definition) => definition.group === 'tent')} draft={drafts[selectedTent.id]} onChange={(metricType, patch) => updateSensor(selectedTent.id, metricType, patch)} />
                  <EntityGroup title="RDWC/DWC" definitions={entityDefinitions.filter((definition) => definition.group === 'reservoir')} draft={drafts[selectedTent.id]} onChange={(metricType, patch) => updateSensor(selectedTent.id, metricType, patch)} />
                  <EntityGroup title="Technik" definitions={entityDefinitions.filter((definition) => definition.group === 'hardware')} draft={drafts[selectedTent.id]} onChange={(metricType, patch) => updateSensor(selectedTent.id, metricType, patch)} />
                </article>
              )}
            </section>
          )}
        </>
      )}
    </main>
  )
}

function EntityGroup({ title, definitions, draft, onChange }: { title: string; definitions: EntityDefinition[]; draft: TentMappingDraft; onChange: (metricType: SensorMetricType, patch: Partial<SensorDraft>) => void }) {
  return (
    <section className="entity-group">
      <h3>{title}</h3>
      <div className="entity-list">
        {definitions.map((definition) => {
          const sensor = draft.sensors.find((item) => item.metricType === definition.metricType) ?? createSensorDraft(definition)
          return (
            <label key={definition.metricType} className={classNames('entity-row', sensor.isActive && 'is-active')}>
              <input type="checkbox" checked={sensor.isActive} onChange={(event) => onChange(definition.metricType, { isActive: event.target.checked })} />
              <span>{definition.label}</span>
              <input value={sensor.haEntityId} onChange={(event) => onChange(definition.metricType, { haEntityId: event.target.value })} placeholder={definition.placeholder} />
            </label>
          )
        })}
      </div>
    </section>
  )
}

function createTentDraft(tent: TentDto): TentMappingDraft {
  return {
    cameraEntityId: tent.cameraEntityId ?? '',
    sensors: entityDefinitions.map((definition) => {
      const existing = tent.sensors.find((sensor) => sensor.metricType === definition.metricType)
      return { metricType: definition.metricType, haEntityId: existing?.haEntityId ?? '', displayLabel: existing?.displayLabel ?? definition.label, isActive: existing?.isActive ?? false }
    }),
  }
}

function createSensorDraft(definition: EntityDefinition): SensorDraft {
  return { metricType: definition.metricType, haEntityId: '', displayLabel: definition.label, isActive: false }
}

function toUpdateTentRequest(tent: TentDto, draft: TentMappingDraft): UpdateTentRequest {
  const sensors: UpdateTentSensorRequest[] = draft.sensors.map((sensor) => ({
    id: tent.sensors.find((existing) => existing.metricType === sensor.metricType)?.id ?? 0,
    metricType: sensor.metricType,
    haEntityId: toNullableString(sensor.haEntityId),
    displayLabel: toNullableString(sensor.displayLabel),
    isActive: sensor.isActive && sensor.haEntityId.trim().length > 0,
  }))

  return {
    name: tent.name,
    status: tent.status,
    kind: tent.kind,
    tentType: tent.tentType,
    notes: tent.notes,
    displayOrder: tent.displayOrder,
    accentColor: tent.accentColor,
    widthCm: tent.widthCm,
    depthCm: tent.depthCm,
    tentHeightCm: tent.tentHeightCm,
    lightType: tent.lightType,
    lightWatt: tent.lightWatt,
    lightController: tent.lightController,
    lightControllerEntityId: tent.lightControllerEntityId,
    exhaustFanCount: tent.exhaustFanCount,
    exhaustM3h: tent.exhaustM3h,
    circulationFanCount: tent.circulationFanCount,
    hvacController: tent.hvacController,
    hvacControllerEntityId: tent.hvacControllerEntityId,
    co2Available: tent.co2Available,
    cameraEntityId: toNullableString(draft.cameraEntityId),
    sensors,
  }
}

function formatTentSize(tent: TentDto): string {
  if (!tent.widthCm && !tent.depthCm && !tent.tentHeightCm) return 'Größe offen'
  return `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm`
}

function toNullableString(value: string | null | undefined): string | null {
  const trimmed = (value ?? '').trim()
  return trimmed.length === 0 ? null : trimmed
}

function formatApiError(caught: unknown, fallback: string): string {
  if (caught instanceof ApiRequestError) return caught.message
  return caught instanceof Error ? caught.message : fallback
}

export default HomeAssistantPage
