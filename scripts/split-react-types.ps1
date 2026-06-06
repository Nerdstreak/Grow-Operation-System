$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root 'GrowDiary.React/src/types.ts'
$typesDir = Join-Path $root 'GrowDiary.React/src/types'

$content = [System.IO.File]::ReadAllText($sourcePath, [System.Text.Encoding]::UTF8)
[System.IO.Directory]::CreateDirectory($typesDir) | Out-Null

function IndexOfMarker([string]$marker) {
  $index = $content.IndexOf($marker, [System.StringComparison]::Ordinal)
  if ($index -lt 0) {
    throw "Type split marker not found: $marker"
  }
  return $index
}

function Slice([string]$startMarker, [string]$endMarker) {
  $start = if ($startMarker) { IndexOfMarker $startMarker } else { 0 }
  $end = if ($endMarker) { IndexOfMarker $endMarker } else { $content.Length }
  if ($end -le $start) {
    throw "Invalid type split range: $startMarker -> $endMarker"
  }
  return $content.Substring($start, $end - $start).Trim()
}

function WriteModule([string]$name, [string]$body, [string[]]$imports = @()) {
  $target = Join-Path $typesDir $name
  $prefix = if ($imports.Count -gt 0) { ($imports -join [System.Environment]::NewLine) + [System.Environment]::NewLine + [System.Environment]::NewLine } else { '' }
  [System.IO.File]::WriteAllText($target, $prefix + $body + [System.Environment]::NewLine, [System.Text.Encoding]::UTF8)
}

$shared = Slice $null 'export interface DependencyItemDto'
$grow = Slice 'export interface DependencyItemDto' 'export interface HomeAssistantSettingsDto'
$hardware = Slice 'export interface HomeAssistantSettingsDto' 'export type TentType ='
$production = Slice 'export type TentType =' 'export interface AutoMeasurementConfigDto'
$automation = Slice 'export interface AutoMeasurementConfigDto' 'export interface NutrientProgramStageDto'
$knowledge = Slice 'export interface NutrientProgramStageDto' $null

WriteModule 'shared.ts' $shared
WriteModule 'grow.ts' $grow @(
  "import type { ApiError, DeviationMetric, DeviationSeverity, DeviationSource, GerminationMethod, GrowEntryPoint, GrowEnvironment, GrowStage, GrowStatus, GrowTaskStatus, HydroStyle, JournalEntryType, PhotoTag, PropagationMedium, SeedType, SopInstanceStatus, SopStartSource, SopStepInstanceStatus, StartMaterial, TaskPriority, TreatmentRecommendationConfidence, ValueOrigin, WaterSource } from './shared'"
)
WriteModule 'hardware.ts' $hardware @(
  "import type { CalibrationEventStatus, CalibrationEventType, CalibrationResult, HardwareItemCriticality, HardwareItemStatus, MaintenanceEventStatus, MaintenanceEventType, MaintenanceResult, RiskEventSeverity, RiskEventSource, RiskEventStatus, RiskEventType } from './shared'"
)
WriteModule 'production.ts' $production @(
  "import type { MotherHealthStatus, PlantRole, PlantStatus, QuarantineResult, SetupStatus, SetupType, StrainDominance } from './shared'"
)
WriteModule 'automation.ts' $automation @(
  "import type { AutoMeasurementAggregation, AutoMeasurementField, AutoMeasurementRunStatus, AutoMeasurementStatus, AutoMeasurementTriggerKind, HydroSetupLayoutType, HydroSetupStatus, HydroStyle, ReservoirPosition, SelectableHydroStyle } from './shared'",
  "import type { HomeAssistantSettingsDto } from './hardware'",
  "import type { TentStatus, TentType } from './production'"
)
WriteModule 'knowledge.ts' $knowledge

$entrypoint = @'
export * from './types/shared'
export * from './types/grow'
export * from './types/hardware'
export * from './types/production'
export * from './types/automation'
export * from './types/knowledge'
'@

[System.IO.File]::WriteAllText($sourcePath, $entrypoint + [System.Environment]::NewLine, [System.Text.Encoding]::UTF8)
Write-Host 'Split React types.ts into domain modules.'
