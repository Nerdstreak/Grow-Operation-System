using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;

namespace GrowDiary.Web.Services;

/// <summary>Eine fällige Routine — mit dem Satz, der sagt warum.</summary>
public sealed record FaelligeRoutine(
    string SopId,
    string Name,
    string Severity,
    int TageSeit,
    int IntervallTage,
    string Meldung);

/// <summary>
/// Liest die Zeitpläne der Wissens-Abläufe und sagt, was überfällig ist.
/// </summary>
/// <remarks>
/// <para>Die Abläufe tragen ihre Rhythmen seit jeher: der Wasserwechsel sagt
/// „alle 7 Tage, Warnung nach 8, kritisch nach 10" — und kein Dienst hat es je
/// gelesen; nur der Mappen-Drucker gab es als Text aus. Die App wusste, dass
/// etwas überfällig ist, und schwieg. Genau dieses Schweigen endet hier.</para>
///
/// <para>„Zuletzt gemacht" kommt aus dem, was ohnehin erfasst wird: der
/// Wasserwechsel aus der Lösungswechsel-Markierung der Messungen, die tägliche
/// Routine aus der letzten Messung überhaupt, alles andere aus der letzten
/// abgeschlossenen Instanz. Niemand muss dafür etwas Neues pflegen.</para>
/// </remarks>
public sealed class SopDueService
{
    /// <summary>full, important oder expert — gespeichert als AppSetting.</summary>
    public const string LevelKey = "companion-level";

    private readonly KnowledgeBaseLoader _wissen;
    private readonly GrowRepository _grows;
    private readonly SopRepository _instanzen;
    private readonly AppSettingsRepository _settings;

    public SopDueService(
        KnowledgeBaseLoader wissen,
        GrowRepository grows,
        SopRepository instanzen,
        AppSettingsRepository settings)
    {
        _wissen = wissen;
        _grows = grows;
        _instanzen = instanzen;
        _settings = settings;
    }

    /// <summary>Die eingestellte Begleitungsstufe; Standard ist volle Begleitung.</summary>
    public string Stufe
    {
        get
        {
            var wert = _settings.GetValue(LevelKey);
            return wert is "important" or "expert" ? wert : "full";
        }
        set => _settings.SetValue(LevelKey, value is "important" or "expert" ? value : "full");
    }

    public IReadOnlyList<FaelligeRoutine> FuerGrow(int growId)
    {
        // Der Experte hat sich die Stille ausdrücklich bestellt.
        if (Stufe == "expert") return [];

        var grow = _grows.GetGrow(growId);
        if (grow is null || grow.EndDate is not null) return [];

        var messungen = _grows.GetMeasurementsForGrow(growId);
        var wechsel = _grows.GetChangeoutsForGrow(growId);
        var abgeschlossen = _instanzen.GetSopInstancesByGrow(growId)
            .Where(i => i.Status == SopInstanceStatus.Completed && i.CompletedAtUtc is not null)
            .GroupBy(i => i.SopId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Max(i => i.CompletedAtUtc!.Value.ToLocalTime()), StringComparer.OrdinalIgnoreCase);

        var ergebnis = new List<FaelligeRoutine>();
        var nurKritisch = Stufe == "important";

        foreach (var sop in _wissen.Sops)
        {
            var zeitplan = sop.Triggers?.FirstOrDefault(t =>
                string.Equals(t.Type, "Schedule", StringComparison.OrdinalIgnoreCase) && t.IntervalDays is > 0);
            if (zeitplan?.IntervalDays is not { } intervall) continue;

            var zuletzt = ZuletztGemacht(sop.Id, grow, messungen, abgeschlossen, wechsel);
            var tage = (DateTime.Today - zuletzt.Date).Days;

            var warnungAb = zeitplan.WarningAfterDays ?? intervall + 1;
            var kritischAb = zeitplan.CriticalAfterDays ?? intervall + 3;

            if (tage < warnungAb) continue;
            var kritisch = tage >= kritischAb;
            if (nurKritisch && !kritisch) continue;

            ergebnis.Add(new FaelligeRoutine(
                sop.Id,
                sop.Name,
                kritisch ? "critical" : "warning",
                tage,
                intervall,
                $"{sop.Name}: zuletzt vor {tage} Tagen (Plan: alle {intervall})."));
        }

        return ergebnis.OrderByDescending(r => r.Severity == "critical").ThenByDescending(r => r.TageSeit).ToList();
    }

    /// <summary>
    /// Wann diese Routine zuletzt lief — aus dem, was ohnehin erfasst wird.
    /// </summary>
    /// <remarks>Öffentlich, weil an dieser Datumswahl das ganze Erinnern hängt.</remarks>
    public static DateTime ZuletztGemacht(
        string sopId, GrowRun grow, IReadOnlyList<Measurement> messungen,
        IReadOnlyDictionary<string, DateTime> abgeschlossen,
        IReadOnlyList<ChangeoutEntry>? wechsel = null)
    {
        DateTime? fachlich = sopId switch
        {
            // Der letzte Schritt des Wasserwechsels markiert die Messung als
            // Lösungswechsel — auch wer die SOP nie startet, misst danach. Und
            // wer den Wechsel im Formular nachträgt, ist damit ebenso belegt:
            // seit dem 31.08.2026 zählen beide Wege (siehe Wasserwechsel).
            "weekly-water-change" => Wasserwechsel.ZuletztOrtszeit(messungen, wechsel),
            "daily-measurement-routine" => messungen.Max(m => (DateTime?)m.TakenAt),
            _ => null,
        };

        var instanz = abgeschlossen.TryGetValue(sopId, out var fertig) ? fertig : (DateTime?)null;

        // Der juengste Beleg zaehlt; ganz ohne Beleg zaehlt der Start des Grows —
        // ein frisch gestarteter Lauf ist nicht sofort ueberfaellig.
        return new[] { fachlich, instanz, grow.StartDate }
            .Where(d => d is not null)
            .Max()!.Value;
    }
}
