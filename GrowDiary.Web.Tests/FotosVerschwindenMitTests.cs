using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;

namespace GrowDiary.Web.Tests;

/// <summary>
/// Wird ein Grow gelöscht, verschwinden auch seine Fotodateien.
/// </summary>
/// <remarks>
/// <para><b>Der Anlass (01.09.2026).</b> Der Löschpfad rechnete gegen
/// <c>&lt;contentRoot&gt;/wwwroot/uploads</c>, gespeichert wird aber unter
/// <see cref="AppPaths.UploadRootPath"/> — dem Datenpfad des Add-ons.
/// <c>File.Exists</c> war dort immer false, also wurde nie eine Datei
/// gelöscht: die Datenbankzeile verschwand, die JPEG blieb für immer liegen.
/// Bei einem Grow mit täglichen Fotos summiert sich das über Monate auf der
/// Platte des Home-Assistant-Hosts, und niemand sieht die Dateien je wieder.</para>
///
/// <para>Dieselbe Wegrechnung stand <b>zweimal</b> nebeneinander — beide Male
/// falsch. Jetzt einmal in <c>RepositoryBase</c>.</para>
///
/// <para><b>Warum am Dateisystem und nicht am Pfad.</b> Eine Prüfung, die nur
/// den zurückgegebenen Pfad vergleicht, wäre grün geblieben, solange sie
/// dieselbe falsche Formel benutzt wie der Code. Hier wird eine echte Datei
/// angelegt und danach nachgesehen, ob sie weg ist.</para>
/// </remarks>
public sealed class FotosVerschwindenMitTests : IDisposable
{
    private readonly string _wurzel;
    private readonly AppPaths _pfade;
    private readonly GrowRepository _grows;

    public FotosVerschwindenMitTests()
    {
        _wurzel = Path.Combine(Path.GetTempPath(), "Fotos_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_wurzel);
        _pfade = new AppPaths(_wurzel);
        TestDatabase.Initialize(_pfade);
        _grows = new GrowRepository(_pfade);
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { /* Windows haelt manchmal fest */ }
    }

    [Fact]
    public void GrowLoeschen_NimmtDieFotodateienMit()
    {
        var (growId, datei, relativ) = GrowMitFoto();

        Assert.True(File.Exists(datei), "Die Testdatei wurde gar nicht erst angelegt.");

        _grows.DeleteGrow(growId);

        Assert.True(!File.Exists(datei),
            $"Die Fotodatei liegt nach dem Loeschen des Grows noch da ({datei}). "
            + $"Der gespeicherte Pfad war „{relativ}\" — wenn die Aufloesung woandershin zeigt, "
            + "bleibt jede JPEG fuer immer auf der Platte.");
    }

    [Fact]
    public void MessungLoeschen_NimmtIhreFotodateienMit()
    {
        var (growId, _, _) = GrowMitFoto();
        var messungId = _grows.CreateMeasurement(new Measurement
        {
            GrowId = growId,
            TakenAt = DateTime.Now,
            Stage = GrowStage.Veg,
        });

        var (datei, relativ) = FotoAnlegen(growId, "messung.jpg");
        _grows.AddPhoto(new PhotoAsset
        {
            GrowId = growId,
            MeasurementId = messungId,
            RelativePath = relativ,
        });

        _grows.DeleteMeasurement(messungId);

        Assert.True(!File.Exists(datei),
            $"Die Fotodatei der Messung liegt noch da ({datei}).");
    }

    /// <summary>
    /// Ein Pfad, der aus dem Upload-Verzeichnis herausführt, wird abgelehnt.
    /// </summary>
    /// <remarks>
    /// Der Ausbruchsschutz war in beiden alten Kopien drin und darf durch das
    /// Zusammenlegen nicht wegfallen — sonst könnte ein manipulierter
    /// Datenbankeintrag beliebige Dateien löschen.
    /// </remarks>
    [Fact]
    public void EinAusbruchAusDemUploadOrdner_LoeschtNichts()
    {
        var fremd = Path.Combine(_wurzel, "nicht-anfassen.txt");
        File.WriteAllText(fremd, "wichtig");

        var growId = _grows.CreateGrow(new GrowRun
        {
            Name = "Lauf", HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running, StartDate = DateTime.Today,
        });
        _grows.AddPhoto(new PhotoAsset
        {
            GrowId = growId,
            RelativePath = "/uploads/../nicht-anfassen.txt",
        });

        _grows.DeleteGrow(growId);

        Assert.True(File.Exists(fremd),
            "Ein Pfad mit „..\" hat eine Datei ausserhalb des Upload-Ordners geloescht.");
    }

    /// <summary>
    /// Ein Geschwisterordner, dessen Name mit dem Upload-Ordner anfängt, ist aussen.
    /// </summary>
    /// <remarks>
    /// <para><b>Der Anlass (01.09.2026, vom Prüfer gefunden).</b> Der Schutz
    /// verglich <c>candidatePath.StartsWith(uploadsRoot)</c> — <b>ohne</b>
    /// Trennzeichen. Neben <c>…\uploads</c> rutschte damit alles durch, dessen
    /// Name mit denselben Buchstaben beginnt: <c>uploads-alt</c>,
    /// <c>uploads.bak</c>, <c>uploads2</c>.</para>
    ///
    /// <para>Am laufenden Stand nachgestellt: <c>/uploads/../uploads-alt/geheim.txt</c>
    /// wurde beim Löschen des Grows mitgelöscht. Der schlichte Fall mit
    /// <c>..</c> allein hat es nicht gefunden — der endet eine Ebene höher und
    /// fällt auf.</para>
    /// </remarks>
    [Theory]
    [InlineData("uploads-alt")]
    [InlineData("uploads.bak")]
    [InlineData("uploads2")]
    public void EinGeschwisterordnerMitGleichemAnfang_IstAussen(string ordnername)
    {
        var nachbar = Path.Combine(Path.GetDirectoryName(_pfade.UploadRootPath)!, ordnername);
        Directory.CreateDirectory(nachbar);
        var fremd = Path.Combine(nachbar, "geheim.txt");
        File.WriteAllText(fremd, "wichtig");

        var growId = _grows.CreateGrow(new GrowRun
        {
            Name = "Lauf", HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running, StartDate = DateTime.Today,
        });
        _grows.AddPhoto(new PhotoAsset
        {
            GrowId = growId,
            RelativePath = $"/uploads/../{ordnername}/geheim.txt",
        });

        _grows.DeleteGrow(growId);

        Assert.True(File.Exists(fremd),
            $"Der Ordner {ordnername} liegt NEBEN dem Upload-Ordner, nicht darin — "
            + "und trotzdem hat das Loeschen des Grows die Datei darin mitgenommen. "
            + "Der Vergleich prueft nur den Namensanfang, nicht die Ordnergrenze.");
    }

    private (int GrowId, string Datei, string Relativ) GrowMitFoto()
    {
        var growId = _grows.CreateGrow(new GrowRun
        {
            Name = "Lauf", HydroStyle = HydroStyle.RDWC,
            Status = GrowStatus.Running, StartDate = DateTime.Today,
        });

        var (datei, relativ) = FotoAnlegen(growId, "bild.jpg");
        _grows.AddPhoto(new PhotoAsset
        {
            GrowId = growId,
            RelativePath = relativ,
        });

        return (growId, datei, relativ);
    }

    /// <summary>Legt eine echte Datei dort ab, wo die App sie auch ablegt.</summary>
    private (string Datei, string Relativ) FotoAnlegen(int growId, string name)
    {
        var ordner = Path.Combine(_pfade.UploadRootPath, growId.ToString());
        Directory.CreateDirectory(ordner);
        var datei = Path.Combine(ordner, name);
        File.WriteAllBytes(datei, [0xFF, 0xD8, 0xFF]);
        return (datei, $"/uploads/{growId}/{name}");
    }
}
