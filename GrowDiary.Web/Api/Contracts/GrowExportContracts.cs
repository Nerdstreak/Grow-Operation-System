namespace GrowDiary.Web.Api.Contracts;

public sealed record GrowExportDto(
    string SchemaVersion,
    DateTime ExportedAtUtc,
    bool Anonymized,
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
