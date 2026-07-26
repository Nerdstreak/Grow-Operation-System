import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type { CalibrationEventDto, CreateHardwareItemRequest, HardwareDeviceKind, HardwareItemCriticality, HardwareItemDto, HardwareItemStatus, HydroSetupDto, MaintenanceEventDto, TentDto, UpdateHardwareItemRequest } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1LinkButton, V1Page, V1Section } from '../components/v1'
import { classNames, formatSeverityLabel } from '../utils'
import type { HardwareFilter, HardwareRow } from '../features/hardware/hardware-table-model'
import { buildHardwareRows, countBy, dueLabel, filterHardwareRows, statusLabel, statusTone } from '../features/hardware/hardware-table-model'
import '../features/hardware/hardware.css'

const FILTERS: Array<{ value: HardwareFilter; label: string }> = [
  { value: 'alle', label: 'Alle' },
  { value: 'problem', label: 'Braucht Aufmerksamkeit' },
  { value: 'sensoren', label: 'Sensoren' },
  { value: 'geraete', label: 'Technik' },
  { value: 'pflege', label: 'Pflege geplant' },
]

type HardwareDraft = {
  name: string
  category: string
  deviceKind: HardwareDeviceKind
  status: HardwareItemStatus
  criticality: HardwareItemCriticality
  tentId: string
  hydroSetupId: string
  manufacturer: string
  model: string
  serialNumber: string
  calibrationIntervalDays: string
  notes: string
}

const criticalityOptions: HardwareItemCriticality[] = ['Low', 'Medium', 'High', 'Critical']
const statusOptions: HardwareItemStatus[] = ['Active', 'Offline', 'MaintenanceDue', 'Retired']

// Only manually creatable kinds. Fixed sensors are never created here — they appear
// automatically from the Home Assistant mapping.
const deviceKindOptions: Array<{ value: HardwareDeviceKind; label: string }> = [
  { value: 'HandheldMeter', label: 'Messgerät (mobil, ohne HA)' },
  { value: 'Equipment', label: 'Gerät (Pumpe, Chiller, USV …)' },
]

function deviceKindLabel(kind: HardwareDeviceKind | null | undefined): string | null {
  switch (kind) {
    case 'FixedSensor': return 'HA-Sensor'
    case 'HandheldMeter': return 'Messgerät'
    case 'Equipment': return 'Gerät'
    default: return null
  }
}

function HardwarePage() {
  const [hardware, setHardware] = useState<HardwareItemDto[]>([])
  const [tents, setTents] = useState<TentDto[]>([])
  const [hydroSetups, setHydroSetups] = useState<HydroSetupDto[]>([])
  const [maintenance, setMaintenance] = useState<MaintenanceEventDto[]>([])
  const [calibration, setCalibration] = useState<CalibrationEventDto[]>([])
  const [filter, setFilter] = useState<HardwareFilter>('alle')
  const [formOpen, setFormOpen] = useState(false)
  const [draft, setDraft] = useState<HardwareDraft>(() => createDraft())
  const [editingId, setEditingId] = useState<number | null>(null)
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

  const rows = useMemo(
    () => buildHardwareRows(hardware, tents, maintenance, calibration),
    [hardware, tents, maintenance, calibration],
  )
  const counts = useMemo(() => countBy(rows), [rows])
  const visibleRows = useMemo(() => filterHardwareRows(rows, filter), [rows, filter])

  function startEdit(item: HardwareItemDto) {
    setEditingId(item.id)
    setDraft(createDraft(item))
    setFormOpen(true)
    setError(null)
    setMessage(null)
  }

  function openCreate() {
    setEditingId(null)
    setDraft(createDraft())
    setFormOpen(true)
    setError(null)
    setMessage(null)
  }

  function closeForm() {
    setEditingId(null)
    setDraft(createDraft())
    setFormOpen(false)
    setError(null)
  }

  async function saveHardware(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!draft.name.trim()) {
      setError('Bitte Gerätename eingeben.')
      return
    }

    const existing = editingId ? hardware.find((item) => item.id === editingId) ?? null : null
    const request: CreateHardwareItemRequest | UpdateHardwareItemRequest = {
      name: draft.name.trim(),
      category: draft.category.trim() || 'Sensor',
      status: draft.status,
      criticality: draft.criticality,
      tentId: toIntOrNull(draft.tentId),
      setupId: null,
      hydroSetupId: toIntOrNull(draft.hydroSetupId),
      // Entity mapping lives on the Home Assistant page; the sync writes it onto the
      // item. Preserve the synced value here — a form edit must not clear it.
      haEntityId: existing?.haEntityId ?? null,
      deviceKind: draft.deviceKind,
      manufacturer: nullable(draft.manufacturer),
      model: nullable(draft.model),
      serialNumber: nullable(draft.serialNumber),
      notes: nullable(draft.notes),
      installedAtUtc: existing?.installedAtUtc ?? new Date().toISOString(),
      retiredAtUtc: existing?.retiredAtUtc ?? null,
      wearTemplateId: existing?.wearTemplateId ?? null,
      tentSensorId: existing?.tentSensorId ?? null,
      growId: existing?.growId ?? null,
      expectedLifespanDays: existing?.expectedLifespanDays ?? null,
      inspectionIntervalDays: existing?.inspectionIntervalDays ?? null,
      calibrationIntervalDays: toIntOrNull(draft.calibrationIntervalDays),
    }

    setSaving('hardware')
    setError(null)
    setMessage(null)
    try {
      if (editingId) {
        await apiFetch<HardwareItemDto>(`/api/hardware-items/${editingId}`, { method: 'PUT', body: JSON.stringify(request) })
        setMessage('Sensor gespeichert.')
      } else {
        await apiFetch<HardwareItemDto>('/api/hardware-items', { method: 'POST', body: JSON.stringify(request) })
        setMessage('Hardware angelegt. Live-Werte verbindest du im Tab „Home Assistant" am Zelt.')
      }
      setEditingId(null)
      setDraft(createDraft())
      await load()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : editingId ? 'Sensor konnte nicht gespeichert werden.' : 'Hardware konnte nicht angelegt werden.')
    } finally {
      setSaving(null)
    }
  }

  async function updateHardwareStatus(item: HardwareItemDto, status: HardwareItemStatus) {
    setSaving(`hardware-${item.id}`)
    setError(null)
    const request: UpdateHardwareItemRequest = {
      name: item.name,
      category: item.category,
      status,
      criticality: item.criticality,
      tentId: item.tentId,
      setupId: item.setupId,
      hydroSetupId: item.hydroSetupId,
      haEntityId: item.haEntityId,
      manufacturer: item.manufacturer,
      model: item.model,
      serialNumber: item.serialNumber,
      notes: item.notes,
      installedAtUtc: item.installedAtUtc,
      retiredAtUtc: item.retiredAtUtc,
      wearTemplateId: item.wearTemplateId,
      tentSensorId: item.tentSensorId,
      growId: item.growId,
      expectedLifespanDays: item.expectedLifespanDays,
      inspectionIntervalDays: item.inspectionIntervalDays,
      calibrationIntervalDays: item.calibrationIntervalDays,
    }
    try {
      await apiFetch<HardwareItemDto>(`/api/hardware-items/${item.id}`, { method: 'PUT', body: JSON.stringify(request) })
      await load()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Hardwarestatus konnte nicht geändert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function deleteHardware(item: HardwareItemDto) {
    if (saving) return
    const confirmed = window.confirm(`${item.name} endgültig löschen?`)
    if (!confirmed) return

    setSaving(`hardware-${item.id}`)
    setError(null)
    setMessage(null)
    try {
      await apiFetch(`/api/hardware-items/${item.id}`, { method: 'DELETE' })
      setHardware((current) => current.filter((hardwareItem) => hardwareItem.id !== item.id))
      setMessage('Sensor gelöscht.')
      if (editingId === item.id) {
        setEditingId(null)
        setDraft(createDraft())
      }
      await load()
    } catch (caught) {
      if (isNotFound(caught)) {
        setHardware((current) => current.filter((hardwareItem) => hardwareItem.id !== item.id))
        setMessage('Eintrag existiert bereits nicht mehr.')
        if (editingId === item.id) {
          setEditingId(null)
          setDraft(createDraft())
        }
        await load()
        return
      }
      setError(caught instanceof Error ? caught.message : 'Sensor konnte nicht gelöscht werden.')
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

      {loading ? <V1Empty title="Lade Sensoren..." /> : (
        <>
          <V1Section
            title="Geräte"
            action={!formOpen && <V1Button variant="primary" onClick={openCreate}>Gerät anlegen</V1Button>}
          >
            {/* Filter statt Tabs: die Tabs zeigten Teilmengen derselben Liste auf
                getrennten Seiten, sodass dieselbe Sonde in dreien davon stand und
                man Status und Kalibriertermin nicht zusammen sehen konnte. */}
            <div className="hw-filters" role="group" aria-label="Liste einschränken">
              {FILTERS.map(({ value, label }) => (
                <button
                  key={value}
                  type="button"
                  className={classNames('hw-filter', filter === value && 'active')}
                  aria-pressed={filter === value}
                  onClick={() => setFilter(value)}
                  data-audit={`hardware-filter-${value}`}
                >
                  {label}<span className="hw-filter-count">{counts[value]}</span>
                </button>
              ))}
            </div>

            {visibleRows.length === 0 ? (
              <V1Empty
                title={rows.length === 0 ? 'Noch keine Hardware angelegt' : 'Nichts in dieser Auswahl'}
                text={rows.length === 0 ? 'Feste Sensoren erscheinen automatisch, sobald du sie unter Home Assistant zuordnest. Messgeräte und Technik legst du hier an.' : undefined}
                action={rows.length === 0 ? <V1Button variant="primary" onClick={openCreate}>Gerät anlegen</V1Button> : <V1Button onClick={() => setFilter('alle')}>Alle zeigen</V1Button>}
              />
            ) : (
              <div className="hw-table-wrap">
                <table className="hw-table" data-audit="hardware-table">
                  <thead>
                    <tr>
                      <th scope="col">Gerät</th>
                      <th scope="col">Zelt</th>
                      <th scope="col">Status</th>
                      <th scope="col">Home Assistant</th>
                      <th scope="col">Nächste Pflege</th>
                      <th scope="col"><span className="sr-only">Aktionen</span></th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleRows.map((row) => (
                      <HardwareRowView
                        key={row.item.id}
                        row={row}
                        saving={saving === `hardware-${row.item.id}`}
                        onStatus={updateHardwareStatus}
                        onEdit={startEdit}
                        onDelete={deleteHardware}
                      />
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <p className="hw-note">
              Feste Sensoren legt Grow OS selbst an, sobald du unter{' '}
              <V1LinkButton to="/home-assistant" variant="ghost">Home Assistant</V1LinkButton>{' '}
              eine Entity zuordnest — samt Kalibrier-Intervall, das du hier je Sensor ändern kannst.
            </p>
          </V1Section>

          {formOpen && (
          <V1Section title={editingId ? 'Sensor oder Gerät bearbeiten' : 'Sensor oder Gerät anlegen'}>
            <form className="ops1b-form" data-audit="hardware-edit-form" onSubmit={(event) => void saveHardware(event)}>
              <div className="ops1b-form-grid">
                <V1Field label="Name" wide><input value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} placeholder="pH Sonde Hauptzelt" /></V1Field>
                {draft.deviceKind === 'FixedSensor' ? (
                  <V1Field label="Art" hint="Kommt aus dem HA-Mapping.">
                    <input value="Fester Sensor" readOnly disabled />
                  </V1Field>
                ) : (
                  <V1Field label="Art" hint="Messgeräte werden kalibriert, Geräte gewartet.">
                    <select value={draft.deviceKind} onChange={(event) => setDraft((current) => ({ ...current, deviceKind: event.target.value as HardwareDeviceKind }))}>
                      {deviceKindOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
                    </select>
                  </V1Field>
                )}
                <V1Field label="Kategorie"><input value={draft.category} onChange={(event) => setDraft((current) => ({ ...current, category: event.target.value }))} placeholder="Sensor / Pumpe / Chiller" /></V1Field>
                <V1Field label="Status"><select value={draft.status} onChange={(event) => setDraft((current) => ({ ...current, status: event.target.value as HardwareItemStatus }))}>{statusOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></V1Field>
                <V1Field label="Kritikalität"><select value={draft.criticality} onChange={(event) => setDraft((current) => ({ ...current, criticality: event.target.value as HardwareItemCriticality }))}>{criticalityOptions.map((item) => <option key={item} value={item}>{formatSeverityLabel(item)}</option>)}</select></V1Field>
                <V1Field label="Zelt"><select value={draft.tentId} onChange={(event) => setDraft((current) => ({ ...current, tentId: event.target.value, hydroSetupId: '' }))}><option value="">Kein Zelt</option>{tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}</select></V1Field>
                <V1Field label="Hydro-Setup"><select value={draft.hydroSetupId} onChange={(event) => setDraft((current) => ({ ...current, hydroSetupId: event.target.value }))}><option value="">Kein Hydro-Setup</option>{hydroSetups.filter((setup) => !draft.tentId || String(setup.tentId) === draft.tentId).map((setup) => <option key={setup.id} value={setup.id}>{setup.name}</option>)}</select></V1Field>
                <V1Field label="Hersteller"><input value={draft.manufacturer} onChange={(event) => setDraft((current) => ({ ...current, manufacturer: event.target.value }))} /></V1Field>
                <V1Field label="Modell"><input value={draft.model} onChange={(event) => setDraft((current) => ({ ...current, model: event.target.value }))} /></V1Field>
                <V1Field label="Seriennummer"><input value={draft.serialNumber} onChange={(event) => setDraft((current) => ({ ...current, serialNumber: event.target.value }))} /></V1Field>
                <V1Field label="Kalibrieren alle (Tage)" hint="Erinnerung nach jeder Kalibrierung; leer = Standard je Typ"><input type="number" min="1" value={draft.calibrationIntervalDays} onChange={(event) => setDraft((current) => ({ ...current, calibrationIntervalDays: event.target.value }))} placeholder="z. B. 14" /></V1Field>
                <V1Field label="Notizen" wide><textarea value={draft.notes} onChange={(event) => setDraft((current) => ({ ...current, notes: event.target.value }))} rows={3} /></V1Field>
              </div>
              <div className="ops1b-sticky-actions">
                <V1Button type="button" variant="ghost" onClick={closeForm}>Abbrechen</V1Button>
                <V1Button type="submit" variant="primary" disabled={saving === 'hardware'}>{saving === 'hardware' ? 'Speichert...' : editingId ? 'Speichern' : 'Hardware anlegen'}</V1Button>
              </div>
            </form>
          </V1Section>
          )}
        </>
      )}
    </V1Page>
  )
}

/** Eine Gerätezeile. Bei schmalem Rahmen legt hardware.css sie zu einer Karte um. */
function HardwareRowView({ row, saving, onStatus, onEdit, onDelete }: { row: HardwareRow; saving: boolean; onStatus: (item: HardwareItemDto, status: HardwareItemStatus) => void; onEdit: (item: HardwareItemDto) => void; onDelete: (item: HardwareItemDto) => void }) {
  const { item } = row
  return (
    <tr className={classNames(row.overdue && 'overdue')}>
      <td data-label="Gerät">
        <strong>{item.name}</strong>
        <span className="hw-sub">{deviceKindLabel(item.deviceKind) ?? item.category}{item.manufacturer ? ` · ${item.manufacturer}` : ''}</span>
      </td>
      <td data-label="Zelt">{row.tentName ?? <span className="hw-empty">—</span>}</td>
      <td data-label="Status"><V1Badge tone={statusTone(item.status)}>{statusLabel(item.status)}</V1Badge></td>
      <td data-label="Home Assistant">{item.haEntityId ? <code className="hw-entity">{item.haEntityId}</code> : <span className="hw-empty">nicht gemappt</span>}</td>
      <td data-label="Nächste Pflege">
        {row.nextCare ? (
          <>
            <strong className={classNames(row.overdue && 'hw-overdue')}>{dueLabel(row.dueInDays)}</strong>
            <span className="hw-sub">{row.nextCare.kind}: {row.nextCare.title}</span>
          </>
        ) : <span className="hw-empty">—</span>}
      </td>
      <td data-label="Aktionen">
        <div className="hw-actions">
          <V1Button onClick={() => onEdit(item)}>Bearbeiten</V1Button>
          <V1Button disabled={saving} onClick={() => void onStatus(item, item.status === 'Offline' ? 'Active' : 'Offline')}>{item.status === 'Offline' ? 'Aktivieren' : 'Offline'}</V1Button>
          <V1Button variant="danger" disabled={saving} audit="hardware-delete-button" onClick={() => void onDelete(item)}>{saving ? 'Löscht...' : 'Löschen'}</V1Button>
        </div>
      </td>
    </tr>
  )
}

function inferDeviceKind(item?: HardwareItemDto): HardwareDeviceKind {
  if (item?.deviceKind) return item.deviceKind
  if (item?.haEntityId || item?.metricType) return 'FixedSensor'
  if (item && isSensorLike(item)) return 'HandheldMeter'
  return item ? 'Equipment' : 'HandheldMeter'
}

function createDraft(item?: HardwareItemDto): HardwareDraft {
  return {
    name: item?.name ?? '',
    category: item?.category ?? 'Sensor',
    deviceKind: inferDeviceKind(item),
    status: item?.status ?? 'Active',
    criticality: item?.criticality ?? 'High',
    tentId: item?.tentId ? String(item.tentId) : '',
    hydroSetupId: item?.hydroSetupId ? String(item.hydroSetupId) : '',
    manufacturer: item?.manufacturer ?? '',
    model: item?.model ?? '',
    serialNumber: item?.serialNumber ?? '',
    calibrationIntervalDays: item?.calibrationIntervalDays != null ? String(item.calibrationIntervalDays) : '',
    notes: item?.notes ?? '',
  }
}

function isSensorLike(item: HardwareItemDto) {
  // Explicit device kind wins; keyword matching is only the legacy fallback.
  if (item.deviceKind === 'FixedSensor' || item.deviceKind === 'HandheldMeter') return true
  if (item.deviceKind === 'Equipment') return false
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



function isNotFound(caught: unknown) {
  return caught instanceof ApiRequestError && caught.status === 404
}

export default HardwarePage
