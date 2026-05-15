namespace GrowDiary.Web.Api.Contracts;

public sealed record ApiErrorContractDto(
    string SchemaVersion,
    string Format,
    DateTime GeneratedAtUtc,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<string> OptionalFields,
    IReadOnlyList<string> StandardCodes,
    IReadOnlyList<string> StandardStatuses,
    IReadOnlyList<string> Rules
);
