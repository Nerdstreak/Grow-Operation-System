import type { AutoMeasurementAggregation, AutoMeasurementField, AutoMeasurementRunStatus, AutoMeasurementStatus, AutoMeasurementTriggerKind, HydroSetupLayoutType, HydroSetupStatus, HydroStyle, ReservoirPosition, SelectableHydroStyle } from './shared'
import type { HomeAssistantSettingsDto } from './hardware'
import type { TentStatus, TentType } from './production'

export interface AutoMeasurementConfigDto {
  id: number
  growId: number
  tentId: number | null
  name: string
  status: AutoMeasurementStatus
  triggerKind: AutoMeasurementTriggerKind
  delayMinutes: number | null
  windowMinutes: number
  captureSnapshot: boolean
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
  captureSnapshot?: boolean
}

export interface UpdateAutoMeasurementConfigRequest {
  tentId?: number | null
  name: string
  status: AutoMeasurementStatus
  triggerKind: AutoMeasurementTriggerKind
  delayMinutes?: number | null
  windowMinutes: number
  captureSnapshot?: boolean
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

export interface AutoMeasurementConfigStatusDto {
  configId: number
  growId: number
  name: string
  status: AutoMeasurementStatus
  triggerKind: AutoMeasurementTriggerKind
  delayMinutes: number | null
  windowMinutes: number
  mappingCount: number
  requiredMappingCount: number
  lastRunStatus: AutoMeasurementRunStatus | null
  lastRunScheduledForUtc: string | null
  lastRunMeasurementId: number | null
  lastRunErrorMessage: string | null
  createdRunCount: number
  skippedRunCount: number
  failedRunCount: number
  latestRelevantLightTransitionAtUtc: string | null
  latestRelevantLightTransitionKind: LightTransitionKind | null
}

export interface AutoMeasurementGrowStatusDto {
  growId: number
  configs: AutoMeasurementConfigStatusDto[]
}

export type LightState = 'Unknown' | 'On' | 'Off'
export type LightTransitionKind = 'LightOn' | 'LightOff'
export type LightSource = 'Manual' | 'HomeAssistant'

export interface LightScheduleDto {
  id: number
  tentId: number
  name: string
  isActive: boolean
  lightsOnTime: string
  lightsOffTime: string
  timeZoneId: string | null
  source: LightSource
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateLightScheduleRequest {
  tentId: number
  name: string
  isActive: boolean
  lightsOnTime: string
  lightsOffTime: string
  timeZoneId?: string | null
  source: LightSource
}

export interface UpdateLightScheduleRequest {
  name: string
  isActive: boolean
  lightsOnTime: string
  lightsOffTime: string
  timeZoneId?: string | null
  source: LightSource
}

export interface LightTransitionEventDto {
  id: number
  tentId: number
  kind: LightTransitionKind
  occurredAtUtc: string
  source: LightSource
  rawState: string | null
  createdAtUtc: string
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
  status: TentStatus
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
  status: TentStatus
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

export interface CreateTentRequest {
  name: string
  kind: string
  tentType: TentType
  status?: TentStatus
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

export interface HydroSetupDto {
  id: number
  name: string
  tentId: number | null
  tentName: string | null
  hydroStyle: HydroStyle
  potCount: number | null
  potSizeLiters: number | null
  reservoirLiters: number | null
  totalVolumeLiters: number | null
  layoutType: HydroSetupLayoutType
  reservoirPosition: ReservoirPosition
  status: HydroSetupStatus
  hasCirculationPump: boolean
  circulationPumpNotes: string | null
  hasAirPump: boolean
  airPumpNotes: string | null
  airStoneCount: number | null
  hasChiller: boolean
  hasUvSterilizer: boolean
  notes: string | null
  displayOrder: number
  activeGrowCount: number
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateHydroSetupRequest {
  tentId: number | null
  name: string
  hydroStyle: SelectableHydroStyle
  potCount: number | null
  potSizeLiters: number | null
  reservoirLiters: number | null
  layoutType: HydroSetupLayoutType
  reservoirPosition: ReservoirPosition
  hasCirculationPump: boolean
  circulationPumpNotes?: string | null
  hasAirPump: boolean
  airPumpNotes?: string | null
  airStoneCount: number | null
  hasChiller: boolean
  hasUvSterilizer: boolean
  notes?: string | null
  displayOrder: number
}

export interface UpdateHydroSetupRequest extends CreateHydroSetupRequest {
  status: HydroSetupStatus
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
