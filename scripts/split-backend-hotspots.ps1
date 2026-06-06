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
    if ($classEnd -lt 0) {
        throw "Class end not found in ${Path}"
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
        $groupMarkers = [string[]]$Groups[$groupName]
        $outLines = [System.Collections.Generic.List[string]]::new()
        foreach ($line in ($Header -split "`r?`n")) {
            $outLines.Add($line)
        }
        $outLines.Add("")
        $outLines.Add("public sealed partial class $ClassName")
        $outLines.Add("{")
        foreach ($marker in $groupMarkers) {
            $outLines.AddRange([string[]]$sections[$marker].Lines)
            $outLines.Add("")
        }
        if ($outLines[$outLines.Count - 1] -eq "") {
            $outLines.RemoveAt($outLines.Count - 1)
        }
        $outLines.Add("}")

        $directory = [System.IO.Path]::GetDirectoryName($Path)
        $outPath = [System.IO.Path]::Combine($directory, "$ClassName.$groupName.cs")
        [System.IO.File]::WriteAllLines($outPath, $outLines)
    }

    $ranges = foreach ($marker in $Markers) {
        [pscustomobject]@{
            Start = [int]$sections[$marker].Start
            End = [int]$sections[$marker].End
        }
    }
    $ranges = $ranges | Sort-Object Start -Descending
    foreach ($range in $ranges) {
        for ($i = $range.End; $i -ge $range.Start; $i--) {
            $lines.RemoveAt($i)
        }
    }

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $lines[$i] = $lines[$i].Replace("public sealed class $ClassName", "public sealed partial class $ClassName")
    }

    [System.IO.File]::WriteAllLines($Path, $lines)
}

$systemHeader = @'
using System.IO.Compression;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace GrowDiary.Web.Api.Controllers;
'@

$systemMarkers = @(
    '    [HttpGet("backend-health")]',
    '    [HttpGet("release-readiness")]',
    '    [HttpGet("api-manifest")]',
    '    [HttpGet("error-contract")]',
    '    [HttpGet("security-status")]',
    '    [HttpGet("audit-events")]',
    '    [HttpGet("database-status")]',
    '    [HttpGet("migration-status")]',
    '    [HttpGet("migration-plan")]',
    '    [HttpPost("upgrade-preflight")]',
    '    [HttpGet("backup/{fileName}/validate")]',
    '    [HttpPost("backup/{fileName}/restore-plan")]',
    '    [HttpPost("backup")]',
    '    [HttpPost("backup/{fileName}/restore")]',
    '    [HttpGet("backup/{fileName}")]',
    '    private void LogSystemAudit',
    '    private SchemaMigrationPlanDto BuildMigrationPlan',
    '    private SchemaMigrationStatusDto BuildMigrationStatus',
    '    private static T ExtractOk',
    '    private string? ResolveBackupPath',
    '    private static ApiEndpointDto Endpoint',
    '    private SqliteConnection OpenReadConnection',
    '    private static string? ReadAppSetting',
    '    private static bool TableExists',
    '    private static bool ColumnExists',
    '    private static bool IsSafeBackupFileName',
    '    private static string? ResolveRestoreEntryKind',
    '    private bool WouldOverwriteRestoreTarget',
    '    private static bool IsUnsafeZipEntryName',
    '    private static string? ReadSchemaVersionFromBackupDatabase',
    '    private static string CreateUniqueBackupFileName',
    '    private static string RunSqliteQuickCheck',
    '    private static void RestoreFileWithRollback',
    '    private static void RestoreOptionalFileWithRollback',
    '    private static void RestoreDirectoryWithRollback',
    '    private static void RestoreRollbackFiles',
    '    private static void RestoreRollbackFile',
    '    private static void CopyDirectory',
    '    private static void DeleteDirectoryBestEffort',
    '    private static void AddIfExists'
)

$systemGroups = @{
    'StatusEndpoints' = @(
        '    [HttpGet("backend-health")]',
        '    [HttpGet("release-readiness")]',
        '    [HttpGet("api-manifest")]',
        '    [HttpGet("error-contract")]',
        '    [HttpGet("security-status")]',
        '    [HttpGet("audit-events")]',
        '    [HttpGet("database-status")]'
    )
    'MigrationEndpoints' = @(
        '    [HttpGet("migration-status")]',
        '    [HttpGet("migration-plan")]',
        '    [HttpPost("upgrade-preflight")]',
        '    private SchemaMigrationPlanDto BuildMigrationPlan',
        '    private SchemaMigrationStatusDto BuildMigrationStatus',
        '    private static T ExtractOk'
    )
    'BackupEndpoints' = @(
        '    [HttpGet("backup/{fileName}/validate")]',
        '    [HttpPost("backup/{fileName}/restore-plan")]',
        '    [HttpPost("backup")]',
        '    [HttpPost("backup/{fileName}/restore")]',
        '    [HttpGet("backup/{fileName}")]'
    )
    'Support' = @(
        '    private void LogSystemAudit',
        '    private string? ResolveBackupPath',
        '    private static ApiEndpointDto Endpoint',
        '    private SqliteConnection OpenReadConnection',
        '    private static string? ReadAppSetting',
        '    private static bool TableExists',
        '    private static bool ColumnExists',
        '    private static bool IsSafeBackupFileName',
        '    private static string? ResolveRestoreEntryKind',
        '    private bool WouldOverwriteRestoreTarget',
        '    private static bool IsUnsafeZipEntryName',
        '    private static string? ReadSchemaVersionFromBackupDatabase',
        '    private static string CreateUniqueBackupFileName',
        '    private static string RunSqliteQuickCheck',
        '    private static void RestoreFileWithRollback',
        '    private static void RestoreOptionalFileWithRollback',
        '    private static void RestoreDirectoryWithRollback',
        '    private static void RestoreRollbackFiles',
        '    private static void RestoreRollbackFile',
        '    private static void CopyDirectory',
        '    private static void DeleteDirectoryBestEffort',
        '    private static void AddIfExists'
    )
}

Split-Members `
    -Path 'GrowDiary.Web\Api\Controllers\SystemApiController.cs' `
    -ClassName 'SystemApiController' `
    -Markers $systemMarkers `
    -Groups $systemGroups `
    -Header $systemHeader

$exportHeader = @'
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Mapping;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;
'@

$exportMarkers = @(
    '    [HttpPost("validate")]',
    '    [HttpPost("import-plan")]',
    '    [HttpPost("import")]',
    '    [HttpGet("{id:int}")]',
    '    private static TentDto? TryReadTentSnapshotDto',
    '    private static HydroSetupDto? TryReadHydroSetupSnapshotDto',
    '    private static T? TryDeserializeSnapshot',
    '    private GrowImportPlanDto BuildImportPlan',
    '    private GrowRun ToImportedGrowRun',
    '    private static string? AppendImportNote',
    '    private static Measurement ToImportedMeasurement',
    '    private static JournalEntry ToImportedJournalEntry',
    '    private static GrowTask ToImportedHistoricalTask',
    '    private static AddbackLogEntry ToImportedAddbackLog',
    '    private static ChangeoutEntry ToImportedChangeout',
    '    private static HarvestEntry ToImportedHarvest',
    '    private static GrowTentSnapshot ToTentSnapshot',
    '    private static GrowHydroSetupSnapshot ToHydroSetupSnapshot',
    '    private ImportSafetyBackup? CreateImportSafetyBackup',
    '    private static string CreateUniqueImportSafetyBackupFileName',
    '    private static void AddBackupEntryIfExists',
    '    private readonly record struct ImportSafetyBackup',
    '    private void LogExportAudit',
    '    private static GrowExportValidationDto BuildValidation',
    '    private static GrowExportSectionCountsDto CountSections',
    '    private static bool SectionCountsEqual',
    '    private static string ComputeIntegrityHash',
    '    private static bool ContainsPotentialSecrets'
)

$exportGroups = @{
    'Validation' = @(
        '    [HttpPost("validate")]',
        '    private static GrowExportValidationDto BuildValidation',
        '    private static GrowExportSectionCountsDto CountSections',
        '    private static bool SectionCountsEqual',
        '    private static string ComputeIntegrityHash',
        '    private static bool ContainsPotentialSecrets'
    )
    'Planning' = @(
        '    [HttpPost("import-plan")]',
        '    private GrowImportPlanDto BuildImportPlan'
    )
    'Import' = @(
        '    [HttpPost("import")]',
        '    private GrowRun ToImportedGrowRun',
        '    private static string? AppendImportNote',
        '    private static Measurement ToImportedMeasurement',
        '    private static JournalEntry ToImportedJournalEntry',
        '    private static GrowTask ToImportedHistoricalTask',
        '    private static AddbackLogEntry ToImportedAddbackLog',
        '    private static ChangeoutEntry ToImportedChangeout',
        '    private static HarvestEntry ToImportedHarvest',
        '    private ImportSafetyBackup? CreateImportSafetyBackup',
        '    private static string CreateUniqueImportSafetyBackupFileName',
        '    private static void AddBackupEntryIfExists',
        '    private readonly record struct ImportSafetyBackup'
    )
    'Export' = @(
        '    [HttpGet("{id:int}")]'
    )
    'Snapshots' = @(
        '    private static TentDto? TryReadTentSnapshotDto',
        '    private static HydroSetupDto? TryReadHydroSetupSnapshotDto',
        '    private static T? TryDeserializeSnapshot',
        '    private static GrowTentSnapshot ToTentSnapshot',
        '    private static GrowHydroSetupSnapshot ToHydroSetupSnapshot'
    )
    'Audit' = @(
        '    private void LogExportAudit'
    )
}

Split-Members `
    -Path 'GrowDiary.Web\Api\Controllers\GrowExportsApiController.cs' `
    -ClassName 'GrowExportsApiController' `
    -Markers $exportMarkers `
    -Groups $exportGroups `
    -Header $exportHeader

$dbHeader = @'
using GrowDiary.Web.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GrowDiary.Web.Infrastructure;
'@

$dbMarkers = @(
    '    private void DropLegacyTentSchemaIfNeeded',
    '    private static void RenameTableIfExists',
    '    private void EnsureSchema',
    '    private static void RecordSchemaVersion',
    '    private static void RecordAppliedSchemaMigrations',
    '    private static void EnsureSchemaMigrationMetadataColumns',
    '    private static void UpsertAppSetting',
    '    private void SeedDefaults',
    '    private void AutoAssignExistingGrowsToTents',
    '    private static void EnsureColumn',
    '    private static bool TableExists',
    '    private static int GetTentId',
    '    private SqliteConnection OpenConnection'
)

$dbGroups = @{
    'LegacySchema' = @(
        '    private void DropLegacyTentSchemaIfNeeded',
        '    private static void RenameTableIfExists'
    )
    'Schema' = @(
        '    private void EnsureSchema',
        '    private static void RecordSchemaVersion',
        '    private static void RecordAppliedSchemaMigrations',
        '    private static void EnsureSchemaMigrationMetadataColumns',
        '    private static void UpsertAppSetting',
        '    private static void EnsureColumn',
        '    private static bool TableExists',
        '    private SqliteConnection OpenConnection'
    )
    'Defaults' = @(
        '    private void SeedDefaults'
    )
    'Backfill' = @(
        '    private void AutoAssignExistingGrowsToTents',
        '    private static int GetTentId'
    )
}

Split-Members `
    -Path 'GrowDiary.Web\Infrastructure\DatabaseInitializer.cs' `
    -ClassName 'DatabaseInitializer' `
    -Markers $dbMarkers `
    -Groups $dbGroups `
    -Header $dbHeader
