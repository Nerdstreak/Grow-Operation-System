using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;
using GrowDiary.Web.Services.Knowledge.Schema;

namespace GrowDiary.Web.Services;

/// <summary>Eine Zeile des Mischplans — eine Komponente mit ihrer Menge.</summary>
/// <param name="Komponente">Grow A, CaMg, Cleanse …</param>
/// <param name="MlProLiter">Dosis laut Chart (bei Spannen der Mittelwert-freie Text unten).</param>
/// <param name="MlGesamtMin">Menge für das Betriebsvolumen, untere Kante.</param>
/// <param name="MlGesamtMax">Obere Kante; gleich Min, wenn das Chart keine Spanne nennt.</param>
public sealed record MischplanZeile(
    string Komponente,
    string MlProLiter,
    double MlGesamtMin,
    double MlGesamtMax);

/// <summary>Der Mischplan für heute — oder der Grund, warum es keinen gibt.</summary>
public sealed record Mischplan(
    string? ProgrammName,
    string? SpalteLabel,
    double? VolumenLiter,
    IReadOnlyList<MischplanZeile> Zeilen,
    double? EcZiel,
    double? PhMin,
    double? PhMax,
    string? Herkunft,
    string? Luecke);

/// <summary>
/// Rechnet das Wochen-Chart des Programms auf das Betriebsvolumen des Grows um.
/// </summary>
/// <remarks>
/// <para>Der Kern der Begleitung: „Hauptkomponenten nach Plan zugeben" wird zu
/// „Grow A 375 ml". Programm und Woche kommen vom Grow, das Volumen von seiner
/// Anlage — der Betreiber rechnet nichts und rät nichts.</para>
///
/// <para>Fehlt ein Glied der Kette, sagt der Plan WELCHES, statt zu raten:
/// kein Programm gewählt, Programm ohne Zahlen-Chart, kein Volumen an der
/// Anlage. Eine leere Antwort ohne Grund wäre die alte Stummheit in neu.</para>
/// </remarks>
public sealed class MischplanService
{
    private readonly GrowRepository _grows;
    private readonly HydroSetupRepository _setups;
    private readonly KnowledgeBaseLoader _wissen;
    private readonly WeekCounterService _wochen;

    public MischplanService(
        GrowRepository grows,
        HydroSetupRepository setups,
        KnowledgeBaseLoader wissen,
        WeekCounterService wochen)
    {
        _grows = grows;
        _setups = setups;
        _wissen = wissen;
        _wochen = wochen;
    }

    public Mischplan? FuerGrow(int growId)
    {
        var grow = _grows.GetGrow(growId);
        if (grow is null) return null;

        if (string.IsNullOrWhiteSpace(grow.FeedProgramId))
        {
            return Leer("Kein Düngerprogramm gewählt — am Grow unter Bearbeiten festlegen.");
        }

        var programm = _wissen.NutrientPrograms.FirstOrDefault(
            p => string.Equals(p.Id, grow.FeedProgramId, StringComparison.OrdinalIgnoreCase));
        if (programm is null)
        {
            return Leer($"Das Programm „{grow.FeedProgramId}“ gibt es im Wissen nicht mehr.");
        }

        if (programm.FeedChart is not { Columns.Count: > 0 } chart)
        {
            return Leer($"{programm.Name} hat noch kein Zahlen-Chart hinterlegt — der Plan kann keine Mengen nennen.",
                programm.Name);
        }

        var volumen = grow.SystemId is { } systemId
            ? _setups.GetSystem(systemId) is { } anlage
                ? HydroSetupMappingVolumen(anlage)
                : null
            : null;

        var spalte = SpalteFuer(chart, grow);
        if (spalte is null)
        {
            return Leer("Für die aktuelle Phase kennt das Chart keine Spalte.", programm.Name);
        }

        var zeilen = spalte.Items.Select(item =>
        {
            var proLiter = item.MinMlPerLiter == item.MaxMlPerLiter
                ? item.MinMlPerLiter.ToString("0.##", AppCulture.German)
                : $"{item.MinMlPerLiter.ToString("0.##", AppCulture.German)}–{item.MaxMlPerLiter.ToString("0.##", AppCulture.German)}";
            return new MischplanZeile(
                item.Component,
                proLiter,
                volumen is { } v ? Math.Round(item.MinMlPerLiter * v, 0) : 0,
                volumen is { } v2 ? Math.Round(item.MaxMlPerLiter * v2, 0) : 0);
        }).ToList();

        var herkunft = $"{programm.Name} · {spalte.Label}"
            + (volumen is { } vol ? $" · gerechnet auf {vol.ToString("0.#", AppCulture.German)} L" : "")
            + (string.IsNullOrWhiteSpace(chart.Note) ? "" : $" — {chart.Note}");

        return new Mischplan(
            programm.Name,
            spalte.Label,
            volumen,
            zeilen,
            spalte.EcTarget,
            spalte.PhMin,
            spalte.PhMax,
            herkunft,
            volumen is null ? "Kein Volumen an der Anlage hinterlegt — die Spalten zeigen deshalb nur ml je Liter." : null);
    }

    private static Mischplan Leer(string luecke, string? programmName = null)
        => new(programmName, null, null, [], null, null, null, null, luecke);

    private static double? HydroSetupMappingVolumen(GrowSystem anlage)
        => Api.Mapping.HydroSetupMapping.CalculateTotalVolumeLiters(
            anlage.PotCount, anlage.PotSizeLiters, anlage.ReservoirLiters);

    /// <summary>
    /// Die Spalte des Charts, die zur Lage des Grows passt.
    /// </summary>
    /// <remarks>
    /// Wochen über das Chart hinaus halten die letzte Spalte der Phase — Woche
    /// 6 einer 4-Wochen-Veg mischt weiter wie Woche 4, statt ins Leere zu
    /// laufen. Sortenabhängig verschieben bleibt Sache des Betreibers; genau
    /// das sagt die Chart-Notiz.
    /// </remarks>
    public static FeedChartColumn? SpalteFuer(FeedChartDefinition chart, GrowRun grow)
    {
        var stage = GrowStageResolver.Resolve(grow, DateTime.Today);

        string chartStage = stage switch
        {
            GrowStage.Seedling or GrowStage.Clone => "Clone",
            GrowStage.Veg => "Veg",
            GrowStage.Transition or GrowStage.Flower => "Flower",
            GrowStage.Finish => "Finish",
            _ => "Finish",
        };

        var kandidaten = chart.Columns.Where(c => string.Equals(c.Stage, chartStage, StringComparison.OrdinalIgnoreCase)).ToList();
        if (kandidaten.Count == 0) return null;

        var wochenSpalten = kandidaten.Where(c => c.Week is not null).OrderBy(c => c.Week).ToList();
        if (wochenSpalten.Count == 0)
        {
            // Sonderspalten (Vorweichen/Anfüttern/Flush): die letzte ist der
            // Normalfall — beim Klon ist das „Anfüttern".
            return kandidaten[^1];
        }

        var woche = WocheInPhase(grow, chartStage);
        return wochenSpalten.LastOrDefault(c => c.Week <= Math.Max(1, woche)) ?? wochenSpalten[0];
    }

    /// <summary>Woche innerhalb der Chart-Phase, ab 1.</summary>
    private static int WocheInPhase(GrowRun grow, string chartStage)
    {
        var heute = DateTime.Today;

        if (chartStage == "Flower" && grow.FlipDate is { } flip)
        {
            return Math.Max(1, ((heute - flip.Date).Days / 7) + 1);
        }

        var start = grow.VegStartedAt?.Date ?? grow.StartDate.Date;
        var ende = chartStage == "Flower" ? heute : (grow.FlipDate?.Date ?? heute);
        return Math.Max(1, ((ende - start).Days / 7) + 1);
    }
}
