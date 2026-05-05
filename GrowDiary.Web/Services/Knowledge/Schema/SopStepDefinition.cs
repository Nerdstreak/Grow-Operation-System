using System.Text.Json.Serialization;

namespace GrowDiary.Web.Services.Knowledge.Schema;

public sealed class SopStepDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("stepType")]
    public string StepType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("waitMinutes")]
    public int? WaitMinutes { get; set; }

    [JsonPropertyName("subSopId")]
    public string? SubSopId { get; set; }

    [JsonPropertyName("expectedInputs")]
    public List<string>? ExpectedInputs { get; set; }

    [JsonPropertyName("photoRequired")]
    public bool PhotoRequired { get; set; } = false;

    [JsonPropertyName("photoRecommended")]
    public bool PhotoRecommended { get; set; } = false;
}
