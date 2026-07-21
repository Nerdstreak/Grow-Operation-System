import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { HomeAssistantEntity, HomeAssistantSettingsDto, SensorMetricType, SettingsOverviewDto, TentDto, UpdateTentRequest, UpdateTentSensorRequest } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section, V1Switch, V1Tabs } from '../components/v1'
import { toNullableString } from '../components/v1-utils'
import { resolveUrl } from '../base'

type GroupKey = 'tent' | 'reservoir' | 'hardware'
type SensorDraft = { metricType: SensorMetricType; haEntityId: string; displayLabel: string; isActive: boolean }
type TentMappingDraft = { cameraEntityId: string; sensors: SensorDraft[] }
type EntityDefinition = { metricType: SensorMetricType; label: string; group: GroupKey; placeholder: string; importance: 'core' | 'optional'; unit?: string }
type SavingState = 'ha' | `tent-${number}` | null
type CameraStatus = { ok: boolean; status: string; message: string; cameraEntityId: string | null; previewUrl: string | null }

const groups: Array<{ key: GroupKey; label: string; text: string }> = [
  { key: 'tent', label: 'Zelt', text: 'Klima, Licht, VPD und Kamera' },
  { key: 'reservoir', label: 'RDWC/DWC', text: 'pH, EC, Wasser, ORP und Sauerstoff' },
  { key: 'hardware', label: 'Technik', text: 'Pumpen, Chiller, USV und Schalter' },
]

const definitions: EntityDefinition[] = [
  { metricType: 'AirTemperature', label: 'Lufttemperatur', group: 'tent', placeholder: 'sensor.zelt_temperatur', unit: '°C', importance: 'core' },
  { metricType: 'Humidity', label: 'Luftfeuchte', group: 'tent', placeholder: 'sensor.zelt_luftfeuchte', unit: '%', importance: 'core' },
  { metricType: 'Vpd', label: 'VPD', group: 'tent', placeholder: 'sensor.zelt_vpd', unit: 'kPa', importance: 'core' },
  { metricType: 'Ppfd', label: 'PPFD', group: 'tent', placeholder: 'sensor.lampe_ppfd', unit: 'µmol/m²/s', importance: 'optional' },
  { metricType: 'Co2', label: 'CO₂', group: 'tent', placeholder: 'sensor.zelt_co2', unit: 'ppm', importance: 'optional' },
  { metricType: 'LightStatus', label: 'Licht', group: 'tent', placeholder: 'switch.licht', importance: 'optional' },
  { metricType: 'ReservoirPh', label: 'pH', group: 'reservoir', placeholder: 'sensor.rdwc_ph', importance: 'core' },
  { metricType: 'ReservoirEc', label: 'EC', group: 'reservoir', placeholder: 'sensor.rdwc_ec', unit: 'mS/cm', importance: 'core' },
  { metricType: 'ReservoirWaterTemp', label: 'Wassertemperatur', group: 'reservoir', placeholder: 'sensor.rdwc_wassertemperatur', unit: '°C', importance: 'core' },
  { metricType: 'ReservoirLevel', label: 'Wasserstand (Liter)', group: 'reservoir', placeholder: 'sensor.rdwc_wasserstand_liter', unit: 'L', importance: 'core' },
  { metricType: 'ReservoirLevelCm', label: 'Wasserstand (cm)', group: 'reservoir', placeholder: 'sensor.rdwc_wasserstand_cm', unit: 'cm', importance: 'optional' },
  { metricType: 'ReservoirOrp', label: 'ORP', group: 'reservoir', placeholder: 'sensor.rdwc_orp', unit: 'mV', importance: 'optional' },
  { metricType: 'ReservoirDissolvedOxygen', label: 'DO', group: 'reservoir', placeholder: 'sensor.rdwc_do', unit: 'mg/L', importance: 'optional' },
  { metricType: 'PumpCirculation', label: 'Umwälzpumpe', group: 'hardware', placeholder: 'switch.rdwc_pumpe', importance: 'optional' },
  { metricType: 'PumpAir', label: 'Luftpumpe', group: 'hardware', placeholder: 'switch.luftpumpe', importance: 'optional' },
  { metricType: 'Chiller', label: 'Chiller', group: 'hardware', placeholder: 'climate.chiller', importance: 'optional' },
  { metricType: 'UpsStatus', label: 'USV', group: 'hardware', placeholder: 'sensor.usv_status', importance: 'optional' },
]

// Per-metric hints for the entity picker: which Home Assistant domains / device
// classes are plausible for each sensor, so the dropdown suggests the right ones
// first. Filters are best-effort — if nothing matches, the full list is offered.
const suggestionFilters: Partial<Record<SensorMetricType, { domains?: string[]; deviceClass?: string }>> = {
  AirTemperature: { domains: ['sensor'], deviceClass: 'temperature' },
  Humidity: { domains: ['sensor'], deviceClass: 'humidity' },
  Co2: { domains: ['sensor'], deviceClass: 'carbon_dioxide' },
  ReservoirWaterTemp: { domains: ['sensor'], deviceClass: 'temperature' },
  Vpd: { domains: ['sensor'] },
  Ppfd: { domains: ['sensor'] },
  ReservoirPh: { domains: ['sensor'] },
  ReservoirEc: { domains: ['sensor'] },
  ReservoirLevel: { domains: ['sensor'] },
  ReservoirLevelCm: { domains: ['sensor'] },
  ReservoirOrp: { domains: ['sensor'] },
  ReservoirDissolvedOxygen: { domains: ['sensor'] },
  UpsStatus: { domains: ['sensor', 'binary_sensor'] },
  LightStatus: { domains: ['switch', 'light', 'binary_sensor', 'input_boolean'] },
  PumpCirculation: { domains: ['switch', 'input_boolean'] },
  PumpAir: { domains: ['switch', 'input_boolean'] },
  Chiller: { domains: ['climate', 'switch'] },
}

function suggestionsForMetric(entities: HomeAssistantEntity[], metricType: SensorMetricType): HomeAssistantEntity[] {
  const filter = suggestionFilters[metricType]
  if (!filter) return entities
  if (filter.deviceClass) {
    const byClass = entities.filter((entity) => entity.deviceClass === filter.deviceClass)
    if (byClass.length > 0) return byClass
  }
  if (filter.domains) {
    const byDomain = entities.filter((entity) => filter.domains!.includes(entity.domain))
    if (byDomain.length > 0) return byDomain
  }
  return entities
}

function entityOptionLabel(entity: HomeAssistantEntity): string {
  const name = entity.friendlyName ?? entity.entityId
  if (entity.state == null || entity.state === '') return name
  const unit = entity.unitOfMeasurement ? ` ${entity.unitOfMeasurement}` : ''
  return `${name} — ${entity.state}${unit}`
}

function HomeAssistantPage() {
  const [ha, setHa] = useState<HomeAssistantSettingsDto>({ baseUrl: '', accessToken: '', enabled: false })
  const [entities, setEntities] = useState<HomeAssistantEntity[]>([])
  const [tents, setTents] = useState<TentDto[]>([])
  const [drafts, setDrafts] = useState<Record<number, TentMappingDraft>>({})
  const [selectedTentId, setSelectedTentId] = useState<number | null>(null)
  const [activeGroup, setActiveGroup] = useState<GroupKey>('tent')
  const [showToken, setShowToken] = useState(false)
  const [cameraStatus, setCameraStatus] = useState<CameraStatus | null>(null)
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
        if (controller.signal.aborted) return
        const sorted = [...overview.tents].sort((a, b) => scoreTent(b) - scoreTent(a) || a.displayOrder - b.displayOrder || a.name.localeCompare(b.name))
        setHa(overview.homeAssistant)
        setTents(sorted)
        setDrafts(Object.fromEntries(sorted.map((tent) => [tent.id, createTentDraft(tent)])))
        setSelectedTentId((current) => current ?? sorted[0]?.id ?? null)

        // Best-effort: load live entities so the mapping can use a dropdown.
        // Returns [] when Home Assistant is unreachable or not configured.
        const entityList = await apiFetch<HomeAssistantEntity[]>('/api/home-assistant/entities', { signal: controller.signal }).catch(() => [])
        if (!controller.signal.aborted) setEntities(entityList)
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
  const mappedCount = useMemo(() => Object.values(drafts).reduce((sum, draft) => sum + (draft.cameraEntityId.trim() ? 1 : 0) + draft.sensors.filter((sensor) => sensor.isActive && sensor.haEntityId.trim()).length, 0), [drafts])
  // Prefer camera.* entities but fall back to the full list so the picker is never
  // empty when HA exposes the camera under a different domain (e.g. image.*).
  const cameraEntities = useMemo(() => {
    const cams = entities.filter((entity) => entity.domain === 'camera' || entity.domain === 'image')
    return cams.length > 0 ? cams : entities
  }, [entities])
  const coreMappedCount = selectedDraft ? selectedDraft.sensors.filter((sensor) => sensor.isActive && sensor.haEntityId.trim() && definitions.find((definition) => definition.metricType === sensor.metricType)?.importance === 'core').length : 0

  async function saveConnection(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving('ha')
    setError(null)
    setMessage(null)
    try {
      const saved = await apiFetch<HomeAssistantSettingsDto>('/api/settings/home-assistant', { method: 'PUT', body: JSON.stringify({ baseUrl: toNullableString(ha.baseUrl), accessToken: toNullableString(ha.accessToken), enabled: ha.enabled }) })
      setHa(saved)
      setMessage('Home-Assistant-Verbindung gespeichert.')
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
      setCameraStatus(null)
    } catch (caught) {
      setError(formatApiError(caught, 'Entity-Mapping konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function testCamera() {
    if (!selectedTent) return
    setCameraStatus(null)
    setError(null)
    try {
      const status = await apiFetch<CameraStatus>(`/api/camera/tents/${selectedTent.id}/status`)
      setCameraStatus(status)
    } catch (caught) {
      setCameraStatus({ ok: false, status: 'request_failed', message: formatApiError(caught, 'Kamera-Test fehlgeschlagen.'), cameraEntityId: selectedDraft?.cameraEntityId ?? null, previewUrl: null })
    }
  }

  function updateCamera(value: string) {
    if (!selectedTent) return
    setDrafts((current) => ({ ...current, [selectedTent.id]: { ...current[selectedTent.id], cameraEntityId: value } }))
    setCameraStatus(null)
  }

  function updateSensor(metricType: SensorMetricType, patch: Partial<SensorDraft>) {
    if (!selectedTent) return
    setDrafts((current) => ({ ...current, [selectedTent.id]: { ...current[selectedTent.id], sensors: current[selectedTent.id].sensors.map((sensor) => sensor.metricType === metricType ? { ...sensor, ...patch } : sensor) } }))
  }

  return (
    <V1Page eyebrow="Home Assistant" title="HA einrichten" subtitle={ha.isManagedByAddon ? 'Zelt wählen, Kamera testen und Sensoren mappen — verbunden ist Grow OS übers Add-on automatisch.' : 'Verbindung speichern, Zelt wählen, Kamera testen und Sensoren mappen.'}>
      {error && <V1Alert message={error} tone="warn" />}
      {message && <V1Alert message={message} tone="ok" />}

      <section className="v1-kpi-grid">
        <V1Card tone={ha.isManagedByAddon || ha.enabled ? 'ok' : 'warn'}><span className="v1-card-kicker">Verbindung</span><h2>{ha.isManagedByAddon || ha.enabled ? 'aktiv' : 'inaktiv'}</h2><p>{ha.isManagedByAddon ? 'Über Add-on' : (ha.baseUrl || 'URL offen')}</p></V1Card>
        <V1Card><span className="v1-card-kicker">Entitäten</span><h2>{mappedCount}</h2><p>gesamt gemappt</p></V1Card>
        <V1Card><span className="v1-card-kicker">Kernwerte</span><h2>{coreMappedCount}</h2><p>im ausgewählten Zelt</p></V1Card>
        <V1Card><span className="v1-card-kicker">Zelte</span><h2>{tents.length}</h2><p>verfügbar</p></V1Card>
      </section>

      {loading ? <V1Empty title="Lade Home Assistant..." /> : (
        <>
          <V1Section title="1. Verbindung">
            {ha.isManagedByAddon ? (
              <V1Card tone="ok">
                <span className="v1-card-kicker">Home Assistant</span>
                <h2>Über Add-on verbunden</h2>
                <p>Grow OS läuft als Home-Assistant-Add-on und ist automatisch verbunden — keine URL und kein Token nötig. Wähle unten einfach deine Sensoren aus.</p>
              </V1Card>
            ) : (
              <form className="v1-ha-connect-form rc2-ha-connect-form" data-audit="ha-connection-layout" onSubmit={(event) => void saveConnection(event)}>
                <V1Field label="Home Assistant URL" hint="Beispiel: http://homeassistant.local:8123">
                  <input value={ha.baseUrl ?? ''} onChange={(event) => setHa((current) => ({ ...current, baseUrl: event.target.value }))} placeholder="http://homeassistant.local:8123" />
                </V1Field>
                <V1Field label="Long-Lived Access Token">
                  <div className="v1-inline-input">
                    <input type={showToken ? 'text' : 'password'} value={ha.accessToken ?? ''} onChange={(event) => setHa((current) => ({ ...current, accessToken: event.target.value }))} autoComplete="off" />
                    <V1Button onClick={() => setShowToken((current) => !current)}>{showToken ? 'Verbergen' : 'Anzeigen'}</V1Button>
                  </div>
                </V1Field>
                <div className="rc2-ha-connection-actions" data-audit="ha-connection-actions">
                  <V1Switch label="Home Assistant aktiv" checked={ha.enabled} onChange={(checked) => setHa((current) => ({ ...current, enabled: checked }))} />
                  <V1Button type="submit" variant="primary" disabled={saving === 'ha'} className="rc2-compact-action">{saving === 'ha' ? 'Speichert...' : 'Verbindung speichern'}</V1Button>
                </div>
              </form>
            )}
          </V1Section>

          {tents.length === 0 ? (
            <V1Empty title="Kein Zelt angelegt" action={<Link to="/zelte" className="v1-button is-primary">Zelt anlegen</Link>} />
          ) : selectedTent && selectedDraft && (
            <section className="v1-ha-layout">
              <V1Section title="2. Zelt wählen">
                <V1Tabs label="Zelt" active={selectedTent.id} onChange={(id) => { setSelectedTentId(id); setCameraStatus(null) }} items={tents.map((tent) => ({ value: tent.id, label: tent.name, meta: formatTentSize(tent) }))} />
              </V1Section>

              <V1Section title={`3. ${selectedTent.name} mappen`} action={<V1Button variant="primary" disabled={saving === `tent-${selectedTent.id}`} onClick={() => void saveSelectedTent()}>{saving === `tent-${selectedTent.id}` ? 'Speichert...' : 'Mapping speichern'}</V1Button>}>
                <div className="v1-card-grid">
                  <V1Card tone={cameraStatus?.ok ? 'ok' : cameraStatus ? 'warn' : 'neutral'}>
                    <span className="v1-card-kicker">Kamera</span>
                    <h2>{cameraStatus?.ok ? 'Snapshot OK' : selectedDraft.cameraEntityId.trim() ? 'eingetragen' : 'optional'}</h2>
                    <div className="rc2-ha-camera-field-action" data-audit="ha-camera-field-action">
                    <V1Field label="Kamera Entity">
                      <input value={selectedDraft.cameraEntityId} onChange={(event) => updateCamera(event.target.value)} placeholder="camera.hauptzelt" list={entities.length > 0 ? 'ha-entities-camera' : undefined} />
                      {entities.length > 0 && (
                        <datalist id="ha-entities-camera">
                          {cameraEntities.map((entity) => (
                            <option key={entity.entityId} value={entity.entityId}>{entityOptionLabel(entity)}</option>
                          ))}
                        </datalist>
                      )}
                    </V1Field>
                      <V1Button onClick={() => void testCamera()}>Kamera testen</V1Button>
                      {cameraStatus?.previewUrl && <a className="v1-button is-secondary" href={resolveUrl(cameraStatus.previewUrl)} target="_blank" rel="noreferrer">Snapshot öffnen</a>}
                    </div>
                    {cameraStatus && <p>{cameraStatus.message}</p>}
                    {cameraStatus?.previewUrl && <img className="rc2-camera-preview" src={resolveUrl(cameraStatus.previewUrl)} alt="Kamera Vorschau" />}
                  </V1Card>
                  <V1Card>
                    <span className="v1-card-kicker">Mapping</span>
                    <h2>{groups.find((group) => group.key === activeGroup)?.text}</h2>
                    <p>Core-Werte beeinflussen Live-Dashboard und Systemscore stärker als optionale Technikwerte.</p>
                    <p>{entities.length > 0 ? `${entities.length} Home-Assistant-Entitäten geladen — im Feld tippen oder aus der Liste wählen.` : 'Keine HA-Entitäten geladen — Entity-IDs manuell eintragen.'}</p>
                  </V1Card>
                </div>

                <V1Tabs label="Entity-Gruppe" active={activeGroup} onChange={setActiveGroup} items={groups.map((group) => ({ value: group.key, label: group.label, meta: group.text }))} />

                <div className="v1-entity-list">
                  {definitions.filter((definition) => definition.group === activeGroup).map((definition) => {
                    const sensor = selectedDraft.sensors.find((item) => item.metricType === definition.metricType) ?? createSensorDraft(definition)
                    return <EntityRow key={definition.metricType} definition={definition} sensor={sensor} entities={entities} onChange={(patch) => updateSensor(definition.metricType, patch)} />
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

function EntityRow({ definition, sensor, entities, onChange }: { definition: EntityDefinition; sensor: SensorDraft; entities: HomeAssistantEntity[]; onChange: (patch: Partial<SensorDraft>) => void }) {
  const hasEntities = entities.length > 0
  const listId = `ha-entities-${definition.metricType}`
  const options = hasEntities ? suggestionsForMetric(entities, definition.metricType) : []
  return (
    <div className={sensor.isActive ? 'v1-entity-row active' : 'v1-entity-row'}>
      <label>
        <input type="checkbox" checked={sensor.isActive} onChange={(event) => onChange({ isActive: event.target.checked })} />
        <strong>{definition.label}</strong>
        <V1Badge tone={definition.importance === 'core' ? 'accent' : 'neutral'}>{definition.importance === 'core' ? 'Core' : 'optional'}</V1Badge>
        {definition.unit && <span>{definition.unit}</span>}
      </label>
      <input
        value={sensor.haEntityId}
        onChange={(event) => onChange({ haEntityId: event.target.value })}
        placeholder={definition.placeholder}
        list={hasEntities ? listId : undefined}
      />
      {hasEntities && (
        <datalist id={listId}>
          {options.map((entity) => (
            <option key={entity.entityId} value={entity.entityId}>{entityOptionLabel(entity)}</option>
          ))}
        </datalist>
      )}
    </div>
  )
}

function scoreTent(tent: TentDto) {
  return (tent.activeGrowCount > 0 ? 100 : 0) + (tent.tentType === 'Production' ? 10 : 0)
}

function createTentDraft(tent: TentDto): TentMappingDraft {
  return { cameraEntityId: tent.cameraEntityId ?? '', sensors: definitions.map((definition) => {
    const existing = tent.sensors.find((sensor) => sensor.metricType === definition.metricType)
    return { metricType: definition.metricType, haEntityId: existing?.haEntityId ?? '', displayLabel: existing?.displayLabel ?? definition.label, isActive: existing?.isActive ?? false }
  }) }
}

function createSensorDraft(definition: EntityDefinition): SensorDraft {
  return { metricType: definition.metricType, haEntityId: '', displayLabel: definition.label, isActive: false }
}

function toUpdateTentRequest(tent: TentDto, draft: TentMappingDraft): UpdateTentRequest {
  const sensors: UpdateTentSensorRequest[] = draft.sensors.map((sensor) => ({ id: tent.sensors.find((existing) => existing.metricType === sensor.metricType)?.id ?? 0, metricType: sensor.metricType, haEntityId: toNullableString(sensor.haEntityId), displayLabel: toNullableString(sensor.displayLabel), isActive: sensor.isActive && sensor.haEntityId.trim().length > 0 }))
  return { name: tent.name, status: tent.status, kind: tent.kind, tentType: tent.tentType, notes: tent.notes, displayOrder: tent.displayOrder, accentColor: tent.accentColor, widthCm: tent.widthCm, depthCm: tent.depthCm, tentHeightCm: tent.tentHeightCm, lightType: tent.lightType, lightWatt: tent.lightWatt, lightController: tent.lightController, lightControllerEntityId: tent.lightControllerEntityId, exhaustFanCount: tent.exhaustFanCount, exhaustM3h: tent.exhaustM3h, circulationFanCount: tent.circulationFanCount, hvacController: tent.hvacController, hvacControllerEntityId: tent.hvacControllerEntityId, co2Available: tent.co2Available, cameraEntityId: toNullableString(draft.cameraEntityId), sensors }
}

function formatTentSize(tent: TentDto) {
  return !tent.widthCm && !tent.depthCm && !tent.tentHeightCm ? 'Größe offen' : `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm`
}

function formatApiError(caught: unknown, fallback: string) {
  return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback
}

export default HomeAssistantPage
