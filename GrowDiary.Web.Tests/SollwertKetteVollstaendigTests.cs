using System.Text.RegularExpressions;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Niemand fragt die Sollwerte am Profil vorbei.
///
/// <para><b>Der Anlass.</b> Die Diagnose rief
/// <c>GetTargets(grow.HydroStyle, stage)</c> — die Abkürzung, die immer beim
/// Standardprofil landet. Wer eigene Sollwerte eingetragen hatte, sah sie auf
/// der Live-Kachel und in der Diagnose nicht: gemessen EC 0,6–0,8 gegen
/// 0,9–1,1, derselbe Grow, dieselbe Minute.</para>
///
/// <para>Beim Aufräumen fanden sich zwei weitere Stellen mit demselben Aufruf
/// (der Addback-Vorschlag und der Trend-Wächter). Ein Fehler, den man dreimal
/// findet, ist kein Versehen mehr, sondern eine offene Tür — deshalb dieser
/// Test statt einer dritten Korrektur.</para>
///
/// <para><b>Warum über den Quelltext.</b> Die Abkürzung ist eine gültige
/// Überladung; kein Typ und keine Signatur kann sie verbieten. Was man prüfen
/// kann, ist ihre Verwendung.</para>
/// </summary>
public sealed class SollwertKetteVollstaendigTests
{
    /// <summary>
    /// Dateien, die die Abkürzung benutzen dürfen — jeweils mit Grund.
    /// </summary>
    private static readonly Dictionary<string, string> GewollteAusnahmen = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TargetValueService.cs"] = "Die Abkürzung selbst — sie reicht an die lange Form durch",
        ["SetpointProfilesApiController.cs"] = "Dort IST die Profil-Kennung die Frage, nicht das Ergebnis einer Auflösung",
    };

    private static string ProjektWurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web"))) return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }

    [Fact]
    public void Keine_Sollwert_Abfrage_umgeht_die_Profil_Kette()
    {
        var wurzel = Path.Combine(ProjektWurzel(), "GrowDiary.Web");
        var dateien = Directory.GetFiles(wurzel, "*.cs", SearchOption.AllDirectories);

        // Der Selbsttest: findet die Suche gar keine Dateien, wäre der Test leer
        // und trotzdem grün — die Falle, in die seine Vorgänger gelaufen sind.
        Assert.True(dateien.Length >= 100, $"Nur {dateien.Length} Quelldateien gefunden — der Pfad stimmt nicht, der Test würde nichts prüfen.");

        // `GetTargets(irgendwas.HydroStyle, …)` oder `GetTargets(HydroStyle.…, …)`
        var abkuerzung = new Regex(@"GetTargets\(\s*[A-Za-z_][A-Za-z0-9_.]*\.HydroStyle\s*,|GetTargets\(\s*HydroStyle\.");
        var treffer = new List<string>();

        foreach (var datei in dateien)
        {
            var name = Path.GetFileName(datei);
            if (GewollteAusnahmen.ContainsKey(name)) continue;

            var zeilen = File.ReadAllLines(datei);
            for (var i = 0; i < zeilen.Length; i++)
            {
                if (abkuerzung.IsMatch(zeilen[i]))
                {
                    treffer.Add($"{name}:{i + 1}  {zeilen[i].Trim()}");
                }
            }
        }

        Assert.True(treffer.Count == 0,
            "Diese Stellen fragen die Sollwerte am Profil des Nutzers vorbei und landen immer beim Standardprofil:\n"
            + string.Join("\n", treffer)
            + "\n\nRichtig ist: SetpointProfileResolver.Resolve(grow.SetpointProfileId, systemProfilId, grow.HydroStyle) "
            + "und dann GetTargets(profil.ProfileId, stage). "
            + "Gibt es einen Grund für die Abkürzung, gehört die Datei mit diesem Grund in GewollteAusnahmen.");
    }

    [Fact]
    public void Der_Test_findet_die_Abkuerzung_wenn_es_sie_gibt()
    {
        // Eine Prüfung, von der niemand gezeigt hat, dass sie beißen kann, ist
        // wertlos. Also hier an einem Beispielsatz belegt.
        var abkuerzung = new Regex(@"GetTargets\(\s*[A-Za-z_][A-Za-z0-9_.]*\.HydroStyle\s*,|GetTargets\(\s*HydroStyle\.");

        Assert.Matches(abkuerzung, "var targets = _targetValueService.GetTargets(grow.HydroStyle, stage);");
        Assert.Matches(abkuerzung, "_targets.GetTargets(HydroStyle.RDWC, stage)");
        Assert.DoesNotMatch(abkuerzung, "_targetValues.GetTargets(profil.ProfileId, stage);");
    }
}
