using System.Globalization;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;
using GrowDiary.Web.Services.Knowledge.Schema;

namespace GrowDiary.Web.Services.Ai;

/// <summary>
/// Puts the relevant pages on the table for one question.
///
/// A language model brings its own opinions about growing, and for RDWC they are often the
/// opposite of this growplan — chase the pH, feed when EC drops, more light is more yield.
/// So the answer cannot come from the model's memory: it has to come from material we hand
/// over on every single request. This class selects that material.
///
/// Selection is deliberately generous. The whole knowledge base is a few dozen small files,
/// so a stage-filtered slice fits in the request with room to spare — which buys us
/// simplicity (no embeddings, no vector store, nothing to keep in sync) at no real cost.
/// </summary>
public sealed class AiContextBuilder
{
    private const int MeasurementsToInclude = 7;

    private readonly GrowRepository _repository;
    private readonly KnowledgeBaseLoader _knowledge;
    private readonly TargetValueService _targets;
    private readonly DeviationAnalyzerService _deviations;

    public AiContextBuilder(
        GrowRepository repository,
        KnowledgeBaseLoader knowledge,
        TargetValueService targets,
        DeviationAnalyzerService deviations)
    {
        _repository = repository;
        _knowledge = knowledge;
        _targets = targets;
        _deviations = deviations;
    }

    public AiContext? BuildForGrow(int growId)
    {
        var grow = _repository.GetGrow(growId);
        if (grow is null)
        {
            return null;
        }

        var measurements = _repository.GetMeasurementsForGrow(growId)
            .OrderByDescending(measurement => measurement.TakenAt)
            .ToList();

        var stage = measurements.FirstOrDefault()?.Stage ?? GrowStage.Veg;
        var context = new AiContext();

        AddGrowFacts(context, grow, stage);
        AddMeasurements(context, measurements);
        AddDeviations(context, grow, measurements);
        AddSetpoints(context, grow, stage);
        AddGuidance(context, grow, stage);
        AddSops(context, grow);

        return context;
    }

    private static void AddGrowFacts(AiContext context, GrowRun grow, GrowStage stage)
    {
        context.GrowFacts.Add($"Grow: {grow.Name}");
        if (!string.IsNullOrWhiteSpace(grow.Strain))
        {
            var breeder = string.IsNullOrWhiteSpace(grow.Breeder) ? string.Empty : $" ({grow.Breeder})";
            context.GrowFacts.Add($"Sorte: {grow.Strain}{breeder}");
        }

        context.GrowFacts.Add($"System: {grow.HydroStyle} / {grow.MediumType}");
        context.GrowFacts.Add($"Phase: {stage}");
        if (!string.IsNullOrWhiteSpace(grow.TentName))
        {
            context.GrowFacts.Add($"Zelt: {grow.TentName}");
        }
    }

    private static void AddMeasurements(AiContext context, List<Measurement> measurements)
    {
        // Oldest first: a trend reads naturally that way, and the trend is usually the point.
        foreach (var measurement in measurements.Take(MeasurementsToInclude).AsEnumerable().Reverse())
        {
            var parts = new List<string> { measurement.TakenAt.ToString("dd.MM. HH:mm", AppCulture.German) };
            Add(parts, "pH", measurement.ReservoirPh, "0.00");
            Add(parts, "EC", measurement.ReservoirEc, "0.00");
            Add(parts, "ORP", measurement.OrpMv, "0", "mV");
            Add(parts, "Wasser", measurement.ReservoirWaterTempC, "0.0", "°C");
            Add(parts, "Luft", measurement.AirTemperatureC, "0.0", "°C");
            Add(parts, "RLF", measurement.HumidityPercent, "0", "%");
            Add(parts, "O2", measurement.DissolvedOxygenMgL, "0.0", "mg/L");
            Add(parts, "PPFD", measurement.PpfdMol, "0");
            Add(parts, "CO2", measurement.Co2Ppm, "0", "ppm");
            if (measurement.SolutionChange)
            {
                parts.Add("Wasserwechsel");
            }

            context.Measurements.Add(string.Join(" · ", parts));
        }

        static void Add(List<string> parts, string label, double? value, string format, string? unit = null)
        {
            if (value is not { } number)
            {
                return;
            }

            var text = number.ToString(format, AppCulture.German);
            parts.Add(unit is null ? $"{label} {text}" : $"{label} {text} {unit}");
        }
    }

    private void AddDeviations(AiContext context, GrowRun grow, List<Measurement> measurements)
    {
        if (measurements.Count == 0)
        {
            return;
        }

        foreach (var deviation in _deviations.Analyze(grow, measurements))
        {
            context.OpenDeviations.Add($"[{deviation.Severity}] {deviation.Message}");
        }
    }

    private void AddSetpoints(AiContext context, GrowRun grow, GrowStage stage)
    {
        if (_targets.GetTargets(grow.HydroStyle, stage) is not { } target)
        {
            return;
        }

        var de = AppCulture.German;
        var body =
            $"pH {target.PhMin.ToString("0.0", de)}–{target.PhMax.ToString("0.0", de)} · " +
            $"EC {target.EcMin.ToString("0.0", de)}–{target.EcMax.ToString("0.0", de)} mS/cm · " +
            $"ORP {target.OrpMin:0}–{target.OrpMax:0} mV · " +
            $"Wasser {target.WaterTempDayC.ToString("0.0", de)} °C Tag / {target.WaterTempNightC.ToString("0.0", de)} °C Nacht · " +
            $"VPD {target.VpdMin.ToString("0.0", de)}–{target.VpdMax.ToString("0.0", de)} kPa · " +
            $"PPFD {target.PpfdMin:0}–{target.PpfdMax:0} · " +
            $"CO₂ {target.Co2Min:0}–{target.Co2Max:0} ppm";

        context.Knowledge.Add(new AiKnowledgeItem(
            Id: $"setpoints:{stage}",
            Kind: "Sollwerte",
            Title: $"Sollwerte {stage} ({grow.HydroStyle})",
            Body: body));
    }

    /// <summary>
    /// The rules. Without these the setpoints mislead: a band of pH 5.9–6.0 reads as
    /// "correct anything outside", which is the opposite of what the growplan says.
    /// </summary>
    private void AddGuidance(AiContext context, GrowRun grow, GrowStage stage)
    {
        var setup = grow.HydroStyle.ToString();
        foreach (var rule in _knowledge.Guidance)
        {
            if (rule.Stages.Count > 0 && !rule.Stages.Contains(stage.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (rule.ApplicableSetups.Count > 0
                && !rule.ApplicableSetups.Contains(setup, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var body = rule.Rule;
            if (!string.IsNullOrWhiteSpace(rule.Rationale))
            {
                body += $" Begründung: {rule.Rationale}";
            }

            // The wrong answer travels with the right one on purpose: it is the answer the
            // model would otherwise reach for.
            if (!string.IsNullOrWhiteSpace(rule.CommonMistake))
            {
                body += $" Häufiger Irrtum: {rule.CommonMistake}";
            }

            var source = rule.Sources.FirstOrDefault();
            context.Knowledge.Add(new AiKnowledgeItem(
                Id: $"guidance:{rule.Id}",
                Kind: "Regel",
                Title: rule.Title,
                Body: body,
                SourceTitle: source?.Title,
                SourceReference: source?.Reference,
                SourceUrl: source?.Url));
        }
    }

    private void AddSops(AiContext context, GrowRun grow)
    {
        var setup = grow.HydroStyle.ToString();
        foreach (var sop in _knowledge.Sops)
        {
            if (sop.ApplicableSetups.Count > 0
                && !sop.ApplicableSetups.Contains(setup, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var steps = string.Join(" ", sop.Steps.Take(6).Select((step, index) => $"{index + 1}. {StepText(step)}"));
            var source = sop.Sources.FirstOrDefault();
            context.Knowledge.Add(new AiKnowledgeItem(
                Id: $"sop:{sop.Id}",
                Kind: "SOP",
                Title: sop.Name,
                Body: string.IsNullOrWhiteSpace(steps) ? sop.Name : steps,
                SourceTitle: source?.Title,
                SourceReference: source?.Reference,
                SourceUrl: source?.Url));
        }

        static string StepText(SopStepDefinition step) =>
            !string.IsNullOrWhiteSpace(step.Description) ? step.Description : step.Title;
    }
}
