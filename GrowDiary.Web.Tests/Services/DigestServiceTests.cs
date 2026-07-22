using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Services;

public sealed class DigestServiceTests
{
    private static DigestService.DigestTent Ok(string name, params (string, string)[] metrics)
        => new(name, 0, metrics);

    private static DigestService.DigestTent Issues(string name, int count)
        => new(name, count, System.Array.Empty<(string, string)>());

    [Fact]
    public void NoTents_ReturnsHint()
        => Assert.Equal("Noch kein Zelt eingerichtet.", DigestService.BuildMessage(System.Array.Empty<DigestService.DigestTent>(), detailed: false));

    [Fact]
    public void Summary_AllOk_SaysGreen()
    {
        var message = DigestService.BuildMessage(new[] { Ok("Zelt-A"), Ok("Zelt-B") }, detailed: false);

        Assert.Contains("Alles im grünen Bereich", message);
        Assert.Contains("✅ Zelt-A", message);
        Assert.Contains("✅ Zelt-B", message);
    }

    [Fact]
    public void Summary_WithIssues_CountsThem()
    {
        var message = DigestService.BuildMessage(new[] { Ok("Zelt-A"), Issues("Zelt-B", 2) }, detailed: false);

        Assert.Contains("2 offene(r) Hinweis(e)", message);
        Assert.Contains("⚠️ Zelt-B", message);
    }

    [Fact]
    public void Summary_OmitsMetrics()
    {
        var message = DigestService.BuildMessage(new[] { Ok("Zelt-A", ("pH", "6.1")) }, detailed: false);

        Assert.DoesNotContain("pH 6.1", message);
    }

    [Fact]
    public void Detailed_IncludesMetrics()
    {
        var message = DigestService.BuildMessage(new[] { Ok("Zelt-A", ("pH", "6.1"), ("EC", "1.8")) }, detailed: true);

        Assert.Contains("pH 6.1 · EC 1.8", message);
    }
}
