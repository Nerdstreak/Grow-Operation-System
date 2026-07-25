using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services.Ai;

/// <summary>
/// The rules the assistant has to be able to find. Each of these is a place where common
/// growing advice is the opposite of the growplan — exactly what a language model would
/// otherwise answer from its own training. If a rule went missing from the knowledge base,
/// the assistant would confidently give the wrong answer, so their presence is asserted.
/// </summary>
public sealed class GuidanceCatalogTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly KnowledgeBaseLoader _loader;

    public GuidanceCatalogTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "Guidance_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        CopyDefaults(Path.Combine(FindProjectRoot(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _tempRoot);

        _loader = new KnowledgeBaseLoader(new AppPaths(_tempRoot), NullLogger<KnowledgeBaseLoader>.Instance);
        _loader.Initialize();
    }

    [Theory]
    [InlineData("ph-drift-band")]
    [InlineData("ec-keep-hungry")]
    [InlineData("ppfd-requires-co2")]
    [InlineData("orp-rises-with-stage")]
    [InlineData("weekly-water-change")]
    public void TheLoadBearingRulesExist(string id)
    {
        Assert.Contains(_loader.Guidance, rule => rule.Id == id);
    }

    [Fact]
    public void EveryRule_HasATextAndASource()
    {
        Assert.NotEmpty(_loader.Guidance);
        foreach (var rule in _loader.Guidance)
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Title), $"{rule.Id}: kein Titel");
            Assert.False(string.IsNullOrWhiteSpace(rule.Rule), $"{rule.Id}: kein Regeltext");
            Assert.NotEmpty(rule.Sources);
            Assert.All(rule.Sources, source => Assert.False(string.IsNullOrWhiteSpace(source.Title)));
        }
    }

    [Fact]
    public void ThePhRule_SaysNotToChaseAndNamesTheOppositeAdvice()
    {
        var rule = Assert.Single(_loader.Guidance, item => item.Id == "ph-drift-band");

        // The band and both correction thresholds have to be in the text itself: the model
        // is told to answer from this, so anything missing here is missing everywhere.
        Assert.Contains("5,8", rule.Rule);
        Assert.Contains("6,2", rule.Rule);
        Assert.Contains("5,5", rule.Rule);
        Assert.Contains("6,5", rule.Rule);

        // The wrong answer travels with the right one, because it is the answer a model
        // reaches for by default.
        Assert.False(string.IsNullOrWhiteSpace(rule.CommonMistake));
        Assert.Contains("5,8", rule.CommonMistake!);
    }

    [Fact]
    public void ThePpfdRule_CarriesTheCo2Caveat()
    {
        var rule = Assert.Single(_loader.Guidance, item => item.Id == "ppfd-requires-co2");

        Assert.Contains("CO₂", rule.Rule);
        Assert.Contains("800", rule.Rule);
        Assert.Contains("900", rule.Rule);
        Assert.Contains("30 cm", rule.Rule);
    }

    [Fact]
    public void RuleIdsAreUnique()
    {
        // Ids are citation keys — a duplicate would make a citation ambiguous.
        var duplicates = _loader.Guidance
            .GroupBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* temp dir */ }
    }

    private static void CopyDefaults(string source, string tempRoot)
    {
        var destination = Path.Combine(tempRoot, "wwwroot", "knowledge-defaults");
        foreach (var file in Directory.EnumerateFiles(source, "*.json", SearchOption.AllDirectories))
        {
            var target = file.Replace(source, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string FindProjectRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory, "GrowDiary.Web")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
