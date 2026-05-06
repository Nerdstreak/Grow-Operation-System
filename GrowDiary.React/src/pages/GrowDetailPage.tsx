import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type {
  AutoMeasurementAggregation,
  AutoMeasurementConfigDto,
  AutoMeasurementField,
  AutoMeasurementFieldMappingDto,
  AutoMeasurementFieldMappingUpsertRequest,
  AutoMeasurementStatus,
  AutoMeasurementTriggerKind,
  GrowActionResultDto,
  GrowDetail,
  GrowTaskDto,
  JournalEntryDto,
  MeasurementDto,
  PhotoAssetDto,
  PhotoTag,
  ValueOrigin,
} from '../types'
import { formatDate, formatDateTime, formatNumber, toLocalInputValue } from '../utils'

interface DetailBundle {
  grow: GrowDetail | null
  measurements: MeasurementDto[]
  tasks: GrowTaskDto[]
  journal: JournalEntryDto[]
}

const photoTags: PhotoTag[] = ['Overview', 'Canopy', 'Leaf', 'Root', 'Training', 'Flower', 'Problem', 'Comparison', 'Other']
const autoMeasurementFields: AutoMeasurementField[] = [
  'AirTemperatureC',
  'HumidityPercent',
  'ReservoirPh',
  'ReservoirEc',
  'ReservoirWaterTempC',
  'ReservoirLevelLiters',
  'ReservoirLevelCm',
  'DissolvedOxygenMgL',
  'OrpMv',
  'PpfdMol',
  'Co2Ppm',
]
const autoMeasurementAggregations: AutoMeasurementAggregation[] = ['Latest', 'Median', 'Average']
const autoMeasurementTriggerKinds: AutoMeasurementTriggerKind[] = ['Manual', 'LightOnDelay', 'LightOffDelay']
const autoMeasurementStatuses: AutoMeasurementStatus[] = ['Enabled', 'Disabled']
const defaultMetricKeyByField: Record<AutoMeasurementField, string> = {
  AirTemperatureC: 'temperature',
  HumidityPercent: 'humidity',
  ReservoirPh: 'reservoir-ph',
  ReservoirEc: 'reservoir-ec',
  ReservoirWaterTempC: 'reservoir-temp',
  ReservoirLevelLiters: 'reservoir-level',
  ReservoirLevelCm: 'reservoir-level',
  DissolvedOxygenMgL: 'dissolved-oxygen',
  OrpMv: 'orp',
  PpfdMol: 'ppfd',
  Co2Ppm: 'co2',
}

const emptyMeasurementForm = () => ({
  takenAtLocal: toLocalInputValue(),
  stage: 'Veg',
  source: 'Manual',
  airTemperatureC: '',
  humidityPercent: '',
  reservoirPh: '',
  reservoirEc: '',
  reservoirWaterTempC: '',
  notes: '',
})

const emptyTaskForm = () => ({
  title: '',
  dueAtLocal: '',
  priority: 'Normal',
  notes: '',
})

const emptyJournalForm = () => ({
  title: '',
  body: '',
  entryType: 'Observation',
  source: 'Manual',
  occurredAtLocal: toLocalInputValue(),
})

const emptyPhotoForm = () => ({
  photoCaption: '',
  photoTag: 'Overview' as PhotoTag,
  useAsReferenceShot: false,
  source: 'Manual' as ValueOrigin,
  files: [] as File[],
})

const emptyAutoConfigForm = () => ({
  name: '',
  status: 'Enabled' as AutoMeasurementStatus,
  triggerKind: 'Manual' as AutoMeasurementTriggerKind,
  delayMinutes: '',
  windowMinutes: '20',
})

const emptyMappingDraft = (): AutoMeasurementFieldMappingUpsertRequest => ({
  measurementField: 'AirTemperatureC',
  metricKey: defaultMetricKeyByField.AirTemperatureC,
  aggregation: 'Latest',
  isRequired: true,
})

function GrowDetailPage() {
  const { growId } = useParams()
  const [bundle, setBundle] = useState<DetailBundle>({ grow: null, measurements: [], tasks: [], journal: [] })
  const [photos, setPhotos] = useState<PhotoAssetDto[]>([])
  const [selectedMeasurementId, setSelectedMeasurementId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [photoLoading, setPhotoLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState<string | null>(null)
  const [measurementForm, setMeasurementForm] = useState(emptyMeasurementForm)
  const [taskForm, setTaskForm] = useState(emptyTaskForm)
  const [journalForm, setJournalForm] = useState(emptyJournalForm)
  const [photoForm, setPhotoForm] = useState(emptyPhotoForm)
  const [autoConfigs, setAutoConfigs] = useState<AutoMeasurementConfigDto[]>([])
  const [autoMappingsByConfigId, setAutoMappingsByConfigId] = useState<Record<number, AutoMeasurementFieldMappingDto[]>>({})
  const [mappingDraftsByConfigId, setMappingDraftsByConfigId] = useState<Record<number, AutoMeasurementFieldMappingUpsertRequest[]>>({})
  const [autoConfigForm, setAutoConfigForm] = useState(emptyAutoConfigForm)
  const [autoLoading, setAutoLoading] = useState(false)

  const loadPhotos = useCallback(async (measurementId: number, signal?: AbortSignal) => {
    setPhotoLoading(true)
    try {
      const nextPhotos = await apiFetch<PhotoAssetDto[]>(`/api/measurements/${measurementId}/photos`, { signal })
      setPhotos(nextPhotos)
    } catch (caught) {
      if (signal?.aborted) return
      setError(caught instanceof ApiRequestError ? caught.message : 'Fotos konnten nicht geladen werden.')
    } finally {
      if (!signal?.aborted) setPhotoLoading(false)
    }
  }, [])

  const loadAutoMeasurements = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    setAutoLoading(true)
    try {
      const configs = await apiFetch<AutoMeasurementConfigDto[]>(`/api/auto-measurements/configs?growId=${growId}`, { signal })
      const mappingEntries = await Promise.all(configs.map(async (config) => {
        const mappings = await apiFetch<AutoMeasurementFieldMappingDto[]>(`/api/auto-measurements/configs/${config.id}/mappings`, { signal })
        return [config.id, mappings] as const
      }))
      const nextMappings = Object.fromEntries(mappingEntries)
      setAutoConfigs(configs)
      setAutoMappingsByConfigId(nextMappings)
      setMappingDraftsByConfigId(Object.fromEntries(mappingEntries.map(([configId, mappings]) => [
        configId,
        mappings.map((mapping) => ({
          measurementField: mapping.measurementField,
          metricKey: mapping.metricKey,
          aggregation: mapping.aggregation,
          isRequired: mapping.isRequired,
        })),
      ])))
    } catch (caught) {
      if (signal?.aborted) return
      setError(caught instanceof ApiRequestError ? caught.message : 'AutoMeasurement-Konfigurationen konnten nicht geladen werden.')
    } finally {
      if (!signal?.aborted) setAutoLoading(false)
    }
  }, [growId])

  const loadBundle = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    try {
      const [grow, measurements, tasks, journal] = await Promise.all([
        apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal }),
        apiFetch<MeasurementDto[]>(`/api/grows/${growId}/measurements`, { signal }),
        apiFetch<GrowTaskDto[]>(`/api/grows/${growId}/tasks`, { signal }),
        apiFetch<JournalEntryDto[]>(`/api/grows/${growId}/journal`, { signal }),
      ])
      const nextMeasurementId = measurements.find((measurement) => measurement.id === selectedMeasurementId)?.id ?? measurements[0]?.id ?? null
      setBundle({ grow, measurements, tasks, journal })
      setSelectedMeasurementId(nextMeasurementId)
      setError(null)
      if (nextMeasurementId) {
        await loadPhotos(nextMeasurementId, signal)
      } else {
        setPhotos([])
      }
    } catch (caught) {
      if (signal?.aborted) return
      setError(caught instanceof ApiRequestError ? caught.message : 'Grow-Details konnten nicht geladen werden.')
    } finally {
      if (!signal?.aborted) setLoading(false)
    }
  }, [growId, loadPhotos, selectedMeasurementId])

  useEffect(() => {
    const controller = new AbortController()
    const handle = window.setTimeout(() => {
      void loadBundle(controller.signal)
      void loadAutoMeasurements(controller.signal)
    }, 0)
    return () => {
      window.clearTimeout(handle)
      controller.abort()
    }
  }, [loadAutoMeasurements, loadBundle])

  const openTasks = useMemo(() => bundle.tasks.filter((task) => task.status === 'Open'), [bundle.tasks])
  const closedTasks = useMemo(() => bundle.tasks.filter((task) => task.status !== 'Open'), [bundle.tasks])
  const selectedMeasurement = useMemo(
    () => bundle.measurements.find((measurement) => measurement.id === selectedMeasurementId) ?? null,
    [bundle.measurements, selectedMeasurementId],
  )

  async function handleMeasurementSelection(nextId: number | null) {
    setSelectedMeasurementId(nextId)
    if (!nextId) {
      setPhotos([])
      return
    }

    await loadPhotos(nextId)
  }

  async function handleMeasurementSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!growId) return

    setSaving('measurement')
    try {
      await apiFetch(`/api/grows/${growId}/measurements`, {
        method: 'POST',
        body: JSON.stringify({
          takenAtLocal: measurementForm.takenAtLocal,
          stage: measurementForm.stage,
          source: measurementForm.source,
          airTemperatureC: toNullableNumber(measurementForm.airTemperatureC),
          humidityPercent: toNullableNumber(measurementForm.humidityPercent),
          reservoirPh: toNullableNumber(measurementForm.reservoirPh),
          reservoirEc: toNullableNumber(measurementForm.reservoirEc),
          reservoirWaterTempC: toNullableNumber(measurementForm.reservoirWaterTempC),
          notes: measurementForm.notes || null,
        }),
      })
      setMeasurementForm(emptyMeasurementForm())
      setNotice('Messung gespeichert.')
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Messung konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function handleTaskSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!growId) return

    setSaving('task')
    try {
      await apiFetch(`/api/grows/${growId}/tasks`, {
        method: 'POST',
        body: JSON.stringify({
          title: taskForm.title,
          notes: taskForm.notes || null,
          dueAtLocal: taskForm.dueAtLocal || null,
          priority: taskForm.priority,
        }),
      })
      setTaskForm(emptyTaskForm())
      setNotice('Task gespeichert.')
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Aufgabe konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function handleJournalSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!growId) return

    setSaving('journal')
    try {
      await apiFetch(`/api/grows/${growId}/journal`, {
        method: 'POST',
        body: JSON.stringify({
          title: journalForm.title || null,
          body: journalForm.body || null,
          entryType: journalForm.entryType,
          source: journalForm.source,
          occurredAtLocal: journalForm.occurredAtLocal,
        }),
      })
      setJournalForm(emptyJournalForm())
      setNotice('Journal gespeichert.')
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Journal konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function handlePhotoSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedMeasurement || photoForm.files.length === 0) {
      setError('Bitte waehle eine Messung und mindestens ein Foto aus.')
      return
    }

    setSaving('photo')
    try {
      const formData = new FormData()
      formData.append('photoCaption', photoForm.photoCaption)
      formData.append('photoTag', photoForm.photoTag)
      formData.append('useAsReferenceShot', String(photoForm.useAsReferenceShot))
      formData.append('source', photoForm.source)
      for (const file of photoForm.files) {
        formData.append('photos', file)
      }

      await apiFetch<PhotoAssetDto[]>(`/api/measurements/${selectedMeasurement.id}/photos`, { method: 'POST', body: formData })
      setPhotoForm(emptyPhotoForm())
      setNotice('Fotos hochgeladen.')
      await Promise.all([loadBundle(), loadPhotos(selectedMeasurement.id)])
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Fotos konnten nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function handleAutoConfigSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!bundle.grow) return

    const windowMinutes = toNullableInteger(autoConfigForm.windowMinutes)
    if (!autoConfigForm.name.trim() || !windowMinutes) {
      setError('Name und gueltiges Zeitfenster sind erforderlich.')
      return
    }

    setSaving('auto-config')
    try {
      await apiFetch('/api/auto-measurements/configs', {
        method: 'POST',
        body: JSON.stringify({
          growId: bundle.grow.id,
          tentId: bundle.grow.tentId,
          name: autoConfigForm.name.trim(),
          status: autoConfigForm.status,
          triggerKind: autoConfigForm.triggerKind,
          delayMinutes: toNullableInteger(autoConfigForm.delayMinutes),
          windowMinutes,
        }),
      })
      setAutoConfigForm(emptyAutoConfigForm())
      setNotice('AutoMeasurement-Konfiguration gespeichert.')
      await loadAutoMeasurements()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'AutoMeasurement-Konfiguration konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  function addMappingDraft(configId: number) {
    setMappingDraftsByConfigId((current) => ({
      ...current,
      [configId]: [...(current[configId] ?? []), emptyMappingDraft()],
    }))
  }

  function updateMappingDraft(configId: number, index: number, patch: Partial<AutoMeasurementFieldMappingUpsertRequest>) {
    setMappingDraftsByConfigId((current) => ({
      ...current,
      [configId]: (current[configId] ?? []).map((mapping, currentIndex) => {
        if (currentIndex !== index) return mapping
        const next = { ...mapping, ...patch }
        if (patch.measurementField && patch.metricKey === undefined) {
          next.metricKey = defaultMetricKeyByField[patch.measurementField]
        }
        return next
      }),
    }))
  }

  function removeMappingDraft(configId: number, index: number) {
    setMappingDraftsByConfigId((current) => ({
      ...current,
      [configId]: (current[configId] ?? []).filter((_, currentIndex) => currentIndex !== index),
    }))
  }

  async function saveMappingDrafts(configId: number) {
    const mappings = mappingDraftsByConfigId[configId] ?? []
    if (mappings.some((mapping) => !mapping.metricKey.trim())) {
      setError('MetricKey darf nicht leer sein.')
      return
    }

    setSaving(`auto-mappings-${configId}`)
    try {
      await apiFetch(`/api/auto-measurements/configs/${configId}/mappings`, {
        method: 'PUT',
        body: JSON.stringify({
          mappings: mappings.map((mapping) => ({
            ...mapping,
            metricKey: mapping.metricKey.trim(),
          })),
        }),
      })
      setNotice('Mappings gespeichert.')
      await loadAutoMeasurements()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Mappings konnten nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function updateTaskStatus(taskId: number, status: 'Open' | 'Done' | 'Skipped') {
    setSaving(`task-status-${taskId}`)
    try {
      await apiFetch(`/api/tasks/${taskId}/status`, { method: 'PATCH', body: JSON.stringify({ status }) })
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Task-Status konnte nicht geaendert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function handleGrowAction(action: 'germination' | 'rooting' | 'flip') {
    if (!growId) return

    const route = action === 'germination'
      ? 'confirm-germination'
      : action === 'rooting'
        ? 'confirm-rooting'
        : 'flip-to-flower'

    setSaving(`action-${action}`)
    try {
      const result = await apiFetch<GrowActionResultDto>(`/api/grows/${growId}/actions/${route}`, { method: 'POST' })
      setNotice(result.message)
      setError(null)
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Grow-Aktion konnte nicht ausgefuehrt werden.')
    } finally {
      setSaving(null)
    }
  }

  if (loading) {
    return (
      <>
        <div className="topbar"><span className="topbar-title">Grow-Detail</span></div>
        <div className="page-scroll"><div className="empty-hint">Lade Daten...</div></div>
      </>
    )
  }

  if (!bundle.grow) {
    return (
      <>
        <div className="topbar"><Link className="btn" to="/">Zurueck</Link></div>
        <div className="page-scroll">
          <div className="empty-hint" style={{ color: 'var(--red)' }}>{error ?? 'Grow nicht gefunden.'}</div>
        </div>
      </>
    )
  }

  const grow = bundle.grow
  const latest = grow.latestMeasurement
  const canConfirmGermination = grow.startMaterial === 'Seed' && !grow.germinatedAt
  const canConfirmRooting = grow.startMaterial === 'Clone' && !grow.rootedAt
  const canFlipToFlower = grow.seedType !== 'Autoflower' && !grow.flipDate

  return (
    <>
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to="/">Zurueck</Link>
          <span className="topbar-title">{grow.name}</span>
        </div>
        <div className="topbar-right">
          <span className={`badge ${grow.status === 'Running' ? 'badge-ok' : grow.status === 'Planning' ? 'badge-warn' : 'badge-neutral'}`}>{grow.status}</span>
          <Link className="btn btn-primary" to={`/grows/${grow.id}/setup`}>Setup bearbeiten</Link>
        </div>
      </div>

      <div className="page-scroll">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)' }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}
        {notice && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)', background: 'var(--green-bg)', borderColor: 'var(--green)' }}>
            <div className="alert-dot" style={{ background: 'var(--green)' }} />
            <strong style={{ color: 'var(--green)' }}>Info</strong>
            <span>{notice}</span>
          </div>
        )}

        <div className="grow-hero">
          <div className="grow-hero-title">{grow.name}</div>
          <div className="grow-hero-sub">{grow.strain ?? 'Unbekannter Strain'} · {grow.breeder ?? 'kein Breeder'} · {grow.hydroStyle} · {grow.tentName ?? 'ohne Zelt'}</div>
          <div className="grow-kpis">
            <div className="grow-kpi">
              <div className="grow-kpi-val">{formatNumber(latest?.reservoirPh, 2)}</div>
              <div className="grow-kpi-label">Reservoir pH</div>
            </div>
            <div className="grow-kpi">
              <div className="grow-kpi-val">{formatNumber(latest?.reservoirEc, 2)}</div>
              <div className="grow-kpi-label">Reservoir EC</div>
            </div>
            <div className="grow-kpi">
              <div className="grow-kpi-val">{latest ? `${formatNumber(latest.airTemperatureC, 1)}°` : '—'}</div>
              <div className="grow-kpi-label">Lufttemp</div>
            </div>
            <div className="grow-kpi">
              <div className="grow-kpi-val">{latest ? `${formatNumber(latest.humidityPercent, 0)}%` : '—'}</div>
              <div className="grow-kpi-label">Luftfeuchte</div>
            </div>
            <div className="grow-kpi">
              <div className="grow-kpi-val">{bundle.measurements.length}</div>
              <div className="grow-kpi-label">Messungen</div>
            </div>
            <div className="grow-kpi">
              <div className="grow-kpi-val">{openTasks.length}</div>
              <div className="grow-kpi-label">Offene Tasks</div>
            </div>
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, marginTop: 14 }}>
            <Link className="btn" to={`/grows/${grow.id}/addback`}>Addback</Link>
            <Link className="btn" to={`/grows/${grow.id}/harvest`}>Harvest</Link>
            <Link className="btn" to={`/analyse?leftGrowId=${grow.id}`}>Vergleichen</Link>
            <a className="btn" href={`/grows/${grow.id}/export`}>Export</a>
            {canConfirmGermination && (
              <button type="button" className="btn" disabled={saving === 'action-germination'} onClick={() => void handleGrowAction('germination')}>
                {saving === 'action-germination' ? 'Bestaetigt...' : 'Keimung bestaetigen'}
              </button>
            )}
            {canConfirmRooting && (
              <button type="button" className="btn" disabled={saving === 'action-rooting'} onClick={() => void handleGrowAction('rooting')}>
                {saving === 'action-rooting' ? 'Bestaetigt...' : 'Bewurzelung bestaetigen'}
              </button>
            )}
            {canFlipToFlower && (
              <button type="button" className="btn" disabled={saving === 'action-flip'} onClick={() => void handleGrowAction('flip')}>
                {saving === 'action-flip' ? 'Traegt ein...' : 'Flip zu 12/12'}
              </button>
            )}
          </div>
        </div>

        <div className="detail-layout">
          <div>
            <div className="section-label">Messungen</div>
            <div className="card" style={{ marginBottom: 14 }}>
              <div className="card-header">
                <span className="card-title">Verlauf</span>
                <span className="text-muted" style={{ fontSize: 13 }}>{bundle.measurements.length} gesamt</span>
              </div>
              {bundle.measurements.length === 0 ? (
                <div className="empty-hint">Noch keine Messungen vorhanden.</div>
              ) : (
                bundle.measurements.slice(0, 15).map((measurement) => (
                  <div
                    key={measurement.id}
                    className="timeline-item"
                    style={{ cursor: 'pointer', padding: '12px 16px', background: selectedMeasurementId === measurement.id ? 'var(--surface2)' : undefined }}
                    onClick={() => void handleMeasurementSelection(measurement.id)}
                  >
                    <div className="tl-dot-col">
                      <div className="tl-dot measurement" />
                      <div className="tl-line" />
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="tl-title">{measurement.stage} · pH {formatNumber(measurement.reservoirPh, 2)} · EC {formatNumber(measurement.reservoirEc, 2)}</div>
                      <div className="tl-sub">{formatNumber(measurement.airTemperatureC, 1)}°C · {formatNumber(measurement.humidityPercent, 0)}% rF</div>
                    </div>
                    <div style={{ display: 'grid', gap: 6, justifyItems: 'end' }}>
                      <div className="tl-time">{formatDateTime(measurement.takenAt)}</div>
                      <Link className="btn" to={`/grows/measurements/${measurement.id}/edit`} onClick={(event) => event.stopPropagation()}>Bearbeiten</Link>
                    </div>
                  </div>
                ))
              )}
            </div>

            <div className="section-label">Journal</div>
            <div className="card" style={{ marginBottom: 14 }}>
              <div className="card-header">
                <span className="card-title">Eintraege</span>
                <span className="text-muted" style={{ fontSize: 13 }}>{bundle.journal.length}</span>
              </div>
              {bundle.journal.length === 0 ? (
                <div className="empty-hint">Noch keine Journal-Eintraege.</div>
              ) : (
                bundle.journal.map((entry) => (
                  <div key={entry.id} className="timeline-item" style={{ padding: '12px 16px' }}>
                    <div className="tl-dot-col">
                      <div className="tl-dot journal" />
                      <div className="tl-line" />
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="tl-title">{entry.title ?? entry.entryType}</div>
                      {entry.body && <div className="tl-sub">{entry.body}</div>}
                    </div>
                    <div className="tl-time">{formatDateTime(entry.occurredAtUtc)}</div>
                  </div>
                ))
              )}
            </div>

            <div className="section-label">Neue Messung</div>
            <div className="card" style={{ marginBottom: 14 }}>
              <div className="card-header"><span className="card-title">Messung eintragen</span></div>
              <form onSubmit={handleMeasurementSubmit} style={{ padding: '16px 20px' }}>
                <div className="meas-fields" style={{ marginBottom: 16 }}>
                  <div className="meas-field">
                    <label>Zeitpunkt</label>
                    <input className="meas-input" style={{ fontSize: 15 }} type="datetime-local" value={measurementForm.takenAtLocal} onChange={(event) => setMeasurementForm((current) => ({ ...current, takenAtLocal: event.target.value }))} />
                  </div>
                  <div className="meas-field">
                    <label>Phase</label>
                    <select className="meas-input" style={{ fontSize: 15 }} value={measurementForm.stage} onChange={(event) => setMeasurementForm((current) => ({ ...current, stage: event.target.value }))}>
                      <option>Seedling</option><option>Clone</option><option>Veg</option><option>Transition</option><option>Flower</option><option>Finish</option><option>Dry</option><option>Cure</option>
                    </select>
                  </div>
                  <div className="meas-field">
                    <label>pH</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={measurementForm.reservoirPh} onChange={(event) => setMeasurementForm((current) => ({ ...current, reservoirPh: event.target.value }))} placeholder="5.8" />
                      <span className="meas-unit">pH</span>
                    </div>
                  </div>
                  <div className="meas-field">
                    <label>EC</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={measurementForm.reservoirEc} onChange={(event) => setMeasurementForm((current) => ({ ...current, reservoirEc: event.target.value }))} placeholder="1.6" />
                      <span className="meas-unit">mS/cm</span>
                    </div>
                  </div>
                  <div className="meas-field">
                    <label>Wassertemp</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={measurementForm.reservoirWaterTempC} onChange={(event) => setMeasurementForm((current) => ({ ...current, reservoirWaterTempC: event.target.value }))} placeholder="19.0" />
                      <span className="meas-unit">°C</span>
                    </div>
                  </div>
                  <div className="meas-field">
                    <label>Lufttemp</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={measurementForm.airTemperatureC} onChange={(event) => setMeasurementForm((current) => ({ ...current, airTemperatureC: event.target.value }))} placeholder="24.0" />
                      <span className="meas-unit">°C</span>
                    </div>
                  </div>
                  <div className="meas-field">
                    <label>Luftfeuchte</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={measurementForm.humidityPercent} onChange={(event) => setMeasurementForm((current) => ({ ...current, humidityPercent: event.target.value }))} placeholder="60" />
                      <span className="meas-unit">%</span>
                    </div>
                  </div>
                </div>
                <div className="field" style={{ marginBottom: 14 }}>
                  <label>Notiz</label>
                  <textarea value={measurementForm.notes} onChange={(event) => setMeasurementForm((current) => ({ ...current, notes: event.target.value }))} rows={2} placeholder="Zustand, Auffaelligkeiten, Korrekturen..." />
                </div>
                <button className="btn btn-primary" disabled={saving === 'measurement'}>{saving === 'measurement' ? 'Speichert...' : 'Messung speichern'}</button>
              </form>
            </div>

            <div className="section-label">AutoMeasurement</div>
            <div className="card" style={{ marginBottom: 14 }}>
              <div className="card-header">
                <span className="card-title">Konfigurationen</span>
                <span className="text-muted" style={{ fontSize: 13 }}>{autoConfigs.length} aktiv</span>
              </div>
              <form onSubmit={handleAutoConfigSubmit} style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
                <div className="meas-fields" style={{ marginBottom: 14 }}>
                  <div className="meas-field">
                    <label>Name</label>
                    <input className="meas-input" value={autoConfigForm.name} onChange={(event) => setAutoConfigForm((current) => ({ ...current, name: event.target.value }))} placeholder="z. B. Licht an" />
                  </div>
                  <div className="meas-field">
                    <label>Status</label>
                    <select className="meas-input" value={autoConfigForm.status} onChange={(event) => setAutoConfigForm((current) => ({ ...current, status: event.target.value as AutoMeasurementStatus }))}>
                      {autoMeasurementStatuses.map((status) => <option key={status} value={status}>{status}</option>)}
                    </select>
                  </div>
                  <div className="meas-field">
                    <label>Trigger</label>
                    <select className="meas-input" value={autoConfigForm.triggerKind} onChange={(event) => setAutoConfigForm((current) => ({ ...current, triggerKind: event.target.value as AutoMeasurementTriggerKind }))}>
                      {autoMeasurementTriggerKinds.map((trigger) => <option key={trigger} value={trigger}>{trigger}</option>)}
                    </select>
                  </div>
                  <div className="meas-field">
                    <label>Fenster</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={autoConfigForm.windowMinutes} onChange={(event) => setAutoConfigForm((current) => ({ ...current, windowMinutes: event.target.value }))} />
                      <span className="meas-unit">min</span>
                    </div>
                  </div>
                  <div className="meas-field">
                    <label>Delay</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={autoConfigForm.delayMinutes} onChange={(event) => setAutoConfigForm((current) => ({ ...current, delayMinutes: event.target.value }))} placeholder="optional" />
                      <span className="meas-unit">min</span>
                    </div>
                  </div>
                </div>
                <button className="btn btn-primary" disabled={saving === 'auto-config'}>{saving === 'auto-config' ? 'Speichert...' : 'Config anlegen'}</button>
              </form>

              {autoLoading ? (
                <div className="empty-hint">Lade AutoMeasurement-Konfigurationen...</div>
              ) : autoConfigs.length === 0 ? (
                <div className="empty-hint">Noch keine AutoMeasurement-Konfigurationen.</div>
              ) : (
                autoConfigs.map((config) => {
                  const drafts = mappingDraftsByConfigId[config.id] ?? []
                  const savedMappingCount = autoMappingsByConfigId[config.id]?.length ?? 0
                  return (
                    <div key={config.id} style={{ padding: '14px 20px', borderTop: '1px solid var(--border)', display: 'grid', gap: 12 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
                        <div>
                          <div className="tl-title">{config.name}</div>
                          <div className="tl-sub">{config.triggerKind} - {config.windowMinutes} min Fenster{config.delayMinutes !== null ? ` - ${config.delayMinutes} min Delay` : ''}</div>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                          <span className={`badge ${config.status === 'Enabled' ? 'badge-ok' : 'badge-neutral'}`}>{config.status}</span>
                          <span className="text-muted" style={{ fontSize: 13 }}>{savedMappingCount} Mappings</span>
                        </div>
                      </div>

                      <div style={{ display: 'grid', gap: 8 }}>
                        {drafts.length === 0 ? (
                          <div className="empty-hint" style={{ padding: 0 }}>Keine Mappings.</div>
                        ) : (
                          drafts.map((mapping, index) => (
                            <div key={`${config.id}-${index}`} className="meas-fields" style={{ alignItems: 'end' }}>
                              <div className="meas-field">
                                <label>Feld</label>
                                <select className="meas-input" value={mapping.measurementField} onChange={(event) => updateMappingDraft(config.id, index, { measurementField: event.target.value as AutoMeasurementField })}>
                                  {autoMeasurementFields.map((field) => <option key={field} value={field}>{field}</option>)}
                                </select>
                              </div>
                              <div className="meas-field">
                                <label>MetricKey</label>
                                <input className="meas-input" value={mapping.metricKey} onChange={(event) => updateMappingDraft(config.id, index, { metricKey: event.target.value })} />
                              </div>
                              <div className="meas-field">
                                <label>Aggregation</label>
                                <select className="meas-input" value={mapping.aggregation} onChange={(event) => updateMappingDraft(config.id, index, { aggregation: event.target.value as AutoMeasurementAggregation })}>
                                  {autoMeasurementAggregations.map((aggregation) => <option key={aggregation} value={aggregation}>{aggregation}</option>)}
                                </select>
                              </div>
                              <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, color: 'var(--muted)', minHeight: 40 }}>
                                <input type="checkbox" checked={mapping.isRequired} onChange={(event) => updateMappingDraft(config.id, index, { isRequired: event.target.checked })} />
                                Pflicht
                              </label>
                              <button type="button" className="btn" onClick={() => removeMappingDraft(config.id, index)}>Entfernen</button>
                            </div>
                          ))
                        )}
                      </div>

                      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                        <button type="button" className="btn" onClick={() => addMappingDraft(config.id)}>Mapping hinzufuegen</button>
                        <button type="button" className="btn btn-primary" disabled={saving === `auto-mappings-${config.id}`} onClick={() => void saveMappingDrafts(config.id)}>
                          {saving === `auto-mappings-${config.id}` ? 'Speichert...' : 'Mappings speichern'}
                        </button>
                      </div>
                    </div>
                  )
                })
              )}
            </div>
          </div>

          <div className="side-panel">
            <div className="panel-card">
              <div className="panel-card-header">
                <span className="panel-card-title">Offene Tasks</span>
                <span className="panel-card-count">{openTasks.length}</span>
              </div>
              {openTasks.length === 0 ? (
                <div style={{ padding: '14px', fontSize: '12px', color: 'var(--faint)' }}>Keine offenen Tasks.</div>
              ) : (
                openTasks.map((task) => (
                  <div key={task.id} className="task-item">
                    <button
                      type="button"
                      className="task-check"
                      disabled={saving === `task-status-${task.id}`}
                      onClick={() => void updateTaskStatus(task.id, 'Done')}
                    />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="task-title">{task.title}</div>
                      {task.dueAtUtc && <div className="task-sub">faellig {formatDate(task.dueAtUtc)}</div>}
                    </div>
                  </div>
                ))
              )}
              {closedTasks.length > 0 && (
                <div style={{ borderTop: '1px solid var(--border)', padding: '8px 14px' }}>
                  {closedTasks.slice(0, 5).map((task) => (
                    <div key={task.id} className="task-item" style={{ opacity: 0.5 }}>
                      <button type="button" className="task-check done" disabled={saving === `task-status-${task.id}`} onClick={() => void updateTaskStatus(task.id, 'Open')} />
                      <div className="task-title" style={{ textDecoration: 'line-through' }}>{task.title}</div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="panel-card">
              <div className="panel-card-header"><span className="panel-card-title">Grow-Info</span></div>
              <div style={{ padding: '12px 14px', fontSize: 13, display: 'grid', gap: 8 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Start</span><span>{formatDate(grow.startDate)}</span></div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Medium</span><span>{grow.mediumType}</span></div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Wasser</span><span>{grow.waterSource}</span></div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Licht</span><span>{grow.light ?? '—'}</span></div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Reservoir</span><span>{grow.reservoirSize ?? '—'}</span></div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Naehrstoffe</span><span>{grow.nutrients ?? '—'}</span></div>
              </div>
            </div>

            <div className="panel-card">
              <div className="panel-card-header"><span className="panel-card-title">Journal-Eintrag</span></div>
              <form onSubmit={handleJournalSubmit} style={{ padding: '12px 14px', display: 'grid', gap: 10 }}>
                <div className="field">
                  <label>Titel</label>
                  <input value={journalForm.title} onChange={(event) => setJournalForm((current) => ({ ...current, title: event.target.value }))} placeholder="Heute deutlich mehr Durst" />
                </div>
                <div className="field">
                  <label>Typ</label>
                  <select value={journalForm.entryType} onChange={(event) => setJournalForm((current) => ({ ...current, entryType: event.target.value }))}>
                    <option>Observation</option><option>Action</option><option>Problem</option><option>Solution</option><option>Training</option><option>Feeding</option><option>ReservoirChange</option>
                  </select>
                </div>
                <div className="field">
                  <label>Eintrag</label>
                  <textarea value={journalForm.body} onChange={(event) => setJournalForm((current) => ({ ...current, body: event.target.value }))} rows={3} placeholder="Was ist passiert?" />
                </div>
                <button className="btn btn-primary" disabled={saving === 'journal'}>{saving === 'journal' ? 'Speichert...' : 'Journal speichern'}</button>
              </form>
            </div>

            <div className="panel-card">
              <div className="panel-card-header"><span className="panel-card-title">Task anlegen</span></div>
              <form onSubmit={handleTaskSubmit} style={{ padding: '12px 14px', display: 'grid', gap: 10 }}>
                <div className="field">
                  <label>Titel</label>
                  <input value={taskForm.title} onChange={(event) => setTaskForm((current) => ({ ...current, title: event.target.value }))} placeholder="z. B. EC nach Addback pruefen" />
                </div>
                <div className="field">
                  <label>Prioritaet</label>
                  <select value={taskForm.priority} onChange={(event) => setTaskForm((current) => ({ ...current, priority: event.target.value }))}>
                    <option>Low</option><option>Normal</option><option>High</option><option>Critical</option>
                  </select>
                </div>
                <div className="field">
                  <label>Faellig</label>
                  <input type="datetime-local" value={taskForm.dueAtLocal} onChange={(event) => setTaskForm((current) => ({ ...current, dueAtLocal: event.target.value }))} />
                </div>
                <button className="btn btn-primary" disabled={saving === 'task'}>{saving === 'task' ? 'Speichert...' : 'Task speichern'}</button>
              </form>
            </div>

            <div className="panel-card">
              <div className="panel-card-header">
                <span className="panel-card-title">Fotos</span>
                <span className="panel-card-count">{photoLoading ? '...' : photos.length}</span>
              </div>
              <form onSubmit={handlePhotoSubmit} style={{ padding: '12px 14px', display: 'grid', gap: 10 }}>
                <div className="field">
                  <label>Messung</label>
                  <select value={selectedMeasurementId ?? ''} onChange={(event) => void handleMeasurementSelection(event.target.value ? parseInt(event.target.value, 10) : null)} disabled={bundle.measurements.length === 0}>
                    {bundle.measurements.length === 0 ? <option value="">Keine Messungen</option> : null}
                    {bundle.measurements.map((measurement) => (
                      <option key={measurement.id} value={measurement.id}>#{measurement.id} · {measurement.stage} · {formatDateTime(measurement.takenAt)}</option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label>Tag</label>
                  <select value={photoForm.photoTag} onChange={(event) => setPhotoForm((current) => ({ ...current, photoTag: event.target.value as PhotoTag }))}>
                    {photoTags.map((tag) => <option key={tag} value={tag}>{tag}</option>)}
                  </select>
                </div>
                <div className="field">
                  <label>Caption</label>
                  <input value={photoForm.photoCaption} onChange={(event) => setPhotoForm((current) => ({ ...current, photoCaption: event.target.value }))} />
                </div>
                <div className="field">
                  <label>Dateien</label>
                  <input type="file" accept="image/png,image/jpeg,image/webp" multiple onChange={(event) => setPhotoForm((current) => ({ ...current, files: Array.from(event.target.files ?? []) }))} />
                </div>
                <button className="btn btn-primary" disabled={saving === 'photo' || bundle.measurements.length === 0}>{saving === 'photo' ? 'Laedt hoch...' : 'Fotos hochladen'}</button>
              </form>
              {photos.length > 0 && (
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, padding: '0 14px 14px' }}>
                  {photos.map((photo) => (
                    <div key={photo.id} style={{ borderRadius: 8, overflow: 'hidden', border: '1px solid var(--border)' }}>
                      <img src={photo.relativePath} alt={photo.caption ?? `Foto ${photo.id}`} loading="lazy" style={{ width: '100%', aspectRatio: '4/3', objectFit: 'cover', display: 'block' }} />
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  )
}

function toNullableNumber(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number(trimmed.replace(',', '.'))
  return Number.isNaN(parsed) ? null : parsed
}

function toNullableInteger(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number(trimmed)
  return Number.isInteger(parsed) ? parsed : null
}

export default GrowDetailPage
