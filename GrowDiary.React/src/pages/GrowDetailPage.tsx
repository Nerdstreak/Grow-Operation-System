import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type {
  AutoMeasurementAggregation,
  AutoMeasurementConfigDto,
  AutoMeasurementGrowStatusDto,
  AutoMeasurementField,
  AutoMeasurementFieldMappingDto,
  AutoMeasurementFieldMappingUpsertRequest,
  AutoMeasurementRunDto,
  AutoMeasurementStatus,
  AutoMeasurementTriggerKind,
  GrowDeviationDto,
  GrowTreatmentRecommendationDto,
  GrowActionResultDto,
  GrowDetail,
  GrowTaskDto,
  JournalEntryDto,
  MeasurementDto,
  PhotoAssetDto,
  PhotoTag,
  SopInstanceDto,
  SopStepInstanceDto,
  SopStepInstanceStatus,
  StartSopInstanceRequest,
  TreatmentRecommendationDto,
  UpdateSopStepInstanceRequest,
  ValueOrigin,
} from '../types'
import { formatDate, formatDateTime, formatNumber, toLocalInputValue } from '../utils'

interface DetailBundle {
  grow: GrowDetail | null
  measurements: MeasurementDto[]
  tasks: GrowTaskDto[]
  journal: JournalEntryDto[]
}

type GrowDetailSection = 'overview' | 'measurements' | 'diagnosis' | 'sops' | 'journal' | 'automation'

const detailSections: Array<{ key: GrowDetailSection; label: string }> = [
  { key: 'overview', label: 'Überblick' },
  { key: 'measurements', label: 'Messungen' },
  { key: 'diagnosis', label: 'Diagnose' },
  { key: 'sops', label: 'SOPs' },
  { key: 'journal', label: 'Journal/Fotos/Tasks' },
  { key: 'automation', label: 'Automatisierung' },
]

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
  const navigate = useNavigate()
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
  const [autoRunsByConfigId, setAutoRunsByConfigId] = useState<Record<number, AutoMeasurementRunDto[]>>({})
  const [autoStatus, setAutoStatus] = useState<AutoMeasurementGrowStatusDto | null>(null)
  const [autoStatusError, setAutoStatusError] = useState<string | null>(null)
  const [deviations, setDeviations] = useState<GrowDeviationDto[]>([])
  const [deviationError, setDeviationError] = useState<string | null>(null)
  const [treatmentRecommendations, setTreatmentRecommendations] = useState<GrowTreatmentRecommendationDto | null>(null)
  const [treatmentRecommendationError, setTreatmentRecommendationError] = useState<string | null>(null)
  const [sopInstances, setSopInstances] = useState<SopInstanceDto[]>([])
  const [sopStepsByInstanceId, setSopStepsByInstanceId] = useState<Record<number, SopStepInstanceDto[]>>({})
  const [sopStepNotesById, setSopStepNotesById] = useState<Record<number, string>>({})
  const [sopInstanceError, setSopInstanceError] = useState<string | null>(null)
  const [mappingDraftsByConfigId, setMappingDraftsByConfigId] = useState<Record<number, AutoMeasurementFieldMappingUpsertRequest[]>>({})
  const [autoConfigForm, setAutoConfigForm] = useState(emptyAutoConfigForm)
  const [autoLoading, setAutoLoading] = useState(false)
  const [activeSection, setActiveSection] = useState<GrowDetailSection>('overview')

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
      try {
        const status = await apiFetch<AutoMeasurementGrowStatusDto>(`/api/auto-measurements/grows/${growId}/status`, { signal })
        setAutoStatus(status)
        setAutoStatusError(null)
      } catch (caught) {
        if (signal?.aborted) return
        setAutoStatus(null)
        setAutoStatusError(caught instanceof ApiRequestError ? caught.message : 'AutoMeasurement-Status konnte nicht geladen werden.')
      }
      const detailEntries = await Promise.all(configs.map(async (config) => {
        const [mappings, runs] = await Promise.all([
          apiFetch<AutoMeasurementFieldMappingDto[]>(`/api/auto-measurements/configs/${config.id}/mappings`, { signal }),
          apiFetch<AutoMeasurementRunDto[]>(`/api/auto-measurements/configs/${config.id}/runs`, { signal }),
        ])
        return [config.id, mappings, runs] as const
      }))
      const mappingEntries = detailEntries.map(([configId, mappings]) => [configId, mappings] as const)
      const runEntries = detailEntries.map(([configId, , runs]) => [configId, runs] as const)
      const nextMappings = Object.fromEntries(mappingEntries)
      setAutoConfigs(configs)
      setAutoMappingsByConfigId(nextMappings)
      setAutoRunsByConfigId(Object.fromEntries(runEntries))
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

  const loadDeviations = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    try {
      const nextDeviations = await apiFetch<GrowDeviationDto[]>(`/api/grows/${growId}/deviations`, { signal })
      setDeviations(nextDeviations)
      setDeviationError(null)
    } catch (caught) {
      if (signal?.aborted) return
      setDeviations([])
      setDeviationError(caught instanceof ApiRequestError ? caught.message : 'Deviations konnten nicht geladen werden.')
    }
  }, [growId])

  const loadTreatmentRecommendations = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    try {
      const nextRecommendations = await apiFetch<GrowTreatmentRecommendationDto>(`/api/grows/${growId}/treatment-recommendations`, { signal })
      setTreatmentRecommendations(nextRecommendations)
      setTreatmentRecommendationError(null)
    } catch (caught) {
      if (signal?.aborted) return
      setTreatmentRecommendations(null)
      setTreatmentRecommendationError(caught instanceof ApiRequestError ? caught.message : 'Treatment-Empfehlungen konnten nicht geladen werden.')
    }
  }, [growId])

  const loadSopInstances = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    try {
      const nextInstances = await apiFetch<SopInstanceDto[]>(`/api/sop-instances?growId=${growId}`, { signal })
      const visibleInstances = nextInstances.filter((instance) => instance.status !== 'Cancelled')
      const stepEntries = await Promise.all(visibleInstances.map(async (instance) => {
        const steps = await apiFetch<SopStepInstanceDto[]>(`/api/sop-instances/${instance.id}/steps`, { signal })
        return [instance.id, steps] as const
      }))
      setSopInstances(visibleInstances)
      setSopStepsByInstanceId(Object.fromEntries(stepEntries))
      setSopStepNotesById(Object.fromEntries(stepEntries.flatMap(([, steps]) => steps.map((step) => [step.id, step.notes ?? ''] as const))))
      setSopInstanceError(null)
    } catch (caught) {
      if (signal?.aborted) return
      setSopInstances([])
      setSopStepsByInstanceId({})
      setSopStepNotesById({})
      setSopInstanceError(caught instanceof ApiRequestError ? caught.message : 'SOP-Instanzen konnten nicht geladen werden.')
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
      void loadDeviations(controller.signal)
      void loadTreatmentRecommendations(controller.signal)
      void loadSopInstances(controller.signal)
    }, 0)
    return () => {
      window.clearTimeout(handle)
      controller.abort()
    }
  }, [loadAutoMeasurements, loadBundle, loadDeviations, loadTreatmentRecommendations, loadSopInstances])

  const openTasks = useMemo(() => bundle.tasks.filter((task) => task.status === 'Open'), [bundle.tasks])
  const closedTasks = useMemo(() => bundle.tasks.filter((task) => task.status !== 'Open'), [bundle.tasks])
  const selectedMeasurement = useMemo(
    () => bundle.measurements.find((measurement) => measurement.id === selectedMeasurementId) ?? null,
    [bundle.measurements, selectedMeasurementId],
  )
  const autoStatusByConfigId = useMemo(
    () => Object.fromEntries((autoStatus?.configs ?? []).map((configStatus) => [configStatus.configId, configStatus] as const)),
    [autoStatus],
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
      await Promise.all([loadBundle(), loadDeviations(), loadTreatmentRecommendations()])
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
      setError('Bitte wähle eine Messung und mindestens ein Foto aus.')
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
      setError('Name und gültiges Zeitfenster sind erforderlich.')
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
      setError(caught instanceof Error ? caught.message : 'Task-Status konnte nicht geändert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function startRecommendedSop(recommendation: TreatmentRecommendationDto) {
    if (!growId || !recommendation.sopId) return
    const savingKey = `start-sop-${recommendation.stableKey}`
    setSaving(savingKey)
    setNotice(null)
    try {
      const payload: StartSopInstanceRequest = {
        growId: parseInt(growId, 10),
        sopId: recommendation.sopId,
        source: 'Recommendation',
        sourceRecommendationKey: recommendation.stableKey,
        treatmentRecommendationStableKey: recommendation.stableKey,
        notes: 'Gestartet aus Diagnoseempfehlung',
      }
      await apiFetch<SopInstanceDto>('/api/sop-instances/start', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
      setNotice('SOP gestartet.')
      setError(null)
      await loadSopInstances()
    } catch (caught) {
      if (caught instanceof ApiRequestError && caught.payload?.code === 'active_sop_exists') {
        setNotice('SOP ist bereits aktiv.')
        setError(null)
      } else {
        setError(caught instanceof ApiRequestError ? caught.message : 'SOP konnte nicht gestartet werden.')
      }
    } finally {
      setSaving(null)
    }
  }

  async function updateSopStep(step: SopStepInstanceDto, status: SopStepInstanceStatus) {
    const savingKey = `sop-step-${step.id}-${status}`
    setSaving(savingKey)
    setNotice(null)
    try {
      const payload: UpdateSopStepInstanceRequest = {
        status,
        notes: sopStepNotesById[step.id]?.trim() || null,
        measurementId: null,
        journalEntryId: null,
        photoAssetId: null,
      }
      await apiFetch<SopStepInstanceDto>(`/api/sop-instances/steps/${step.id}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
      setNotice('SOP-Step aktualisiert.')
      setError(null)
      await loadSopInstances()
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'SOP-Step konnte nicht aktualisiert werden.')
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
      setError(caught instanceof Error ? caught.message : 'Grow-Aktion konnte nicht ausgeführt werden.')
    } finally {
      setSaving(null)
    }
  }

  async function archiveGrow() {
    if (!growId || !bundle.grow) return
    const confirmed = window.confirm(`${bundle.grow.name} beenden und archivieren?`)
    if (!confirmed) return

    setSaving('grow-archive')
    setError(null)
    setNotice(null)
    try {
      await apiFetch<GrowDetail>(`/api/grows/${growId}/archive`, { method: 'POST' })
      setNotice('Grow beendet und archiviert.')
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Grow konnte nicht beendet werden.')
    } finally {
      setSaving(null)
    }
  }

  async function deleteGrow() {
    if (!growId || !bundle.grow) return
    const confirmed = window.confirm(`${bundle.grow.name} endgueltig loeschen?`)
    if (!confirmed) return

    setSaving('grow-delete')
    setError(null)
    setNotice(null)
    try {
      await apiFetch(`/api/grows/${growId}`, { method: 'DELETE' })
      navigate('/')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Grow konnte nicht geloescht werden.')
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
        <div className="topbar"><Link className="btn" to="/">Zurück</Link></div>
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
  const canArchiveGrow = grow.status === 'Planning' || grow.status === 'Running'

  return (
    <>
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to="/">Zurück</Link>
          <span className="topbar-title">{grow.name}</span>
        </div>
        <div className="topbar-right">
          <span className={`badge ${grow.status === 'Running' ? 'badge-ok' : grow.status === 'Planning' ? 'badge-warn' : 'badge-neutral'}`}>{grow.status}</span>
          <div className="grow-management-actions" data-audit="grow-management-actions">
            <Link className="btn btn-primary" to={`/grows/${grow.id}/setup`}>Bearbeiten</Link>
            {canArchiveGrow && (
              <button type="button" className="btn" disabled={saving === 'grow-archive'} onClick={() => void archiveGrow()}>
                {saving === 'grow-archive' ? 'Beendet...' : 'Beenden'}
              </button>
            )}
            <button type="button" className="btn" disabled={saving === 'grow-delete'} onClick={() => void deleteGrow()}>
              {saving === 'grow-delete' ? 'Loescht...' : 'Loeschen'}
            </button>
          </div>
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

        <div className="section-tabs detail-tabs" style={{ marginBottom: 18 }}>
          {detailSections.map((section) => (
            <button
              key={section.key}
              type="button"
              className={`btn ${activeSection === section.key ? 'btn-primary' : ''}`}
              onClick={() => setActiveSection(section.key)}
            >
              {section.label}
            </button>
          ))}
        </div>

        <div className="grow-hero" style={{ display: activeSection === 'overview' ? undefined : 'none' }}>
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
                {saving === 'action-germination' ? 'Bestätigt...' : 'Keimung bestätigen'}
              </button>
            )}
            {canConfirmRooting && (
              <button type="button" className="btn" disabled={saving === 'action-rooting'} onClick={() => void handleGrowAction('rooting')}>
                {saving === 'action-rooting' ? 'Bestätigt...' : 'Bewurzelung bestätigen'}
              </button>
            )}
            {canFlipToFlower && (
              <button type="button" className="btn" disabled={saving === 'action-flip'} onClick={() => void handleGrowAction('flip')}>
                {saving === 'action-flip' ? 'Trägt ein...' : 'Flip zu 12/12'}
              </button>
            )}
          </div>
        </div>

        <div className="section-label" style={{ display: activeSection === 'diagnosis' ? undefined : 'none' }}>Deviations</div>
        <div className="card" style={{ marginBottom: 14, display: activeSection === 'diagnosis' ? undefined : 'none' }}>
          <div className="card-header">
            <span className="card-title">Hydro-Abweichungen</span>
            <span className="text-muted" style={{ fontSize: 13 }}>{deviations.length}</span>
          </div>
          {deviationError ? (
            <div className="empty-hint" style={{ color: 'var(--red)' }}>{deviationError}</div>
          ) : deviations.length === 0 ? (
            <div className="empty-hint">Keine strukturierten Hydro-Deviations erkannt.</div>
          ) : (
            <div style={{ display: 'grid' }}>
              {deviations.map((deviation) => (
                <div key={deviation.stableKey} style={{ display: 'grid', gridTemplateColumns: '120px minmax(120px, 0.7fr) minmax(180px, 1fr) minmax(0, 2fr)', gap: 10, alignItems: 'center', padding: '12px 16px', borderTop: '1px solid var(--border)' }}>
                  <span className={`badge ${deviation.severity === 'Critical' ? 'badge-warn' : deviation.severity === 'Warning' ? 'badge-neutral' : 'badge-ok'}`}>{deviation.severity}</span>
                  <div>
                    <div className="tl-title">{deviation.metric}</div>
                    <div className="tl-sub">{deviation.source}</div>
                  </div>
                  <div className="tl-sub">
                    Ist {formatDeviationValue(deviation.actualValue, deviation.unit)}
                    {formatDeviationTarget(deviation) ? ` / Ziel ${formatDeviationTarget(deviation)}` : ''}
                  </div>
                  <div className="tl-sub">
                    {deviation.message}
                    <span> Folge {deviation.consecutiveCount}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="section-label" style={{ display: activeSection === 'diagnosis' ? undefined : 'none' }}>Treatment-Empfehlungen</div>
        <div className="card" style={{ marginBottom: 14, display: activeSection === 'diagnosis' ? undefined : 'none' }}>
          <div className="card-header">
            <span className="card-title">Knowledge-Vorschläge</span>
            <span className="text-muted" style={{ fontSize: 13 }}>{treatmentRecommendations?.recommendations.length ?? 0}</span>
          </div>
          {treatmentRecommendationError ? (
            <div className="empty-hint" style={{ color: 'var(--red)' }}>{treatmentRecommendationError}</div>
          ) : !treatmentRecommendations || treatmentRecommendations.recommendations.length === 0 ? (
            <div className="empty-hint">Keine Treatment- oder SOP-Empfehlungen für die aktuellen Deviations.</div>
          ) : (
            <div style={{ display: 'grid' }}>
              {treatmentRecommendations.recommendations.map((recommendation) => (
                <div key={recommendation.stableKey} style={{ display: 'grid', gridTemplateColumns: '120px minmax(180px, 1fr) minmax(0, 2fr)', gap: 10, alignItems: 'start', padding: '12px 16px', borderTop: '1px solid var(--border)' }}>
                  <div style={{ display: 'grid', gap: 6 }}>
                    <span className={`badge ${recommendation.confidence === 'High' ? 'badge-warn' : recommendation.confidence === 'Medium' ? 'badge-neutral' : 'badge-ok'}`}>{recommendation.confidence}</span>
                    <span className="tl-sub">{recommendation.severity}</span>
                  </div>
                  <div>
                    <div className="tl-title">{recommendation.treatmentName ?? recommendation.sopTitle ?? recommendation.metric}</div>
                    <div className="tl-sub">
                      {recommendation.treatmentId ?? recommendation.sopId ?? recommendation.symptomId ?? 'Diagnosehinweis'}
                    </div>
                  </div>
                  <div className="tl-sub" style={{ display: 'grid', gap: 5 }}>
                    <span>{recommendation.reason}</span>
                    {recommendation.safetyNotes.length > 0 && <span>Hinweise: {recommendation.safetyNotes.join(' | ')}</span>}
                    {recommendation.conflictTreatmentIds.length > 0 && <span>Konflikte: {recommendation.conflictTreatmentIds.join(', ')}</span>}
                    {recommendation.hardwareRequirements.length > 0 && <span>Hardware: {recommendation.hardwareRequirements.join(', ')}</span>}
                    {recommendation.sopId && (
                      <div>
                        <button type="button" className="btn" disabled={saving === `start-sop-${recommendation.stableKey}`} onClick={() => void startRecommendedSop(recommendation)}>
                          {saving === `start-sop-${recommendation.stableKey}` ? 'Startet...' : 'SOP starten'}
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="section-label" style={{ display: activeSection === 'sops' ? undefined : 'none' }}>SOPs</div>
        <div className="card" style={{ marginBottom: 14, display: activeSection === 'sops' ? undefined : 'none' }}>
          <div className="card-header">
            <span className="card-title">SOP-Instanzen</span>
            <span className="text-muted" style={{ fontSize: 13 }}>{sopInstances.length}</span>
          </div>
          {sopInstanceError ? (
            <div className="empty-hint" style={{ color: 'var(--red)' }}>{sopInstanceError}</div>
          ) : sopInstances.length === 0 ? (
            <div className="empty-hint">Keine SOP-Instanz.</div>
          ) : (
            <div style={{ display: 'grid' }}>
              {sopInstances.map((instance) => (
                <div key={instance.id} style={{ display: 'grid', gap: 10, padding: '12px 16px', borderTop: '1px solid var(--border)' }}>
                  <div style={{ display: 'grid', gridTemplateColumns: 'minmax(180px, 1fr) 120px 120px 1fr', gap: 10, alignItems: 'center' }}>
                  <div>
                    <div className="tl-title">{instance.sopName}</div>
                    <div className="tl-sub">{instance.sopId}</div>
                  </div>
                  <span className="badge badge-neutral">{instance.sopType}</span>
                  <span className={`badge ${instance.status === 'Completed' ? 'badge-ok' : 'badge-neutral'}`}>{instance.status}</span>
                  <div className="tl-sub">
                    {instance.stepCount} Steps &ndash; Start {formatDateTime(instance.startedAtUtc)}
                    {instance.isRecurring && <span className="badge badge-neutral" style={{ marginLeft: 8 }}>Recurring</span>}
                    {instance.dueAtUtc && <span style={{ marginLeft: 8 }}>Fällig: {formatDateTime(instance.dueAtUtc)}</span>}
                    {instance.nextStepDueAtUtc && instance.status === 'Active' && (
                      <span style={{ marginLeft: 8 }}>Nächster Step: {formatDateTime(instance.nextStepDueAtUtc)}</span>
                    )}
                  </div>
                  </div>
                  <div style={{ display: 'grid', gap: 8 }}>
                    {(sopStepsByInstanceId[instance.id] ?? []).map((step) => (
                      <div key={step.id} style={{ display: 'grid', gridTemplateColumns: '48px minmax(180px, 1fr) 120px 120px minmax(180px, 1fr) 240px', gap: 8, alignItems: 'start' }}>
                        <span className="tl-sub">#{step.order}</span>
                        <div>
                          <div className="tl-title">{step.title}</div>
                          <div className="tl-sub">{step.stepType}</div>
                          {step.dueAtUtc && (
                            <div className="tl-sub">Fällig: {formatDateTime(step.dueAtUtc)}</div>
                          )}
                          {step.availableAtUtc && !step.dueAtUtc && (
                            <div className="tl-sub">Verfügbar ab: {formatDateTime(step.availableAtUtc)}</div>
                          )}
                          {step.reminderTaskId && (
                            <div className="tl-sub" style={{ opacity: 0.6 }}>Task #{step.reminderTaskId}</div>
                          )}
                        </div>
                        <span className="badge badge-neutral">{step.status}</span>
                        <span className="tl-sub">{step.subSopId ? `SubSOP: ${step.subSopId}` : ''}</span>
                        <input
                          value={sopStepNotesById[step.id] ?? ''}
                          onChange={(event) => setSopStepNotesById((current) => ({ ...current, [step.id]: event.target.value }))}
                          placeholder="Notiz"
                          disabled={instance.status !== 'Active'}
                        />
                        {instance.status === 'Active' ? (
                          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                            {step.availableAtUtc && new Date(step.availableAtUtc) > new Date() && (
                              <span className="tl-sub" style={{ alignSelf: 'center', width: '100%' }}>
                                Verfügbar ab {formatDateTime(step.availableAtUtc)}
                              </span>
                            )}
                            <button type="button" className="btn btn-secondary" disabled={saving === `sop-step-${step.id}-InProgress`} onClick={() => void updateSopStep(step, 'InProgress')}>
                              Starten
                            </button>
                            <button type="button" className="btn" disabled={saving === `sop-step-${step.id}-Done`} onClick={() => void updateSopStep(step, 'Done')}>
                              Erledigt
                            </button>
                            <button type="button" className="btn btn-secondary" disabled={saving === `sop-step-${step.id}-Skipped`} onClick={() => void updateSopStep(step, 'Skipped')}>
                              Überspringen
                            </button>
                          </div>
                        ) : (
                          <span className="tl-sub">Keine Aktionen</span>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="detail-layout" style={{ display: activeSection === 'overview' || activeSection === 'diagnosis' || activeSection === 'sops' ? 'none' : undefined }}>
          <div>
            <div className="section-label" style={{ display: activeSection === 'measurements' ? undefined : 'none' }}>Messungen</div>
            <div className="card" style={{ marginBottom: 14, display: activeSection === 'measurements' ? undefined : 'none' }}>
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

            <div className="section-label" style={{ display: activeSection === 'journal' ? undefined : 'none' }}>Journal</div>
            <div className="card" style={{ marginBottom: 14, display: activeSection === 'journal' ? undefined : 'none' }}>
              <div className="card-header">
                <span className="card-title">Einträge</span>
                <span className="text-muted" style={{ fontSize: 13 }}>{bundle.journal.length}</span>
              </div>
              {bundle.journal.length === 0 ? (
                <div className="empty-hint">Noch keine Journal-Einträge.</div>
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

            <div className="section-label" style={{ display: activeSection === 'measurements' ? undefined : 'none' }}>Neue Messung</div>
            <div className="card" style={{ marginBottom: 14, display: activeSection === 'measurements' ? undefined : 'none' }}>
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
                  <textarea value={measurementForm.notes} onChange={(event) => setMeasurementForm((current) => ({ ...current, notes: event.target.value }))} rows={2} placeholder="Zustand, Auffälligkeiten, Korrekturen..." />
                </div>
                <button className="btn btn-primary" disabled={saving === 'measurement'}>{saving === 'measurement' ? 'Speichert...' : 'Messung speichern'}</button>
              </form>
            </div>

            <div className="section-label" style={{ display: activeSection === 'automation' ? undefined : 'none' }}>AutoMeasurement</div>
            <div className="card" style={{ marginBottom: 14, display: activeSection === 'automation' ? undefined : 'none' }}>
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
              {autoStatusError && (
                <div className="empty-hint" style={{ borderBottom: '1px solid var(--border)' }}>{autoStatusError}</div>
              )}

              {autoLoading ? (
                <div className="empty-hint">Lade AutoMeasurement-Konfigurationen...</div>
              ) : autoConfigs.length === 0 ? (
                <div className="empty-hint">Noch keine AutoMeasurement-Konfigurationen.</div>
              ) : (
                autoConfigs.map((config) => {
                  const drafts = mappingDraftsByConfigId[config.id] ?? []
                  const savedMappingCount = autoMappingsByConfigId[config.id]?.length ?? 0
                  const runs = autoRunsByConfigId[config.id] ?? []
                  const status = autoStatusByConfigId[config.id]
                  const mappingCount = status?.mappingCount ?? savedMappingCount
                  const requiredMappingCount = status?.requiredMappingCount ?? (autoMappingsByConfigId[config.id]?.filter((mapping) => mapping.isRequired).length ?? 0)
                  return (
                    <div key={config.id} style={{ padding: '14px 20px', borderTop: '1px solid var(--border)', display: 'grid', gap: 12 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
                        <div>
                          <div className="tl-title">{config.name}</div>
                          <div className="tl-sub">{config.triggerKind} - {config.windowMinutes} min Fenster{config.delayMinutes != null ? ` - ${config.delayMinutes} min Delay` : ''}</div>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                          <span className={`badge ${config.status === 'Enabled' ? 'badge-ok' : 'badge-neutral'}`}>{config.status}</span>
                          <span className="text-muted" style={{ fontSize: 13 }}>{mappingCount} Mappings / {requiredMappingCount} Pflicht</span>
                        </div>
                      </div>

                      <div style={{ display: 'grid', gap: 6, fontSize: 13, color: 'var(--muted)' }}>
                        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                          <span>Runs: Created {status?.createdRunCount ?? 0}</span>
                          <span>Skipped {status?.skippedRunCount ?? 0}</span>
                          <span>Failed {status?.failedRunCount ?? 0}</span>
                        </div>
                        <div>
                          Letzter Run:{' '}
                          {status?.lastRunStatus ? (
                            <>
                              <span className={`badge ${status.lastRunStatus === 'Created' ? 'badge-ok' : status.lastRunStatus === 'Failed' ? 'badge-warn' : 'badge-neutral'}`}>{status.lastRunStatus}</span>
                              <span> {status.lastRunScheduledForUtc ? formatDateTime(status.lastRunScheduledForUtc) : '-'} </span>
                              <span>{status.lastRunMeasurementId ? `M#${status.lastRunMeasurementId}` : '-'}</span>
                            </>
                          ) : (
                            <span>noch keiner</span>
                          )}
                        </div>
                        {status?.lastRunErrorMessage && <div>{status.lastRunErrorMessage}</div>}
                        <div>
                          Letzte relevante LightTransition:{' '}
                          {status?.latestRelevantLightTransitionKind && status.latestRelevantLightTransitionAtUtc
                            ? `${status.latestRelevantLightTransitionKind} ${formatDateTime(status.latestRelevantLightTransitionAtUtc)}`
                            : '-'}
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
                              <label className="checkbox-row" style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, color: 'var(--muted)', minHeight: 40 }}>
                                <input type="checkbox" checked={mapping.isRequired} onChange={(event) => updateMappingDraft(config.id, index, { isRequired: event.target.checked })} />
                                Pflicht
                              </label>
                              <button type="button" className="btn" onClick={() => removeMappingDraft(config.id, index)}>Entfernen</button>
                            </div>
                          ))
                        )}
                      </div>

                      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                        <button type="button" className="btn" onClick={() => addMappingDraft(config.id)}>Mapping hinzufügen</button>
                        <button type="button" className="btn btn-primary" disabled={saving === `auto-mappings-${config.id}`} onClick={() => void saveMappingDrafts(config.id)}>
                          {saving === `auto-mappings-${config.id}` ? 'Speichert...' : 'Mappings speichern'}
                        </button>
                      </div>

                      <div style={{ display: 'grid', gap: 6 }}>
                        <div className="tl-sub">Letzte Runs</div>
                        {runs.length === 0 ? (
                          <div style={{ fontSize: 13, color: 'var(--faint)' }}>Noch keine Runs.</div>
                        ) : (
                          runs.slice(0, 5).map((run) => (
                            <div key={run.id} style={{ display: 'grid', gridTemplateColumns: '110px minmax(160px, 1fr) 110px minmax(0, 1.3fr)', gap: 8, fontSize: 13, alignItems: 'center' }}>
                              <span className={`badge ${run.status === 'Created' ? 'badge-ok' : run.status === 'Failed' ? 'badge-warn' : 'badge-neutral'}`}>{run.status}</span>
                              <span className="text-muted">{formatDateTime(run.scheduledForUtc)}</span>
                              <span className="text-muted">{run.measurementId ? `M#${run.measurementId}` : '-'}</span>
                              <span className="text-muted" style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{run.errorMessage ?? ''}</span>
                            </div>
                          ))
                        )}
                      </div>
                    </div>
                  )
                })
              )}
            </div>
          </div>

          <div className="side-panel" style={{ display: activeSection === 'journal' ? undefined : 'none' }}>
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
                      {task.dueAtUtc && <div className="task-sub">fällig {formatDate(task.dueAtUtc)}</div>}
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
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Nährstoffe</span><span>{grow.nutrients ?? '—'}</span></div>
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
                  <input value={taskForm.title} onChange={(event) => setTaskForm((current) => ({ ...current, title: event.target.value }))} placeholder="z. B. EC nach Addback prüfen" />
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
                <button className="btn btn-primary" disabled={saving === 'photo' || bundle.measurements.length === 0}>{saving === 'photo' ? 'Lädt hoch...' : 'Fotos hochladen'}</button>
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

function formatDeviationValue(value: number | null, unit: string | null): string {
  if (value == null) return '-'
  return `${formatNumber(value, 2)}${unit ? ` ${unit}` : ''}`
}

function formatDeviationTarget(deviation: GrowDeviationDto): string | null {
  if (deviation.targetMin == null && deviation.targetMax == null) return null
  if (deviation.targetMin != null && deviation.targetMax != null) {
    return `${formatNumber(deviation.targetMin, 2)}-${formatNumber(deviation.targetMax, 2)}${deviation.unit ? ` ${deviation.unit}` : ''}`
  }
  if (deviation.targetMin != null) {
    return `>= ${formatNumber(deviation.targetMin, 2)}${deviation.unit ? ` ${deviation.unit}` : ''}`
  }
  return `<= ${formatNumber(deviation.targetMax, 2)}${deviation.unit ? ` ${deviation.unit}` : ''}`
}

export default GrowDetailPage
