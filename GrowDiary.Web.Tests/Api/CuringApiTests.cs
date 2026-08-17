using GrowDiary.Web.Api.Contracts;
using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Der Rundweg fürs Aushärten: Glas anlegen, ablesen, wiederfinden.
/// </summary>
/// <remarks>
/// <para>Der Punkt, um den es hier geht: das Speichern der Ernte setzt den Grow
/// auf <see cref="GrowStatus.Completed"/>. Genau dann fängt das Aushärten an.
/// Jede Liste, die nach laufenden Grows filtert, verliert die Gläser also in
/// dem Moment, in dem sie wichtig werden — deshalb prüft
/// <see cref="OpenJarsSurviveTheGrowBeingMarkedCompleted"/> ausdrücklich den
/// beendeten Grow.</para>
///
/// <para>Rundweg heißt: was gespeichert wurde, muss unverändert wieder
/// herauskommen. Diese Regel gilt in diesem Projekt seit dem Startdatum-Fehler
/// für jedes Formular.</para>
/// </remarks>
public sealed class CuringApiTests : IDisposable
{
    private readonly string _temp;
    private readonly GrowRepository _grows;
    private readonly CuringRepository _curing;
    private readonly CuringApiController _controller;
    private readonly int _growId;

    public CuringApiTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "Curing_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        var paths = new AppPaths(_temp);
        var tent = TestDatabase.InitializeWithDefaultTent(paths);
        _grows = new GrowRepository(paths);
        _curing = new CuringRepository(paths);
        _controller = new CuringApiController(_curing, _grows, new SetupRepository(paths));

        _growId = _grows.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            Name = "Geerntet",
            StartDate = new DateTime(2026, 4, 1),
            Status = GrowStatus.Running,
        });
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { }
    }

    private CuringJarDto Anlegen(string label = "Glas 1", string? datum = null, bool regler = false)
    {
        var ergebnis = _controller.CreateJar(_growId, new CuringJarUpsertRequest
        {
            Label = label,
            FilledAtLocal = datum ?? DateTime.Today.ToString("yyyy-MM-dd"),
            WeightG = 80,
            HasHumidityPack = regler,
        });
        var created = Assert.IsType<CreatedAtActionResult>(ergebnis.Result);
        return Assert.IsType<CuringJarDto>(created.Value);
    }

    [Fact]
    public void AJarComesBackExactlyAsItWasEnteredIn()
    {
        var angelegt = Anlegen("Mimosa oben");

        var liste = Assert.IsType<OkObjectResult>(_controller.JarsForGrow(_growId).Result).Value;
        var glas = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<CuringJarDto>>(liste));

        Assert.Equal("Mimosa oben", glas.Label);
        Assert.Equal(80, glas.WeightG);
        Assert.Equal(angelegt.Id, glas.Id);
        // Tag 1 am Einglastag — nicht Tag 0 und nicht Tag 2 durch die
        // UTC-Umrechnung des Datums.
        Assert.Equal(1, glas.Duty.DayInCure);
    }

    [Fact]
    public void OpenJarsSurviveTheGrowBeingMarkedCompleted()
    {
        Anlegen();

        // Genau das passiert beim Speichern der Ernte.
        var grow = _grows.GetGrow(_growId)!;
        grow.Status = GrowStatus.Completed;
        grow.EndDate = DateTime.Today;
        _grows.UpdateGrow(grow);

        var offen = Assert.IsAssignableFrom<IReadOnlyList<CuringJarDto>>(
            Assert.IsType<OkObjectResult>(_controller.OpenJars().Result).Value);

        Assert.Single(offen);
    }

    [Fact]
    public void AHumidityReadingComesBackRatedWithItsSource()
    {
        var glas = Anlegen();

        _controller.AddReading(glas.Id, new CuringReadingRequest { HumidityPercent = 67 });

        var offen = Assert.IsAssignableFrom<IReadOnlyList<CuringJarDto>>(
            Assert.IsType<OkObjectResult>(_controller.OpenJars().Result).Value);
        var feuchte = Assert.Single(offen).LatestHumidity;

        Assert.NotNull(feuchte);
        Assert.Equal(67, feuchte!.Percent);
        Assert.Equal("MoldRisk", feuchte.Level);
        // Ohne Quelle waere die Bewertung eine Behauptung.
        Assert.False(string.IsNullOrWhiteSpace(feuchte.RatingSource));
    }

    [Fact]
    public void AnEmptyReadingIsRejected()
    {
        var glas = Anlegen();

        // Sonst ginge „nichts eingetragen" als Lueften durch und wuerde den
        // naechsten Termin verschieben, ohne dass jemand ein Glas geoeffnet hat.
        var ergebnis = _controller.AddReading(glas.Id, new CuringReadingRequest());

        Assert.IsType<BadRequestObjectResult>(ergebnis.Result);
        Assert.Empty(_curing.GetReadings(glas.Id));
    }

    [Fact]
    public void OnlyBurpingClearsTheDutyNotMerelyLooking()
    {
        var glas = Anlegen(datum: DateTime.Today.AddDays(-3).ToString("yyyy-MM-dd"));

        // Nur abgelesen: der Termin bleibt faellig.
        _controller.AddReading(glas.Id, new CuringReadingRequest { HumidityPercent = 60 });
        Assert.Null(_curing.GetLastBurp(glas.Id));

        // Erst das Lueften zaehlt.
        _controller.AddReading(glas.Id, new CuringReadingRequest { BurpedMinutes = 7 });
        Assert.NotNull(_curing.GetLastBurp(glas.Id));
    }

    [Fact]
    public void AFinishedJarLeavesTheOpenList()
    {
        var glas = Anlegen();

        _controller.FinishJar(glas.Id);

        var offen = Assert.IsAssignableFrom<IReadOnlyList<CuringJarDto>>(
            Assert.IsType<OkObjectResult>(_controller.OpenJars().Result).Value);
        Assert.Empty(offen);

        // Am Grow steht es weiterhin — die Geschichte des Laufs bleibt.
        var amGrow = Assert.IsAssignableFrom<IReadOnlyList<CuringJarDto>>(
            Assert.IsType<OkObjectResult>(_controller.JarsForGrow(_growId).Result).Value);
        Assert.Single(amGrow);
        Assert.NotNull(Assert.Single(amGrow).FinishedAtUtc);
    }

    [Fact]
    public void EditingAJarDoesNotSecretlyFinishIt()
    {
        var glas = Anlegen();
        _controller.FinishJar(glas.Id);

        _controller.UpdateJar(glas.Id, new CuringJarUpsertRequest
        {
            Label = "Neuer Name",
            FilledAtLocal = DateTime.Today.ToString("yyyy-MM-dd"),
        });

        // Umgekehrt genauso: Umbenennen darf ein abgeschlossenes Glas nicht
        // wieder oeffnen.
        Assert.NotNull(_curing.GetJar(glas.Id)!.FinishedAtUtc);
    }

    [Fact]
    public void AJarOnAMissingGrowIsRefused()
    {
        var ergebnis = _controller.CreateJar(9999, new CuringJarUpsertRequest
        {
            Label = "Geist",
            FilledAtLocal = DateTime.Today.ToString("yyyy-MM-dd"),
        });

        Assert.IsType<NotFoundObjectResult>(ergebnis.Result);
    }
}
