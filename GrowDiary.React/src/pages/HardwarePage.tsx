import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type {
  AcknowledgeRiskEventRequest,
  CalibrationEventDto,
  CalibrationEventStatus,
  CalibrationEventType,
  CalibrationResult,
  CreateCalibrationEventRequest,
  CreateHardwareItemRequest,
  CreateMaintenanceEventRequest,
  CreateRiskEventRequest,
  GrowSummary,
  HardwareItemCriticality,
  HardwareItemDto,
  HardwareItemStatus,
  MaintenanceEventDto,
  MaintenanceEventStatus,
  MaintenanceEventType,
  MaintenanceResult,
  ResolveRiskEventRequest,
  RiskEventDto,
  RiskEventSeverity,
  RiskEventSopRecommendationDto,
  RiskEventSource,
  RiskEventStatus,
  RiskEventType,
  SopInstanceDto,
  StartRiskEventSopRequest,
  TentDto,
  UpdateCalibrationEventRequest,
  UpdateHardwareItemRequest,
  UpdateMaintenanceEventRequest,
  UpdateRiskEventRequest,
  WearTemplateDto,
} from '../types'

type HardwareDraft = {
  name: string
  category: string
  status: HardwareItemStatus
  criticality: HardwareItemCriticality
  tentId: number | null
  wearTemplateId: string
  haEntityId: string
  manufacturer: string
  model: string
  installedAtUtc: string
  notes: string
}

type MaintenanceDraft = {
  hardwareItemId: number | null
  eventType: MaintenanceEventType
  status: MaintenanceEventStatus
  title: string
  dueAtUtc: string
  performedAtUtc: string
  notes: string
}

type CalibrationDraft = {
  hardwareItemId: number | null
  calibrationType: CalibrationEventType
  status: CalibrationEventStatus
  title: string
  referenceSolution: string
  referenceValue: string
  beforeValue: string
  afterValue: string
  temperatureC: string
  dueAtUtc: string
  performedAtUtc: string
  notes: string
}

type RiskDraft = {
  eventType: RiskEventType
  severity: RiskEventSeverity
  source: RiskEventSource
  title: string
  hardwareItemId: number | null
  tentId: number | null
  growId: number | null
  dedupeKey: string
  notes: string
}

type HardwareSection = 'inventory' | 'maintenance' | 'calibration' | 'risks'

const hardwareStatusOptions: HardwareItemStatus[] = ['Active', 'MaintenanceDue', 'Offline', 'Retired']
const hardwareCriticalityOptions: HardwareItemCriticality[] = ['Low', 'Medium', 'High', 'Critical']
const maintenanceEventTypeOptions: MaintenanceEventType[] = ['Inspection', 'Cleaning', 'Replacement', 'Repair', 'Other']
const maintenanceStatusOptions: MaintenanceEventStatus[] = ['Planned', 'Completed', 'Skipped', 'Cancelled']
const maintenanceResultOptions: MaintenanceResult[] = ['Unknown', 'Passed', 'ActionNeeded', 'Replaced', 'Failed']
const calibrationEventTypeOptions: CalibrationEventType[] = ['Ph', 'Ec', 'Orp', 'Do', 'Other']
const calibrationStatusOptions: CalibrationEventStatus[] = ['Planned', 'Completed', 'Failed', 'Skipped', 'Cancelled']
const calibrationResultOptions: CalibrationResult[] = ['Unknown', 'Passed', 'AdjustmentNeeded', 'Failed']
const riskEventTypeOptions: RiskEventType[] = ['PowerOutage', 'UpsOnBattery', 'PumpOffline', 'ChillerOffline', 'LightMismatch', 'HomeAssistantUnavailable', 'CriticalDo', 'SensorUnavailable', 'Other']
const riskSeverityOptions: RiskEventSeverity[] = ['Info', 'Warning', 'Critical']
const riskStatusOptions: RiskEventStatus[] = ['Open', 'Acknowledged', 'Resolved', 'Ignored']
const riskSourceOptions: RiskEventSource[] = ['Manual', 'HomeAssistant', 'AutoMeasurement', 'Deviation', 'System']

function HardwarePage() {
  const [activeSection, setActiveSection] = useState<HardwareSection>('inventory')
  const [tents, setTents] = useState<TentDto[]>([])
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [hardwareItems, setHardwareItems] = useState<HardwareItemDto[]>([])
  const [wearTemplates, setWearTemplates] = useState<WearTemplateDto[]>([])
  const [hardwareDraft, setHardwareDraft] = useState<HardwareDraft>(createHardwareDraft())
  const [hardwareError, setHardwareError] = useState<string | null>(null)
  const [maintenanceEvents, setMaintenanceEvents] = useState<MaintenanceEventDto[]>([])
  const [maintenanceDraft, setMaintenanceDraft] = useState<MaintenanceDraft>(createMaintenanceDraft())
  const [maintenanceError, setMaintenanceError] = useState<string | null>(null)
  const [calibrationEvents, setCalibrationEvents] = useState<CalibrationEventDto[]>([])
  const [calibrationDraft, setCalibrationDraft] = useState<CalibrationDraft>(createCalibrationDraft())
  const [calibrationError, setCalibrationError] = useState<string | null>(null)
  const [riskEvents, setRiskEvents] = useState<RiskEventDto[]>([])
  const [riskSopRecommendations, setRiskSopRecommendations] = useState<Record<number, RiskEventSopRecommendationDto[]>>({})
  const [riskDraft, setRiskDraft] = useState<RiskDraft>(createRiskDraft())
  const [riskError, setRiskError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState<string | null>(null)

  const nowUtc = Date.now()
  const activeHardwareCount = hardwareItems.filter((item) => item.status === 'Active').length
  const dueMaintenanceCount = maintenanceEvents.filter((item) => isDueOpen(item.status, item.dueAtUtc, nowUtc)).length
  const dueCalibrationCount = calibrationEvents.filter((item) => isDueOpen(item.status, item.dueAtUtc, nowUtc)).length
  const criticalOpenRiskCount = riskEvents.filter((item) => item.status === 'Open' && item.severity === 'Critical').length

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)

      try {
        const [tentData, growData, hardwareData, maintenanceData, calibrationData, riskData, wearData] = await Promise.all([
          apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal }),
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }),
          apiFetch<HardwareItemDto[]>('/api/hardware-items', { signal: controller.signal }),
          apiFetch<MaintenanceEventDto[]>('/api/maintenance-events', { signal: controller.signal }),
          apiFetch<CalibrationEventDto[]>('/api/calibration-events', { signal: controller.signal }),
          apiFetch<RiskEventDto[]>('/api/risk-events', { signal: controller.signal }),
          apiFetch<WearTemplateDto[]>('/api/knowledge/wear', { signal: controller.signal }),
        ])
        setTents(tentData)
        setGrows(growData)
        setHardwareItems(hardwareData)
        setMaintenanceEvents(maintenanceData)
        setCalibrationEvents(calibrationData)
        setRiskEvents(riskData)
        await loadRiskSopRecommendations(riskData, controller.signal)
        setWearTemplates(wearData)
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Hardware-Daten konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  async function loadRiskEvents() {
    const next = await apiFetch<RiskEventDto[]>('/api/risk-events')
    setRiskEvents(next)
    await loadRiskSopRecommendations(next)
  }

  async function loadRiskSopRecommendations(items: RiskEventDto[], signal?: AbortSignal) {
    const recommendationCandidates = items.filter((item) => item.status === 'Open' || item.status === 'Acknowledged')

    if (recommendationCandidates.length === 0) {
      setRiskSopRecommendations({})
      return
    }

    const entries = await Promise.all(recommendationCandidates.map(async (item) => [
      item.id,
      await apiFetch<RiskEventSopRecommendationDto[]>(`/api/risk-events/${item.id}/sop-recommendations`, { signal }),
    ] as const))
    setRiskSopRecommendations(Object.fromEntries(entries))
  }

  async function handleCreateHardwareItem(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving('hardware-new')
    setHardwareError(null)

    const request: CreateHardwareItemRequest = {
      name: toNullableString(hardwareDraft.name),
      category: toNullableString(hardwareDraft.category),
      status: hardwareDraft.status,
      criticality: hardwareDraft.criticality,
      tentId: hardwareDraft.tentId,
      wearTemplateId: toNullableString(hardwareDraft.wearTemplateId),
      haEntityId: toNullableString(hardwareDraft.haEntityId),
      manufacturer: toNullableString(hardwareDraft.manufacturer),
      model: toNullableString(hardwareDraft.model),
      installedAtUtc: toNullableDateTime(hardwareDraft.installedAtUtc),
      notes: toNullableString(hardwareDraft.notes),
    }

    try {
      const saved = await apiFetch<HardwareItemDto>('/api/hardware-items', {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setHardwareItems((current) => [...current, saved])
      setHardwareDraft(createHardwareDraft())
    } catch (caught) {
      setHardwareError(formatApiError(caught, 'HardwareItem konnte nicht angelegt werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function saveHardwareItem(item: HardwareItemDto) {
    setSaving(`hardware-${item.id}`)
    setHardwareError(null)

    const request: UpdateHardwareItemRequest = {
      name: item.name.trim(),
      category: item.category.trim(),
      status: item.status,
      criticality: item.criticality,
      tentId: item.tentId,
      setupId: item.setupId,
      growId: item.growId,
      wearTemplateId: item.wearTemplateId,
      tentSensorId: item.tentSensorId,
      haEntityId: item.haEntityId,
      manufacturer: item.manufacturer,
      model: item.model,
      serialNumber: item.serialNumber,
      installedAtUtc: item.installedAtUtc,
      retiredAtUtc: item.retiredAtUtc,
      expectedLifespanDays: item.expectedLifespanDays,
      inspectionIntervalDays: item.inspectionIntervalDays,
      notes: item.notes,
    }

    try {
      const saved = await apiFetch<HardwareItemDto>(`/api/hardware-items/${item.id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      })
      setHardwareItems((current) => current.map((existing) => existing.id === saved.id ? saved : existing))
    } catch (caught) {
      setHardwareError(formatApiError(caught, 'HardwareItem konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function handleCreateMaintenanceEvent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!maintenanceDraft.hardwareItemId) {
      setMaintenanceError('HardwareItem auswählen.')
      return
    }

    setSaving('maintenance-new')
    setMaintenanceError(null)

    const request: CreateMaintenanceEventRequest = {
      hardwareItemId: maintenanceDraft.hardwareItemId,
      eventType: maintenanceDraft.eventType,
      status: maintenanceDraft.status,
      result: 'Unknown',
      title: maintenanceDraft.title.trim(),
      dueAtUtc: toNullableDateTime(maintenanceDraft.dueAtUtc),
      performedAtUtc: toNullableDateTime(maintenanceDraft.performedAtUtc),
      notes: toNullableString(maintenanceDraft.notes),
    }

    try {
      const saved = await apiFetch<MaintenanceEventDto>('/api/maintenance-events', {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setMaintenanceEvents((current) => [...current, saved])
      setMaintenanceDraft(createMaintenanceDraft())
    } catch (caught) {
      setMaintenanceError(formatApiError(caught, 'MaintenanceEvent konnte nicht angelegt werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function saveMaintenanceEvent(item: MaintenanceEventDto) {
    setSaving(`maintenance-${item.id}`)
    setMaintenanceError(null)

    const request: UpdateMaintenanceEventRequest = {
      hardwareItemId: item.hardwareItemId,
      eventType: item.eventType,
      status: item.status,
      result: item.result,
      title: item.title,
      description: item.description,
      dueAtUtc: item.dueAtUtc,
      performedAtUtc: item.performedAtUtc,
      nextDueAtUtc: item.nextDueAtUtc,
      growTaskId: item.growTaskId,
      sopInstanceId: item.sopInstanceId,
      notes: item.notes,
    }

    try {
      const saved = await apiFetch<MaintenanceEventDto>(`/api/maintenance-events/${item.id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      })
      setMaintenanceEvents((current) => current.map((existing) => existing.id === saved.id ? saved : existing))
    } catch (caught) {
      setMaintenanceError(formatApiError(caught, 'MaintenanceEvent konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function handleCreateCalibrationEvent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!calibrationDraft.hardwareItemId) {
      setCalibrationError('HardwareItem auswählen.')
      return
    }

    setSaving('calibration-new')
    setCalibrationError(null)

    const request: CreateCalibrationEventRequest = {
      hardwareItemId: calibrationDraft.hardwareItemId,
      calibrationType: calibrationDraft.calibrationType,
      status: calibrationDraft.status,
      result: 'Unknown',
      title: calibrationDraft.title.trim(),
      referenceSolution: toNullableString(calibrationDraft.referenceSolution),
      referenceValue: toNullableNumber(calibrationDraft.referenceValue),
      beforeValue: toNullableNumber(calibrationDraft.beforeValue),
      afterValue: toNullableNumber(calibrationDraft.afterValue),
      temperatureC: toNullableNumber(calibrationDraft.temperatureC),
      dueAtUtc: toNullableDateTime(calibrationDraft.dueAtUtc),
      performedAtUtc: toNullableDateTime(calibrationDraft.performedAtUtc),
      notes: toNullableString(calibrationDraft.notes),
    }

    try {
      const saved = await apiFetch<CalibrationEventDto>('/api/calibration-events', {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setCalibrationEvents((current) => [...current, saved])
      setCalibrationDraft(createCalibrationDraft())
    } catch (caught) {
      setCalibrationError(formatApiError(caught, 'CalibrationEvent konnte nicht angelegt werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function saveCalibrationEvent(item: CalibrationEventDto) {
    setSaving(`calibration-${item.id}`)
    setCalibrationError(null)

    const request: UpdateCalibrationEventRequest = {
      hardwareItemId: item.hardwareItemId,
      calibrationType: item.calibrationType,
      status: item.status,
      result: item.result,
      title: item.title,
      referenceSolution: item.referenceSolution,
      referenceValue: item.referenceValue,
      beforeValue: item.beforeValue,
      afterValue: item.afterValue,
      temperatureC: item.temperatureC,
      dueAtUtc: item.dueAtUtc,
      performedAtUtc: item.performedAtUtc,
      nextDueAtUtc: item.nextDueAtUtc,
      growTaskId: item.growTaskId,
      notes: item.notes,
    }

    try {
      const saved = await apiFetch<CalibrationEventDto>(`/api/calibration-events/${item.id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      })
      setCalibrationEvents((current) => current.map((existing) => existing.id === saved.id ? saved : existing))
    } catch (caught) {
      setCalibrationError(formatApiError(caught, 'CalibrationEvent konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function handleCreateRiskEvent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving('risk-new')
    setRiskError(null)

    const request: CreateRiskEventRequest = {
      eventType: riskDraft.eventType,
      severity: riskDraft.severity,
      status: 'Open',
      source: riskDraft.source,
      title: riskDraft.title.trim(),
      hardwareItemId: riskDraft.hardwareItemId,
      tentId: riskDraft.tentId,
      growId: riskDraft.growId,
      dedupeKey: toNullableString(riskDraft.dedupeKey),
      notes: toNullableString(riskDraft.notes),
    }

    try {
      const saved = await apiFetch<RiskEventDto>('/api/risk-events', {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setRiskEvents((current) => [saved, ...current.filter((item) => item.id !== saved.id)])
      if (saved.status === 'Open' || saved.status === 'Acknowledged') {
        const recommendations = await apiFetch<RiskEventSopRecommendationDto[]>(`/api/risk-events/${saved.id}/sop-recommendations`)
        setRiskSopRecommendations((current) => ({ ...current, [saved.id]: recommendations }))
      }
      setRiskDraft(createRiskDraft())
    } catch (caught) {
      setRiskError(formatApiError(caught, 'RiskEvent konnte nicht angelegt werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function saveRiskEvent(item: RiskEventDto) {
    setSaving(`risk-${item.id}`)
    setRiskError(null)

    const request: UpdateRiskEventRequest = {
      eventType: item.eventType,
      severity: item.severity,
      status: item.status,
      source: item.source,
      title: item.title,
      description: item.description,
      hardwareItemId: item.hardwareItemId,
      tentId: item.tentId,
      growId: item.growId,
      tentSensorId: item.tentSensorId,
      haEntityId: item.haEntityId,
      sopInstanceId: item.sopInstanceId,
      growTaskId: item.growTaskId,
      startedAtUtc: item.startedAtUtc,
      lastSeenAtUtc: item.lastSeenAtUtc,
      resolvedAtUtc: item.resolvedAtUtc,
      acknowledgedAtUtc: item.acknowledgedAtUtc,
      dedupeKey: item.dedupeKey,
      rawValue: item.rawValue,
      notes: item.notes,
    }

    try {
      const saved = await apiFetch<RiskEventDto>(`/api/risk-events/${item.id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      })
      setRiskEvents((current) => current.map((existing) => existing.id === saved.id ? saved : existing))
      if (saved.status === 'Open' || saved.status === 'Acknowledged') {
        const recommendations = await apiFetch<RiskEventSopRecommendationDto[]>(`/api/risk-events/${saved.id}/sop-recommendations`)
        setRiskSopRecommendations((current) => ({ ...current, [saved.id]: recommendations }))
      }
    } catch (caught) {
      setRiskError(formatApiError(caught, 'RiskEvent konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function acknowledgeRiskEvent(item: RiskEventDto) {
    setSaving(`risk-ack-${item.id}`)
    setRiskError(null)
    const request: AcknowledgeRiskEventRequest = { notes: item.notes }

    try {
      const saved = await apiFetch<RiskEventDto>(`/api/risk-events/${item.id}/acknowledge`, {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setRiskEvents((current) => current.map((existing) => existing.id === saved.id ? saved : existing))
      const recommendations = await apiFetch<RiskEventSopRecommendationDto[]>(`/api/risk-events/${saved.id}/sop-recommendations`)
      setRiskSopRecommendations((current) => ({ ...current, [saved.id]: recommendations }))
    } catch (caught) {
      setRiskError(formatApiError(caught, 'RiskEvent konnte nicht bestätigt werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function resolveRiskEvent(item: RiskEventDto) {
    setSaving(`risk-resolve-${item.id}`)
    setRiskError(null)
    const request: ResolveRiskEventRequest = { notes: item.notes }

    try {
      const saved = await apiFetch<RiskEventDto>(`/api/risk-events/${item.id}/resolve`, {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setRiskEvents((current) => current.map((existing) => existing.id === saved.id ? saved : existing))
      setRiskSopRecommendations((current) => {
        const next = { ...current }
        delete next[saved.id]
        return next
      })
    } catch (caught) {
      setRiskError(formatApiError(caught, 'RiskEvent konnte nicht gelöst werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function startRiskEventSop(item: RiskEventDto, recommendation: RiskEventSopRecommendationDto) {
    setSaving(`risk-sop-${item.id}-${recommendation.sopId}`)
    setRiskError(null)

    const request: StartRiskEventSopRequest = {
      sopId: recommendation.sopId,
      notes: item.notes,
    }

    try {
      await apiFetch<SopInstanceDto>(`/api/risk-events/${item.id}/start-sop`, {
        method: 'POST',
        body: JSON.stringify(request),
      })
      await loadRiskEvents()
    } catch (caught) {
      setRiskError(formatApiError(caught, 'SOP konnte nicht aus RiskEvent gestartet werden.'))
    } finally {
      setSaving(null)
    }
  }

  function updateHardwareItem(id: number, patch: Partial<HardwareItemDto>) {
    setHardwareItems((current) => current.map((item) => item.id === id ? { ...item, ...patch } : item))
  }

  function updateMaintenanceEvent(id: number, patch: Partial<MaintenanceEventDto>) {
    setMaintenanceEvents((current) => current.map((item) => item.id === id ? { ...item, ...patch } : item))
  }

  function updateCalibrationEvent(id: number, patch: Partial<CalibrationEventDto>) {
    setCalibrationEvents((current) => current.map((item) => item.id === id ? { ...item, ...patch } : item))
  }

  function updateRiskEvent(id: number, patch: Partial<RiskEventDto>) {
    setRiskEvents((current) => current.map((item) => item.id === id ? { ...item, ...patch } : item))
  }

  if (loading) {
    return (
      <>
        <div className="topbar"><span className="topbar-title">Hardware</span></div>
        <div className="empty-hint">Lade Hardware-Daten...</div>
      </>
    )
  }

  if (error) {
    return (
      <>
        <div className="topbar"><span className="topbar-title">Hardware</span></div>
        <div className="empty-hint">{error}</div>
      </>
    )
  }

  return (
    <>
      <div className="topbar">
        <span className="topbar-title">Hardware</span>
      </div>

      <div className="page-scroll">
        <h1 className="sr-only">Hardware</h1>
        <div className="stats-row">
          <div className="stat-chip"><strong>{activeHardwareCount}</strong>Aktive Hardware</div>
          <div className="stat-chip"><strong>{dueMaintenanceCount}</strong>Wartung fällig</div>
          <div className="stat-chip"><strong>{dueCalibrationCount}</strong>Kalibrierung fällig</div>
          <div className="stat-chip"><strong>{criticalOpenRiskCount}</strong>Kritische Risiken</div>
        </div>

        <div className="section-tabs" style={{ marginBottom: 16 }}>
          <button type="button" className={`btn ${activeSection === 'inventory' ? 'btn-primary' : ''}`} onClick={() => setActiveSection('inventory')}>
            Inventar
          </button>
          <button type="button" className={`btn ${activeSection === 'maintenance' ? 'btn-primary' : ''}`} onClick={() => setActiveSection('maintenance')}>
            Wartung
          </button>
          <button type="button" className={`btn ${activeSection === 'calibration' ? 'btn-primary' : ''}`} onClick={() => setActiveSection('calibration')}>
            Kalibrierung
          </button>
          <button type="button" className={`btn ${activeSection === 'risks' ? 'btn-primary' : ''}`} onClick={() => setActiveSection('risks')}>
            Risiken
          </button>
        </div>

        {activeSection === 'inventory' && (
        <>
        <div className="section-label">Inventar</div>
        <div className="card management-card" style={{ marginBottom: 24 }}>
          <div className="card-header"><span className="card-title">HardwareItems</span></div>
          <div style={{ padding: '14px 16px', display: 'grid', gap: 12 }}>
            {hardwareError && <div style={{ fontSize: 13, color: 'var(--red)' }}>{hardwareError}</div>}
            {hardwareItems.length === 0 ? (
              <div style={{ fontSize: 13, color: 'var(--faint)' }}>Keine HardwareItems angelegt.</div>
            ) : (
              <div style={{ display: 'grid', gap: 8 }}>
                {hardwareItems.map((item) => (
                  <div
                    key={item.id}
                    style={{
                      display: 'grid',
                      gridTemplateColumns: 'minmax(130px, 1.2fr) minmax(90px, 0.8fr) minmax(110px, 0.8fr) minmax(90px, 0.8fr) minmax(100px, 0.8fr) minmax(130px, 1fr) minmax(105px, 0.7fr) minmax(70px, 0.5fr) auto',
                      gap: 8,
                      alignItems: 'end',
                      padding: '9px 10px',
                      border: '1px solid var(--border)',
                      borderRadius: 7,
                      background: 'var(--surface2)',
                    }}
                  >
                    <label className="field">
                      <span>Name</span>
                      <input value={item.name} onChange={(event) => updateHardwareItem(item.id, { name: event.target.value })} />
                    </label>
                    <label className="field">
                      <span>Category</span>
                      <input value={item.category} onChange={(event) => updateHardwareItem(item.id, { category: event.target.value })} />
                    </label>
                    <label className="field">
                      <span>Status</span>
                      <select value={item.status} onChange={(event) => updateHardwareItem(item.id, { status: event.target.value as HardwareItemStatus })}>
                        {hardwareStatusOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                      </select>
                    </label>
                    <label className="field">
                      <span>Criticality</span>
                      <select value={item.criticality} onChange={(event) => updateHardwareItem(item.id, { criticality: event.target.value as HardwareItemCriticality })}>
                        {hardwareCriticalityOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                      </select>
                    </label>
                    <div className="field">
                      <label>Tent</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{getTentName(tents, item.tentId)}</div>
                    </div>
                    <div className="field">
                      <label>WearTemplate</label>
                      <div className="mono" style={{ fontSize: 12, minHeight: 34, display: 'flex', alignItems: 'center' }}>{item.wearTemplateId ?? '-'}</div>
                    </div>
                    <div className="field">
                      <label>InstalledAt</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{formatDate(item.installedAtUtc)}</div>
                    </div>
                    <div className="field">
                      <label>Intervall</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{item.inspectionIntervalDays ?? '-'}</div>
                    </div>
                    <button type="button" className="btn" disabled={saving === `hardware-${item.id}`} onClick={() => void saveHardwareItem(item)}>
                      {saving === `hardware-${item.id}` ? 'Speichert...' : 'Speichern'}
                    </button>
                    <label className="field" style={{ gridColumn: '1 / 5' }}>
                      <span>Notes</span>
                      <input value={item.notes ?? ''} onChange={(event) => updateHardwareItem(item.id, { notes: event.target.value })} />
                    </label>
                    <label className="field" style={{ gridColumn: '5 / 7' }}>
                      <span>RetiredAtUtc</span>
                      <input type="datetime-local" value={toDateTimeInputValue(item.retiredAtUtc)} onChange={(event) => updateHardwareItem(item.id, { retiredAtUtc: toNullableDateTime(event.target.value) })} />
                    </label>
                  </div>
                ))}
              </div>
            )}

            <form onSubmit={(event) => void handleCreateHardwareItem(event)} style={{ display: 'grid', gridTemplateColumns: 'repeat(4, minmax(120px, 1fr)) auto', gap: 8, alignItems: 'end' }}>
              <label className="field">
                <span>WearTemplate</span>
                <select value={hardwareDraft.wearTemplateId} onChange={(event) => setHardwareDraft((current) => ({ ...current, wearTemplateId: event.target.value }))}>
                  <option value="">Keine Vorlage</option>
                  {wearTemplates.map((template) => <option key={template.id} value={template.id}>{template.name}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Name</span>
                <input value={hardwareDraft.name} onChange={(event) => setHardwareDraft((current) => ({ ...current, name: event.target.value }))} placeholder="Backend kann Vorlage übernehmen" />
              </label>
              <label className="field">
                <span>Category</span>
                <input value={hardwareDraft.category} onChange={(event) => setHardwareDraft((current) => ({ ...current, category: event.target.value }))} placeholder="Sensor, Pump, Filter..." />
              </label>
              <label className="field">
                <span>Tent</span>
                <select value={hardwareDraft.tentId ?? ''} onChange={(event) => setHardwareDraft((current) => ({ ...current, tentId: toNullableInteger(event.target.value) }))}>
                  <option value="">Global</option>
                  {tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}
                </select>
              </label>
              <button type="submit" className="btn" disabled={saving === 'hardware-new'}>
                {saving === 'hardware-new' ? 'Legt an...' : 'Hardware anlegen'}
              </button>
              <label className="field">
                <span>Status</span>
                <select value={hardwareDraft.status} onChange={(event) => setHardwareDraft((current) => ({ ...current, status: event.target.value as HardwareItemStatus }))}>
                  {hardwareStatusOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Criticality</span>
                <select value={hardwareDraft.criticality} onChange={(event) => setHardwareDraft((current) => ({ ...current, criticality: event.target.value as HardwareItemCriticality }))}>
                  {hardwareCriticalityOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                </select>
              </label>
              <label className="field">
                <span>HA Entity</span>
                <input className="mono" value={hardwareDraft.haEntityId} onChange={(event) => setHardwareDraft((current) => ({ ...current, haEntityId: event.target.value }))} placeholder="sensor.entity" />
              </label>
              <label className="field">
                <span>InstalledAt</span>
                <input type="datetime-local" value={hardwareDraft.installedAtUtc} onChange={(event) => setHardwareDraft((current) => ({ ...current, installedAtUtc: event.target.value }))} />
              </label>
              <label className="field">
                <span>Manufacturer</span>
                <input value={hardwareDraft.manufacturer} onChange={(event) => setHardwareDraft((current) => ({ ...current, manufacturer: event.target.value }))} />
              </label>
              <label className="field">
                <span>Model</span>
                <input value={hardwareDraft.model} onChange={(event) => setHardwareDraft((current) => ({ ...current, model: event.target.value }))} />
              </label>
              <label className="field" style={{ gridColumn: '3 / -1' }}>
                <span>Notes</span>
                <input value={hardwareDraft.notes} onChange={(event) => setHardwareDraft((current) => ({ ...current, notes: event.target.value }))} />
              </label>
              {hardwareDraft.wearTemplateId && (
                <div style={{ gridColumn: '1 / -1', fontSize: 12, color: 'var(--muted)' }}>
                  Vorlage: {wearTemplates.find((template) => template.id === hardwareDraft.wearTemplateId)?.name ?? hardwareDraft.wearTemplateId}. Name, Category, Lebensdauer und Inspektionsintervall werden bei leeren Feldern serverseitig übernommen.
                </div>
              )}
            </form>
          </div>
        </div>
        </>
        )}

        {activeSection === 'maintenance' && (
        <>
        <div className="section-label">Wartung</div>
        <div className="card management-card" style={{ marginBottom: 24 }}>
          <div className="card-header"><span className="card-title">MaintenanceEvents</span></div>
          <div style={{ padding: '14px 16px', display: 'grid', gap: 12 }}>
            {maintenanceError && <div style={{ fontSize: 13, color: 'var(--red)' }}>{maintenanceError}</div>}
            {maintenanceEvents.length === 0 ? (
              <div style={{ fontSize: 13, color: 'var(--faint)' }}>Keine MaintenanceEvents angelegt.</div>
            ) : (
              <div style={{ display: 'grid', gap: 8 }}>
                {maintenanceEvents.map((item) => (
                  <div
                    key={item.id}
                    style={{
                      display: 'grid',
                      gridTemplateColumns: 'minmax(120px, 1fr) minmax(130px, 1.2fr) minmax(105px, 0.8fr) minmax(105px, 0.8fr) minmax(105px, 0.8fr) minmax(105px, 0.8fr) minmax(105px, 0.8fr) minmax(80px, 0.6fr) auto',
                      gap: 8,
                      alignItems: 'end',
                      padding: '9px 10px',
                      border: '1px solid var(--border)',
                      borderRadius: 7,
                      background: 'var(--surface2)',
                    }}
                  >
                    <div className="field">
                      <label>Hardware</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{getHardwareName(hardwareItems, item.hardwareItemId)}</div>
                    </div>
                    <label className="field">
                      <span>Title</span>
                      <input value={item.title} onChange={(event) => updateMaintenanceEvent(item.id, { title: event.target.value })} />
                    </label>
                    <div className="field">
                      <label>EventType</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{item.eventType}</div>
                    </div>
                    <label className="field">
                      <span>Status</span>
                      <select value={item.status} onChange={(event) => updateMaintenanceEvent(item.id, { status: event.target.value as MaintenanceEventStatus })}>
                        {maintenanceStatusOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                      </select>
                    </label>
                    <label className="field">
                      <span>Result</span>
                      <select value={item.result} onChange={(event) => updateMaintenanceEvent(item.id, { result: event.target.value as MaintenanceResult })}>
                        {maintenanceResultOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                      </select>
                    </label>
                    <div className="field">
                      <label>DueAt</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{formatDate(item.dueAtUtc)}</div>
                    </div>
                    <label className="field">
                      <span>PerformedAt</span>
                      <input type="datetime-local" value={toDateTimeInputValue(item.performedAtUtc)} onChange={(event) => updateMaintenanceEvent(item.id, { performedAtUtc: toNullableDateTime(event.target.value) })} />
                    </label>
                    <div className="field">
                      <label>GrowTask</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{item.growTaskId ?? '-'}</div>
                    </div>
                    <button type="button" className="btn" disabled={saving === `maintenance-${item.id}`} onClick={() => void saveMaintenanceEvent(item)}>
                      {saving === `maintenance-${item.id}` ? 'Speichert...' : 'Speichern'}
                    </button>
                    <div className="field">
                      <label>NextDueAt</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{formatDate(item.nextDueAtUtc)}</div>
                    </div>
                    <label className="field" style={{ gridColumn: '2 / -1' }}>
                      <span>Notes</span>
                      <input value={item.notes ?? ''} onChange={(event) => updateMaintenanceEvent(item.id, { notes: event.target.value })} />
                    </label>
                  </div>
                ))}
              </div>
            )}

            <form onSubmit={(event) => void handleCreateMaintenanceEvent(event)} style={{ display: 'grid', gridTemplateColumns: 'repeat(5, minmax(120px, 1fr)) auto', gap: 8, alignItems: 'end' }}>
              <label className="field">
                <span>HardwareItem</span>
                <select value={maintenanceDraft.hardwareItemId ?? ''} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, hardwareItemId: toNullableInteger(event.target.value) }))}>
                  <option value="">Auswählen</option>
                  {hardwareItems.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                </select>
              </label>
              <label className="field">
                <span>EventType</span>
                <select value={maintenanceDraft.eventType} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, eventType: event.target.value as MaintenanceEventType }))}>
                  {maintenanceEventTypeOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Status</span>
                <select value={maintenanceDraft.status} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, status: event.target.value as MaintenanceEventStatus }))}>
                  {maintenanceStatusOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Title</span>
                <input value={maintenanceDraft.title} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, title: event.target.value }))} placeholder="Filter reinigen" />
              </label>
              <label className="field">
                <span>DueAt</span>
                <input type="datetime-local" value={maintenanceDraft.dueAtUtc} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, dueAtUtc: event.target.value }))} />
              </label>
              <button type="submit" className="btn" disabled={saving === 'maintenance-new'}>
                {saving === 'maintenance-new' ? 'Legt an...' : 'Maintenance anlegen'}
              </button>
              <label className="field">
                <span>PerformedAt</span>
                <input type="datetime-local" value={maintenanceDraft.performedAtUtc} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, performedAtUtc: event.target.value }))} />
              </label>
              <label className="field" style={{ gridColumn: '2 / -1' }}>
                <span>Notes</span>
                <input value={maintenanceDraft.notes} onChange={(event) => setMaintenanceDraft((current) => ({ ...current, notes: event.target.value }))} />
              </label>
            </form>
          </div>
        </div>
        </>
        )}

        {activeSection === 'calibration' && (
        <>
        <div className="section-label">Kalibrierung</div>
        <div className="card management-card" style={{ marginBottom: 24 }}>
          <div className="card-header"><span className="card-title">CalibrationEvents</span></div>
          <div style={{ padding: '14px 16px', display: 'grid', gap: 12 }}>
            {calibrationError && <div style={{ fontSize: 13, color: 'var(--red)' }}>{calibrationError}</div>}
            {calibrationEvents.length === 0 ? (
              <div style={{ fontSize: 13, color: 'var(--faint)' }}>Keine CalibrationEvents angelegt.</div>
            ) : (
              <div style={{ display: 'grid', gap: 8 }}>
                {calibrationEvents.map((item) => (
                  <div
                    key={item.id}
                    style={{
                      display: 'grid',
                      gridTemplateColumns: 'minmax(120px, 1fr) minmax(130px, 1.2fr) minmax(95px, 0.7fr) minmax(105px, 0.8fr) minmax(120px, 0.9fr) minmax(95px, 0.7fr) minmax(105px, 0.8fr) minmax(105px, 0.8fr) minmax(80px, 0.6fr) auto',
                      gap: 8,
                      alignItems: 'end',
                      padding: '9px 10px',
                      border: '1px solid var(--border)',
                      borderRadius: 7,
                      background: 'var(--surface2)',
                    }}
                  >
                    <div className="field">
                      <label>Hardware</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{getHardwareName(hardwareItems, item.hardwareItemId)}</div>
                    </div>
                    <label className="field">
                      <span>Title</span>
                      <input value={item.title} onChange={(event) => updateCalibrationEvent(item.id, { title: event.target.value })} />
                    </label>
                    <div className="field">
                      <label>Type</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{item.calibrationType}</div>
                    </div>
                    <label className="field">
                      <span>Status</span>
                      <select value={item.status} onChange={(event) => updateCalibrationEvent(item.id, { status: event.target.value as CalibrationEventStatus })}>
                        {calibrationStatusOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                      </select>
                    </label>
                    <label className="field">
                      <span>Result</span>
                      <select value={item.result} onChange={(event) => updateCalibrationEvent(item.id, { result: event.target.value as CalibrationResult })}>
                        {calibrationResultOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                      </select>
                    </label>
                    <div className="field">
                      <label>DueAt</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{formatDate(item.dueAtUtc)}</div>
                    </div>
                    <label className="field">
                      <span>PerformedAt</span>
                      <input type="datetime-local" value={toDateTimeInputValue(item.performedAtUtc)} onChange={(event) => updateCalibrationEvent(item.id, { performedAtUtc: toNullableDateTime(event.target.value) })} />
                    </label>
                    <div className="field">
                      <label>NextDueAt</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{formatDate(item.nextDueAtUtc)}</div>
                    </div>
                    <div className="field">
                      <label>GrowTask</label>
                      <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{item.growTaskId ?? '-'}</div>
                    </div>
                    <button type="button" className="btn" disabled={saving === `calibration-${item.id}`} onClick={() => void saveCalibrationEvent(item)}>
                      {saving === `calibration-${item.id}` ? 'Speichert...' : 'Speichern'}
                    </button>
                    <label className="field">
                      <span>Before</span>
                      <input type="number" step="0.001" value={item.beforeValue ?? ''} onChange={(event) => updateCalibrationEvent(item.id, { beforeValue: toNullableNumber(event.target.value) })} />
                    </label>
                    <label className="field">
                      <span>After</span>
                      <input type="number" step="0.001" value={item.afterValue ?? ''} onChange={(event) => updateCalibrationEvent(item.id, { afterValue: toNullableNumber(event.target.value) })} />
                    </label>
                    <label className="field" style={{ gridColumn: '3 / -1' }}>
                      <span>Notes</span>
                      <input value={item.notes ?? ''} onChange={(event) => updateCalibrationEvent(item.id, { notes: event.target.value })} />
                    </label>
                  </div>
                ))}
              </div>
            )}

            <form onSubmit={(event) => void handleCreateCalibrationEvent(event)} style={{ display: 'grid', gridTemplateColumns: 'repeat(6, minmax(110px, 1fr)) auto', gap: 8, alignItems: 'end' }}>
              <label className="field">
                <span>HardwareItem</span>
                <select value={calibrationDraft.hardwareItemId ?? ''} onChange={(event) => setCalibrationDraft((current) => ({ ...current, hardwareItemId: toNullableInteger(event.target.value) }))}>
                  <option value="">Auswählen</option>
                  {hardwareItems.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Type</span>
                <select value={calibrationDraft.calibrationType} onChange={(event) => setCalibrationDraft((current) => ({ ...current, calibrationType: event.target.value as CalibrationEventType }))}>
                  {calibrationEventTypeOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Status</span>
                <select value={calibrationDraft.status} onChange={(event) => setCalibrationDraft((current) => ({ ...current, status: event.target.value as CalibrationEventStatus }))}>
                  {calibrationStatusOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Title</span>
                <input value={calibrationDraft.title} onChange={(event) => setCalibrationDraft((current) => ({ ...current, title: event.target.value }))} placeholder="pH 7.00 prüfen" />
              </label>
              <label className="field">
                <span>ReferenceSolution</span>
                <input value={calibrationDraft.referenceSolution} onChange={(event) => setCalibrationDraft((current) => ({ ...current, referenceSolution: event.target.value }))} placeholder="pH 7.00" />
              </label>
              <label className="field">
                <span>ReferenceValue</span>
                <input type="number" step="0.001" value={calibrationDraft.referenceValue} onChange={(event) => setCalibrationDraft((current) => ({ ...current, referenceValue: event.target.value }))} />
              </label>
              <button type="submit" className="btn" disabled={saving === 'calibration-new'}>
                {saving === 'calibration-new' ? 'Legt an...' : 'Calibration anlegen'}
              </button>
              <label className="field">
                <span>Before</span>
                <input type="number" step="0.001" value={calibrationDraft.beforeValue} onChange={(event) => setCalibrationDraft((current) => ({ ...current, beforeValue: event.target.value }))} />
              </label>
              <label className="field">
                <span>After</span>
                <input type="number" step="0.001" value={calibrationDraft.afterValue} onChange={(event) => setCalibrationDraft((current) => ({ ...current, afterValue: event.target.value }))} />
              </label>
              <label className="field">
                <span>TemperatureC</span>
                <input type="number" step="0.1" value={calibrationDraft.temperatureC} onChange={(event) => setCalibrationDraft((current) => ({ ...current, temperatureC: event.target.value }))} />
              </label>
              <label className="field">
                <span>DueAt</span>
                <input type="datetime-local" value={calibrationDraft.dueAtUtc} onChange={(event) => setCalibrationDraft((current) => ({ ...current, dueAtUtc: event.target.value }))} />
              </label>
              <label className="field">
                <span>PerformedAt</span>
                <input type="datetime-local" value={calibrationDraft.performedAtUtc} onChange={(event) => setCalibrationDraft((current) => ({ ...current, performedAtUtc: event.target.value }))} />
              </label>
              <label className="field" style={{ gridColumn: '6 / -1' }}>
                <span>Notes</span>
                <input value={calibrationDraft.notes} onChange={(event) => setCalibrationDraft((current) => ({ ...current, notes: event.target.value }))} />
              </label>
            </form>
          </div>
        </div>
        </>
        )}

        {activeSection === 'risks' && (
        <>
        <div className="section-label">Risiken</div>
        <div className="card management-card" style={{ marginBottom: 24 }}>
          <div className="card-header"><span className="card-title">RiskEvents</span></div>
          <div style={{ padding: '14px 16px', display: 'grid', gap: 12 }}>
            {riskError && <div style={{ fontSize: 13, color: 'var(--red)' }}>{riskError}</div>}
            {riskEvents.length === 0 ? (
              <div style={{ fontSize: 13, color: 'var(--faint)' }}>Keine RiskEvents angelegt.</div>
            ) : (
              <div style={{ display: 'grid', gap: 8 }}>
                {riskEvents.map((item) => {
                  const recommendations = riskSopRecommendations[item.id] ?? []
                  const canShowRecommendations = item.status === 'Open' || item.status === 'Acknowledged'

                  return (
                    <div
                      key={item.id}
                      style={{
                        display: 'grid',
                        gridTemplateColumns: 'minmax(95px, 0.7fr) minmax(105px, 0.8fr) minmax(125px, 1fr) minmax(150px, 1.2fr) minmax(120px, 1fr) minmax(100px, 0.8fr) minmax(90px, 0.7fr) minmax(90px, 0.7fr) minmax(90px, 0.7fr) auto auto auto',
                        gap: 8,
                        alignItems: 'end',
                        padding: '9px 10px',
                        border: '1px solid var(--border)',
                        borderRadius: 7,
                        background: 'var(--surface2)',
                      }}
                    >
                      <label className="field">
                        <span>Severity</span>
                        <select value={item.severity} onChange={(event) => updateRiskEvent(item.id, { severity: event.target.value as RiskEventSeverity })}>
                          {riskSeverityOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                        </select>
                      </label>
                      <label className="field">
                        <span>Status</span>
                        <select value={item.status} onChange={(event) => updateRiskEvent(item.id, { status: event.target.value as RiskEventStatus })}>
                          {riskStatusOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                        </select>
                      </label>
                      <div className="field">
                        <label>EventType</label>
                        <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{item.eventType}</div>
                      </div>
                      <label className="field">
                        <span>Title</span>
                        <input value={item.title} onChange={(event) => updateRiskEvent(item.id, { title: event.target.value })} />
                      </label>
                      <div className="field">
                        <label>Hardware</label>
                        <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{item.hardwareItemId ? getHardwareName(hardwareItems, item.hardwareItemId) : '-'}</div>
                      </div>
                      <div className="field">
                        <label>Tent</label>
                        <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{getTentName(tents, item.tentId)}</div>
                      </div>
                      <div className="field">
                        <label>Started</label>
                        <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{formatDate(item.startedAtUtc)}</div>
                      </div>
                      <div className="field">
                        <label>LastSeen</label>
                        <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{formatDate(item.lastSeenAtUtc)}</div>
                      </div>
                      <div className="field">
                        <label>Resolved</label>
                        <div style={{ fontSize: 13, minHeight: 34, display: 'flex', alignItems: 'center' }}>{formatDate(item.resolvedAtUtc)}</div>
                      </div>
                      <button type="button" className="btn" disabled={saving === `risk-${item.id}`} onClick={() => void saveRiskEvent(item)}>
                        {saving === `risk-${item.id}` ? 'Speichert...' : 'Speichern'}
                      </button>
                      <button type="button" className="btn" disabled={saving === `risk-ack-${item.id}`} onClick={() => void acknowledgeRiskEvent(item)}>
                        {saving === `risk-ack-${item.id}` ? '...' : 'Ack'}
                      </button>
                      <button type="button" className="btn" disabled={saving === `risk-resolve-${item.id}`} onClick={() => void resolveRiskEvent(item)}>
                        {saving === `risk-resolve-${item.id}` ? '...' : 'Resolve'}
                      </button>
                      <label className="field" style={{ gridColumn: '1 / 5' }}>
                        <span>DedupeKey</span>
                        <input value={item.dedupeKey ?? ''} onChange={(event) => updateRiskEvent(item.id, { dedupeKey: event.target.value })} />
                      </label>
                      <label className="field" style={{ gridColumn: '5 / -1' }}>
                        <span>Notes</span>
                        <input value={item.notes ?? ''} onChange={(event) => updateRiskEvent(item.id, { notes: event.target.value })} />
                      </label>
                      {canShowRecommendations && (
                        <div style={{ gridColumn: '1 / -1', display: 'grid', gap: 6, borderTop: '1px solid var(--border)', paddingTop: 8 }}>
                          <div style={{ fontSize: 12, color: 'var(--muted)' }}>SOP-Empfehlungen</div>
                          {recommendations.length === 0 ? (
                            <div style={{ fontSize: 13, color: 'var(--faint)' }}>Keine passende SOP-Empfehlung.</div>
                          ) : (
                            recommendations.map((recommendation) => {
                              const missingGrow = !item.growId
                              const startDisabled = missingGrow || recommendation.alreadyActive || saving === `risk-sop-${item.id}-${recommendation.sopId}`

                              return (
                                <div key={`${item.id}-${recommendation.sopId}`} style={{ display: 'grid', gridTemplateColumns: 'minmax(180px, 1fr) minmax(80px, 0.4fr) minmax(220px, 1.3fr) auto', gap: 8, alignItems: 'center' }}>
                                  <div style={{ fontSize: 13, fontWeight: 600 }}>
                                    {recommendation.sopName}
                                    {recommendation.activeSopInstanceId ? ` (aktiv #${recommendation.activeSopInstanceId})` : ''}
                                  </div>
                                  <div style={{ fontSize: 13 }}>{recommendation.confidence}</div>
                                  <div style={{ fontSize: 13, color: 'var(--muted)' }}>
                                    {recommendation.reason}
                                    {missingGrow ? ' SOP-Start benötigt Grow-Zuordnung.' : ''}
                                    {recommendation.alreadyActive ? ' SOP bereits aktiv.' : ''}
                                  </div>
                                  <button type="button" className="btn" disabled={startDisabled} onClick={() => void startRiskEventSop(item, recommendation)}>
                                    {saving === `risk-sop-${item.id}-${recommendation.sopId}` ? 'Startet...' : 'SOP starten'}
                                  </button>
                                </div>
                              )
                            })
                          )}
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
            )}

            <form onSubmit={(event) => void handleCreateRiskEvent(event)} style={{ display: 'grid', gridTemplateColumns: 'repeat(6, minmax(120px, 1fr)) auto', gap: 8, alignItems: 'end' }}>
              <label className="field">
                <span>EventType</span>
                <select value={riskDraft.eventType} onChange={(event) => setRiskDraft((current) => ({ ...current, eventType: event.target.value as RiskEventType }))}>
                  {riskEventTypeOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Severity</span>
                <select value={riskDraft.severity} onChange={(event) => setRiskDraft((current) => ({ ...current, severity: event.target.value as RiskEventSeverity }))}>
                  {riskSeverityOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Source</span>
                <select value={riskDraft.source} onChange={(event) => setRiskDraft((current) => ({ ...current, source: event.target.value as RiskEventSource }))}>
                  {riskSourceOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Title</span>
                <input value={riskDraft.title} onChange={(event) => setRiskDraft((current) => ({ ...current, title: event.target.value }))} placeholder="Pumpe offline" />
              </label>
              <label className="field">
                <span>HardwareItem</span>
                <select value={riskDraft.hardwareItemId ?? ''} onChange={(event) => setRiskDraft((current) => ({ ...current, hardwareItemId: toNullableInteger(event.target.value) }))}>
                  <option value="">Optional</option>
                  {hardwareItems.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Tent</span>
                <select value={riskDraft.tentId ?? ''} onChange={(event) => setRiskDraft((current) => ({ ...current, tentId: toNullableInteger(event.target.value) }))}>
                  <option value="">Optional</option>
                  {tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}
                </select>
              </label>
              <button type="submit" className="btn" disabled={saving === 'risk-new'}>
                {saving === 'risk-new' ? 'Legt an...' : 'Risk anlegen'}
              </button>
              <label className="field">
                <span>Grow</span>
                <select value={riskDraft.growId ?? ''} onChange={(event) => setRiskDraft((current) => ({ ...current, growId: toNullableInteger(event.target.value) }))}>
                  <option value="">Optional</option>
                  {grows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
                </select>
              </label>
              <label className="field" style={{ gridColumn: '2 / 4' }}>
                <span>DedupeKey</span>
                <input value={riskDraft.dedupeKey} onChange={(event) => setRiskDraft((current) => ({ ...current, dedupeKey: event.target.value }))} placeholder="pump:main" />
              </label>
              <label className="field" style={{ gridColumn: '4 / -1' }}>
                <span>Notes</span>
                <input value={riskDraft.notes} onChange={(event) => setRiskDraft((current) => ({ ...current, notes: event.target.value }))} />
              </label>
            </form>
          </div>
        </div>
        </>
        )}
      </div>
    </>
  )
}

function createHardwareDraft(): HardwareDraft {
  return {
    name: '',
    category: '',
    status: 'Active',
    criticality: 'Medium',
    tentId: null,
    wearTemplateId: '',
    haEntityId: '',
    manufacturer: '',
    model: '',
    installedAtUtc: '',
    notes: '',
  }
}

function createMaintenanceDraft(): MaintenanceDraft {
  return {
    hardwareItemId: null,
    eventType: 'Inspection',
    status: 'Planned',
    title: '',
    dueAtUtc: '',
    performedAtUtc: '',
    notes: '',
  }
}

function createCalibrationDraft(): CalibrationDraft {
  return {
    hardwareItemId: null,
    calibrationType: 'Ph',
    status: 'Planned',
    title: '',
    referenceSolution: '',
    referenceValue: '',
    beforeValue: '',
    afterValue: '',
    temperatureC: '',
    dueAtUtc: '',
    performedAtUtc: '',
    notes: '',
  }
}

function createRiskDraft(): RiskDraft {
  return {
    eventType: 'Other',
    severity: 'Warning',
    source: 'Manual',
    title: '',
    hardwareItemId: null,
    tentId: null,
    growId: null,
    dedupeKey: '',
    notes: '',
  }
}

function formatApiError(caught: unknown, fallback: string): string {
  if (caught instanceof ApiRequestError) {
    const firstFieldError = caught.payload?.fieldErrors ? Object.values(caught.payload.fieldErrors).flat()[0] : null
    return firstFieldError ?? caught.message
  }

  return caught instanceof Error ? caught.message : fallback
}

function toNullableString(value: string | null | undefined): string | null {
  const normalized = value?.trim() ?? ''
  return normalized.length > 0 ? normalized : null
}

function toNullableDateTime(value: string): string | null {
  return value ? new Date(value).toISOString() : null
}

function toDateTimeInputValue(value: string | null | undefined): string {
  return value ? value.slice(0, 16) : ''
}

function formatDate(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : '-'
}

function isDueOpen(status: string, dueAtUtc: string | null, nowUtc: number): boolean {
  if (!dueAtUtc || status !== 'Planned') return false
  const dueAt = Date.parse(dueAtUtc)
  return Number.isFinite(dueAt) && dueAt <= nowUtc
}

function getTentName(tents: TentDto[], tentId: number | null): string {
  return tentId ? tents.find((tent) => tent.id === tentId)?.name ?? `Tent #${tentId}` : 'Global'
}

function getHardwareName(items: HardwareItemDto[], hardwareItemId: number): string {
  return items.find((item) => item.id === hardwareItemId)?.name ?? `Hardware #${hardwareItemId}`
}

function toNullableInteger(value: string): number | null {
  const normalized = value.trim()
  if (!normalized) return null

  const parsed = Number.parseInt(normalized, 10)
  return Number.isNaN(parsed) ? null : parsed
}

function toNullableNumber(value: string): number | null {
  const normalized = value.trim()
  if (!normalized) return null

  const parsed = Number.parseFloat(normalized)
  return Number.isNaN(parsed) ? null : parsed
}

export default HardwarePage
