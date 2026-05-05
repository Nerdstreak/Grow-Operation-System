using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services.Knowledge;

public sealed class KnowledgeBaseLoaderTests : IDisposable
{
    private readonly string _tempRoot;

    public KnowledgeBaseLoaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "KBLoaderTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private AppPaths MakePaths() => new(_tempRoot);

    private KnowledgeBaseLoader MakeLoader(ILogger<KnowledgeBaseLoader>? logger = null) =>
        new(MakePaths(), logger ?? NullLogger<KnowledgeBaseLoader>.Instance);

    private void WriteDefaultJson(string category, string filename, string json)
    {
        var dir = Path.Combine(_tempRoot, "wwwroot", "knowledge-defaults", category);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, filename), json);
    }

    private void WriteDataJson(string category, string filename, string json)
    {
        var dir = Path.Combine(_tempRoot, "App_Data", "knowledge", category);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, filename), json);
    }

    private const string ValidTreatmentJson = """
        {
          "schemaVersion": "1.0",
          "id": "example-treatment",
          "name": "Test",
          "type": "FoliarSpray",
          "targetSymptoms": [],
          "dosage": { "standard": "1 ml/L" },
          "application": { "method": "Spray" },
          "restrictions": [],
          "conflicts": [],
          "hardwareRequirements": [],
          "sources": [],
          "difficulty": "Easy",
          "expectedTimeToEffect": "24h",
          "tags": []
        }
        """;

    [Fact]
    public void Loader_LoadsExampleTreatment_Successfully()
    {
        WriteDataJson("treatments", "treatment.json", ValidTreatmentJson);

        var loader = MakeLoader();
        loader.Reload();

        Assert.Single(loader.Treatments);
        Assert.Equal("example-treatment", loader.Treatments[0].Id);
        Assert.Equal("Test", loader.Treatments[0].Name);
    }

    [Fact]
    public void Loader_LoadsAllCategories_NoErrors()
    {
        WriteDataJson("treatments", "t.json", ValidTreatmentJson);
        WriteDataJson("sops", "s.json", """{"schemaVersion":"1.0","id":"sop-1","name":"SOP","type":"Linear","applicableSetups":[],"triggers":[],"requiredMaterials":[],"steps":[],"sources":[]}""");
        WriteDataJson("nutrient-programs", "n.json", """{"schemaVersion":"1.0","id":"np-1","name":"NP","manufacturer":"X","category":"Mineral","summary":"","bestFor":"","waterGuidance":"","phGuidance":"","ecGuidance":"","scheduleStyle":"","officialHighlights":"","practiceNotes":"","stages":[],"tips":[],"searchTerms":[]}""");
        WriteDataJson("setpoints", "sp.json", """{"schemaVersion":"1.0","id":"sp-1","name":"SP","systemType":"RDWC","stages":{}}""");
        WriteDataJson("pathogens", "p.json", """{"schemaVersion":"1.0","id":"path-1","name":"PM","category":"Fungal","symptoms":[],"treatable":true,"riskLevel":"Medium","notes":""}""");
        WriteDataJson("symptoms", "sym.json", """{"schemaVersion":"1.0","id":"sym-1","name":"Sym","category":"Leaf","possibleCauses":[],"suggestedTreatmentIds":[],"suggestedSopIds":[],"diagnosticChecks":[]}""");
        WriteDataJson("wear", "w.json", """{"schemaVersion":"1.0","id":"wear-1","name":"Sensor","category":"Sensor","expectedLifespanDays":365,"replacementTriggers":[]}""");

        var loader = MakeLoader();
        loader.Reload();

        Assert.Single(loader.Treatments);
        Assert.Single(loader.Sops);
        Assert.Single(loader.NutrientPrograms);
        Assert.Single(loader.Setpoints);
        Assert.Single(loader.Pathogens);
        Assert.Single(loader.Symptoms);
        Assert.Single(loader.WearTemplates);
    }

    [Fact]
    public void Loader_HandlesMissingFolder_LogsWarning()
    {
        // No data folders created — App_Data/knowledge exists but no category subdirs
        var dataRoot = Path.Combine(_tempRoot, "App_Data", "knowledge");
        Directory.CreateDirectory(dataRoot);

        var capturing = new CapturingLogger<KnowledgeBaseLoader>();
        var loader = MakeLoader(capturing);
        loader.Reload();

        Assert.Empty(loader.Treatments);
        Assert.Contains(capturing.Messages, m => m.Contains("Knowledge-Kategorie-Ordner nicht gefunden"));
    }

    [Fact]
    public void Loader_HandlesInvalidJson_SkipsFileAndLogs()
    {
        WriteDataJson("treatments", "bad.json", "{ this is not valid json }");
        WriteDataJson("treatments", "good.json", ValidTreatmentJson);

        var capturing = new CapturingLogger<KnowledgeBaseLoader>();
        var loader = MakeLoader(capturing);
        loader.Reload();

        Assert.Single(loader.Treatments);
        Assert.Contains(capturing.Messages, m => m.Contains("bad.json"));
    }

    [Fact]
    public void Loader_DetectsDuplicateIds_LogsError()
    {
        var second = ValidTreatmentJson; // same id: "example-treatment"
        WriteDataJson("treatments", "a.json", ValidTreatmentJson);
        WriteDataJson("treatments", "b.json", second);

        var capturing = new CapturingLogger<KnowledgeBaseLoader>();
        var loader = MakeLoader(capturing);
        loader.Reload();

        Assert.Single(loader.Treatments);
        Assert.Contains(capturing.Messages, m => m.Contains("Doppelte ID"));
    }

    [Fact]
    public void Loader_ValidatesSchemaVersion_RejectsMissing()
    {
        var noSchema = """{"id":"t1","name":"X","type":"FoliarSpray","targetSymptoms":[],"dosage":{"standard":"1ml"},"application":{"method":"S"},"restrictions":[],"conflicts":[],"hardwareRequirements":[],"sources":[],"difficulty":"Easy","expectedTimeToEffect":"24h","tags":[]}""";
        WriteDataJson("treatments", "no-schema.json", noSchema);

        var capturing = new CapturingLogger<KnowledgeBaseLoader>();
        var loader = MakeLoader(capturing);
        loader.Reload();

        Assert.Empty(loader.Treatments);
        Assert.Contains(capturing.Messages, m => m.Contains("schemaVersion"));
    }

    [Fact]
    public void Loader_FirstStart_CopiesDefaultsToAppData()
    {
        WriteDefaultJson("treatments", "example-treatment.json", ValidTreatmentJson);

        var loader = MakeLoader();
        loader.Initialize();

        var copied = Path.Combine(_tempRoot, "App_Data", "knowledge", "treatments", "example-treatment.json");
        Assert.True(File.Exists(copied));
        Assert.Single(loader.Treatments);
    }

    [Fact]
    public void Loader_Reload_PicksUpNewFiles()
    {
        WriteDataJson("treatments", "a.json", ValidTreatmentJson);

        var loader = MakeLoader();
        loader.Reload();
        Assert.Single(loader.Treatments);

        var second = ValidTreatmentJson.Replace("\"example-treatment\"", "\"treatment-two\"");
        WriteDataJson("treatments", "b.json", second);

        loader.Reload();
        Assert.Equal(2, loader.Treatments.Count);
    }
}

/// <summary>Captures log messages for assertion in tests.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }
}
