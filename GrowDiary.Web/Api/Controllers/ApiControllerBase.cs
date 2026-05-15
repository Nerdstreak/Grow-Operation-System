using GrowDiary.Web.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult ValidationError(string message = "Eingaben konnten nicht validiert werden.")
        => BadRequest(ApiErrorFactory.Validation(message, ToFieldErrors(), TraceId));

    protected ActionResult BadRequestError(string code, string message)
        => BadRequest(ApiErrorFactory.BadRequest(code, message, TraceId));

    protected ActionResult NotFoundError(string code, string message)
        => NotFound(ApiErrorFactory.NotFound(code, message, TraceId));

    protected ActionResult ConflictError(string code, string message)
        => Conflict(ApiErrorFactory.Conflict(code, message, TraceId));

    protected ActionResult ForbiddenError(string code, string message)
        => StatusCode(StatusCodes.Status403Forbidden, ApiErrorFactory.Forbidden(code, message, TraceId));

    private string? TraceId => HttpContext?.TraceIdentifier;

    private IReadOnlyDictionary<string, string[]> ToFieldErrors()
        => ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => string.IsNullOrWhiteSpace(entry.Key) ? "$" : entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Ungueltiger Wert." : error.ErrorMessage)
                    .Distinct()
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
}
