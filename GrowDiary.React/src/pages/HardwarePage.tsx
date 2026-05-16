import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch } from '../api'
import type {
  CalibrationEventDto,
  CalibrationEventType,
  CalibrationResult,
  CreateCalibrationEventRequest,
  CreateHardwareItemRequest,
  CreateMaintenanceEventRequest,
  HardwareItemCriticality,
  HardwareItemDto,
  HardwareItemStatus,
  MaintenanceEventDto,
  MaintenanceEventType,
  MaintenanceResult,
  RiskEventDto,
  TentDto,
  UpdateCalibrationEventRequest,
  UpdateHardwareItemRequest,
  UpdateMaintenanceEventRequest,
} from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section, V1Tabs } from '../components/v1'
import { classNames, formatDate, formatDateTime, toLocalInputValue } from '../utils'

type OpsState = {
  hardware: HardwareItemDto[]
  calibration: CalibrationEventDto[]
  maintenance: MaintenanceEventDto[]
  risks: RiskEventDto[]
  tents: TentDto[]
}

type OpsTab = 'overview' | 'calibration' | 'maintenance' | 'inventory'

type CalibrationDraft = {
  hardwareItemId: string
  calibrationType: CalibrationEventType
  title: string
  referenceSolution: string
  referenceValue: string
  beforeValue: string
  afterValue: string
  temperatureC: string
  performedAtLocal: string
  nextDueAtLocal: string
  result: CalibrationResult
  notes: string
}

type MaintenanceDraft = {
  hardwareItemId: string
  eventType: MaintenanceEventType
  title: string
  performedAtLocal: string
  nextDueAtLocal: string
  result: MaintenanceResult
  notes: string
}

type HardwareDraft = {
  name: string
  category: string
  criticality: HardwareItemCriticality
  tentId: string
  haEntityId: string
  manufacturer: string
  model: string
  notes: string
}

type SensorTrust = {
  score: number
  label: string
  tone: 'ok' | 'warn' | 'critical' | 'neutral'
  sensors: HardwareItemDto[]
  offline: number
  dueCalibration: CalibrationEventDto[]
  dueMaintenance: MaintenanceEventDto[]
  criticalRisks: RiskEventDto[]
}

const emptyState: OpsState = { hardware: [], calibration: [], maintenance: [], risks: [], tents: [] }
const calibrationTypes: CalibrationEventType[] = ['Ph', 'Ec', 'Orp', 'Do', 'Other']
const calibrationResults: CalibrationResult[] = ['Passed', 'AdjustmentNeeded', 'Failed', 'Unknown']
const maintenanceTypes: MaintenanceEventType[] = ['Inspection', 'Cleaning', 'Replacement', 'Repair', 'Other']
const maintenanceResults: MaintenanceResult[] = ['Passed', 'ActionNeeded', 'Replaced', 'Failed', 'Unknown']
const criticalityOptions: HardwareItemCriticality[] = ['Low', 'Medium', 'High', 'Critical']

function HardwarePage() {
  const [state, setState] = useState<OpsState>(emptyState)
  const [tab, setTab] = useState<OpsTab>('overview')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [refresh, setRefresh] = useState(0)
  const [calibrationDraft, setCalibrationDraft] = useState<CalibrationDraft>(() => createCalibrationDraft())
  const [maintenanceDraft, setMaintenanceDraft] = useState<MaintenanceDraft>(() => createMaintenanceDraft())
  const [hardwareDraft, setHardwareDraft] = useState<HardwareDraft>(() => createHardwareDraft())

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const [hardware, calibration, maintenance, risks, tents] = await Promise.all([
          apiFetch<HardwareItemDto[]>('/api/hardware-items', { signal: controller.signal }),
          apiFetch<CalibrationEventDto[]>('/api/calibration-events', { signal: controller.signal }),
          apiFetch<MaintenanceEventDto[]>('/api/maintenance-events', { signal: controller.signal }),
          apiFetch<RiskEventDto[]>('/api/risk-events?status=Open', { signal: controller.signal }),
          apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal }),
        ])

        if (controller.signal.aborted) return
        setState({ hardware, calibration, maintenance, risks: risks.filter((risk) => risk.status === 'Open'), tents })
        setCalibrationDraft((current) => current.hardwareItemId ? current : createCalibrationDraft(selectDefaultSensor(hardware)?.id))
        setMaintenanceDraft((current) => current.hardwareItemId ? current : createMaintenanceDraft(hardware[0]?.id))
        setLoading(false)
      } catch (caught) {
        if (!controller.signal.aborted) {
          setError(formatUnknownError(caught, 'Sensoren und Wartung konnten nicht geladen werden.'))
          setLoading(false)
        }
      }
    }

    void load()
    return () => controller.abort()
  }, [refresh])

  const trust = useMemo(() => buildSensorTrust(state), [state])
  const sortedHardware = useMemo(() => [...state.hardware].sort(sortHardware), [state.hardware])
  const sensors = trust.sensors
  const openCalibration = useMemo(() => state.calibration.filter((event) => event.status === 'Planned').sort(sortDue), [state.calibration])
  const openMaintenance = useMemo(() => state.maintenance.filter((event) => event.status === 'Planned').sort(sortDue), [state.maintenance])
  const sensorIssues = useMemo(() => buildSensorIssueRows(state, trust), [state, trust])

  async function reload() {
    setRefresh((current) => current + 1)
  }

  async function saveCalibration(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const hardwareItemId = Number.parseInt(calibrationDraft.hardwareItemId, 10)
    if (!Number.isFinite(hardwareItemId)) {
      setError('Bitte Sensor auswählen.')
      return
    }

    setSaving('calibration')
    setError(null)
    setMessage(null)
    const request: CreateCalibrationEventRequest = {
      hardwareItemId,
      calibrationType: calibrationDraft.calibrationType,
      status: 'Completed',
      result: calibrationDraft.result,
      title: calibrationDraft.title.trim() || `${labelCalibrationType(calibrationDraft.calibrationType)} kalibriert`,
      referenceSolution: nullable(calibrationDraft.referenceSolution),
      referenceValue: toNumber(calibrationDraft.referenceValue),
      beforeValue: toNumber(calibrationDraft.beforeValue),
      afterValue: toNumber(calibrationDraft.afterValue),
      temperatureC: toNumber(calibrationDraft.temperatureC),
      performedAtUtc: toUtc(calibrationDraft.performedAtLocal),
      nextDueAtUtc: toUtc(calibrationDraft.nextDueAtLocal),
      notes: nullable(calibrationDraft.notes),
    }

    try {
      await apiFetch<CalibrationEventDto>('/api/calibration-events', { method: 'POST', body: JSON.stringify(request) })
      setMessage('Kalibrierung gespeichert.')
      setCalibrationDraft(createCalibrationDraft(hardwareItemId))
      await reload()
    } catch (caught) {
      setError(formatUnknownError(caught, 'Kalibrierung konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function saveMaintenance(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const hardwareItemId = Number.parseInt(maintenanceDraft.hardwareItemId, 10)
    if (!Number.isFinite(hardwareItemId)) {
      setError('Bitte Hardware auswählen.')
      return
    }

    setSaving('maintenance')
    setError(null)
    setMessage(null)
    const request: CreateMaintenanceEventRequest = {
      hardwareItemId,
      eventType: maintenanceDraft.eventType,
      status: 'Completed',
      result: maintenanceDraft.result,
      title: maintenanceDraft.title.trim() || `${labelMaintenanceType(maintenanceDraft.eventType)} erledigt`,
      performedAtUtc: toUtc(maintenanceDraft.performedAtLocal),
      nextDueAtUtc: toUtc(maintenanceDraft.nextDueAtLocal),
      notes: nullable(maintenanceDraft.notes),
    }

    try {
      await apiFetch<MaintenanceEventDto>('/api/maintenance-events', { method: 'POST', body: JSON.stringify(request) })
      setMessage('Wartung gespeichert.')
      setMaintenanceDraft(createMaintenanceDraft(hardwareItemId))
      await reload()
    } catch (caught) {
      setError(formatUnknownError(caught, 'Wartung konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function saveHardware(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!hardwareDraft.name.trim()) {
      setError('Bitte Gerätename eingeben.')
      return
    }

    setSaving('hardware')
    setError(null)
    setMessage(null)
    const request: CreateHardwareItemRequest = {
      name: hardwareDraft.name.trim(),
      category: hardwareDraft.category.trim() || 'Sensor',
      status: 'Active',
      criticality: hardwareDraft.criticality,
      tentId: toIntOrNull(hardwareDraft.tentId),
      haEntityId: nullable(hardwareDraft.haEntityId),
      manufacturer: nullable(hardwareDraft.manufacturer),
      model: nullable(hardwareDraft.model),
      notes: nullable(hardwareDraft.notes),
      installedAtUtc: new Date().toISOString(),
    }

    try {
      const saved = await apiFetch<HardwareItemDto>('/api/hardware-items', { method: 'POST', body: JSON.stringify(request) })
      setMessage('Hardware angelegt.')
      setHardwareDraft(createHardwareDraft())
      setCalibrationDraft((current) => current.hardwareItemId ? current : createCalibrationDraft(saved.id))
      await reload()
    } catch (caught) {
      setError(formatUnknownError(caught, 'Hardware konnte nicht angelegt werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function updateHardwareStatus(item: HardwareItemDto, status: HardwareItemStatus) {
    setSaving(`hardware-${item.id}`)
    setError(null)
    setMessage(null)
    const request: UpdateHardwareItemRequest = { ...hardwareToRequest(item), status }
    try {
      await apiFetch<HardwareItemDto>(`/api/hardware-items/${item.id}`, { method: 'PUT', body: JSON.stringify(request) })
      setMessage(`${item.name} aktualisiert.`)
      await reload()
    } catch (caught) {
      setError(formatUnknownError(caught, 'Hardwarestatus konnte nicht aktualisiert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function completeCalibrationEvent(item: CalibrationEventDto) {
    setSaving(`calibration-${item.id}`)
    setError(null)
    setMessage(null)
    const request: UpdateCalibrationEventRequest = {
      hardwareItemId: item.hardwareItemId,
      calibrationType: item.calibrationType,
      status: 'Completed',
      result: item.result === 'Unknown' ? 'Passed' : item.result,
      title: item.title,
      referenceSolution: item.referenceSolution,
      referenceValue: item.referenceValue,
      beforeValue: item.beforeValue,
      afterValue: item.afterValue,
      temperatureC: item.temperatureC,
      dueAtUtc: item.dueAtUtc,
      performedAtUtc: new Date().toISOString(),
      nextDueAtUtc: item.nextDueAtUtc ?? new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
      growTaskId: item.growTaskId,
      notes: item.notes,
    }
    try {
      await apiFetch<CalibrationEventDto>(`/api/calibration-events/${item.id}`, { method: 'PUT', body: JSON.stringify(request) })
      setMessage('Kalibriertermin erledigt.')
      await reload()
    } catch (caught) {
      setError(formatUnknownError(caught, 'Kalibriertermin konnte nicht abgeschlossen werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function completeMaintenanceEvent(item: MaintenanceEventDto) {
    setSaving(`maintenance-${item.id}`)
    setError(null)
    setMessage(null)
    const request: UpdateMaintenanceEventRequest = {
      hardwareItemId: item.hardwareItemId,
      eventType: item.eventType,
      status: 'Completed',
      result: item.result === 'Unknown' ? 'Passed' : item.result,
      title: item.title,
      description: item.description,
      dueAtUtc: item.dueAtUtc,
      performedAtUtc: new Date().toISOString(),
      nextDueAtUtc: item.nextDueAtUtc ?? new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
      growTaskId: item.growTaskId,
      sopInstanceId: item.sopInstanceId,
      notes: item.notes,
    }
    try {
      await apiFetch<MaintenanceEventDto>(`/api/maintenance-events/${item.id}`, { method: 'PUT', body: JSON.stringify(request) })
      setMessage('Wartung erledigt.')
      await reload()
    } catch (caught) {
      setError(formatUnknownError(caught, 'Wartung konnte nicht abgeschlossen werden.'))
    } finally {
      setSaving(null)
    }
  }

  return (
    <V1Page
      eyebrow="Ops"
      title="Sensoren"
      subtitle="Sensorvertrauen, Kalibrierung und Wartung."
      action={<V1Button onClick={() => setRefresh((current) => current + 1)}>Aktualisieren</V1Button>}
      className="ops1b-page"
    >
      {error && <V1Alert title="Fehler" message={error} tone="critical" />}
      {message && <V1Alert title="Gespeichert" message={message} tone="ok" />}

      <section className="ops1b-hero-grid">
        <V1Card className="ops1b-score-card" tone={trust.tone}>
          <div>
            <span className="ops1b-kicker">Sensorvertrauen</span>
            <strong>{loading ? '...' : trust.score}<em>{loading ? '' : '%'}</em></strong>
            <p>{trust.label}</p>
          </div>
          <div className="ops1b-score-meta">
            <span>{trust.sensors.length} Sensoren</span>
            <span>{trust.offline} offline</span>
            <span>{trust.dueCalibration.length} Kalibrierung</span>
            <span>{trust.dueMaintenance.length} Wartung</span>
          </div>
        </V1Card>

        <V1Card className="ops1b-next-card" tone={sensorIssues.length > 0 ? 'warn' : 'ok'}>
          <div className="v1-card-title-row">
            <div>
              <span className="v1-card-kicker">Jetzt wichtig</span>
              <h2>{sensorIssues.length > 0 ? 'Prüfen' : 'Stabil'}</h2>
            </div>
            <V1Badge tone={sensorIssues.length > 0 ? 'warn' : 'ok'}>{sensorIssues.length}</V1Badge>
          </div>

          {sensorIssues.length === 0 ? (
            <p className="ops1b-soft-text">Keine akuten Sensor-, Kalibrierungs- oder Wartungsthemen.</p>
          ) : (
            <div className="ops1b-mini-list">
              {sensorIssues.slice(0, 3).map((item) => (
                <button key={item.id} type="button" className={classNames('ops1b-mini-row', item.tone)} onClick={() => setTab(item.badge === 'Kalibrierung' ? 'calibration' : item.badge === 'Wartung' ? 'maintenance' : 'overview')}>
                  <strong>{item.title}</strong>
                  <span>{item.meta}</span>
                </button>
              ))}
            </div>
          )}

          <div className="ops1b-action-grid">
            <V1Button onClick={() => setTab('calibration')}>Kalibrieren</V1Button>
            <V1Button onClick={() => setTab('maintenance')}>Wartung</V1Button>
            <V1Button onClick={() => setTab('inventory')}>Inventar</V1Button>
          </div>
        </V1Card>
      </section>

      <V1Tabs<OpsTab>
        label="Sensoren Bereich"
        active={tab}
        onChange={setTab}
        items={[
          { value: 'overview', label: 'Status', meta: trust.label },
          { value: 'calibration', label: 'Kalibrierung', meta: `${openCalibration.length} offen` },
          { value: 'maintenance', label: 'Wartung', meta: `${openMaintenance.length} offen` },
          { value: 'inventory', label: 'Inventar', meta: `${state.hardware.length} Geräte` },
        ]}
      />

      {tab === 'overview' && (
        <div className="ops1b-stack">
          <V1Section title="Status">
            {sensorIssues.length === 0 ? (
              <V1Empty title="Keine akuten Sensor-Themen" text="Kalibrierung und Wartung wirken aktuell stabil." />
            ) : (
              <div className="ops1b-issue-grid">
                {sensorIssues.map((item) => (
                  <button key={item.id} type="button" className={classNames('ops1b-issue-card', item.tone)} onClick={() => setTab(item.badge === 'Kalibrierung' ? 'calibration' : item.badge === 'Wartung' ? 'maintenance' : 'overview')}>
                    <span>{item.badge}</span>
                    <strong>{item.title}</strong>
                    <small>{item.meta}</small>
                  </button>
                ))}
              </div>
            )}
          </V1Section>

          <V1Section title="Sensoren">
            {sensors.length === 0 ? (
              <V1Empty title="Noch keine Sensor-Hardware" text="Lege pH-, EC-, ORP- oder DO-Sonden im Inventar an." action={<V1Button onClick={() => setTab('inventory')} variant="primary">Sensor anlegen</V1Button>} />
            ) : (
              <div className="ops1b-sensor-grid">
                {sensors.map((item) => <SensorCard key={item.id} item={item} state={state} onStatus={updateHardwareStatus} saving={saving === `hardware-${item.id}`} />)}
              </div>
            )}
          </V1Section>
        </div>
      )}

      {tab === 'calibration' && (
        <div className="ops1b-workflow-grid">
          <V1Section title="Kalibrier-Assistent">
            <form className="ops1b-form" onSubmit={saveCalibration}>
              <div className="ops1b-form-grid">
                <V1Field label="Sensor" wide>
                  <select value={calibrationDraft.hardwareItemId} onChange={(event) => setCalibrationDraft((current) => ({ ...current, hardwareItemId: event.target.value }))}>
                    <option value="">Sensor auswählen</option>
                    {sortedHardware.map((item) => <option key={item.id} value={item.id}>{item.name} · {item.category}</option>)}
                  </select>
                </V1Field>
                <V1Field label="Typ"><select value={calibrationDraft.calibrationType} onChange={(event) => setCalibrationDraft((current) => ({ ...current, calibrationType: event.target.value as CalibrationEventType }))}>{calibrationTypes.map((type) => <option key={type} value={type}>{labelCalibrationType(type)}</option>)}</select></V1Field>
                <V1Field label="Ergebnis"><select value={calibrationDraft.result} onChange={(event) => setCalibrationDraft((current) => ({ ...current, result: event.target.value as CalibrationResult }))}>{calibrationResults.map((result) => <option key={result} value={result}>{labelCalibrationResult(result)}</option>)}</select></V1Field>
                <V1Field label="Referenzlösung"><input value={calibrationDraft.referenceSolution} onChange={(event) => setCalibrationDraft((current) => ({ ...current, referenceSolution: event.target.value }))} placeholder="pH 7.00 / 1413 µS" /></V1Field>
                <V1Field label="Referenzwert"><input inputMode="decimal" value={calibrationDraft.referenceValue} onChange={(event) => setCalibrationDraft((current) => ({ ...current, referenceValue: event.target.value }))} /></V1Field>
                <V1Field label="Vorher"><input inputMode="decimal" value={calibrationDraft.beforeValue} onChange={(event) => setCalibrationDraft((current) => ({ ...current, beforeValue: event.target.value }))} /></V1Field>
                <V1Field label="Nachher"><input inputMode="decimal" value={calibrationDraft.afterValue} onChange={(event) => setCalibrationDraft((current) => ({ ...current, afterValue: event.target.value }))} /></V1Field>
                <V1Field label="Temperatur °C"><input inputMode="decimal" value={calibrationDraft.temperatureC} onChange={(event) => setCalibrationDraft((current) => ({ ...current, temperatureC: event.target.value }))} /></V1Field>
                <V1Field label="Durchgeführt"><input type="datetime-local" value={calibrationDraft.performedAtLocal} onChange={(event) => setCalibrationDraft((current) => ({ ...current, performedAtLocal: event.target.value }))} /></V1Field>
                <V1Field label="Nächste Fälligkeit"><input type="datetime-local" value={calibrationDraft.nextDueAtLocal} onChange={(event) => setCalibrationDraft((current) => ({ ...current, nextDueAtLocal: event.target.value }))} /></V1Field>
                <V1Field label="Titel" wide><input value={calibrationDraft.title} onChange={(event) => setCalibrationDraft((current) => ({ ...current, title: event.target.value }))} placeholder="z. B. pH Sonde 2-Punkt-Kalibrierung" /></V1Field>
                <V1Field label="Notizen" wide><textarea value={calibrationDraft.notes} onChange={(event) => setCalibrationDraft((current) => ({ ...current, notes: event.target.value }))} rows={3} /></V1Field>
              </div>
              <div className="ops1b-sticky-actions"><V1Button type="submit" variant="primary" disabled={saving === 'calibration'}>{saving === 'calibration' ? 'Speichert...' : 'Kalibrierung speichern'}</V1Button></div>
            </form>
          </V1Section>

          <V1Section title="Offene Kalibrierungen">
            <EventList
              type="calibration"
              calibration={openCalibration}
              maintenance={[]}
              hardware={state.hardware}
              saving={saving}
              onCalibrationDone={completeCalibrationEvent}
              onMaintenanceDone={completeMaintenanceEvent}
            />
          </V1Section>
        </div>
      )}

      {tab === 'maintenance' && (
        <div className="ops1b-workflow-grid">
          <V1Section title="Wartung dokumentieren">
            <form className="ops1b-form" onSubmit={saveMaintenance}>
              <div className="ops1b-form-grid">
                <V1Field label="Hardware" wide><select value={maintenanceDraft.hardwareItemId} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, hardwareItemId: event.target.value }))}><option value="">Hardware auswählen</option>{sortedHardware.map((item) => <option key={item.id} value={item.id}>{item.name} · {item.category}</option>)}</select></V1Field>
                <V1Field label="Typ"><select value={maintenanceDraft.eventType} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, eventType: event.target.value as MaintenanceEventType }))}>{maintenanceTypes.map((type) => <option key={type} value={type}>{labelMaintenanceType(type)}</option>)}</select></V1Field>
                <V1Field label="Ergebnis"><select value={maintenanceDraft.result} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, result: event.target.value as MaintenanceResult }))}>{maintenanceResults.map((result) => <option key={result} value={result}>{labelMaintenanceResult(result)}</option>)}</select></V1Field>
                <V1Field label="Durchgeführt"><input type="datetime-local" value={maintenanceDraft.performedAtLocal} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, performedAtLocal: event.target.value }))} /></V1Field>
                <V1Field label="Nächste Fälligkeit"><input type="datetime-local" value={maintenanceDraft.nextDueAtLocal} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, nextDueAtLocal: event.target.value }))} /></V1Field>
                <V1Field label="Titel" wide><input value={maintenanceDraft.title} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, title: event.target.value }))} placeholder="z. B. Luftsteine gereinigt" /></V1Field>
                <V1Field label="Notizen" wide><textarea value={maintenanceDraft.notes} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, notes: event.target.value }))} rows={3} /></V1Field>
              </div>
              <div className="ops1b-sticky-actions"><V1Button type="submit" variant="primary" disabled={saving === 'maintenance'}>{saving === 'maintenance' ? 'Speichert...' : 'Wartung speichern'}</V1Button></div>
            </form>
          </V1Section>

          <V1Section title="Offene Wartung">
            <EventList
              type="maintenance"
              calibration={[]}
              maintenance={openMaintenance}
              hardware={state.hardware}
              saving={saving}
              onCalibrationDone={completeCalibrationEvent}
              onMaintenanceDone={completeMaintenanceEvent}
            />
          </V1Section>
        </div>
      )}

      {tab === 'inventory' && (
        <div className="ops1b-workflow-grid">
          <V1Section title="Inventar">
            {sortedHardware.length === 0 ? (
              <V1Empty title="Keine Hardware angelegt" />
            ) : (
              <div className="ops1b-inventory-grid">
                {sortedHardware.map((item) => <InventoryRow key={item.id} item={item} onStatus={updateHardwareStatus} saving={saving === `hardware-${item.id}`} />)}
              </div>
            )}
          </V1Section>

          <V1Section title="Anlegen">
            <details className="ops1b-details">
              <summary>Sensor oder Hardware anlegen</summary>
              <form className="ops1b-form" onSubmit={saveHardware}>
                <div className="ops1b-form-grid">
                  <V1Field label="Name" wide><input value={hardwareDraft.name} onChange={(event) => setHardwareDraft((current) => ({ ...current, name: event.target.value }))} placeholder="pH Sonde Hauptzelt" /></V1Field>
                  <V1Field label="Kategorie"><input value={hardwareDraft.category} onChange={(event) => setHardwareDraft((current) => ({ ...current, category: event.target.value }))} placeholder="Sensor / Pumpe / Chiller" /></V1Field>
                  <V1Field label="Kritikalität"><select value={hardwareDraft.criticality} onChange={(event) => setHardwareDraft((current) => ({ ...current, criticality: event.target.value as HardwareItemCriticality }))}>{criticalityOptions.map((item) => <option key={item} value={item}>{labelCriticality(item)}</option>)}</select></V1Field>
                  <V1Field label="Zelt"><select value={hardwareDraft.tentId} onChange={(event) => setHardwareDraft((current) => ({ ...current, tentId: event.target.value }))}><option value="">Kein Zelt</option>{state.tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}</select></V1Field>
                  <V1Field label="HA Entity"><input value={hardwareDraft.haEntityId} onChange={(event) => setHardwareDraft((current) => ({ ...current, haEntityId: event.target.value }))} placeholder="sensor.ph_hauptzelt" /></V1Field>
                  <V1Field label="Hersteller"><input value={hardwareDraft.manufacturer} onChange={(event) => setHardwareDraft((current) => ({ ...current, manufacturer: event.target.value }))} /></V1Field>
                  <V1Field label="Modell"><input value={hardwareDraft.model} onChange={(event) => setHardwareDraft((current) => ({ ...current, model: event.target.value }))} /></V1Field>
                  <V1Field label="Notizen" wide><textarea value={hardwareDraft.notes} onChange={(event) => setHardwareDraft((current) => ({ ...current, notes: event.target.value }))} rows={3} /></V1Field>
                </div>
                <div className="ops1b-sticky-actions"><V1Button type="submit" variant="primary" disabled={saving === 'hardware'}>{saving === 'hardware' ? 'Speichert...' : 'Hardware anlegen'}</V1Button></div>
              </form>
            </details>
          </V1Section>
        </div>
      )}
    </V1Page>
  )
}

function SensorCard({ item, state, onStatus, saving }: { item: HardwareItemDto; state: OpsState; onStatus: (item: HardwareItemDto, status: HardwareItemStatus) => void; saving: boolean }) {
  const lastCalibration = latestCalibration(item.id, state.calibration)
  const nextMaintenance = nextMaintenanceFor(item.id, state.maintenance)
  const tone = item.status === 'Offline' || item.status === 'Retired' ? 'critical' : isDue(lastCalibration?.nextDueAtUtc) || isDue(nextMaintenance?.dueAtUtc) ? 'warn' : 'ok'
  return (
    <V1Card className="ops-sensor-card" tone={tone}>
      <div className="v1-card-title-row">
        <div><span className="v1-card-kicker">{item.category}</span><h2>{item.name}</h2></div>
        <V1Badge tone={tone}>{labelHardwareStatus(item.status)}</V1Badge>
      </div>
      <div className="ops-fact-list">
        <Row label="Kalibrierung" value={lastCalibration ? `${formatDate(lastCalibration.performedAtUtc)} · nächste ${formatDate(lastCalibration.nextDueAtUtc)}` : 'keine Historie'} />
        <Row label="Wartung" value={nextMaintenance ? `${labelMaintenanceType(nextMaintenance.eventType)} · ${formatDate(nextMaintenance.dueAtUtc)}` : 'keine offene Wartung'} />
        <Row label="Entity" value={item.haEntityId ?? 'nicht gemappt'} />
      </div>
      <div className="v1-action-row">
        <V1Button onClick={() => onStatus(item, item.status === 'Offline' ? 'Active' : 'Offline')} disabled={saving}>{item.status === 'Offline' ? 'Aktiv' : 'Offline'}</V1Button>
      </div>
    </V1Card>
  )
}

function InventoryRow({ item, onStatus, saving }: { item: HardwareItemDto; onStatus: (item: HardwareItemDto, status: HardwareItemStatus) => void; saving: boolean }) {
  return (
    <div className="ops-inventory-row">
      <div><strong>{item.name}</strong><span>{item.category} · {item.manufacturer ?? 'Hersteller offen'} {item.model ?? ''}</span></div>
      <V1Badge tone={item.status === 'Active' ? 'ok' : item.status === 'MaintenanceDue' ? 'warn' : 'critical'}>{labelHardwareStatus(item.status)}</V1Badge>
      <V1Button onClick={() => onStatus(item, item.status === 'Active' ? 'MaintenanceDue' : 'Active')} disabled={saving}>{item.status === 'Active' ? 'Wartung' : 'Aktiv'}</V1Button>
    </div>
  )
}

function EventList({ type, calibration, maintenance, hardware, saving, onCalibrationDone, onMaintenanceDone }: { type: 'calibration' | 'maintenance'; calibration: CalibrationEventDto[]; maintenance: MaintenanceEventDto[]; hardware: HardwareItemDto[]; saving: string | null; onCalibrationDone: (item: CalibrationEventDto) => void; onMaintenanceDone: (item: MaintenanceEventDto) => void }) {
  if (type === 'calibration') {
    return calibration.length === 0 ? <V1Empty title="Keine offenen Kalibrierungen" /> : <div className="ops-event-list">{calibration.map((item) => <div key={item.id} className="ops-event-row"><div><strong>{item.title}</strong><span>{hardwareName(hardware, item.hardwareItemId)} · {formatDateTime(item.dueAtUtc)}</span></div><V1Button onClick={() => onCalibrationDone(item)} disabled={saving === `calibration-${item.id}`}>Erledigt</V1Button></div>)}</div>
  }
  return maintenance.length === 0 ? <V1Empty title="Keine offene Wartung" /> : <div className="ops-event-list">{maintenance.map((item) => <div key={item.id} className="ops-event-row"><div><strong>{item.title}</strong><span>{hardwareName(hardware, item.hardwareItemId)} · {formatDateTime(item.dueAtUtc)}</span></div><V1Button onClick={() => onMaintenanceDone(item)} disabled={saving === `maintenance-${item.id}`}>Erledigt</V1Button></div>)}</div>
}

function Row({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>
}

function buildSensorIssueRows(state: OpsState, trust: SensorTrust) {
  return [
    ...trust.criticalRisks.map((risk) => ({ id: `risk-${risk.id}`, title: risk.title, meta: risk.description ?? risk.eventType, badge: 'Risiko', tone: 'critical' as const })),
    ...trust.dueCalibration.map((event) => ({ id: `cal-${event.id}`, title: event.title, meta: `${hardwareName(state.hardware, event.hardwareItemId)} · ${formatDateTime(event.dueAtUtc)}`, badge: 'Kalibrierung', tone: 'warn' as const })),
    ...trust.dueMaintenance.map((event) => ({ id: `maint-${event.id}`, title: event.title, meta: `${hardwareName(state.hardware, event.hardwareItemId)} · ${formatDateTime(event.dueAtUtc)}`, badge: 'Wartung', tone: 'warn' as const })),
    ...state.hardware.filter((item) => item.status === 'Offline' || item.status === 'Retired').map((item) => ({ id: `hardware-${item.id}`, title: item.name, meta: `${item.category} · ${labelHardwareStatus(item.status)}`, badge: 'Offline', tone: 'critical' as const })),
  ].slice(0, 12)
}

function buildSensorTrust(state: OpsState): SensorTrust {
  const sensors = state.hardware.filter(isSensorLike)
  const hardwareBase = sensors.length > 0 ? sensors : state.hardware
  const offline = hardwareBase.filter((item) => item.status === 'Offline' || item.status === 'Retired').length
  const dueCalibration = state.calibration.filter((event) => event.status === 'Planned' && isDue(event.dueAtUtc))
  const dueMaintenance = state.maintenance.filter((event) => event.status === 'Planned' && isDue(event.dueAtUtc))
  const criticalRisks = state.risks.filter((risk) => risk.severity === 'Critical')
  const score = Math.max(0, Math.min(100, 100 - offline * 25 - dueCalibration.length * 15 - dueMaintenance.length * 10 - criticalRisks.length * 18))
  const tone = score < 55 ? 'critical' : score < 82 ? 'warn' : 'ok'
  const label = score < 55 ? 'kritisch' : score < 82 ? 'prüfen' : 'vertrauenswürdig'
  return { score, label, tone, sensors, offline, dueCalibration, dueMaintenance, criticalRisks }
}

function isSensorLike(item: HardwareItemDto) {
  const haystack = `${item.name} ${item.category} ${item.wearTemplateId ?? ''}`.toLowerCase()
  return ['sensor', 'sonde', 'probe', 'ph', 'ec', 'orp', 'do', 'sauerstoff', 'temperatur', 'level', 'wasserstand'].some((term) => haystack.includes(term))
}

function latestCalibration(hardwareItemId: number, events: CalibrationEventDto[]) {
  const items = events.filter((event) => event.hardwareItemId === hardwareItemId && event.status === 'Completed')
  return items.sort((a, b) => (b.performedAtUtc ?? b.createdAtUtc).localeCompare(a.performedAtUtc ?? a.createdAtUtc))[0] ?? null
}

function nextMaintenanceFor(hardwareItemId: number, events: MaintenanceEventDto[]) {
  const items = events.filter((event) => event.hardwareItemId === hardwareItemId && event.status === 'Planned')
  return items.sort(sortDue)[0] ?? null
}

function selectDefaultSensor(hardware: HardwareItemDto[]) {
  return hardware.find(isSensorLike) ?? hardware[0] ?? null
}

function sortHardware(a: HardwareItemDto, b: HardwareItemDto) {
  const criticality = criticalityRank(b.criticality) - criticalityRank(a.criticality)
  return criticality || a.name.localeCompare(b.name)
}

function sortDue(a: { dueAtUtc: string | null; createdAtUtc: string }, b: { dueAtUtc: string | null; createdAtUtc: string }) {
  return (a.dueAtUtc ?? a.createdAtUtc).localeCompare(b.dueAtUtc ?? b.createdAtUtc)
}

function criticalityRank(value: HardwareItemCriticality) {
  return value === 'Critical' ? 4 : value === 'High' ? 3 : value === 'Medium' ? 2 : 1
}

function isDue(value: string | null | undefined) {
  if (!value) return false
  return new Date(value).getTime() <= Date.now() + 7 * 24 * 60 * 60 * 1000
}

function createCalibrationDraft(hardwareItemId?: number): CalibrationDraft {
  return { hardwareItemId: hardwareItemId ? String(hardwareItemId) : '', calibrationType: 'Ph', title: '', referenceSolution: 'pH 7.00', referenceValue: '7.00', beforeValue: '', afterValue: '', temperatureC: '20', performedAtLocal: toLocalInputValue(), nextDueAtLocal: toLocalInputValue(new Date(Date.now() + 30 * 24 * 60 * 60 * 1000)), result: 'Passed', notes: '' }
}

function createMaintenanceDraft(hardwareItemId?: number): MaintenanceDraft {
  return { hardwareItemId: hardwareItemId ? String(hardwareItemId) : '', eventType: 'Inspection', title: '', performedAtLocal: toLocalInputValue(), nextDueAtLocal: toLocalInputValue(new Date(Date.now() + 30 * 24 * 60 * 60 * 1000)), result: 'Passed', notes: '' }
}

function createHardwareDraft(): HardwareDraft {
  return { name: '', category: 'Sensor', criticality: 'High', tentId: '', haEntityId: '', manufacturer: '', model: '', notes: '' }
}

function hardwareToRequest(item: HardwareItemDto): UpdateHardwareItemRequest {
  return { name: item.name, category: item.category, status: item.status, criticality: item.criticality, tentId: item.tentId, setupId: item.setupId, growId: item.growId, wearTemplateId: item.wearTemplateId, tentSensorId: item.tentSensorId, haEntityId: item.haEntityId, manufacturer: item.manufacturer, model: item.model, serialNumber: item.serialNumber, installedAtUtc: item.installedAtUtc, retiredAtUtc: item.retiredAtUtc, expectedLifespanDays: item.expectedLifespanDays, inspectionIntervalDays: item.inspectionIntervalDays, notes: item.notes }
}

function hardwareName(items: HardwareItemDto[], id: number) {
  return items.find((item) => item.id === id)?.name ?? `Hardware #${id}`
}

function nullable(value: string) {
  const trimmed = value.trim()
  return trimmed.length === 0 ? null : trimmed
}

function toNumber(value: string) {
  const normalized = value.trim().replace(',', '.')
  if (!normalized) return null
  const parsed = Number.parseFloat(normalized)
  return Number.isFinite(parsed) ? parsed : null
}

function toIntOrNull(value: string) {
  if (!value) return null
  const parsed = Number.parseInt(value, 10)
  return Number.isFinite(parsed) ? parsed : null
}

function toUtc(value: string) {
  return value ? new Date(value).toISOString() : null
}

function labelCalibrationType(value: CalibrationEventType) {
  return value === 'Ph' ? 'pH' : value === 'Ec' ? 'EC' : value === 'Orp' ? 'ORP' : value === 'Do' ? 'DO' : 'Sonstige'
}

function labelCalibrationResult(value: CalibrationResult) {
  return value === 'Passed' ? 'Bestanden' : value === 'AdjustmentNeeded' ? 'Nachjustiert' : value === 'Failed' ? 'Fehlerhaft' : 'Unbekannt'
}

function labelMaintenanceType(value: MaintenanceEventType) {
  return value === 'Inspection' ? 'Inspektion' : value === 'Cleaning' ? 'Reinigung' : value === 'Replacement' ? 'Tausch' : value === 'Repair' ? 'Reparatur' : 'Sonstige'
}

function labelMaintenanceResult(value: MaintenanceResult) {
  return value === 'Passed' ? 'Bestanden' : value === 'ActionNeeded' ? 'Maßnahme nötig' : value === 'Replaced' ? 'Getauscht' : value === 'Failed' ? 'Fehlerhaft' : 'Unbekannt'
}

function labelHardwareStatus(value: HardwareItemStatus) {
  return value === 'Active' ? 'Aktiv' : value === 'MaintenanceDue' ? 'Wartung' : value === 'Offline' ? 'Offline' : 'Ausgemustert'
}

function labelCriticality(value: HardwareItemCriticality) {
  return value === 'Critical' ? 'Kritisch' : value === 'High' ? 'Hoch' : value === 'Medium' ? 'Mittel' : 'Niedrig'
}

function formatUnknownError(caught: unknown, fallback: string) {
  if (caught instanceof Error) return caught.message || fallback
  return fallback
}

export default HardwarePage
