$ErrorActionPreference = 'Stop'

function Split-Members {
    param(
        [string]$Path,
        [string]$ClassName,
        [string[]]$Markers,
        [hashtable]$Groups,
        [string]$Header
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        $lines.Add($line)
    }

    $markerIndexes = @{}
    foreach ($marker in $Markers) {
        $index = -1
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i].Contains($marker)) {
                $index = $i
                break
            }
        }
        if ($index -lt 0) {
            throw "Marker not found in ${Path}: ${marker}"
        }
        $markerIndexes[$marker] = $index
    }

    $classEnd = -1
    for ($i = $lines.Count - 1; $i -ge 0; $i--) {
        if ($lines[$i] -eq '}') {
            $classEnd = $i
            break
        }
    }

    $sections = @{}
    for ($m = 0; $m -lt $Markers.Count; $m++) {
        $marker = $Markers[$m]
        $start = [int]$markerIndexes[$marker]
        $end = if ($m -lt $Markers.Count - 1) { [int]$markerIndexes[$Markers[$m + 1]] - 1 } else { $classEnd - 1 }
        $sections[$marker] = @{
            Start = $start
            End = $end
            Lines = $lines[$start..$end]
        }
    }

    foreach ($groupName in $Groups.Keys) {
        $outLines = [System.Collections.Generic.List[string]]::new()
        foreach ($line in ($Header -split "`r?`n")) {
            $outLines.Add($line)
        }
        $outLines.Add("")
        $outLines.Add("public sealed partial class $ClassName")
        $outLines.Add("{")
        foreach ($marker in ([string[]]$Groups[$groupName])) {
            $outLines.AddRange([string[]]$sections[$marker].Lines)
            $outLines.Add("")
        }
        if ($outLines[$outLines.Count - 1] -eq "") {
            $outLines.RemoveAt($outLines.Count - 1)
        }
        $outLines.Add("}")

        $directory = [System.IO.Path]::GetDirectoryName($Path)
        [System.IO.File]::WriteAllLines([System.IO.Path]::Combine($directory, "$ClassName.$groupName.cs"), $outLines)
    }

    $ranges = foreach ($marker in $Markers) {
        [pscustomobject]@{
            Start = [int]$sections[$marker].Start
            End = [int]$sections[$marker].End
        }
    }
    foreach ($range in ($ranges | Sort-Object Start -Descending)) {
        for ($i = $range.End; $i -ge $range.Start; $i--) {
            $lines.RemoveAt($i)
        }
    }

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $lines[$i] = $lines[$i].Replace("public sealed class $ClassName", "public sealed partial class $ClassName")
    }

    [System.IO.File]::WriteAllLines($Path, $lines)
}

$header = @'
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Infrastructure;
'@

$markers = @(
    '    public HardwareItem CreateHardwareItem',
    '    public void UpdateHardwareItem',
    '    public void DeleteHardwareItem',
    '    public HardwareItem? GetHardwareItem',
    '    public List<HardwareItem> GetHardwareItems()',
    '    public List<HardwareItem> GetHardwareItemsByTent',
    '    public List<HardwareItem> GetHardwareItemsByHydroSetup',
    '    public List<HardwareItem> GetHardwareItemsByStatus',
    '    public MaintenanceEvent CreateMaintenanceEvent',
    '    public void UpdateMaintenanceEvent',
    '    public MaintenanceEvent? GetMaintenanceEvent',
    '    public List<MaintenanceEvent> GetMaintenanceEvents()',
    '    public List<MaintenanceEvent> GetMaintenanceEventsByHardwareItem',
    '    public List<MaintenanceEvent> GetOpenMaintenanceEventsByHardwareItem',
    '    public List<MaintenanceEvent> GetDueMaintenanceEvents',
    '    public CalibrationEvent CreateCalibrationEvent',
    '    public void UpdateCalibrationEvent',
    '    public CalibrationEvent? GetCalibrationEvent',
    '    public List<CalibrationEvent> GetCalibrationEvents()',
    '    public List<CalibrationEvent> GetCalibrationEventsByHardwareItem',
    '    public List<CalibrationEvent> GetOpenCalibrationEventsByHardwareItem',
    '    public List<CalibrationEvent> GetDueCalibrationEvents',
    '    public CalibrationEvent? GetLatestCompletedCalibrationEvent',
    '    public RiskEvent CreateRiskEvent',
    '    public void UpdateRiskEvent',
    '    public RiskEvent? GetRiskEvent',
    '    public List<RiskEvent> GetRiskEvents()',
    '    public List<RiskEvent> GetOpenRiskEvents',
    '    public List<RiskEvent> GetRiskEventsByHardwareItem',
    '    public List<RiskEvent> GetRiskEventsByTent',
    '    public List<RiskEvent> GetRiskEventsByGrow',
    '    public List<RiskEvent> GetRiskEventsByStatus',
    '    public RiskEvent? FindOpenRiskEventByDedupeKey',
    '    public RiskEvent ResolveRiskEvent',
    '    public RiskEvent AcknowledgeRiskEvent',
    '    private List<HardwareItem> GetHardwareItemsByWhere',
    '    private List<MaintenanceEvent> GetMaintenanceEventsByWhere',
    '    private List<CalibrationEvent> GetCalibrationEventsByWhere',
    '    private List<RiskEvent> GetRiskEventsByWhere',
    '    private void ValidateHardwareItem',
    '    private static void ApplyRetiredTimestamp',
    '    private HardwareItem ValidateMaintenanceEvent',
    '    private static void ApplyMaintenanceDefaults',
    '    private static int? TryCreateMaintenanceReminderTask',
    '    private static TaskPriority ToMaintenanceTaskPriority',
    '    private HardwareItem ValidateCalibrationEvent',
    '    private static void ApplyCalibrationDefaults',
    '    private static int? TryCreateCalibrationReminderTask',
    '    private void ValidateRiskEvent',
    '    private static void ApplyRiskEventDefaults',
    '    private bool RowExists',
    '    private (bool exists, int? tentId) GetHydroSetupTentId',
    '    private static DateTime NormalizeStartedAt',
    '    private static string? AppendNotes',
    '    private static HardwareItem MapHardwareItem',
    '    private static MaintenanceEvent MapMaintenanceEvent',
    '    private static CalibrationEvent MapCalibrationEvent',
    '    private static RiskEvent MapRiskEvent',
    '    private static void AddHardwareItemParameters',
    '    private static void AddMaintenanceEventParameters',
    '    private static void AddCalibrationEventParameters',
    '    private static void AddRiskEventParameters'
)

$groups = @{
    'Items' = @(
        '    public HardwareItem CreateHardwareItem',
        '    public void UpdateHardwareItem',
        '    public void DeleteHardwareItem',
        '    public HardwareItem? GetHardwareItem',
        '    public List<HardwareItem> GetHardwareItems()',
        '    public List<HardwareItem> GetHardwareItemsByTent',
        '    public List<HardwareItem> GetHardwareItemsByHydroSetup',
        '    public List<HardwareItem> GetHardwareItemsByStatus',
        '    private List<HardwareItem> GetHardwareItemsByWhere',
        '    private void ValidateHardwareItem',
        '    private static void ApplyRetiredTimestamp',
        '    private static HardwareItem MapHardwareItem',
        '    private static void AddHardwareItemParameters'
    )
    'Maintenance' = @(
        '    public MaintenanceEvent CreateMaintenanceEvent',
        '    public void UpdateMaintenanceEvent',
        '    public MaintenanceEvent? GetMaintenanceEvent',
        '    public List<MaintenanceEvent> GetMaintenanceEvents()',
        '    public List<MaintenanceEvent> GetMaintenanceEventsByHardwareItem',
        '    public List<MaintenanceEvent> GetOpenMaintenanceEventsByHardwareItem',
        '    public List<MaintenanceEvent> GetDueMaintenanceEvents',
        '    private List<MaintenanceEvent> GetMaintenanceEventsByWhere',
        '    private HardwareItem ValidateMaintenanceEvent',
        '    private static void ApplyMaintenanceDefaults',
        '    private static int? TryCreateMaintenanceReminderTask',
        '    private static TaskPriority ToMaintenanceTaskPriority',
        '    private static MaintenanceEvent MapMaintenanceEvent',
        '    private static void AddMaintenanceEventParameters'
    )
    'Calibration' = @(
        '    public CalibrationEvent CreateCalibrationEvent',
        '    public void UpdateCalibrationEvent',
        '    public CalibrationEvent? GetCalibrationEvent',
        '    public List<CalibrationEvent> GetCalibrationEvents()',
        '    public List<CalibrationEvent> GetCalibrationEventsByHardwareItem',
        '    public List<CalibrationEvent> GetOpenCalibrationEventsByHardwareItem',
        '    public List<CalibrationEvent> GetDueCalibrationEvents',
        '    public CalibrationEvent? GetLatestCompletedCalibrationEvent',
        '    private List<CalibrationEvent> GetCalibrationEventsByWhere',
        '    private HardwareItem ValidateCalibrationEvent',
        '    private static void ApplyCalibrationDefaults',
        '    private static int? TryCreateCalibrationReminderTask',
        '    private static CalibrationEvent MapCalibrationEvent',
        '    private static void AddCalibrationEventParameters'
    )
    'Risks' = @(
        '    public RiskEvent CreateRiskEvent',
        '    public void UpdateRiskEvent',
        '    public RiskEvent? GetRiskEvent',
        '    public List<RiskEvent> GetRiskEvents()',
        '    public List<RiskEvent> GetOpenRiskEvents',
        '    public List<RiskEvent> GetRiskEventsByHardwareItem',
        '    public List<RiskEvent> GetRiskEventsByTent',
        '    public List<RiskEvent> GetRiskEventsByGrow',
        '    public List<RiskEvent> GetRiskEventsByStatus',
        '    public RiskEvent? FindOpenRiskEventByDedupeKey',
        '    public RiskEvent ResolveRiskEvent',
        '    public RiskEvent AcknowledgeRiskEvent',
        '    private List<RiskEvent> GetRiskEventsByWhere',
        '    private void ValidateRiskEvent',
        '    private static void ApplyRiskEventDefaults',
        '    private static DateTime NormalizeStartedAt',
        '    private static string? AppendNotes',
        '    private static RiskEvent MapRiskEvent',
        '    private static void AddRiskEventParameters'
    )
    'Support' = @(
        '    private bool RowExists',
        '    private (bool exists, int? tentId) GetHydroSetupTentId'
    )
}

Split-Members `
    -Path 'GrowDiary.Web\Infrastructure\HardwareRepository.cs' `
    -ClassName 'HardwareRepository' `
    -Markers $markers `
    -Groups $groups `
    -Header $header
