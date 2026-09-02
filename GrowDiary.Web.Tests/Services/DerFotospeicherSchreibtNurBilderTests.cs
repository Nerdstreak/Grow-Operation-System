using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Fotospeicher legt keine Datei an, die kein Bild ist.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> <c>PhotoStorageService</c> stand auf
/// der Liste der Klassen, die kein Test je anfasst. Beim Hinsehen: die
/// Erlaubnisliste (<c>ValidatePhotos</c>) und das Schreiben
/// (<c>SaveMeasurementPhotosAsync</c>) sind zwei getrennte Schritte. Das
/// Schreiben nimmt die Endung, wie sie kommt — <c>Path.GetExtension</c>,
/// kleingeschrieben, sonst <c>.jpg</c> — und prüft nichts.</para>
///
/// <para><b>Kein gefundener Fehler, sondern eine offene Flanke.</b> Heute gibt
/// es genau <i>einen</i> Aufrufer, und der prüft vorher
/// (<c>MeasurementsApiController</c>). Die Reihenfolge steht aber nirgends
/// geschrieben: wer morgen einen zweiten Weg baut — Journal, Symptom-Fotos,
/// Import — schreibt beliebige Endungen unter <c>/uploads</c>, und dieser Ordner
/// wird statisch ausgeliefert.</para>
///
/// <para>Deshalb liegt die Sperre jetzt dort, wo geschrieben wird, statt bei den
/// Aufrufern. Eine Zählung über Aufrufer wäre die schwächere Antwort: sie
/// erwischt den Fall erst, wenn ihn jemand baut.</para>
/// </remarks>
public sealed class DerFotospeicherSchreibtNurBilderTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly GrowRepository _grows;
    private readonly PhotoStorageService _fotos;

    public DerFotospeicherSchreibtNurBilderTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Fotospeicher_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _grows = new GrowRepository(_pfade);
        _fotos = new PhotoStorageService(_pfade, _grows);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>Ein gewöhnliches Foto geht durch — und liegt danach da.</summary>
    /// <remarks>
    /// Mengenwächter für alles darunter: schreibt der Dienst überhaupt etwas,
    /// dann sagt eine verweigerte Datei etwas aus.
    /// </remarks>
    [Theory]
    [InlineData("bild.jpg")]
    [InlineData("bild.JPEG")]
    [InlineData("bild.png")]
    [InlineData("bild.webp")]
    public async Task EinGewoehnlichesFoto_LandetAufDerPlatte(string name)
    {
        var grow = Grow();

        var angelegt = await _fotos.SaveMeasurementPhotosAsync(
            grow, MessungZu(grow), [Datei(name)], PhotoTag.Overview, null, false, ValueOrigin.Manual);

        Assert.True(angelegt.Count == 1, $"„{name}\" wurde nicht gespeichert — ein Foto ist das.");

        var abgelegt = Path.Combine(_pfade.UploadRootPath, grow.Id.ToString(),
            Path.GetFileName(angelegt[0].RelativePath));
        Assert.True(File.Exists(abgelegt), $"Die Datei liegt nicht unter „{abgelegt}\".");
        Assert.True(_grows.GetPhotosForGrow(grow.Id).Count == 1,
            "Die Datei liegt da, steht aber in keiner Datenbankzeile — dann findet sie niemand.");
    }

    /// <summary>Was kein Bild ist, wird gar nicht erst geschrieben.</summary>
    /// <remarks>
    /// Laut und nicht still: ein übergangenes Foto wäre der schlechtere Fall.
    /// Wer diesen Fehler auslöst, hat die Prüfung davor vergessen — das gehört
    /// gemeldet, nicht verschluckt.
    /// </remarks>
    [Theory]
    [InlineData("schaedlich.aspx")]
    [InlineData("schaedlich.html")]
    [InlineData("liste.svg")]
    [InlineData("archiv.zip")]
    public async Task WasKeinBildIst_WirdNichtGeschrieben(string name)
    {
        var grow = Grow();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _fotos.SaveMeasurementPhotosAsync(
            grow, MessungZu(grow), [Datei(name)], PhotoTag.Overview, null, false, ValueOrigin.Manual));

        var ordner = Path.Combine(_pfade.UploadRootPath, grow.Id.ToString());
        var dateien = Directory.Exists(ordner) ? Directory.GetFiles(ordner) : [];
        Assert.True(dateien.Length == 0,
            $"„{name}\" wurde trotzdem geschrieben: {string.Join(", ", dateien.Select(Path.GetFileName))}. "
            + "Der Ordner /uploads wird statisch ausgeliefert.");
    }

    /// <summary>Und eine Datei ganz ohne Endung ebenso wenig.</summary>
    /// <remarks>
    /// Vorher fiel dieser Fall auf <c>.jpg</c> zurück — eine Umbenennung, die
    /// niemand angeordnet hat. Ein Browser richtet sich ohnehin nach dem Inhalt;
    /// die erfundene Endung nützt niemandem und verschleiert, was da liegt.
    /// </remarks>
    [Fact]
    public async Task OhneEndung_WirdNichtsErfunden()
    {
        var grow = Grow();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _fotos.SaveMeasurementPhotosAsync(
            grow, MessungZu(grow), [Datei("ohne_endung")], PhotoTag.Overview, null, false, ValueOrigin.Manual));
    }

    /// <summary>
    /// Die Erlaubnisliste des Schreibens ist dieselbe wie die der Prüfung.
    /// </summary>
    /// <remarks>
    /// Zwei Listen laufen auseinander — das ist keine Frage des Ob, sondern des
    /// Wann (<c>CLAUDE.md</c>: EINE WAHRHEIT JE ZAHL). Geprüft wird über das
    /// Verhalten: was <c>ValidatePhotos</c> durchlässt, muss das Schreiben
    /// annehmen, und umgekehrt.
    /// </remarks>
    [Theory]
    [InlineData("a.jpg", true)]
    [InlineData("a.jpeg", true)]
    [InlineData("a.png", true)]
    [InlineData("a.webp", true)]
    [InlineData("a.gif", false)]
    [InlineData("a.bmp", false)]
    [InlineData("a.aspx", false)]
    public async Task PruefungUndSchreiben_SindSichEinig(string name, bool erlaubt)
    {
        var zustand = new ModelStateDictionary();
        _fotos.ValidatePhotos([Datei(name)], zustand);
        var pruefungSagtJa = zustand.IsValid;

        var grow = Grow();
        bool schreibenSagtJa;
        try
        {
            await _fotos.SaveMeasurementPhotosAsync(
                grow, MessungZu(grow), [Datei(name)], PhotoTag.Overview, null, false, ValueOrigin.Manual);
            schreibenSagtJa = true;
        }
        catch (ArgumentException)
        {
            schreibenSagtJa = false;
        }

        Assert.True(pruefungSagtJa == erlaubt,
            $"ValidatePhotos sagt zu „{name}\" {(pruefungSagtJa ? "ja" : "nein")}, erwartet war "
            + $"{(erlaubt ? "ja" : "nein")}.");
        Assert.True(schreibenSagtJa == erlaubt,
            $"Das Schreiben sagt zu „{name}\" {(schreibenSagtJa ? "ja" : "nein")}, die Pruefung "
            + $"davor {(pruefungSagtJa ? "ja" : "nein")}. Zwei Listen, die auseinanderlaufen.");
    }

    // ------------------------------------------------------------------ Hilfe

    /// <summary>Eine echte Messung — eine erfundene Kennung bricht am Fremdschluessel.</summary>
    private int MessungZu(GrowRun grow)
        => _grows.CreateMeasurement(new Measurement
        {
            GrowId = grow.Id,
            TakenAt = DateTime.Now,
            Stage = GrowStage.Veg,
            Source = ValueOrigin.Manual,
        });

    private GrowRun Grow()
    {
        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        var id = _grows.CreateGrow(new GrowRun
        {
            Name = "Lauf", TentId = zelt.Id, HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running, StartDate = DateTime.Today.AddDays(-5),
        });
        return _grows.GetGrow(id)!;
    }

    /// <summary>Eine hochgeladene Datei mit echtem Inhalt.</summary>
    private static IFormFile Datei(string name)
    {
        // Ein winziges, gueltiges PNG — der Inhalt ist immer derselbe, damit der
        // Unterschied allein am NAMEN haengt.
        var inhalt = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        return new FormFile(new MemoryStream(inhalt), 0, inhalt.Length, "photos", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png",
        };
    }
}
