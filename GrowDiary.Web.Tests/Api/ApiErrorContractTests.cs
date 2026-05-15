using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GrowDiary.Web.Tests.Api;

public sealed class ApiErrorContractTests
{
    [Fact]
    public void ApiErrorFactory_NormalizesValidationErrors()
    {
        var error = ApiErrorFactory.Validation(
            fieldErrors: new Dictionary<string, string[]>
            {
                ["Name"] = new[] { " Name darf nicht leer sein. ", "Name darf nicht leer sein." }
            },
            traceId: "trace-1");

        Assert.Equal(ApiErrorFactory.SchemaVersion, error.SchemaVersion);
        Assert.Equal("validation_failed", error.Code);
        Assert.Equal(StatusCodes.Status400BadRequest, error.Status);
        Assert.Equal("trace-1", error.TraceId);
        Assert.NotNull(error.FieldErrors);
        var fieldError = Assert.Single(error.FieldErrors!["Name"]);
        Assert.Equal("Name darf nicht leer sein.", fieldError);
    }

    [Fact]
    public void ApiControllerBase_ValidationErrorUsesUniformApiErrorFormat()
    {
        var controller = new TestApiController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-2" }
        };
        controller.ModelState.AddModelError("systemId", "HydroSetup ist erforderlich.");

        var result = controller.InvokeValidationError();

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var error = Assert.IsType<ApiError>(objectResult.Value);
        Assert.Equal(ApiErrorFactory.SchemaVersion, error.SchemaVersion);
        Assert.Equal("validation_failed", error.Code);
        Assert.Equal(StatusCodes.Status400BadRequest, error.Status);
        Assert.Equal("trace-2", error.TraceId);
        Assert.NotNull(error.FieldErrors);
        Assert.Equal("HydroSetup ist erforderlich.", Assert.Single(error.FieldErrors!["systemId"]));
    }

    private sealed class TestApiController : ApiControllerBase
    {
        public ActionResult InvokeValidationError() => ValidationError();
    }
}
