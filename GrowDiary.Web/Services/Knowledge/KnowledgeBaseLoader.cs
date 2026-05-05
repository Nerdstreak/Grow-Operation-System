using System.Text.Json;
using System.Text.Json.Serialization;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services.Knowledge.Schema;

namespace GrowDiary.Web.Services.Knowledge;

public sealed class KnowledgeBaseLoader
{
    private readonly AppPaths _paths;
    private readonly ILogger<KnowledgeBaseLoader> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private IReadOnlyList<TreatmentDefinition> _treatments = Array.Empty<TreatmentDefinition>();
    private IReadOnlyList<SopDefinition> _sops = Array.Empty<SopDefinition>();
    private IReadOnlyList<NutrientProgramDefinition> _nutrientPrograms = Array.Empty<NutrientProgramDefinition>();
    private IReadOnlyList<SetpointDefinition> _setpoints = Array.Empty<SetpointDefinition>();
    private IReadOnlyList<PathogenDefinition> _pathogens = Array.Empty<PathogenDefinition>();
    private IReadOnlyList<SymptomDefinition> _symptoms = Array.Empty<SymptomDefinition>();
    private IReadOnlyList<WearTemplateDefinition> _wearTemplates = Array.Empty<WearTemplateDefinition>();

    public IReadOnlyList<TreatmentDefinition> Treatments => _treatments;
    public IReadOnlyList<SopDefinition> Sops => _sops;
    public IReadOnlyList<NutrientProgramDefinition> NutrientPrograms => _nutrientPrograms;
    public IReadOnlyList<SetpointDefinition> Setpoints => _setpoints;
    public IReadOnlyList<PathogenDefinition> Pathogens => _pathogens;
    public IReadOnlyList<SymptomDefinition> Symptoms => _symptoms;
    public IReadOnlyList<WearTemplateDefinition> WearTemplates => _wearTemplates;

    public KnowledgeBaseLoader(AppPaths paths, ILogger<KnowledgeBaseLoader> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public void Initialize()
    {
        EnsureKnowledgeDirectory();
        Reload();
    }

    public void Reload()
    {
        _treatments = LoadCategory<TreatmentDefinition>("treatments");
        _sops = LoadCategory<SopDefinition>("sops");
        _nutrientPrograms = LoadCategory<NutrientProgramDefinition>("nutrient-programs");
        _setpoints = LoadCategory<SetpointDefinition>("setpoints");
        _pathogens = LoadCategory<PathogenDefinition>("pathogens");
        _symptoms = LoadCategory<SymptomDefinition>("symptoms");
        _wearTemplates = LoadCategory<WearTemplateDefinition>("wear");

        _logger.LogInformation(
            "Knowledge-Base geladen: {TC} Treatments, {SC} SOPs, {NC} Programme, {SetC} Setpoints, {PC} Pathogens, {SymC} Symptoms, {WC} Wear-Templates",
            _treatments.Count, _sops.Count, _nutrientPrograms.Count,
            _setpoints.Count, _pathogens.Count, _symptoms.Count, _wearTemplates.Count);
    }

    private void EnsureKnowledgeDirectory()
    {
        var knowledgeDataPath = _paths.KnowledgeDataPath;
        Directory.CreateDirectory(knowledgeDataPath);

        var hasContent = Directory
            .EnumerateFiles(knowledgeDataPath, "*.json", SearchOption.AllDirectories)
            .Any();

        if (!hasContent)
        {
            var defaultsPath = _paths.KnowledgeDefaultsPath;
            if (Directory.Exists(defaultsPath))
            {
                foreach (var sourceFile in Directory.EnumerateFiles(defaultsPath, "*.json", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(defaultsPath, sourceFile);
                    var destFile = Path.Combine(knowledgeDataPath, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    File.Copy(sourceFile, destFile, overwrite: false);
                }
                _logger.LogInformation("Knowledge-Base-Defaults nach {Path} kopiert.", knowledgeDataPath);
            }
            else
            {
                _logger.LogWarning("Knowledge-Defaults-Verzeichnis nicht gefunden: {Path}", defaultsPath);
            }
        }
    }

    private IReadOnlyList<T> LoadCategory<T>(string folder) where T : KnowledgeFileMetadata
    {
        var categoryPath = Path.Combine(_paths.KnowledgeDataPath, folder);
        if (!Directory.Exists(categoryPath))
        {
            _logger.LogWarning("Knowledge-Kategorie-Ordner nicht gefunden: {Path}", categoryPath);
            return Array.Empty<T>();
        }

        var results = new List<T>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(categoryPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json = File.ReadAllText(file);
                var item = JsonSerializer.Deserialize<T>(json, JsonOptions);

                if (item == null)
                {
                    _logger.LogError("Konnte {File} nicht deserialisieren (null).", file);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.SchemaVersion))
                {
                    _logger.LogError("Fehlende schemaVersion in {File} — Datei übersprungen.", file);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    _logger.LogError("Fehlende id in {File} — Datei übersprungen.", file);
                    continue;
                }

                if (!seenIds.Add(item.Id))
                {
                    _logger.LogError("Doppelte ID '{Id}' in {File} — Datei übersprungen.", item.Id, file);
                    continue;
                }

                if (item.Id.StartsWith("example-"))
                {
                    _logger.LogWarning(
                        "Beispiel-Datei {Id} in {Category} gefunden. Bitte App_Data/knowledge/ leeren für Migration.",
                        item.Id, folder);
                }

                results.Add(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden von {File} — übersprungen.", file);
            }
        }

        return results.AsReadOnly();
    }
}
