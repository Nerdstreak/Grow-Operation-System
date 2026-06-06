import type { ApiError, DeviationMetric, DeviationSeverity, DeviationSource, GerminationMethod, GrowEntryPoint, GrowEnvironment, GrowStage, GrowStatus, GrowTaskStatus, HydroStyle, JournalEntryType, PhotoTag, PropagationMedium, SeedType, SopInstanceStatus, SopStartSource, SopStepInstanceStatus, StartMaterial, TaskPriority, TreatmentRecommendationConfidence, ValueOrigin, WaterSource } from './shared'

export interface DependencyItemDto {
  id: number
  name: string
  status: string | null
  type: string | null
}

export interface TentDependencySummaryDto {
  activeGrows: DependencyItemDto[]
  archivedGrows: DependencyItemDto[]
  hydroSetups: DependencyItemDto[]
  sensors: DependencyItemDto[]
  measurements: DependencyItemDto[]
  other: DependencyItemDto[]
}

export interface TentDependencyError extends ApiError {
  dependencies: TentDependencySummaryDto
}

export interface GrowSummary {
  id: number
  name: string
  strain: string | null
  breeder: string | null
  status: GrowStatus
  hydroStyle: HydroStyle
  environment: GrowEnvironment
  seedType: SeedType
  startMaterial: StartMaterial
  plantCount: number | null
  tentId: number | null
  systemId: number | null
  setupId: number | null
  tentName: string | null
  hydroSetupName: string | null
  startDate: string
  endDate: string | null
  flipDate: string | null
  germinatedAt: string | null
  rootedAt: string | null
  measurementCount: number
  latestPhotoPath: string | null
  latestStage: GrowStage | null
  latestReservoirPh: number | null
  latestReservoirEc: number | null
  latestMeasurementAt: string | null
}

export interface MeasurementDto {
  id: number
  growId: number
  takenAt: string
  stage: GrowStage
  source: ValueOrigin
  notes: string | null
  airTemperatureC: number | null
  humidityPercent: number | null
  heightCm: number | null
  waterAmountMl: number | null
  runoffAmountMl: number | null
  irrigationPh: number | null
  irrigationEc: number | null
  drainPh: number | null
  drainEc: number | null
  reservoirPh: number | null
  reservoirEc: number | null
  reservoirWaterTempC: number | null
  reservoirLevelCm: number | null
  reservoirLevelLiters: number | null
  dissolvedOxygenMgL: number | null
  orpMv: number | null
  topOffLiters: number | null
  addbackEc: number | null
  solutionChange: boolean
  ppfdMol: number | null
  co2Ppm: number | null
}

export interface GrowDeviationDto {
  growId: number
  growName: string
  stableKey: string
  metric: DeviationMetric
  actualValue: number | null
  targetMin: number | null
  targetMax: number | null
  unit: string | null
  severity: DeviationSeverity
  message: string
  recommendationHint: string | null
  symptomId: string | null
  sourceMeasurementIds: number[]
  recommendation: string
  consecutiveCount: number
  firstDetectedAtUtc: string | null
  lastDetectedAtUtc: string | null
  source: DeviationSource
}

export interface TreatmentRecommendationDto {
  stableKey: string
  deviationStableKey: string
  metric: DeviationMetric
  severity: DeviationSeverity
  symptomId: string | null
  treatmentId: string | null
  treatmentName: string | null
  sopId: string | null
  sopTitle: string | null
  confidence: TreatmentRecommendationConfidence
  reason: string
  safetyNotes: string[]
  sourceDocumentIds: string[]
  conflicts: string[]
  conflictTreatmentIds: string[]
  phaseAllowed: boolean | null
  hardwareRequirements: string[]
}

export interface GrowTreatmentRecommendationDto {
  growId: number
  recommendations: TreatmentRecommendationDto[]
}

export interface MeasurementUpsertPayload {
  takenAtLocal: string
  stage: GrowStage
  source: ValueOrigin
  notes: string | null
  airTemperatureC: number | null
  humidityPercent: number | null
  heightCm: number | null
  waterAmountMl: number | null
  runoffAmountMl: number | null
  irrigationPh: number | null
  irrigationEc: number | null
  drainPh: number | null
  drainEc: number | null
  reservoirPh: number | null
  reservoirEc: number | null
  reservoirWaterTempC: number | null
  reservoirLevelCm: number | null
  reservoirLevelLiters: number | null
  dissolvedOxygenMgL: number | null
  orpMv: number | null
  topOffLiters: number | null
  addbackEc: number | null
  solutionChange: boolean
  ppfdMol: number | null
  co2Ppm: number | null
}

export interface PhotoAssetDto {
  id: number
  growId: number
  measurementId: number | null
  relativePath: string
  caption: string | null
  tag: PhotoTag
  source: ValueOrigin
  isReferenceShot: boolean
  takenAtUtc: string
}

export interface GrowDetail {
  id: number
  systemId: number | null
  setupId: number | null
  name: string
  strain: string | null
  breeder: string | null
  status: GrowStatus
  mediumType: string
  feedingStyle: string
  hydroStyle: HydroStyle
  irrigationType: string
  waterSource: WaterSource
  environment: GrowEnvironment
  light: string | null
  containerSize: string | null
  reservoirSize: string | null
  mediumDetail: string | null
  irrigationStyle: string | null
  hasChiller: boolean
  seedType: SeedType
  startMaterial: StartMaterial
  germinationMethod: GerminationMethod | null
  propagationMedium: PropagationMedium | null
  cloneSource: string | null
  cloneIsRooted: boolean
  breederFlowerWeeksMin: number | null
  breederFlowerWeeksMax: number | null
  plantCount: number | null
  phenoNumber: number | null
  tentId: number | null
  tentName: string | null
  hydroSetupName: string | null
  entryPoint: GrowEntryPoint
  daysAlreadyInPhase: number | null
  autoflowerDaysSinceGermination: number | null
  startDate: string
  endDate: string | null
  flipDate: string | null
  germinatedAt: string | null
  rootedAt: string | null
  nutrients: string | null
  notes: string | null
  measurementCount: number
  latestPhotoPath: string | null
  latestMeasurement: MeasurementDto | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface GrowUpsertPayload {
  templateId: number | null
  name: string
  tentId: number | null
  systemId: number | null
  setupId?: number | null
  strain: string | null
  breeder: string | null
  seedType: SeedType
  startMaterial: StartMaterial
  germinationMethod: GerminationMethod | null
  cloneSource: string | null
  cloneIsRooted: boolean
  phenoNumber: number | null
  breederFlowerWeeksMin: number | null
  breederFlowerWeeksMax: number | null
  hydroStyle: HydroStyle
  plantCount: number | null
  reservoirSize: string | null
  containerSize: string | null
  propagationMedium: PropagationMedium | null
  light: string | null
  hasChiller: boolean
  waterSource: WaterSource
  nutrients: string | null
  startDate: string
  entryPoint: GrowEntryPoint
  daysAlreadyInPhase: number | null
  autoflowerDaysSinceGermination: number | null
  flipDate: string | null
  notes: string | null
  status: GrowStatus
  environment: GrowEnvironment
}

export interface GrowTaskDto {
  id: number
  growId: number
  growName: string | null
  title: string
  notes: string | null
  dueAtUtc: string | null
  priority: TaskPriority
  status: GrowTaskStatus
  createdAtUtc: string
  completedAtUtc: string | null
}

export interface SopInstanceDto {
  id: number
  growId: number
  sopId: string
  sopName: string
  sopType: string
  status: SopInstanceStatus
  source: SopStartSource
  sourceRecommendationKey: string | null
  treatmentRecommendationStableKey: string | null
  startedAtUtc: string
  completedAtUtc: string | null
  cancelledAtUtc: string | null
  dueAtUtc: string | null
  nextStepDueAtUtc: string | null
  recurrenceIntervalDays: number | null
  isRecurring: boolean
  notes: string | null
  createdAtUtc: string
  updatedAtUtc: string
  stepCount: number
}

export interface SopStepInstanceDto {
  id: number
  sopInstanceId: number
  stepId: string
  order: number
  title: string
  description: string | null
  stepType: string
  status: SopStepInstanceStatus
  waitMinutes: number | null
  subSopId: string | null
  expectedInputsJson: string | null
  photoRequired: boolean
  photoRecommended: boolean
  dueAtUtc: string | null
  availableAtUtc: string | null
  reminderTaskId: number | null
  startedAtUtc: string | null
  completedAtUtc: string | null
  skippedAtUtc: string | null
  notes: string | null
  measurementId: number | null
  journalEntryId: number | null
  photoAssetId: number | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface StartSopInstanceRequest {
  growId: number
  sopId: string
  source: SopStartSource
  sourceRecommendationKey: string | null
  treatmentRecommendationStableKey: string | null
  notes: string | null
}

export interface UpdateSopStepInstanceRequest {
  status: SopStepInstanceStatus
  notes: string | null
  measurementId: number | null
  journalEntryId: number | null
  photoAssetId: number | null
}

export interface JournalEntryDto {
  id: number
  growId: number
  measurementId: number | null
  title: string | null
  body: string | null
  entryType: JournalEntryType
  source: ValueOrigin
  occurredAtUtc: string
  createdAtUtc: string
}

export interface AddbackDefaultsDto {
  growId: number
  growName: string
  suggestedReservoirLiters: number | null
  suggestedEcIst: number | null
  suggestedEcZiel: number | null
  reservoirLiters: number | null
  ecIst: number | null
  ecZiel: number | null
  ecStock: number
}

export interface AddbackResultDto {
  needsAddback: boolean
  litersToAdd: number | null
  newReservoirVolume: number | null
  errorMessage: string | null
}


export type AddbackLogKind = 'Addback' | 'TopOff' | 'Correction'

export interface AddbackLogDto {
  id: number
  growId: number
  hydroSetupId: number | null
  kind: AddbackLogKind
  performedAtUtc: string
  reservoirLiters: number | null
  ecBefore: number | null
  ecTarget: number | null
  ecStock: number | null
  ecAfter: number | null
  phBefore: number | null
  phAfter: number | null
  litersAdded: number | null
  newReservoirVolumeLiters: number | null
  usedHydroSetupVolume: boolean
  notes: string | null
  createdAtUtc: string
}

export interface CreateAddbackLogRequest {
  kind: AddbackLogKind
  performedAtUtc: string | null
  reservoirLiters: number | null
  ecBefore: number | null
  ecTarget: number | null
  ecStock: number | null
  ecAfter: number | null
  phBefore: number | null
  phAfter: number | null
  litersAdded: number | null
  newReservoirVolumeLiters: number | null
  usedHydroSetupVolume: boolean | null
  notes: string | null
}


export interface HarvestDto {
  growId: number
  growName: string
  harvestedAtLocal: string
  wetWeightG: number | null
  dryWeightG: number | null
  dryDays: number | null
  yieldNotes: string | null
  rating: number | null
  flavorNotes: string | null
  effectNotes: string | null
  nugStructure: string | null
}

export interface GrowActionResultDto {
  grow: GrowDetail
  message: string
}
