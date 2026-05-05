namespace GrowDiary.Web.Api.Contracts;

/// <summary>
/// Einheitliches Error-Format für alle API-Endpoints.
/// Bewusst schlank - kein RFC 7807 / ProblemDetails.
/// </summary>
public sealed record ApiError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null
);
