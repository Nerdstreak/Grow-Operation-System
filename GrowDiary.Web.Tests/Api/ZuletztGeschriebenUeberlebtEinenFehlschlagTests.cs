using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// „Zuletzt geschrieben" verschwindet nicht, weil ein Versuch danebenging.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (02.09.2026).</b> Die Nachtabsenkung holte
/// <c>GetRecent(1, "night-ramp")</c> — also <b>einen</b> Eintrag — und filterte
/// erst danach auf Erfolg. War der letzte Versuch fehlgeschlagen, kam nichts
/// zurück.</para>
///
/// <para><b>Was der Nutzer sah.</b> Die Rampe schreibt seit Wochen zweimal
/// täglich zuverlässig. Bei der letzten Lichtflanke war Home Assistant kurz
/// weg. Danach: alle Haken grün, Kurzfassung „Aktiv" — und daneben, wo sonst
/// der Zeitpunkt steht, nichts. Wer prüfen will, ob die Steuerung wirklich
/// arbeitet, findet die einzige Antwort darauf leer vor.</para>
///
/// <para>Gesucht wird jetzt der letzte <b>erfolgreiche</b> Eintrag, nicht der
/// letzte überhaupt.</para>
/// </remarks>
public sealed class ZuletztGeschriebenUeberlebtEinenFehlschlagTests : IDisposable
{
    private readonly string _wurzel;
    private readonly SystemAuditRepository _protokoll;

    public ZuletztGeschriebenUeberlebtEinenFehlschlagTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "ZuletztGeschrieben_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        var pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(pfade);
        _protokoll = new SystemAuditRepository(pfade);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>Ein Fehlschlag ganz oben verdeckt den Erfolg darunter nicht.</summary>
    [Fact]
    public void EinFehlschlagObenDrauf_VerdecktDenErfolgNicht()
    {
        var jetzt = DateTime.UtcNow;

        // Zwei Wochen zuverlaessig, dann einmal danebengegangen.
        for (var tag = 14; tag >= 1; tag -= 1)
        {
            Eintrag(jetzt.AddDays(-tag), erfolg: true);
        }
        Eintrag(jetzt, erfolg: false);

        // Mengenwaechter: ohne Eintraege waere jede Aussage darunter wertlos.
        Assert.True(_protokoll.GetRecent(500, "night-ramp").Count == 15,
            "Die Eintraege sind gar nicht in der Ablage gelandet.");

        var letzterErfolg = LetzterErfolgreicher();

        Assert.True(letzterErfolg is not null,
            "Nach einem einzigen fehlgeschlagenen Versuch ist „zuletzt geschrieben\" leer — "
            + "obwohl die Rampe zwei Wochen lang zweimal taeglich geschrieben hat. Wer pruefen "
            + "will, ob die Steuerung arbeitet, findet die einzige Antwort darauf leer vor.");

        var abstandTage = (jetzt - letzterErfolg!.Value).TotalDays;
        Assert.True(abstandTage < 1.5,
            $"Gemeldet wird ein Erfolg von vor {abstandTage:0.#} Tagen — erwartet war der von "
            + "gestern, nicht irgendeiner.");
    }

    /// <summary>Ohne jeden Erfolg bleibt es leer — und behauptet nichts.</summary>
    /// <remarks>
    /// Die Gegenrichtung: eine Angabe, die auch dann etwas nennt, wenn nie
    /// etwas geschrieben wurde, wäre schlimmer als gar keine.
    /// </remarks>
    [Fact]
    public void OhneJedenErfolg_BleibtEsLeer()
    {
        Eintrag(DateTime.UtcNow.AddHours(-2), erfolg: false);
        Eintrag(DateTime.UtcNow, erfolg: false);

        Assert.True(LetzterErfolgreicher() is null,
            "Es wurde nie erfolgreich geschrieben, und trotzdem steht dort ein Zeitpunkt.");
    }

    /// <summary>
    /// Und die Suche reicht weit genug zurück.
    /// </summary>
    /// <remarks>
    /// Mengenwächter: holte die Suche wieder nur eine Handvoll Einträge, wäre
    /// der Fall oben grün und der echte Fall (viele Fehlschläge nach einem
    /// Erfolg) trotzdem kaputt.
    /// </remarks>
    [Fact]
    public void AuchHinterVielenFehlschlaegen_WirdDerErfolgGefunden()
    {
        var jetzt = DateTime.UtcNow;
        Eintrag(jetzt.AddDays(-2), erfolg: true);

        // Zwei Tage Ausfall im Minutentakt waeren mehr, als eine kurze Liste
        // fasst — hier reichen 60, um eine Grenze von 1 oder 10 zu sprengen.
        for (var i = 59; i >= 0; i -= 1)
        {
            Eintrag(jetzt.AddMinutes(-i), erfolg: false);
        }

        Assert.True(LetzterErfolgreicher() is not null,
            "Hinter 60 Fehlschlaegen wurde der Erfolg davor nicht mehr gefunden. Bei einem "
            + "laengeren Ausfall von Home Assistant sieht der Nutzer dann nie wieder, wann "
            + "zuletzt geschrieben wurde.");
    }

    // ------------------------------------------------------------------ Hilfe

    /// <summary>Dieselbe Suche, die der Endpunkt fährt.</summary>
    private DateTime? LetzterErfolgreicher()
        => NightRampAuskunft.LetzterErfolgUtc(_protokoll, "night-ramp");

    private void Eintrag(DateTime wann, bool erfolg)
        => _protokoll.Add(new SystemAuditEvent
        {
            EventType = "night-ramp",
            Success = erfolg,
            CreatedAtUtc = wann,
            Action = "write",
            Summary = erfolg ? "Sollwert geschrieben" : "Home Assistant antwortet nicht",
        });
}
