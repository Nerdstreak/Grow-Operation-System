using System.Text.Json;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Ai;

namespace GrowDiary.Web.Tests.Services.Ai;

/// <summary>
/// The two dialects, asserted separately. Anthropic is not OpenAI-compatible, and the
/// difference is not cosmetic: another auth header, the system prompt as its own field,
/// another reply shape. Getting one of those wrong means a pasted key simply never works.
/// </summary>
public sealed class AiRequestFactoryTests
{
    private static AiSettings OpenAi(string? baseUrl = "https://api.openai.com/v1") => new()
    {
        Provider = AiProvider.OpenAiCompatible,
        BaseUrl = baseUrl,
        Model = "gpt-4o-mini",
        ApiKey = "sk-test",
        Enabled = true,
    };

    private static AiSettings Anthropic(string? baseUrl = null) => new()
    {
        Provider = AiProvider.Anthropic,
        BaseUrl = baseUrl,
        Model = "claude-sonnet-5",
        ApiKey = "sk-ant-test",
        Enabled = true,
    };

    [Fact]
    public void OpenAi_UsesBearerAuthAndASystemMessage()
    {
        var shape = AiRequestFactory.Build(OpenAi(), "REGELN", "FRAGE");

        Assert.Equal("https://api.openai.com/v1/chat/completions", shape.Uri.ToString());
        Assert.Equal("Bearer sk-test", shape.Headers["Authorization"]);

        using var body = JsonDocument.Parse(shape.Body);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("REGELN", messages[0].GetProperty("content").GetString());
    }

    [Fact]
    public void Anthropic_UsesItsOwnHeadersAndATopLevelSystemField()
    {
        var shape = AiRequestFactory.Build(Anthropic(), "REGELN", "FRAGE");

        Assert.Equal("https://api.anthropic.com/v1/messages", shape.Uri.ToString());
        Assert.Equal("sk-ant-test", shape.Headers["x-api-key"]);
        Assert.Equal("2023-06-01", shape.Headers["anthropic-version"]);
        Assert.False(shape.Headers.ContainsKey("Authorization"));

        using var body = JsonDocument.Parse(shape.Body);
        Assert.Equal("REGELN", body.RootElement.GetProperty("system").GetString());

        // The system prompt must NOT also appear as a message — Anthropic rejects that role.
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal(1, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
    }

    [Fact]
    public void Anthropic_NeedsNoAddress_BecauseThereIsOnlyOne()
    {
        Assert.True(Anthropic().IsConfigured);
        Assert.Equal("https://api.anthropic.com/v1/messages", AiRequestFactory.AnthropicUri(null).ToString());
    }

    [Theory]
    [InlineData("http://localhost:11434/v1", "http://localhost:11434/v1/chat/completions")]
    [InlineData("http://localhost:11434", "http://localhost:11434/v1/chat/completions")]
    [InlineData("http://localhost:11434/v1/", "http://localhost:11434/v1/chat/completions")]
    [InlineData("http://localhost:11434/v1/chat/completions", "http://localhost:11434/v1/chat/completions")]
    public void TheAddressIsForgivingAboutHowMuchOfThePathWasPasted(string entered, string expected)
    {
        Assert.Equal(expected, AiRequestFactory.OpenAiUri(entered).ToString());
    }

    [Fact]
    public void OpenAiReply_IsRead()
    {
        var body = """{"choices":[{"message":{"role":"assistant","content":"Antwort"}}]}""";

        Assert.Equal("Antwort", AiRequestFactory.ReadContent(AiProvider.OpenAiCompatible, body));
    }

    [Fact]
    public void AnthropicReply_IsRead()
    {
        var body = """{"content":[{"type":"text","text":"Antwort"}],"stop_reason":"end_turn"}""";

        Assert.Equal("Antwort", AiRequestFactory.ReadContent(AiProvider.Anthropic, body));
    }

    [Fact]
    public void AnthropicReply_SkipsNonTextBlocks()
    {
        var body = """{"content":[{"type":"thinking","thinking":"…"},{"type":"text","text":"Antwort"}]}""";

        Assert.Equal("Antwort", AiRequestFactory.ReadContent(AiProvider.Anthropic, body));
    }

    [Fact]
    public void AReplyInTheOtherProvidersShape_IsNotMistakenForAnAnswer()
    {
        // Picking the wrong provider must fail visibly rather than return nonsense.
        var openAiBody = """{"choices":[{"message":{"content":"Antwort"}}]}""";

        Assert.Null(AiRequestFactory.ReadContent(AiProvider.Anthropic, openAiBody));
    }

    [Fact]
    public void WithoutAKey_NoAuthHeaderIsSent()
    {
        // Local endpoints usually reject a bogus Authorization header outright.
        var local = OpenAi("http://localhost:11434/v1");
        local.ApiKey = null;

        Assert.Empty(AiRequestFactory.Build(local, "s", "u").Headers);
    }
}
