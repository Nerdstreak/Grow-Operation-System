using GrowDiary.Web.Api.Controllers;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrowDiary.Web.Tests.Api;

/// <summary>
/// Die eigenen Aufnahmen als Nachschlagewerk zu den Symptomen.
/// </summary>
/// <remarks>
/// <para>Zu den Symptomen der Wissensbasis gab es nie ein Bild. Fremde
/// Beispielbilder sind nicht zu haben, ohne fremde Rechte zu verletzen — die
/// eigene Aufnahme ist ohnehin der bessere Vergleich, gleiches Licht, gleiche
/// Kamera, gleiche Anlage.</para>
///
/// <para>Der wichtigste Test hier ist <see cref="AKeyTheKnowledgeBaseDoesNotKnowIsRefused"/>:
/// eine Zuordnung auf einen Schlüssel, den es nicht gibt, wäre eine Zuordnung
/// ins Leere. Das Bild taucht dann nirgends wieder auf, und niemand merkt es —
/// so wie <c>IsReferenceShot</c>, das seit Jahren gespeichert und nie
/// ausgewertet wird.</para>
/// </remarks>
public sealed class SymptomPhotosApiTests : IDisposable
{
    private readonly string _temp;
    private readonly PhotoRepository _photos;
    private readonly SymptomPhotosApiController _controller;
    private readonly int _growId;
    private readonly string _symptomId;
    private readonly string _anderesSymptom;

    public SymptomPhotosApiTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "SymptomPhotos_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
        var paths = new AppPaths(_temp);
        var tent = TestDatabase.InitializeWithDefaultTent(paths);
        var grows = new GrowRepository(paths);
        _photos = new PhotoRepository(paths);

        // Zwei eigene Symptome statt des ausgelieferten Bestands: so hängt der
        // Test nicht daran, wie die Wissensbasis heute heißt, und er prüft
        // trotzdem gegen echte Schlüssel.
        SchreibeSymptom("wurzeln-braun", "Wurzeln braun und schleimig");
        SchreibeSymptom("blatt-gelb", "Blätter vergilben");

        var loader = new KnowledgeBaseLoader(paths, NullLogger<KnowledgeBaseLoader>.Instance);
        loader.Reload();
        _controller = new SymptomPhotosApiController(_photos, grows, loader);

        // Leer darf der Bestand nicht sein — sonst würde dieser Test nichts
        // prüfen und trotzdem grün bleiben.
        Assert.Equal(2, loader.Symptoms.Count);
        _symptomId = "wurzeln-braun";
        _anderesSymptom = "blatt-gelb";

        _growId = grows.CreateGrow(new GrowRun
        {
            TentId = tent.Id,
            Name = "Lauf mit Wurzelfaeule",
            StartDate = new DateTime(2026, 5, 1),
            Status = GrowStatus.Running,
        });
    }

    /// <summary>Ein Symptom in die Wissensbasis dieses Testlaufs legen.</summary>
    private void SchreibeSymptom(string id, string name)
    {
        var ordner = Path.Combine(_temp, "App_Data", "knowledge", "symptoms");
        Directory.CreateDirectory(ordner);
        File.WriteAllText(Path.Combine(ordner, id + ".json"),
            $$"""{"schemaVersion":"1.0","id":"{{id}}","name":"{{name}}","category":"Root","possibleCauses":[],"suggestedTreatmentIds":[],"suggestedSopIds":[],"diagnosticChecks":[]}""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { }
    }

    private int Foto(string? caption = null)
    {
        var foto = new PhotoAsset
        {
            GrowId = _growId,
            RelativePath = $"/uploads/{_growId}/{Guid.NewGuid():N}.jpg",
            Caption = caption,
            Tag = PhotoTag.Root,
            TakenAtUtc = DateTime.UtcNow,
        };
        _photos.AddPhoto(foto);
        // AddPhoto schreibt die vergebene Id zurueck ans Objekt. Vorher tat es
        // das nicht, und der Upload-Endpunkt lieferte in seiner 201-Antwort
        // „id: 0" — eine Nummer, unter der das Bild nirgends zu finden war.
        Assert.NotEqual(0, foto.Id);
        Assert.Equal(foto.Id, _photos.GetPhotosForGrow(_growId).First(p => p.RelativePath == foto.RelativePath).Id);
        return foto.Id;
    }

    [Fact]
    public void AnAssignedPhotoShowsUpUnderItsSymptom()
    {
        var fotoId = Foto("Wurzeln braun und schleimig");

        Assert.IsType<NoContentResult>(_controller.Assign(fotoId, new SymptomPhotoAssignRequest { SymptomId = _symptomId }));

        var treffer = Assert.IsAssignableFrom<IReadOnlyList<SymptomPhotoDto>>(
            Assert.IsType<OkObjectResult>(_controller.ForSymptom(_symptomId).Result).Value);

        var bild = Assert.Single(treffer);
        Assert.Equal(fotoId, bild.PhotoId);
        // Der Grow-Name gehoert dazu: beim dritten Fall will man wissen, aus
        // welchem Lauf das Bild stammt.
        Assert.Equal("Lauf mit Wurzelfaeule", bild.GrowName);
        Assert.Equal("Wurzeln braun und schleimig", bild.Caption);
    }

    [Fact]
    public void AKeyTheKnowledgeBaseDoesNotKnowIsRefused()
    {
        var fotoId = Foto();

        var ergebnis = _controller.Assign(fotoId, new SymptomPhotoAssignRequest { SymptomId = "gibt-es-nicht" });

        Assert.IsType<BadRequestObjectResult>(ergebnis);
        Assert.Null(_photos.GetById(fotoId)!.SymptomId);
    }

    [Fact]
    public void TheAssignmentCanBeUndone()
    {
        var fotoId = Foto();
        _controller.Assign(fotoId, new SymptomPhotoAssignRequest { SymptomId = _symptomId });

        _controller.Assign(fotoId, new SymptomPhotoAssignRequest { SymptomId = null });

        Assert.Null(_photos.GetById(fotoId)!.SymptomId);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<SymptomPhotoDto>>(
            Assert.IsType<OkObjectResult>(_controller.ForSymptom(_symptomId).Result).Value));
    }

    [Fact]
    public void TheCountsTellWhereThereIsSomethingToShow()
    {
        // Damit die Wissensseite nicht „Bilder" anbietet, wo keine sind.
        _controller.Assign(Foto(), new SymptomPhotoAssignRequest { SymptomId = _symptomId });
        _controller.Assign(Foto(), new SymptomPhotoAssignRequest { SymptomId = _symptomId });

        var zahlen = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            Assert.IsType<OkObjectResult>(_controller.Counts().Result).Value);

        Assert.Equal(2, zahlen[_symptomId]);
        Assert.False(zahlen.ContainsKey(_anderesSymptom));
    }

    [Fact]
    public void AMissingPhotoIsNotFound()
    {
        Assert.IsType<NotFoundObjectResult>(_controller.Assign(9999, new SymptomPhotoAssignRequest { SymptomId = _symptomId }));
    }

    [Fact]
    public void PhotosWithoutASymptomStayOutOfEveryList()
    {
        Foto("nur ein huebsches Bild");

        var zahlen = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(
            Assert.IsType<OkObjectResult>(_controller.Counts().Result).Value);

        Assert.Empty(zahlen);
    }
}
