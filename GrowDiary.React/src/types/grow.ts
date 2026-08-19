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
  /** Wo der Lauf einstieg — der Zeitstrahl der Liste braucht es wie der im Detail. */
  entryPoint?: GrowEntryPoint
  daysAlreadyInPhase?: number | null
  plantCount: number | null
  tentId: number | null
  systemId: number | null
  setupId: number | null
  tentName: string | null
  hydroSetupName: string | null
  startDate: string
  endDate: string | null
  flipDate: string | null
  /** Geplante Veg-Dauer in Tagen ab Bewurzelung (ohne die: ab Start). */
  plannedVegDays: number | null
  setpointProfileId?: string | null
  /** Verweis in die Sorten-Bibliothek; null = nur freier Text. */
  strainId: number | null
  breederFlowerWeeksMin: number | null
  breederFlowerWeeksMax: number | null
  germinatedAt: string | null
  rootedAt: string | null
  vegStartedAt: string | null
  finishStartedAt: string | null
  /** Die Phase von heute, aus dem Resolver — eine Quelle für alle Knöpfe. */
  currentStage: string
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
  /** Luftstrom auf Blattniveau in m/min — RDWC 90–120, sonst 60–90. */
  airflowAtLeafMPerMin: number | null
  /** „Weak" | „Moderate" | „Strong" — Stufen, weil die Quelle keinen Durchsatz nennt. */
  waterFlow: string | null
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
  /** Luftstrom auf Blattniveau in m/min — RDWC 90–120, sonst 60–90. */
  airflowAtLeafMPerMin: number | null
  /** „Weak" | „Moderate" | „Strong" — Stufen, weil die Quelle keinen Durchsatz nennt. */
  waterFlow: string | null
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
  /** Welches Symptom auf dem Bild zu sehen ist — Schlüssel aus der Wissensbasis. */
  symptomId?: string | null
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
  plannedVegDays: number | null
  setpointProfileId?: string | null
  strainId: number | null
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
  vegStartedAt: string | null
  finishStartedAt: string | null
  currentStage: string
  nutrients: string | null
  /* Beide optional: das Backend laesst null-Felder im JSON ganz weg. */
  feedProgramId?: string | null
  useFeedChartTargets?: boolean
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
  plannedVegDays: number | null
  setpointProfileId?: string | null
  strainId: number | null
  hydroStyle: HydroStyle
  plantCount: number | null
  reservoirSize: string | null
  containerSize: string | null
  propagationMedium: PropagationMedium | null
  light: string | null
  hasChiller: boolean
  waterSource: WaterSource
  nutrients: string | null
  /* Programm-Id ins Wissen; ohne sie gibt es keinen Mischplan. */
  feedProgramId?: string | null
  /* Nicht mitgeschickt heisst im Backend: gespeicherten Schalter behalten. */
  useFeedChartTargets?: boolean
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
  /* Womit aufgefuellt wurde. Nicht mitgeschickt = Backend erschliesst es aus
     dem Grow und dem Wasserprofil. */
  waterUsed?: WaterSource | null
  waterEcMsCm?: number | null
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
  /** Einzelgewichte je Pflanze als JSON; die Summe steht in wetWeightG/dryWeightG. */
  plantWeightsJson: string | null
}

export interface GrowActionResultDto {
  grow: GrowDetail
  message: string
}

/**
 * Wie ein Messwert zu seinem Ziel steht.
 *
 * Kommt aus `MeasurementAssessmentService` im Backend — bewusst nicht im
 * Browser gerechnet: die Profil-Kette, die Wissensbasis und der Phasen-Rechner
 * liegen dort. Ein Nachbau wäre die zweite Wahrheit, und genau die ist zwischen
 * Diagnose und Live-Kachel schon einmal entstanden.
 */
export type AssessmentVerdict = 'InTarget' | 'Below' | 'Above' | 'NoTarget' | 'Impossible'

export interface MetricAssessmentDto {
  metric: string
  label: string
  value: number
  unit: string
  targetMin: number | null
  targetMax: number | null
  verdict: AssessmentVerdict
  /** Klartext; bei `NoTarget` steht hier der Grund, warum nicht geprüft wurde. */
  note: string
}

export interface MeasurementAssessmentDto {
  measurementId: number
  takenAt: string
  storedStage: GrowStage
  computedStage: GrowStage | null
  source: ValueOrigin
  /** Aus der Bilanz genommen — mit Grund, aber nicht versteckt. */
  excluded: boolean
  excludedReason: string | null
  metrics: MetricAssessmentDto[]
}

export interface MeasurementAssessmentReportDto {
  measurementCount: number
  excludedCount: number
  checkedValueCount: number
  inTargetCount: number
  offTargetCount: number
  /** Werte, die es physikalisch nicht geben kann — 9000 °C, EC 99999. */
  impossibleCount: number
  profileId: string
  profileLabel: string
  measurements: MeasurementAssessmentDto[]
}
