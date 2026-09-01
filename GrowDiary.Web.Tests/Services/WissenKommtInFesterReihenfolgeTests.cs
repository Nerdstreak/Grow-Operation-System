using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Wissensbestand kommt in fester Reihenfolge — nicht in der des Dateisystems.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> <c>LoadCategory</c> lief über
/// <c>Directory.EnumerateFiles</c> und gab die Liste ungeordnet zurück.
/// Über die Reihenfolge sagt diese Methode nichts zu: unter Windows/NTFS ist
/// sie praktisch alphabetisch, unter Linux/ext4 eine Hash-Reihenfolge — und
/// das Add-on läuft im Linux-Container.</para>
///
/// <para><b>Was daran hängt.</b> Alle acht Kategorien laufen durch diese
/// Methode, und jeder Verbraucher erbt die Reihenfolge:</para>
/// <list type="bullet">
///   <item>Die <b>Suche</b> filtert und nimmt dann <c>.Take(5)</c>. Bei 13
///   Regeln, die „wasser" enthalten, entscheidet die Dateireihenfolge, welche
///   fünf der Nutzer sieht — und ob er einen Eintrag überhaupt findet. Auf dem
///   Entwicklungsrechner andere als bei ihm.</item>
///   <item>Die <b>Wissensseite</b> listet in derselben Reihenfolge.</item>
///   <item>Zwei Prüfungen waren am 01.09.2026 hier grün und im Tor <b>rot</b>,
///   weil dort ein Düngerprogramm ohne Blüte-Chart zuerst kam.</item>
/// </list>
///
/// <para><b>Was hier gemessen wird.</b> Nicht „ist sortiert" gegen sich selbst
/// — das wäre eine Tautologie über dieselbe Liste. Gemessen wird, dass die
/// Kennungen aufsteigend stehen: eine Aussage, die für <i>jede</i>
/// Dateireihenfolge dasselbe verlangt.</para>
/// </remarks>
public sealed class WissenKommtInFesterReihenfolgeTests : IDisposable
{
    private readonly string _wurzel;
    private readonly KnowledgeBaseLoader _wissen;

    public WissenKommtInFesterReihenfolgeTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "WissenReihenfolge_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        KopiereWissen(Path.Combine(ProjektWurzel(), "GrowDiary.Web", "wwwroot", "knowledge-defaults"), _wurzel);

        _wissen = new KnowledgeBaseLoader(new AppPaths(_wurzel), NullLogger<KnowledgeBaseLoader>.Instance);
        _wissen.Initialize();
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    public static IEnumerable<object[]> Kategorien()
    {
        yield return ["Guidance"];
        yield return ["Sops"];
        yield return ["Treatments"];
        yield return ["Pathogens"];
        yield return ["Symptoms"];
        yield return ["NutrientPrograms"];
        yield return ["Setpoints"];
        yield return ["WearTemplates"];
    }

    [Theory]
    [MemberData(nameof(Kategorien))]
    public void JedeKategorieStehtNachKennungSortiert(string kategorie)
    {
        var kennungen = KennungenVon(kategorie);

        // Mengenwaechter: bei null oder einem Eintrag kann keine Reihenfolge
        // falsch sein, und der Fall pruefte nichts.
        Assert.True(kennungen.Count >= 2,
            $"{kategorie} hat nur {kennungen.Count} Eintrag/Eintraege — zu wenig, "
            + "um eine Reihenfolge zu pruefen. Ist der Wissensbestand vollstaendig kopiert?");

        var sortiert = kennungen.OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(kennungen.SequenceEqual(sortiert, StringComparer.Ordinal),
            $"{kategorie} kommt nicht nach Kennung sortiert:\n  ist:      "
            + string.Join(", ", kennungen.Take(8))
            + "\n  erwartet: " + string.Join(", ", sortiert.Take(8))
            + "\n\nDann entscheidet das Dateisystem, welche fuenf Treffer die Suche zeigt — "
            + "auf diesem Rechner andere als im Linux-Container des Add-ons.");
    }

    /// <summary>
    /// Und die Zählung sieht wirklich etwas an.
    /// </summary>
    /// <remarks>
    /// Läuft <c>Initialize</c> ins Leere — falscher Pfad, leerer Ordner —, wäre
    /// jede Kategorie oben mit „zu wenig Eintraege" rot statt still grün. Dieser
    /// Fall macht es zusätzlich sichtbar.
    /// </remarks>
    [Fact]
    public void DerWissensbestandIstUeberhauptGeladen()
    {
        var gesamt = Kategorien().Sum(k => KennungenVon((string)k[0]).Count);

        Assert.True(gesamt >= 80,
            $"Nur {gesamt} Wissenseintraege geladen — der Bestand ist nicht vollstaendig, "
            + "und die Reihenfolge-Pruefungen darueber sagen dann wenig.");
    }

    private List<string> KennungenVon(string kategorie) => kategorie switch
    {
        "Guidance" => _wissen.Guidance.Select(x => x.Id).ToList(),
        "Sops" => _wissen.Sops.Select(x => x.Id).ToList(),
        "Treatments" => _wissen.Treatments.Select(x => x.Id).ToList(),
        "Pathogens" => _wissen.Pathogens.Select(x => x.Id).ToList(),
        "Symptoms" => _wissen.Symptoms.Select(x => x.Id).ToList(),
        "NutrientPrograms" => _wissen.NutrientPrograms.Select(x => x.Id).ToList(),
        "Setpoints" => _wissen.Setpoints.Select(x => x.Id).ToList(),
        "WearTemplates" => _wissen.WearTemplates.Select(x => x.Id).ToList(),
        _ => throw new ArgumentOutOfRangeException(nameof(kategorie), kategorie, "Unbekannte Kategorie."),
    };

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

    private static void KopiereWissen(string quelle, string ziel)
    {
        var nach = Path.Combine(ziel, "wwwroot", "knowledge-defaults");
        foreach (var datei in Directory.EnumerateFiles(quelle, "*.json", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(quelle, datei);
            var pfad = Path.Combine(nach, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
            File.Copy(datei, pfad);
        }
    }
}
