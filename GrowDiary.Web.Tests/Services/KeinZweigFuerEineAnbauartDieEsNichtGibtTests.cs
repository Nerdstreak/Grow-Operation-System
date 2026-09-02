using System.Reflection;
using System.Text.RegularExpressions;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Kein Zweig verspricht eine Anbauart, die es in dieser App nicht gibt.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> <c>MeasurementSanityService</c>
/// verzweigte auf <c>profile.IsHydro</c> — und <c>GrowthProfile.IsHydro</c> ist
/// <c>=&gt; true</c>, eine Konstante. Der <c>else</c>-Zweig
/// (<c>CheckSubstrate</c>, rund 140 Zeilen) war damit <b>unerreichbar</b>, und
/// darin standen acht pH- und EC-Zahlen für Erde: 4,8 / 5,6 / 6,0 / 6,8 / 7,2
/// / 8,0 sowie 3,0 / 4,0.</para>
///
/// <para><b>Warum das schlimmer ist als toter Code.</b> Diese Zahlen sehen aus
/// wie fachliche Wahrheiten. Wer sie im Quelltext liest, hält sie für die
/// Position der App zu Erde — dabei sagt die App zu Erde <i>nichts</i>. In
/// einem Projekt, dessen Regel „Faustregeln nur mit Etikett, Empfehlungen nur
/// mit Quelle" lautet, ist eine Zahl ohne Wirkung eine Behauptung ohne
/// Deckung.</para>
///
/// <para><b>Was diese Zählung hält.</b> Sie prüft die drei Konstanten, an denen
/// die Einwertigkeit hängt. Kommt Erde eines Tages zurück, wird hier zuerst
/// etwas rot — und derjenige sieht nach, welche Zweige dann wieder gebraucht
/// werden, statt eine Attrappe vorzufinden.</para>
/// </remarks>
public sealed class KeinZweigFuerEineAnbauartDieEsNichtGibtTests
{
    /// <summary>Die App kennt genau eine Bewässerungsart.</summary>
    [Fact]
    public void EsGibtGenauEineBewaesserungsart()
    {
        var werte = Enum.GetValues<IrrigationType>();

        Assert.True(werte.Length == 1,
            $"IrrigationType hat jetzt {werte.Length} Werte ({string.Join(", ", werte)}). "
            + "Damit sind die Zweige, die auf IsHydro pruefen, wieder echte Verzweigungen — "
            + "und der Substrat-Zweig, der am 02.09.2026 als unerreichbar geloescht wurde, "
            + "wird wieder gebraucht. Siehe den Commit dazu.");
    }

    /// <summary>Und genau eine Anbauart.</summary>
    [Theory]
    [InlineData(nameof(GrowthProfile.IsHydro), true)]
    [InlineData(nameof(GrowthProfile.IsSoilOrganic), false)]
    public void DieAnbauartIstFest(string name, bool erwartet)
    {
        var profil = new GrowRun { HydroStyle = HydroStyle.RDWC }.Profile;
        var wert = (bool)typeof(GrowthProfile).GetProperty(name)!.GetValue(profil)!;

        Assert.True(wert == erwartet,
            $"GrowthProfile.{name} ist jetzt {wert}. Solange es fest war, konnte der "
            + "Substrat-Zweig geloescht werden — jetzt muss jemand nachsehen, welche "
            + "Pruefungen fuer die neue Anbauart gelten.");
    }

    /// <summary>
    /// Der Sanity-Dienst verzweigt nicht mehr auf eine Konstante.
    /// </summary>
    /// <remarks>
    /// Eine Verzweigung, deren Bedingung immer gleich ausgeht, liest sich wie
    /// eine Wahl und ist keine. Sie verdeckt, was wirklich passiert.
    /// </remarks>
    [Fact]
    public void DerSanityDienstVerzweigtNichtAufEineKonstante()
    {
        var quelle = File.ReadAllText(Path.Combine(
            ProjektWurzel(), "GrowDiary.Web", "Services", "MeasurementSanityService.cs"));

        // Kommentare zaehlen nicht: eine Erwaehnung ist keine Verwendung.
        var ohneKommentar = Regex.Replace(
            Regex.Replace(quelle, @"/\*.*?\*/", " ", RegexOptions.Singleline), @"//[^\n]*", string.Empty);

        // Mengenwaechter: wird die Datei ueberhaupt gelesen?
        Assert.True(ohneKommentar.Length > 3000,
            $"Nur {ohneKommentar.Length} Zeichen gelesen — die Pruefung sieht die Datei nicht.");

        Assert.False(Regex.IsMatch(ohneKommentar, @"\bif\s*\(\s*\w*\.?IsHydro\s*\)"),
            "MeasurementSanityService verzweigt wieder auf IsHydro. Solange GrowthProfile.IsHydro "
            + "=> true ist, geht diese Verzweigung immer gleich aus: sie liest sich wie eine Wahl "
            + "und ist keine, und der andere Zweig ist unerreichbar.");
    }

    private static string ProjektWurzel()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, "GrowDiary.Web"))) return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Projektwurzel nicht gefunden.");
    }
}
