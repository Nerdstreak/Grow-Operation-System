using GrowDiary.Web.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult ValidationError(string message = "Eingaben konnten nicht validiert werden.")
        => BadRequest(new ApiError("validation_failed", message, ToFieldErrors()));

    protected ActionResult NotFoundError(string code, string message)
        => NotFound(new ApiError(code, message));

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
