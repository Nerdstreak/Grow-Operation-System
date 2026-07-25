using GrowDiary.Web.Services.Ai;

namespace GrowDiary.Web.Tests.Services.Ai;

public sealed class AiAnswerParserTests
{
    private static AiContext ContextWith(params string[] ids) => new()
    {
        Knowledge = ids
            .Select(id => new AiKnowledgeItem(id, "Regel", $"Titel {id}", "Text", "Growplan", "Punkt 6", "/docs/x.pdf"))
            .ToList(),
    };

    [Fact]
    public void ClaimCitingSomethingWeSent_IsGrounded()
    {
        var raw = """
            {"antwort":"pH nicht nachregeln.","aussagen":[
              {"text":"pH 6,3 liegt im Band und braucht keine Korrektur.","quelle":"guidance:ph-drift-band"}
            ],"offen":""}
            """;

        var answer = AiAnswerParser.Parse(raw, ContextWith("guidance:ph-drift-band"));

        var claim = Assert.Single(answer.Claims);
        Assert.True(claim.Grounded);
        Assert.Equal("Titel guidance:ph-drift-band", claim.SourceTitle);
        Assert.Equal("/docs/x.pdf", claim.SourceUrl);
        Assert.Equal(0, answer.UngroundedCount);
        Assert.Equal("pH nicht nachregeln.", answer.Summary);
    }

    [Fact]
    public void InventedCitation_IsMarkedUngrounded()
    {
        // The whole point of the check: an id we never handed over cannot have been read
        // anywhere, so the claim must not be presented as coming from the user's documents.
        var raw = """
            {"antwort":"x","aussagen":[
              {"text":"pH sofort auf 5,8 senken.","quelle":"guidance:gibt-es-nicht"}
            ]}
            """;

        var answer = AiAnswerParser.Parse(raw, ContextWith("guidance:ph-drift-band"));

        var claim = Assert.Single(answer.Claims);
        Assert.False(claim.Grounded);
        Assert.Null(claim.SourceTitle);
        Assert.Equal(1, answer.UngroundedCount);
        Assert.True(answer.IsUngrounded);
    }

    [Fact]
    public void ClaimWithoutAnyCitation_CountsAsUngrounded()
    {
        var raw = """{"antwort":"x","aussagen":[{"text":"Mehr Licht hilft immer."}]}""";

        var answer = AiAnswerParser.Parse(raw, ContextWith("guidance:ph-drift-band"));

        Assert.False(Assert.Single(answer.Claims).Grounded);
    }

    [Theory]
    [InlineData("```json\n{\"antwort\":\"a\",\"aussagen\":[]}\n```")]
    [InlineData("Gerne! {\"antwort\":\"a\",\"aussagen\":[]} Sonst noch was?")]
    public void JsonWrappedInProseOrFences_IsStillRead(string raw)
    {
        // Models add fences and pleasantries however clearly they are told not to.
        var answer = AiAnswerParser.Parse(raw, ContextWith("x"));

        Assert.Equal("a", answer.Summary);
    }

    [Fact]
    public void ReplyThatIsNotJsonAtAll_KeepsTheTextButClaimsNothing()
    {
        var answer = AiAnswerParser.Parse("Ich würde den pH senken.", ContextWith("x"));

        Assert.Equal("Ich würde den pH senken.", answer.Summary);
        Assert.Empty(answer.Claims);
        Assert.False(answer.IsUngrounded); // nothing was claimed, so nothing is unfounded
    }

    [Fact]
    public void UnansweredQuestion_IsCarriedThrough()
    {
        var raw = """{"antwort":"a","aussagen":[],"offen":"Zur Sorte steht nichts in den Unterlagen."}""";

        var answer = AiAnswerParser.Parse(raw, ContextWith("x"));

        Assert.Equal("Zur Sorte steht nichts in den Unterlagen.", answer.Unanswered);
    }
}
