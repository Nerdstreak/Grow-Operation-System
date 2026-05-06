export type GrowStatus = 'Planning' | 'Running' | 'Completed' | 'Aborted'
export type GrowStage = 'Seedling' | 'Clone' | 'Veg' | 'Transition' | 'Flower' | 'Finish' | 'Dry' | 'Cure'
export type ValueOrigin = 'Manual' | 'HomeAssistant' | 'Imported' | 'Derived'
export type TaskPriority = 'Low' | 'Normal' | 'High' | 'Critical'
export type GrowTaskStatus = 'Open' | 'Done' | 'Skipped'
export type HydroStyle = 'None' | 'DWC' | 'RDWC' | 'NFT' | 'Aeroponic' | 'Other'
export type GrowEnvironment = 'Indoor' | 'Outdoor' | 'Greenhouse'
export type SeedType = 'Feminized' | 'Autoflower' | 'Regular'
export type StartMaterial = 'Seed' | 'Clone'
export type JournalEntryType = 'Note' | 'Observation' | 'Action' | 'Problem' | 'Solution' | 'Training' | 'Transplant' | 'Feeding' | 'ReservoirChange' | 'GerminationConfirmed' | 'CloneRooted' | 'FlipToFlower'
export type WaterSource = 'Tap' | 'RO' | 'Mixed'
export type GrowEntryPoint = 'Germination' | 'Seedling' | 'Veg' | 'Flower' | 'Flush'
export type GerminationMethod = 'PaperTowel' | 'Rockwool' | 'RapidRooter' | 'DirectInSystem'
export type PropagationMedium = 'Rockwool' | 'Hydroton' | 'RapidRooter' | 'Neoprene'
export type PhotoTag = 'Overview' | 'Canopy' | 'Leaf' | 'Root' | 'Training' | 'Flower' | 'Problem' | 'Comparison' | 'Other'
export type SetupType = 'Production' | 'Mother' | 'Quarantine'
export type SetupStatus = 'Planning' | 'Active' | 'Archived'
export type MotherHealthStatus = 'Stable' | 'Watch' | 'Critical'
export type QuarantineResult = 'Pending' | 'Cleared' | 'Rejected'
export type PlantRole = 'Production' | 'Mother' | 'Clone' | 'Quarantine'
export type PlantStatus = 'Planned' | 'Active' | 'Archived' | 'Culled' | 'Harvested'
export type StrainDominance = 'Unknown' | 'Indica' | 'Sativa' | 'Hybrid'
export type AutoMeasurementStatus = 'Enabled' | 'Disabled'
export type AutoMeasurementAggregation = 'Latest' | 'Median' | 'Average'
export type AutoMeasurementField =
  | 'AirTemperatureC'
  | 'HumidityPercent'
  | 'ReservoirPh'
  | 'ReservoirEc'
  | 'ReservoirWaterTempC'
  | 'ReservoirLevelLiters'
  | 'ReservoirLevelCm'
  | 'DissolvedOxygenMgL'
  | 'OrpMv'
  | 'PpfdMol'
  | 'Co2Ppm'
export type AutoMeasurementTriggerKind = 'Manual' | 'LightOnDelay' | 'LightOffDelay'
export type AutoMeasurementRunStatus = 'Pending' | 'Created' | 'Skipped' | 'Failed'

export interface ApiError {
  code: string
  message: string
  fieldErrors?: Record<string, string[]>
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
  setupId: number | null
  tentName: string | null
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

export interface HomeAssistantSettingsDto {
  baseUrl: string | null
  accessToken: string | null
  enabled: boolean
}

export type TentType = 'Production' | 'Mother' | 'Quarantine' | 'Propagation' | 'MultiPurpose'
export interface SetupDto {
  id: number
  tentId: number
  name: string
  setupType: SetupType
  status: SetupStatus
  notes: string | null
  cloneCounterTotal: number | null
  lastCloneCutAt: string | null
  motherHealthStatus: MotherHealthStatus | null
  quarantineStartedAt: string | null
  quarantinePlannedEndAt: string | null
  quarantineResult: QuarantineResult | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateSetupRequest {
  tentId: number
  name: string
  setupType: SetupType
  notes?: string | null
  cloneCounterTotal?: number | null
  lastCloneCutAt?: string | null
  motherHealthStatus?: MotherHealthStatus | null
  quarantineStartedAt?: string | null
  quarantinePlannedEndAt?: string | null
  quarantineResult?: QuarantineResult | null
}

export interface UpdateSetupRequest {
  name: string
  status: SetupStatus
  notes?: string | null
  cloneCounterTotal?: number | null
  lastCloneCutAt?: string | null
  motherHealthStatus?: MotherHealthStatus | null
  quarantineStartedAt?: string | null
  quarantinePlannedEndAt?: string | null
  quarantineResult?: QuarantineResult | null
}

export interface StrainDto {
  id: number
  name: string
  breeder: string | null
  dominance: StrainDominance
  flowerWeeksMin: number | null
  flowerWeeksMax: number | null
  notes: string | null
  nutrientDemandFactor: number | null
  stretchFactor: number | null
  vpdPreferenceShift: number | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateStrainRequest {
  name: string
  breeder?: string | null
  dominance: StrainDominance
  flowerWeeksMin?: number | null
  flowerWeeksMax?: number | null
  notes?: string | null
  nutrientDemandFactor?: number | null
  stretchFactor?: number | null
  vpdPreferenceShift?: number | null
}

export type UpdateStrainRequest = CreateStrainRequest

export interface PlantInstanceDto {
  id: number
  strainId: number | null
  setupId: number | null
  growId: number | null
  parentPlantId: number | null
  label: string
  plantRole: PlantRole
  plantStatus: PlantStatus
  phenoLabel: string | null
  startedAt: string | null
  endedAt: string | null
  notes: string | null
  strainName: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreatePlantInstanceRequest {
  strainId?: number | null
  setupId?: number | null
  growId?: number | null
  parentPlantId?: number | null
  label: string
  plantRole: PlantRole
  plantStatus: PlantStatus
  phenoLabel?: string | null
  startedAt?: string | null
  endedAt?: string | null
  notes?: string | null
}

export type UpdatePlantInstanceRequest = CreatePlantInstanceRequest

export interface CreateCloneFromMotherRequest {
  motherPlantId: number
  targetSetupId?: number | null
  label: string
  phenoLabel?: string | null
  notes?: string | null
  strainId?: number | null
  cutAt?: string | null
}

export type QuarantineDecision = 'Cleared' | 'Rejected'

export interface DecideQuarantinePlantRequest {
  plantId: number
  decision: QuarantineDecision
  targetSetupId?: number | null
  targetGrowId?: number | null
  decidedAt?: string | null
  notes?: string | null
}

export interface AutoMeasurementConfigDto {
  id: number
  growId: number
  tentId: number | null
  name: string
  status: AutoMeasurementStatus
  triggerKind: AutoMeasurementTriggerKind
  delayMinutes: number | null
  windowMinutes: number
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateAutoMeasurementConfigRequest {
  growId: number
  tentId?: number | null
  name: string
  status: AutoMeasurementStatus
  triggerKind: AutoMeasurementTriggerKind
  delayMinutes?: number | null
  windowMinutes: number
}

export interface UpdateAutoMeasurementConfigRequest {
  tentId?: number | null
  name: string
  status: AutoMeasurementStatus
  triggerKind: AutoMeasurementTriggerKind
  delayMinutes?: number | null
  windowMinutes: number
}

export interface AutoMeasurementFieldMappingDto {
  id: number
  configId: number
  measurementField: AutoMeasurementField
  metricKey: string
  aggregation: AutoMeasurementAggregation
  isRequired: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export interface AutoMeasurementFieldMappingUpsertRequest {
  measurementField: AutoMeasurementField
  metricKey: string
  aggregation: AutoMeasurementAggregation
  isRequired: boolean
}

export interface ReplaceAutoMeasurementFieldMappingsRequest {
  mappings: AutoMeasurementFieldMappingUpsertRequest[]
}

export interface AutoMeasurementRunDto {
  id: number
  configId: number
  growId: number
  triggerKind: AutoMeasurementTriggerKind
  scheduledForUtc: string
  measurementId: number | null
  status: AutoMeasurementRunStatus
  errorMessage: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export type SensorMetricType =
  | 'AirTemperature'
  | 'Humidity'
  | 'Vpd'
  | 'Co2'
  | 'Ppfd'
  | 'LightStatus'
  | 'ReservoirPh'
  | 'ReservoirEc'
  | 'ReservoirOrp'
  | 'ReservoirDissolvedOxygen'
  | 'ReservoirWaterTemp'
  | 'ReservoirLevel'
  | 'PumpCirculation'
  | 'PumpAir'
  | 'Chiller'
  | 'UpsBattery'
  | 'UpsStatus'
export type LightControllerType = 'AcInfinityPro69' | 'AcInfinityCloudline' | 'GenericRelay' | 'Manual' | 'Other'
export type HvacControllerType = 'AcInfinityPro69' | 'AcInfinityCloudline' | 'GenericRelay' | 'Manual' | 'Other'

export interface TentSensorDto {
  id: number
  tentId: number
  metricType: SensorMetricType
  haEntityId: string
  displayLabel: string | null
  isActive: boolean
}

export interface TentDto {
  id: number
  name: string
  kind: string
  tentType: TentType
  notes: string | null
  displayOrder: number
  accentColor: string
  widthCm: number | null
  depthCm: number | null
  tentHeightCm: number | null
  lightType: string | null
  lightWatt: number | null
  lightController: LightControllerType | null
  lightControllerEntityId: string | null
  exhaustFanCount: number | null
  exhaustM3h: number | null
  circulationFanCount: number | null
  hvacController: HvacControllerType | null
  hvacControllerEntityId: string | null
  co2Available: boolean
  cameraEntityId: string | null
  activeGrowCount: number
  archivedGrowCount: number
  activeSetupCount: number
  archivedSetupCount: number
  sensors: TentSensorDto[]
}

export interface UpdateTentSensorRequest {
  id: number
  metricType: SensorMetricType
  haEntityId: string | null
  displayLabel: string | null
  isActive: boolean
}

export interface UpdateTentRequest {
  name: string
  kind: string
  tentType: TentType
  notes: string | null
  displayOrder: number
  accentColor: string
  widthCm: number | null
  depthCm: number | null
  tentHeightCm: number | null
  lightType: string | null
  lightWatt: number | null
  lightController: LightControllerType | null
  lightControllerEntityId: string | null
  exhaustFanCount: number | null
  exhaustM3h: number | null
  circulationFanCount: number | null
  hvacController: HvacControllerType | null
  hvacControllerEntityId: string | null
  co2Available: boolean
  cameraEntityId: string | null
  sensors: UpdateTentSensorRequest[]
}

export interface SettingsOverviewDto {
  homeAssistant: HomeAssistantSettingsDto
  tents: TentDto[]
}

export interface MetricPayload {
  key: string
  label: string
  value: string
  unit: string | null
  tone: string
  hint: string | null
}

export interface TentLivePayload {
  tentId: number
  stateTone: string
  stateLabel: string
  metrics: MetricPayload[]
  cameraUrl: string | null
  refreshedAtUtc: string
}

export interface NutrientProgramStageDto {
  stage: string
  dose: string
  target: string
  notes: string
}

export interface NutrientProgramDto {
  key: string
  name: string
  manufacturer: string
  category: string
  summary: string
  bestFor: string
  waterGuidance: string
  phGuidance: string
  ecGuidance: string
  stages: NutrientProgramStageDto[]
  tips: string[]
}

export interface MediumPlaybookDto {
  key: string
  title: string
  summary: string
  focusPoints: string[]
  redFlags: string[]
}

export interface KnowledgeOverviewDto {
  programs: NutrientProgramDto[]
  playbooks: MediumPlaybookDto[]
}
