import { ApiRequestError } from '../../api'
import type {
  AutoMeasurementAggregation,
  AutoMeasurementField,
  AutoMeasurementFieldMappingUpsertRequest,
  AutoMeasurementStatus,
  AutoMeasurementTriggerKind,
  GrowDetail,
  GrowDeviationDto,
  PhotoTag,
  ValueOrigin,
} from '../../types'
import { formatNumber, toLocalInputValue } from '../../utils'

export type GrowDetailSection = 'overview' | 'measurements' | 'diagnosis' | 'sops' | 'journal' | 'automation'

export const detailSections: Array<{ key: GrowDetailSection; label: string }> = [
  { key: 'overview', label: 'Überblick' },
  { key: 'measurements', label: 'Messungen' },
  { key: 'diagnosis', label: 'Diagnose' },
  { key: 'sops', label: 'SOPs' },
  { key: 'journal', label: 'Journal/Fotos/Tasks' },
  { key: 'automation', label: 'Automatisierung' },
]

export const photoTags: PhotoTag[] = ['Overview', 'Canopy', 'Leaf', 'Root', 'Training', 'Flower', 'Problem', 'Comparison', 'Other']

export const autoMeasurementFields: AutoMeasurementField[] = [
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

export const autoMeasurementAggregations: AutoMeasurementAggregation[] = ['Latest', 'Median', 'Average']
export const autoMeasurementTriggerKinds: AutoMeasurementTriggerKind[] = ['Manual', 'LightOnDelay', 'LightOffDelay']
export const autoMeasurementStatuses: AutoMeasurementStatus[] = ['Enabled', 'Disabled']

export const defaultMetricKeyByField: Record<AutoMeasurementField, string> = {
  AirTemperatureC: 'temperature',
  HumidityPercent: 'humidity',
  ReservoirPh: 'reservoir-ph',
  ReservoirEc: 'reservoir-ec',
  ReservoirWaterTempC: 'reservoir-temp',
  ReservoirLevelLiters: 'reservoir-level',
  ReservoirLevelCm: 'reservoir-level-cm',
  DissolvedOxygenMgL: 'dissolved-oxygen',
  OrpMv: 'orp',
  PpfdMol: 'ppfd',
  Co2Ppm: 'co2',
}

export const emptyMeasurementForm = () => ({
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

export type MeasurementFormState = ReturnType<typeof emptyMeasurementForm>

export const emptyTaskForm = () => ({
  title: '',
  dueAtLocal: '',
  priority: 'Normal',
  notes: '',
})

export type TaskFormState = ReturnType<typeof emptyTaskForm>

export const emptyJournalForm = () => ({
  title: '',
  body: '',
  entryType: 'Observation',
  source: 'Manual',
  occurredAtLocal: toLocalInputValue(),
})

export type JournalFormState = ReturnType<typeof emptyJournalForm>

export const emptyPhotoForm = () => ({
  photoCaption: '',
  photoTag: 'Overview' as PhotoTag,
  useAsReferenceShot: false,
  source: 'Manual' as ValueOrigin,
  files: [] as File[],
})

export type PhotoFormState = ReturnType<typeof emptyPhotoForm>

export const emptyAutoConfigForm = () => ({
  name: '',
  status: 'Enabled' as AutoMeasurementStatus,
  triggerKind: 'Manual' as AutoMeasurementTriggerKind,
  delayMinutes: '',
  windowMinutes: '20',
  captureSnapshot: false,
})

export type AutoConfigFormState = ReturnType<typeof emptyAutoConfigForm>

export const emptyMappingDraft = (): AutoMeasurementFieldMappingUpsertRequest => ({
  measurementField: 'AirTemperatureC',
  metricKey: defaultMetricKeyByField.AirTemperatureC,
  aggregation: 'Latest',
  isRequired: true,
})

export function toNullableNumber(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number(trimmed.replace(',', '.'))
  return Number.isNaN(parsed) ? null : parsed
}

export function toNullableInteger(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number(trimmed)
  return Number.isInteger(parsed) ? parsed : null
}

export function formatDeviationValue(value: number | null, unit: string | null): string {
  if (value == null) return '-'
  return `${formatNumber(value, 2)}${unit ? ` ${unit}` : ''}`
}

export function formatDeviationTarget(deviation: GrowDeviationDto): string | null {
  if (deviation.targetMin == null && deviation.targetMax == null) return null
  if (deviation.targetMin != null && deviation.targetMax != null) {
    return `${formatNumber(deviation.targetMin, 2)}-${formatNumber(deviation.targetMax, 2)}${deviation.unit ? ` ${deviation.unit}` : ''}`
  }
  if (deviation.targetMin != null) {
    return `>= ${formatNumber(deviation.targetMin, 2)}${deviation.unit ? ` ${deviation.unit}` : ''}`
  }
  return `<= ${formatNumber(deviation.targetMax, 2)}${deviation.unit ? ` ${deviation.unit}` : ''}`
}

export function formatGrowStatus(status: GrowDetail['status']) {
  return status === 'Running' ? 'aktiv'
    : status === 'Planning' ? 'geplant'
      : status === 'Completed' ? 'beendet'
        : status === 'Aborted' ? 'abgebrochen'
          : status
}

export function formatGrowHydroMedium(grow: GrowDetail) {
  if (grow.hydroSetupName) return grow.hydroSetupName
  if (grow.hydroStyle !== 'None') return grow.hydroStyle
  return grow.mediumDetail ?? grow.mediumType ?? 'Medium offen'
}

export function formatGrowRuntime(startDate: string | null) {
  if (!startDate) return '-'
  const start = new Date(startDate)
  if (Number.isNaN(start.getTime())) return '-'
  const days = Math.max(0, Math.floor((Date.now() - start.getTime()) / 86_400_000))
  return `${days} d`
}

export function isNotFound(caught: unknown) {
  return caught instanceof ApiRequestError && caught.status === 404
}
