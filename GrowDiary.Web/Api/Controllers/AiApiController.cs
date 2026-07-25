using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Ai;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Api.Controllers;

/// <summary>
/// The AI assistant: its connection, what would be sent, and asking it something.
///
/// Reading data is the only thing that happens without confirmation. Everything the
/// assistant proposes is returned as text for the user to act on — no endpoint here
/// changes a setpoint, a task or a schedule.
/// </summary>
[ApiController]
[Route("api/ai")]
[Produces("application/json")]
public sealed class AiApiController : ApiControllerBase
{
    private readonly AiSettingsRepository _settings;
    private readonly AiContextBuilder _contextBuilder;
    private readonly AiClient _client;

    public AiApiController(AiSettingsRepository settings, AiContextBuilder contextBuilder, AiClient client)
    {
        _settings = settings;
        _contextBuilder = contextBuilder;
        _client = client;
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(AiSettingsDto), StatusCodes.Status200OK)]
    public ActionResult<AiSettingsDto> GetSettings() => Ok(ToDto(_settings.GetAiSettings()));

    [HttpPut("settings")]
    [ProducesResponseType(typeof(AiSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public ActionResult<AiSettingsDto> SaveSettings([FromBody] AiSettingsRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.BaseUrl)
            && !Uri.TryCreate(request.BaseUrl.Trim(), UriKind.Absolute, out _))
        {
            return BadRequestError("ai_bad_url", "Die Adresse ist keine gültige URL.");
        }

        var settings = new AiSettings
        {
            BaseUrl = request.BaseUrl,
            Model = request.Model,
            Enabled = request.Enabled,
            AllowPhotos = request.AllowPhotos,
            ApiKey = request.ApiKey,
        };

        // Null means "not touched" — the UI never gets the key back, so it cannot resend it.
        _settings.SaveAiSettings(settings, replaceApiKey: request.ApiKey is not null);
        return Ok(ToDto(_settings.GetAiSettings()));
    }

    /// <summary>
    /// What would be sent for this grow. Deliberately built with the same code path as a
    /// real request, so this is the payload rather than a description of it.
    /// </summary>
    [HttpGet("preview/{growId:int}")]
    [ProducesResponseType(typeof(AiSendPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public ActionResult<AiSendPreviewDto> Preview(int growId, [FromQuery] string? question)
    {
        var context = _contextBuilder.BuildForGrow(growId);
        if (context is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {growId} existiert nicht.");
        }

        var settings = _settings.GetAiSettings();
        var asked = string.IsNullOrWhiteSpace(question) ? "(deine Frage)" : question;

        return Ok(new AiSendPreviewDto(
            GrowId: growId,
            WouldLeaveTheHouse: settings.IsConfigured && !settings.IsLocalEndpoint,
            Endpoint: settings.BaseUrl,
            GrowFacts: context.GrowFacts,
            Measurements: context.Measurements,
            OpenDeviations: context.OpenDeviations,
            Knowledge: context.Knowledge.Select(ToDto).ToList(),
            SystemMessage: AiPrompt.SystemMessage,
            UserMessage: AiPrompt.UserMessage(context, asked)));
    }

    [HttpPost("ask")]
    [ProducesResponseType(typeof(AiAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiAnswerDto>> Ask([FromBody] AiAskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequestError("ai_no_question", "Es wurde keine Frage gestellt.");
        }

        var settings = _settings.GetAiSettings();
        if (!settings.IsUsable)
        {
            return BadRequestError("ai_not_configured", "Es ist kein KI-Modell eingerichtet.");
        }

        var context = _contextBuilder.BuildForGrow(request.GrowId);
        if (context is null)
        {
            return NotFoundError("grow_not_found", $"Grow mit Id {request.GrowId} existiert nicht.");
        }

        var result = await _client.CompleteAsync(
            settings,
            AiPrompt.SystemMessage,
            AiPrompt.UserMessage(context, request.Question),
            cancellationToken);

        if (!result.Ok)
        {
            return BadRequestError(result.ErrorCode ?? "ai_failed", result.ErrorMessage ?? "Die Anfrage ist fehlgeschlagen.");
        }

        var answer = AiAnswerParser.Parse(result.Raw!, context);
        return Ok(new AiAnswerDto(
            answer.Summary,
            answer.Claims.Select(claim => new AiClaimDto(
                claim.Text, claim.SourceId, claim.Grounded, claim.SourceTitle, claim.SourceUrl)).ToList(),
            answer.Unanswered,
            answer.UngroundedCount,
            answer.IsUngrounded));
    }

    /// <summary>Checks the connection with a throwaway question that carries no grow data.</summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(AiConnectionTestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiConnectionTestDto>> Test(CancellationToken cancellationToken)
    {
        var settings = _settings.GetAiSettings();
        if (!settings.IsConfigured)
        {
            return Ok(new AiConnectionTestDto(false, "ai_not_configured", "Adresse und Modell fehlen noch.", null));
        }

        // Enabled is not required to test — you want to try before switching it on.
        var probe = settings.IsUsable ? settings : Clone(settings);
        var result = await _client.CompleteAsync(
            probe,
            "Antworte mit genau einem Wort.",
            "Sag: bereit",
            cancellationToken);

        return Ok(result.Ok
            ? new AiConnectionTestDto(true, null, null, result.Raw?.Trim())
            : new AiConnectionTestDto(false, result.ErrorCode, result.ErrorMessage, null));
    }

    private static AiSettings Clone(AiSettings settings) => new()
    {
        BaseUrl = settings.BaseUrl,
        ApiKey = settings.ApiKey,
        Model = settings.Model,
        AllowPhotos = settings.AllowPhotos,
        Enabled = true,
    };

    private static AiSettingsDto ToDto(AiSettings settings) => new(
        settings.BaseUrl,
        settings.Model,
        settings.Enabled,
        settings.AllowPhotos,
        HasApiKey: !string.IsNullOrWhiteSpace(settings.ApiKey),
        settings.IsLocalEndpoint,
        settings.IsConfigured);

    private static AiKnowledgeItemDto ToDto(AiKnowledgeItem item) => new(
        item.Id, item.Kind, item.Title, item.Body, item.SourceTitle, item.SourceReference, item.SourceUrl);
}
