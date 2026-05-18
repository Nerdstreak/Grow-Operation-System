import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch } from '../api'
import type { CalibrationEventDto, CreateHardwareItemRequest, HardwareItemCriticality, HardwareItemDto, HardwareItemStatus, HydroSetupDto, MaintenanceEventDto, TentDto, UpdateHardwareItemRequest } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1LinkButton, V1Page, V1Section, V1Tabs } from '../components/v1'

type Tab = 'status' | 'inventory' | 'maintenance' | 'mapping'
type HardwareDraft = { name: string; category: string; criticality: HardwareItemCriticality; tentId: string; setupId: string; manufacturer: string; model: string; notes: string }
const criticalityOptions: HardwareItemCriticality[] = ['Low', 'Medium', 'High', 'Critical']

function HardwarePage() {
  const [hardware, setHardware] = useState<HardwareItemDto[]>([])
  const [tents, setTents] = useState<TentDto[]>([])
  const [hydroSetups, setHydroSetups] = useState<HydroSetupDto[]>([])
  const [maintenance, setMaintenance] = useState<MaintenanceEventDto[]>([])
  const [calibration, setCalibration] = useState<CalibrationEventDto[]>([])
  const [tab, setTab] = useState<Tab>('status')
  const [draft, setDraft] = useState<HardwareDraft>(() => createDraft())
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => { void load() }, [])

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const dueBeforeUtc = new Date(Date.now() + 14 * 24 * 60 * 60 * 1000).toISOString()
      const [items, tentData, hydroData, maintenanceData, calibrationData] = await Promise.all([
        apiFetch<HardwareItemDto[]>('/api/hardware-items'),
        apiFetch<TentDto[]>('/api/settings/tents'),
        apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true'),
        apiFetch<MaintenanceEventDto[]>(`/api/maintenance-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`).catch(() => []),
        apiFetch<CalibrationEventDto[]>(`/api/calibration-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`).catch(() => []),
      ])
      setHardware(items)
      setTents(tentData)
      setHydroSetups(hydroData)
      setMaintenance(maintenanceData)
      setCalibration(calibrationData)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Sensoren konnten nicht geladen werden.')
    } finally {
      setLoading(false)
    }
  }

  const sensors = useMemo(() => hardware.filter((item) => isSensorLike(item)), [hardware])
  const offline = sensors.filter((item) => item.status === 'Offline' || item.status === 'Retired').length
  const trust = sensors.length === 0 ? 0 : Math.max(0, 100 - offline * 25)
  const plannedMaintenance = maintenance.filter((event) => event.status === 'Planned')
  const plannedCalibration = calibration.filter((event) => event.status === 'Planned')

  async function saveHardware(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!draft.name.trim()) {
      setError('Bitte Gerätename eingeben.')
      return
    }

    setSaving('hardware')
    setError(null)
    setMessage(null)
    const request: CreateHardwareItemRequest = {
      name: draft.name.trim(),
      category: draft.category.trim() || 'Sensor',
      status: 'Active',
      criticality: draft.criticality,
      tentId: toIntOrNull(draft.tentId),
      setupId: toIntOrNull(draft.setupId),
      haEntityId: null,
      manufacturer: nullable(draft.manufacturer),
      model: nullable(draft.model),
      notes: nullable(draft.notes),
      installedAtUtc: new Date().toISOString(),
    }

    try {
      await apiFetch<HardwareItemDto>('/api/hardware-items', { method: 'POST', body: JSON.stringify(request) })
      setMessage('Hardware angelegt. Entity-Verknüpfung erfolgt separat im Home-Assistant-Mapping.')
      setDraft(createDraft())
      await load()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Hardware konnte nicht angelegt werden.')
    } finally {
      setSaving(null)
    }
  }

  async function updateHardwareStatus(item: HardwareItemDto, status: HardwareItemStatus) {
    setSaving(`hardware-${item.id}`)
    setError(null)
    const request: UpdateHardwareItemRequest = { name: item.name, category: item.category, status, criticality: item.criticality, tentId: item.tentId, setupId: item.setupId, haEntityId: item.haEntityId, manufacturer: item.manufacturer, model: item.model, notes: item.notes, installedAtUtc: item.installedAtUtc, retiredAtUtc: item.retiredAtUtc, wearTemplateId: item.wearTemplateId }
    try {
      await apiFetch<HardwareItemDto>(`/api/hardware-items/${item.id}`, { method: 'PUT', body: JSON.stringify(request) })
      await load()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Hardwarestatus konnte nicht geändert werden.')
    } finally {
      setSaving(null)
    }
  }

  return (
    <V1Page eyebrow="Ops" title="Sensoren" subtitle="Physisches Inventar, Zeltzuordnung und Wartungsstatus. HA-Entities werden separat unter Home Assistant gemappt.">
      {error && <V1Alert title="Fehler" message={error} tone="warn" />}
      {message && <V1Alert message={message} tone="ok" />}

      <section className="v1-kpi-grid">
        <V1Card tone={sensors.length === 0 ? 'neutral' : trust < 60 ? 'critical' : trust < 85 ? 'warn' : 'ok'}><span className="v1-card-kicker">Sensorvertrauen</span><h2>{sensors.length === 0 ? 'nicht bewertet' : `${trust}%`}</h2><p>{sensors.length} Sensoren · {offline} offline</p></V1Card>
        <V1Card><span className="v1-card-kicker">Inventar</span><h2>{hardware.length}</h2><p>Geräte</p></V1Card>
        <V1Card><span className="v1-card-kicker">Zelte</span><h2>{tents.length}</h2><p>Zuordnung möglich</p></V1Card>
        <V1Card tone={plannedMaintenance.length + plannedCalibration.length > 0 ? 'warn' : 'ok'}><span className="v1-card-kicker">Pflege</span><h2>{plannedMaintenance.length + plannedCalibration.length}</h2><p>Wartung/Kalibrierung fällig</p></V1Card>
      </section>

      <V1Tabs<Tab> label="Sensoren Bereich" active={tab} onChange={setTab} items={[{ value: 'status', label: 'Status', meta: `${sensors.length} Sensoren` }, { value: 'inventory', label: 'Inventar', meta: `${hardware.length} Geräte` }, { value: 'maintenance', label: 'Wartung', meta: `${plannedMaintenance.length + plannedCalibration.length} offen` }, { value: 'mapping', label: 'Mapping', meta: 'HA getrennt' }]} />

      {loading ? <V1Empty title="Lade Sensoren..." /> : tab === 'status' ? (
        <V1Section title="Sensorstatus">
          {sensors.length === 0 ? <V1Empty title="Noch keine Sensor-Hardware" text="Lege pH-, EC-, ORP-, DO- oder Klima-Sensoren im Inventar an." action={<V1Button variant="primary" onClick={() => setTab('inventory')}>Inventar öffnen</V1Button>} /> : (
            <div className="ops1b-sensor-grid">
              {sensors.map((item) => <HardwareCard key={item.id} item={item} saving={saving === `hardware-${item.id}`} onStatus={updateHardwareStatus} />)}
            </div>
          )}
        </V1Section>
      ) : tab === 'inventory' ? (
        <div className="ops1b-workflow-grid">
          <V1Section title="Inventar">
            {hardware.length === 0 ? <V1Empty title="Keine Hardware angelegt" /> : (
              <div className="ops1b-inventory-grid">
                {hardware.map((item) => <HardwareCard key={item.id} item={item} saving={saving === `hardware-${item.id}`} onStatus={updateHardwareStatus} />)}
              </div>
            )}
          </V1Section>

          <V1Section title="Sensor oder Gerät anlegen">
            <form className="ops1b-form" onSubmit={(event) => void saveHardware(event)}>
              <div className="ops1b-form-grid">
                <V1Field label="Name" wide><input value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} placeholder="pH Sonde Hauptzelt" /></V1Field>
                <V1Field label="Kategorie"><input value={draft.category} onChange={(event) => setDraft((current) => ({ ...current, category: event.target.value }))} placeholder="Sensor / Pumpe / Chiller" /></V1Field>
                <V1Field label="Kritikalität"><select value={draft.criticality} onChange={(event) => setDraft((current) => ({ ...current, criticality: event.target.value as HardwareItemCriticality }))}>{criticalityOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></V1Field>
                <V1Field label="Zelt"><select value={draft.tentId} onChange={(event) => setDraft((current) => ({ ...current, tentId: event.target.value }))}><option value="">Kein Zelt</option>{tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}</select></V1Field>
                <V1Field label="Hydro-Setup"><select value={draft.setupId} onChange={(event) => setDraft((current) => ({ ...current, setupId: event.target.value }))}><option value="">Kein Hydro-Setup</option>{hydroSetups.filter((setup) => !draft.tentId || String(setup.tentId) === draft.tentId).map((setup) => <option key={setup.id} value={setup.id}>{setup.name}</option>)}</select></V1Field>
                <V1Field label="Hersteller"><input value={draft.manufacturer} onChange={(event) => setDraft((current) => ({ ...current, manufacturer: event.target.value }))} /></V1Field>
                <V1Field label="Modell"><input value={draft.model} onChange={(event) => setDraft((current) => ({ ...current, model: event.target.value }))} /></V1Field>
                <V1Field label="Notizen" wide><textarea value={draft.notes} onChange={(event) => setDraft((current) => ({ ...current, notes: event.target.value }))} rows={3} /></V1Field>
              </div>
              <div className="ops1b-sticky-actions"><V1Button type="submit" variant="primary" disabled={saving === 'hardware'}>{saving === 'hardware' ? 'Speichert...' : 'Hardware anlegen'}</V1Button></div>
            </form>
          </V1Section>
        </div>
      ) : tab === 'maintenance' ? (
        <V1Section title="Wartung & Kalibrierung">
          {plannedMaintenance.length + plannedCalibration.length === 0 ? <V1Empty title="Keine fälligen Sensoraufgaben" /> : (
            <div className="ops1b-inventory-grid">
              {plannedMaintenance.map((event) => <EventCard key={`m-${event.id}`} title={event.title} meta={`${getHardwareName(hardware, event.hardwareItemId)} · ${formatDate(event.dueAtUtc)}`} />)}
              {plannedCalibration.map((event) => <EventCard key={`c-${event.id}`} title={event.title} meta={`${getHardwareName(hardware, event.hardwareItemId)} · ${formatDate(event.dueAtUtc)}`} />)}
            </div>
          )}
        </V1Section>
      ) : (
        <V1Section title="Home-Assistant-Mapping">
          <V1Card>
            <span className="v1-card-kicker">Getrennte Zuständigkeit</span>
            <h2>Inventar ist nicht Entity-Mapping</h2>
            <p>Sensoren werden hier als Hardware geführt. Die konkrete Home-Assistant-Entity wird im HA-Mapping am Zelt gepflegt.</p>
            <div className="v1-action-row"><V1LinkButton to="/home-assistant" variant="primary">Entity im HA-Mapping verknüpfen</V1LinkButton></div>
          </V1Card>
        </V1Section>
      )}
    </V1Page>
  )
}

function HardwareCard({ item, saving, onStatus }: { item: HardwareItemDto; saving: boolean; onStatus: (item: HardwareItemDto, status: HardwareItemStatus) => void }) {
  const tone = item.status === 'Active' ? 'ok' : item.status === 'MaintenanceDue' ? 'warn' : 'critical'
  return (
    <V1Card tone={tone}>
      <div className="v1-card-title-row"><div><span className="v1-card-kicker">{item.category}</span><h2>{item.name}</h2></div><V1Badge tone={tone}>{item.status}</V1Badge></div>
      <p>{item.manufacturer ?? 'Hersteller offen'} {item.model ?? ''}</p>
      <p>{item.haEntityId ? `HA: ${item.haEntityId}` : 'HA-Entity im HA-Mapping verknüpfen.'}</p>
      <div className="v1-action-row">
        <V1Button disabled={saving} onClick={() => void onStatus(item, item.status === 'Offline' ? 'Active' : 'Offline')}>{item.status === 'Offline' ? 'Aktivieren' : 'Offline setzen'}</V1Button>
        <V1Button disabled={saving} onClick={() => void onStatus(item, 'MaintenanceDue')}>Wartung</V1Button>
      </div>
    </V1Card>
  )
}

function EventCard({ title, meta }: { title: string; meta: string }) {
  return <V1Card tone="warn"><span className="v1-card-kicker">Fällig</span><h2>{title}</h2><p>{meta}</p></V1Card>
}

function createDraft(): HardwareDraft {
  return { name: '', category: 'Sensor', criticality: 'High', tentId: '', setupId: '', manufacturer: '', model: '', notes: '' }
}

function isSensorLike(item: HardwareItemDto) {
  const text = `${item.name} ${item.category}`.toLowerCase()
  return ['sensor', 'sonde', 'probe', 'ph', 'ec', 'orp', 'do', 'temperatur', 'level'].some((term) => text.includes(term))
}

function nullable(value: string) {
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : null
}

function toIntOrNull(value: string) {
  const parsed = Number.parseInt(value, 10)
  return Number.isFinite(parsed) ? parsed : null
}

function getHardwareName(items: HardwareItemDto[], id: number | null) {
  return id == null ? 'Hardware offen' : items.find((item) => item.id === id)?.name ?? `Hardware #${id}`
}

function formatDate(value: string | null) {
  return value ? value.slice(0, 10) : 'kein Datum'
}

export default HardwarePage
