using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.Tests.TestFakes;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Kalibrierlauf schreibt nur, was er wirklich gemessen hat.
/// </summary>
/// <remarks>
/// <para><b>Warum gerade hier.</b> <see cref="LevelCalibrationService"/> stand
/// am 01.09.2026 bei <b>0 %</b> Abdeckung — 124 Zeilen, die niemand je
/// ausgeführt hat. Die Rechnung darunter (<see cref="LevelStability"/>) ist zu
/// 97 % geprüft; der Ablauf, der sie benutzt und das Ergebnis <b>speichert</b>,
/// gar nicht.</para>
///
/// <para><b>Was auf dem Spiel steht.</b> <c>Finish</c> überschreibt
/// <c>LevelSensorEmptyRaw</c>, <c>LevelSensorFullRaw</c>,
/// <c>LevelSensorFullLiters</c> — und <c>ReservoirLiters</c>. Aus dieser
/// Geraden rechnet die App später jeden Füllstand, und aus dem Füllstand kommt
/// der Volumenfaktor der Dosierung. Eine falsch geschriebene Kalibrierung ist
/// nicht ein falscher Wert, sondern ein dauerhaft falscher Maßstab — und sie
/// meldet sich nie.</para>
///
/// <para>Geprüft wird deshalb die Abwehr: jeder Weg, der <b>nicht</b> zu einer
/// belegten Messung führt, darf am Datenbestand nichts ändern.</para>
/// </remarks>
public sealed class KalibrierungSchreibtNurBelegtesTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly GrowRepository _grows;
    private readonly HydroSetupRepository _hydro;
    private readonly GrowSystem _system;

    public KalibrierungSchreibtNurBelegtesTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Kalibrierung_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);

        _grows = new GrowRepository(_pfade);
        _hydro = new HydroSetupRepository(_pfade, new TentRepository(_pfade));

        var zelt = _grows.CreateTent(new Tent { Name = "Zelt", TentType = TentType.Production });
        _system = _hydro.CreateSystem(new GrowSystem
        {
            Name = "RDWC",
            TentId = zelt.Id,
            HydroStyle = nameof(GrowDiary.Web.Models.HydroStyle.RDWC),
            ReservoirLiters = 120,
        });
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    /// <summary>Ohne offenen Lauf wird nichts geschrieben.</summary>
    [Fact]
    public void OhneOffenenLauf_SchreibtFinishNichts()
    {
        var dienst = Dienst();

        var meldung = dienst.Finish(_system.Id, liters: 95);

        Assert.True(meldung is not null, "Finish ohne Lauf meldete Erfolg (null).");
        UnveraendertGeblieben("Ein Finish ohne offenen Kalibrierlauf hat trotzdem geschrieben.");
    }

    /// <summary>
    /// Eine Literzahl, die es nicht geben kann, wird abgelehnt — vor jedem Zugriff.
    /// </summary>
    /// <remarks>
    /// 0 L bedeutet: der Nutzer hat das Feld nicht ausgefüllt. Würde das
    /// durchgehen, stünde <c>LevelSensorFullLiters = 0</c> in der Geraden, und
    /// jede spätere Umrechnung ergäbe 0 L — bei vollem Becken.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void UnmoeglicheLiterzahl_WirdAbgelehnt(double liter)
    {
        var meldung = Dienst().Finish(_system.Id, liter);

        Assert.True(meldung is not null, $"{liter} L wurden angenommen.");
        UnveraendertGeblieben($"Nach einem abgelehnten Finish mit {liter} L wurde trotzdem geschrieben.");
    }

    /// <summary>Ein gestarteter, aber nie gemessener Lauf schreibt nichts.</summary>
    /// <remarks>
    /// Der Nullpunkt entsteht erst, wenn der Wert 15 s ruhig war. Wer sofort
    /// nach dem Start auf „voll" drückt, hat nichts gemessen — und bekommt
    /// keine Gerade.
    /// </remarks>
    [Fact]
    public void GestartetAberNichtGemessen_SchreibtNichts()
    {
        var dienst = Dienst();
        dienst.Start(_system.Id);

        var meldung = dienst.Finish(_system.Id, liters: 95);

        Assert.True(meldung is not null,
            "Ein Lauf ohne einen einzigen Messwert hat eine Kalibrierung geschrieben.");
        UnveraendertGeblieben("Ein Lauf ohne Nullpunkt hat trotzdem geschrieben.");
    }

    /// <summary>Ein abgebrochener Lauf ist weg.</summary>
    [Fact]
    public void NachAbbruch_IstDerLaufWeg()
    {
        var dienst = Dienst();
        dienst.Start(_system.Id);
        dienst.Cancel(_system.Id);

        var meldung = dienst.Finish(_system.Id, liters: 95);

        Assert.True(meldung is not null && meldung.Contains("Kalibrierlauf", StringComparison.OrdinalIgnoreCase),
            $"Nach Cancel meldete Finish „{meldung ?? "Erfolg"}\" statt „kein Lauf offen\".");
        UnveraendertGeblieben("Nach einem Abbruch wurde trotzdem geschrieben.");
    }

    /// <summary>
    /// Der Abbruch trifft nur das eine System.
    /// </summary>
    /// <remarks>
    /// Alle Läufe liegen in <b>einer</b> Ablage im Speicher. Ein Abbruch, der
    /// zu viel wegräumt, beendet den Lauf, an dem jemand anderes gerade mit dem
    /// Schlauch steht — ohne eine Meldung.
    /// </remarks>
    [Fact]
    public void EinAbbruchBeendetNichtDenLaufDesNachbarn()
    {
        var zweites = _hydro.CreateSystem(new GrowSystem
        {
            Name = "Zweites", TentId = _system.TentId, HydroStyle = nameof(GrowDiary.Web.Models.HydroStyle.RDWC), ReservoirLiters = 60,
        });

        var dienst = Dienst();
        dienst.Start(_system.Id);
        dienst.Start(zweites.Id);
        dienst.Cancel(_system.Id);

        // Der Nachbar laeuft weiter: sein Finish scheitert am fehlenden
        // Nullpunkt, NICHT an "kein Lauf offen".
        var meldung = dienst.Finish(zweites.Id, liters: 55);

        Assert.True(meldung is not null, "Der Nachbar-Lauf hat ohne Nullpunkt geschrieben.");
        Assert.False(meldung!.Contains("neu starten", StringComparison.OrdinalIgnoreCase),
            $"Der Abbruch am System {_system.Id} hat auch den Lauf am System {zweites.Id} "
            + $"beendet — dort meldet Finish jetzt „{meldung}\". Erwartet war der Hinweis auf "
            + "den fehlenden Nullpunkt: der Lauf steht noch, er ist nur nicht weit genug.");
    }

    /// <summary>Ohne Sensor sagt der Assistent das, statt zu rechnen.</summary>
    [Fact]
    public async Task OhneSensor_MeldetDerAssistentDas()
    {
        var dienst = Dienst();
        dienst.Start(_system.Id);

        var stand = await dienst.PollAsync(_system.Id);

        Assert.True(stand.Step == CalibrationStep.NoSensor,
            $"Ohne konfiguriertes Home Assistant steht der Assistent auf {stand.Step} "
            + "statt auf NoSensor — er wartet dann auf einen Wert, der nie kommt.");
        Assert.False(string.IsNullOrWhiteSpace(stand.Message),
            "Der Assistent meldet NoSensor ohne einen Satz dazu — der Nutzer sieht eine leere Karte.");
    }

    /// <summary>Ein System ohne Zelt hat keinen Sensor — und sagt es.</summary>
    [Fact]
    public async Task SystemOhneZelt_MeldetDenGrund()
    {
        var ohneZelt = _hydro.CreateSystem(new GrowSystem
        {
            Name = "Frei stehend", TentId = null, HydroStyle = nameof(GrowDiary.Web.Models.HydroStyle.RDWC),
        });

        var dienst = Dienst();
        dienst.Start(ohneZelt.Id);

        var stand = await dienst.PollAsync(ohneZelt.Id);

        Assert.True(stand.Step == CalibrationStep.NoSensor,
            $"Ein System ohne Zelt steht auf {stand.Step} statt auf NoSensor.");
        Assert.True(stand.Message?.Contains("Zelt", StringComparison.OrdinalIgnoreCase) == true,
            $"Die Meldung „{stand.Message}\" nennt den Grund nicht — ohne Zelt gibt es keinen Sensor.");
    }

    /// <summary>Ohne Lauf antwortet der Assistent, statt einen anzulegen.</summary>
    /// <remarks>
    /// Sonst entstünde bei jedem Aufruf der Seite eine Sitzung im Speicher —
    /// und die Aufräumung greift erst nach 30 Minuten.
    /// </remarks>
    [Fact]
    public async Task OhnegestartetenLauf_EntstehtKeiner()
    {
        var dienst = Dienst();

        var stand = await dienst.PollAsync(_system.Id);
        Assert.True(stand.Step == CalibrationStep.NoSensor,
            $"Ohne gestarteten Lauf steht der Assistent auf {stand.Step}.");

        // Und es ist wirklich keiner entstanden: sonst kaeme Finish weiter als
        // bis „kein Lauf offen".
        var meldung = dienst.Finish(_system.Id, liters: 95);
        Assert.True(meldung is not null && meldung.Contains("neu starten", StringComparison.OrdinalIgnoreCase),
            $"Nach einem blossen Poll meldet Finish „{meldung ?? "Erfolg"}\" — es ist also doch "
            + "ein Lauf entstanden, den niemand gestartet hat.");
    }

    // ------------------------------------------------------------------ Hilfe

    private LevelCalibrationService Dienst()
        => new(_hydro, _grows,
            new HomeAssistantService(
                new StubHttpClientFactory(
                    new RecordingHttpHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound))),
                NullLogger<HomeAssistantService>.Instance),
            NullLogger<LevelCalibrationService>.Instance);

    /// <summary>Die Gerade und das Volumen stehen unberührt da.</summary>
    private void UnveraendertGeblieben(string warum)
    {
        var jetzt = _hydro.GetHydroSetup(_system.Id);
        Assert.True(jetzt is not null, "Das System ist verschwunden.");
        Assert.True(jetzt!.LevelSensorEmptyRaw is null && jetzt.LevelSensorFullRaw is null
                    && jetzt.LevelSensorFullLiters is null && jetzt.LevelCalibratedAtUtc is null,
            warum + " Die Gerade steht jetzt bei "
            + $"{jetzt.LevelSensorEmptyRaw?.ToString() ?? "—"} → {jetzt.LevelSensorFullRaw?.ToString() ?? "—"} "
            + $"= {jetzt.LevelSensorFullLiters?.ToString() ?? "—"} L.");
        Assert.True(jetzt.ReservoirLiters == 120,
            warum + $" Das Reservoir-Volumen steht jetzt bei {jetzt.ReservoirLiters} statt 120 L.");
    }
}
