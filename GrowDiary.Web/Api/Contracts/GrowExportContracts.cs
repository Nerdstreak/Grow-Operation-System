namespace GrowDiary.Web.Api.Contracts;

public sealed record GrowExportDto(
    string SchemaVersion,
    string ExportId,
    DateTime ExportedAtUtc,
    bool Anonymized,
    string IntegrityHash,
    GrowExportSectionCountsDto SectionCounts,
    GrowDetailDto Grow,
    TentDto? TentSnapshot,
    HydroSetupDto? HydroSetupSnapshot,
    IReadOnlyList<MeasurementDto> Measurements,
    IReadOnlyList<JournalEntryDto> JournalEntries,
    IReadOnlyList<GrowTaskDto> Tasks,
    IReadOnlyList<HardwareItemDto> HardwareItems,
    HarvestDto? Harvest,
    IReadOnlyList<AddbackLogDto> AddbackLogs,
    IReadOnlyList<ChangeoutDto> Changeouts,
    IReadOnlyList<PhotoAssetDto> Photos,
    IReadOnlyList<string> Warnings);

public sealed record GrowExportSectionCountsDto(
    int Measurements,
    int JournalEntries,
    int Tasks,
    int HardwareItems,
    int AddbackLogs,
    int Changeouts,
    int Photos);

public sealed record GrowExportValidationDto(
    string ValidationSchema,
    DateTime CheckedAtUtc,
    string? ExportSchemaVersion,
    string? ExportId,
    bool IsValid,
    bool IntegrityHashValid,
    bool SectionCountsValid,
    bool ContainsPotentialSecrets,
    GrowExportSectionCountsDto? DeclaredSectionCounts,
    GrowExportSectionCountsDto? ActualSectionCounts,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);


public sealed record GrowImportPlanDto(
    string ImportPlanSchema,
    DateTime CheckedAtUtc,
    bool ExportValid,
    bool ImportSupported,
    bool WouldModifyDatabase,
    bool IsAnonymized,
    string? ExportId,
    string? ExportSchemaVersion,
    string? IntegrityHash,
    GrowImportPlanSourceDto? Source,
    GrowExportSectionCountsDto? SectionCounts,
    IReadOnlyList<GrowImportPlanItemDto> PlannedItems,
    IReadOnlyList<GrowImportPlanConflictDto> Conflicts,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings);

public sealed record GrowImportPlanSourceDto(
    int? OriginalGrowId,
    string? GrowName,
    string? TentName,
    string? HydroSetupName,
    DateTime? ExportedAtUtc);

public sealed record GrowImportPlanItemDto(
    string Kind,
    string Action,
    int Count,
    string? Notes);

public sealed record GrowImportPlanConflictDto(
    string Kind,
    string Severity,
    string Message);

public sealed record GrowImportResultDto(
    string ImportSchema,
    DateTime ImportedAtUtc,
    bool Success,
    string ExportId,
    int ImportedGrowId,
    string ImportedGrowName,
    string SafetyBackupFileName,
    string SafetyBackupDownloadUrl,
    int ImportedMeasurements,
    int ImportedJournalEntries,
    int ImportedTasks,
    int ImportedAddbackLogs,
    int ImportedChangeouts,
    int ImportedHarvestEntries,
    int SkippedHardwareItems,
    int SkippedPhotos,
    IReadOnlyList<string> Warnings);

public sealed record BackendHealthDto(
    string AppName,
    string BackendSchema,
    DateTime CheckedAtUtc,
    int TentCount,
    int HydroSetupCount,
    int GrowCount,
    bool ZeroTentStartupSupported,
    IReadOnlyList<string> Capabilities);

public sealed record BackupManifestDto(
    string BackupSchema,
    DateTime CreatedAtUtc,
    string FileName,
    long SizeBytes,
    bool IncludesDatabase,
    bool IncludesWal,
    bool IncludesKnowledgeRuntimeCopy,
    bool ExcludesSecrets,
    bool ExcludesHomeAssistantConfig,
    bool ExcludesDataProtectionKeys,
    bool ExcludesUploads,
    bool RestoreSupported,
    string DownloadUrl);

public sealed record BackendReleaseReadinessDto(
    string Status,
    string BackendSchema,
    DateTime CheckedAtUtc,
    IReadOnlyList<ReleaseReadinessCheckDto> Checks,
    IReadOnlyList<string> CompletedFoundations,
    IReadOnlyList<string> RemainingBeforeV1);

public sealed record ReleaseReadinessCheckDto(
    string Key,
    string Status,
    string Message);

public sealed record DatabaseStatusDto(
    string ExpectedSchemaVersion,
    string? StoredSchemaVersion,
    DateTime CheckedAtUtc,
    bool DatabaseExists,
    bool IsCurrent,
    IReadOnlyList<string> RequiredTablesPresent,
    IReadOnlyList<string> MissingRequiredTables,
    IReadOnlyList<string> RequiredColumnsPresent,
    IReadOnlyList<string> MissingRequiredColumns,
    IReadOnlyList<string> Warnings);

public sealed record BackupValidationDto(
    string BackupSchema,
    string FileName,
    DateTime CheckedAtUtc,
    bool Exists,
    bool IsValid,
    bool ContainsDatabase,
    bool ContainsWal,
    bool ContainsSecrets,
    bool ContainsDataProtectionKeys,
    bool ContainsUploads,
    int EntryCount,
    IReadOnlyList<string> Warnings);


public sealed record BackupRestorePlanDto(
    string RestorePlanSchema,
    string FileName,
    DateTime CheckedAtUtc,
    bool BackupValid,
    bool DatabaseIncluded,
    bool WalIncluded,
    bool ShmIncluded,
    bool KnowledgeIncluded,
    bool SchemaCompatible,
    bool RestoreSupported,
    bool RequiresManualStop,
    bool WouldOverwriteExistingDatabase,
    string? BackupSchemaVersion,
    string CurrentSchemaVersion,
    IReadOnlyList<BackupRestorePlanFileDto> Files,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings);

public sealed record BackupRestorePlanFileDto(
    string EntryName,
    string RelativeTargetPath,
    string Kind,
    long SizeBytes,
    bool WouldOverwrite);

public sealed record BackupRestoreResultDto(
    string RestoreSchema,
    string FileName,
    DateTime RestoredAtUtc,
    bool Success,
    string SafetyBackupFileName,
    string SafetyBackupDownloadUrl,
    string DatabaseTargetPath,
    bool DatabaseRestored,
    bool WalRestored,
    bool ShmRestored,
    int KnowledgeFileCount,
    IReadOnlyList<string> RestoredKnowledgeFiles,
    IReadOnlyList<string> Warnings);

public sealed record SchemaMigrationStatusDto(
    string MigrationSchema,
    string CurrentSchemaVersion,
    string? StoredSchemaVersion,
    DateTime CheckedAtUtc,
    bool MigrationTableExists,
    bool IsCurrent,
    IReadOnlyList<AppliedSchemaMigrationDto> AppliedMigrations,
    IReadOnlyList<PendingSchemaMigrationDto> PendingMigrations,
    IReadOnlyList<string> Warnings);

public sealed record AppliedSchemaMigrationDto(
    string Id,
    string Name,
    string RequiredForSchemaVersion,
    DateTime? AppliedAtUtc);

public sealed record PendingSchemaMigrationDto(
    string Id,
    string Name,
    string RequiredForSchemaVersion);

public sealed record UpgradePreflightDto(
    string PreflightSchema,
    DateTime CheckedAtUtc,
    bool IsSafeToUpgrade,
    bool DatabaseCurrent,
    bool BackupCreated,
    bool BackupValid,
    string? BackupFileName,
    string? BackupDownloadUrl,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    DatabaseStatusDto DatabaseStatus,
    SchemaMigrationStatusDto MigrationStatus,
    BackupValidationDto? BackupValidation);
