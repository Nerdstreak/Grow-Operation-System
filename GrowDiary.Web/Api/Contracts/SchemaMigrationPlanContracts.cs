namespace GrowDiary.Web.Api.Contracts;

public sealed record SchemaMigrationPlanDto(
    string PlanSchema,
    string CurrentSchemaVersion,
    DateTime CheckedAtUtc,
    bool WouldModifyDatabase,
    bool RequiresBackupBeforeApply,
    bool HasDestructiveSteps,
    bool ApplySupported,
    IReadOnlyList<SchemaMigrationPlanItemDto> Items,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings);

public sealed record SchemaMigrationPlanItemDto(
    string Id,
    string Name,
    string RequiredForSchemaVersion,
    string Status,
    bool IsDestructive,
    bool RequiresBackup,
    string ExecutionMode,
    string? Checksum);
