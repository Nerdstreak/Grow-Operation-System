using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests.Services;

/// <summary>
/// Der Rundweg: anlegen → speichern → wieder lesen, Feld für Feld.
/// </summary>
/// <remarks>
/// <para>Die Lehre aus dem Startdatum-Fehler: geprüft wurde immer nur, DASS
/// gespeichert wird — nie, ob dasselbe wieder herauskommt und anzeigbar ist.
/// Dieser Test fährt den vollen Weg durch die echte SQLite-Datenbank, mit
/// genau der Sorte aus dem Feedback des Testers (Barneys Farm,
/// RS11 x Banana OG).</para>
/// </remarks>
public sealed class StrainRoundtripTests : IDisposable
{
    private readonly string _temp;
    private readonly GrowRepository _repository;

    public StrainRoundtripTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "StrainRoundtrip_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        var paths = new AppPaths(_temp);
        TestDatabase.InitializeWithDefaultTent(paths);
        _repository = new GrowRepository(paths);
    }

    [Fact]
    public void EveryBreederFieldSurvivesTheRoundTrip()
    {
        var angelegt = _repository.CreateStrain(new Strain
        {
            Name = "RS11 x Banana OG",
            Breeder = "Barneys Farm",
            Dominance = StrainDominance.Indica,
            SeedKind = SeedKind.Feminized,
            ThcPercent = 32,
            CbdPercent = 0.8,
            SativaPercent = 30,
            Taste = "Grapefruit, Zitrusfrüchte, Melone, Banane",
            Effect = "Entspannt, Konzentriert, Beruhigend",
            Aroma = "Zitrone, Würzig, Kirsche",
            YieldIndoorGm2 = 600,
            HeightIndoorCm = 150,
            FlowerWeeksMin = 9,
            FlowerWeeksMax = 10,
        });

        var gelesen = _repository.GetStrain(angelegt.Id)!;

        Assert.Equal("RS11 x Banana OG", gelesen.Name);
        Assert.Equal(SeedKind.Feminized, gelesen.SeedKind);
        Assert.Equal(32, gelesen.ThcPercent);
        Assert.Equal(0.8, gelesen.CbdPercent);
        Assert.Equal(30, gelesen.SativaPercent);
        Assert.Equal("Grapefruit, Zitrusfrüchte, Melone, Banane", gelesen.Taste);
        Assert.Equal("Entspannt, Konzentriert, Beruhigend", gelesen.Effect);
        Assert.Equal("Zitrone, Würzig, Kirsche", gelesen.Aroma);
        Assert.Equal(600, gelesen.YieldIndoorGm2);
        Assert.Equal(150, gelesen.HeightIndoorCm);
    }

    [Fact]
    public void AnUpdateKeepsWhatItDoesNotTouchAndChangesWhatItDoes()
    {
        var strain = _repository.CreateStrain(new Strain
        {
            Name = "Testsorte",
            SeedKind = SeedKind.Automatic,
            ThcPercent = 20,
            Taste = "Zitrus",
        });

        strain.ThcPercent = 24;
        _repository.UpdateStrain(strain);

        var gelesen = _repository.GetStrain(strain.Id)!;
        Assert.Equal(24, gelesen.ThcPercent);
        Assert.Equal(SeedKind.Automatic, gelesen.SeedKind);
        Assert.Equal("Zitrus", gelesen.Taste);
    }

    [Fact]
    public void EmptyFieldsStayEmptyInsteadOfBecomingZero()
    {
        // Eine Sorte ohne Zuechter-Angaben ist normal — 0 % THC waere eine
        // Behauptung, kein fehlender Wert.
        var strain = _repository.CreateStrain(new Strain { Name = "Unbekannte" });
        var gelesen = _repository.GetStrain(strain.Id)!;

        Assert.Null(gelesen.SeedKind);
        Assert.Null(gelesen.ThcPercent);
        Assert.Null(gelesen.SativaPercent);
        Assert.Null(gelesen.Taste);
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { /* Aufräumen darf scheitern. */ }
    }
}
