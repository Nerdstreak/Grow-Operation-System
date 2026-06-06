import type { CalibrationEventStatus, CalibrationEventType, CalibrationResult, HardwareItemCriticality, HardwareItemStatus, MaintenanceEventStatus, MaintenanceEventType, MaintenanceResult, RiskEventSeverity, RiskEventSource, RiskEventStatus, RiskEventType } from './shared'

export interface HomeAssistantSettingsDto {
  baseUrl: string | null
  accessToken: string | null
  enabled: boolean
}

export interface HardwareItemDto {
  id: number
  name: string
  category: string
  status: HardwareItemStatus
  criticality: HardwareItemCriticality
  tentId: number | null
  setupId: number | null
  hydroSetupId: number | null
  growId: number | null
  wearTemplateId: string | null
  tentSensorId: number | null
  haEntityId: string | null
  manufacturer: string | null
  model: string | null
  serialNumber: string | null
  installedAtUtc: string | null
  retiredAtUtc: string | null
  expectedLifespanDays: number | null
  inspectionIntervalDays: number | null
  notes: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateHardwareItemRequest {
  name?: string | null
  category?: string | null
  status: HardwareItemStatus
  criticality: HardwareItemCriticality
  tentId?: number | null
  setupId?: number | null
  hydroSetupId?: number | null
  growId?: number | null
  wearTemplateId?: string | null
  tentSensorId?: number | null
  haEntityId?: string | null
  manufacturer?: string | null
  model?: string | null
  serialNumber?: string | null
  installedAtUtc?: string | null
  retiredAtUtc?: string | null
  expectedLifespanDays?: number | null
  inspectionIntervalDays?: number | null
  notes?: string | null
}

export interface UpdateHardwareItemRequest extends CreateHardwareItemRequest {
  name: string
  category: string
}

export interface MaintenanceEventDto {
  id: number
  hardwareItemId: number
  eventType: MaintenanceEventType
  status: MaintenanceEventStatus
  result: MaintenanceResult
  title: string
  description: string | null
  dueAtUtc: string | null
  performedAtUtc: string | null
  nextDueAtUtc: string | null
  growTaskId: number | null
  sopInstanceId: number | null
  notes: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateMaintenanceEventRequest {
  hardwareItemId: number
  eventType: MaintenanceEventType
  status: MaintenanceEventStatus
  result: MaintenanceResult
  title: string
  description?: string | null
  dueAtUtc?: string | null
  performedAtUtc?: string | null
  nextDueAtUtc?: string | null
  growTaskId?: number | null
  sopInstanceId?: number | null
  notes?: string | null
}

export type UpdateMaintenanceEventRequest = CreateMaintenanceEventRequest

export interface CalibrationEventDto {
  id: number
  hardwareItemId: number
  calibrationType: CalibrationEventType
  status: CalibrationEventStatus
  result: CalibrationResult
  title: string
  referenceSolution: string | null
  referenceValue: number | null
  beforeValue: number | null
  afterValue: number | null
  temperatureC: number | null
  dueAtUtc: string | null
  performedAtUtc: string | null
  nextDueAtUtc: string | null
  growTaskId: number | null
  notes: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateCalibrationEventRequest {
  hardwareItemId: number
  calibrationType: CalibrationEventType
  status: CalibrationEventStatus
  result: CalibrationResult
  title: string
  referenceSolution?: string | null
  referenceValue?: number | null
  beforeValue?: number | null
  afterValue?: number | null
  temperatureC?: number | null
  dueAtUtc?: string | null
  performedAtUtc?: string | null
  nextDueAtUtc?: string | null
  growTaskId?: number | null
  notes?: string | null
}

export type UpdateCalibrationEventRequest = CreateCalibrationEventRequest

export interface RiskEventDto {
  id: number
  eventType: RiskEventType
  severity: RiskEventSeverity
  status: RiskEventStatus
  source: RiskEventSource
  title: string
  description: string | null
  hardwareItemId: number | null
  tentId: number | null
  growId: number | null
  tentSensorId: number | null
  haEntityId: string | null
  sopInstanceId: number | null
  growTaskId: number | null
  startedAtUtc: string
  lastSeenAtUtc: string | null
  resolvedAtUtc: string | null
  acknowledgedAtUtc: string | null
  dedupeKey: string | null
  rawValue: string | null
  notes: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateRiskEventRequest {
  eventType: RiskEventType
  severity: RiskEventSeverity
  status: RiskEventStatus
  source: RiskEventSource
  title: string
  description?: string | null
  hardwareItemId?: number | null
  tentId?: number | null
  growId?: number | null
  tentSensorId?: number | null
  haEntityId?: string | null
  sopInstanceId?: number | null
  growTaskId?: number | null
  startedAtUtc?: string | null
  lastSeenAtUtc?: string | null
  resolvedAtUtc?: string | null
  acknowledgedAtUtc?: string | null
  dedupeKey?: string | null
  rawValue?: string | null
  notes?: string | null
}

export type UpdateRiskEventRequest = CreateRiskEventRequest

export interface ResolveRiskEventRequest {
  resolvedAtUtc?: string | null
  notes?: string | null
}

export interface AcknowledgeRiskEventRequest {
  acknowledgedAtUtc?: string | null
  notes?: string | null
}

export interface RiskEventSopRecommendationDto {
  riskEventId: number
  riskEventType: string
  severity: string
  sopId: string
  sopName: string
  reason: string
  confidence: string
  alreadyActive: boolean
  activeSopInstanceId: number | null
}

export interface StartRiskEventSopRequest {
  sopId: string
  notes?: string | null
}
