using System.Text.Json;
using GrowDiary.Web.Infrastructure;
using GrowDiary.Web.Models;
using GrowDiary.Web.Services;
using GrowDiary.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GrowDiary.Web.Controllers;

[Route("grows")]
public sealed class GrowsController : Controller
{
    private readonly GrowRepository _repository;
    private readonly RecommendationEngine _recommendationEngine;
    private readonly AppPaths _paths;
    private readonly CultivationKnowledgeService _knowledgeService;
    private readonly MeasurementSanityService _measurementSanityService;
    private readonly GrowDashboardComposer _composer;
    private readonly TaskRepository _taskRepository;
    private readonly JournalRepository _journalRepository;
    private readonly AuditRepository _auditRepository;
    private readonly TemplateRepository _templateRepository;
    private readonly TimelineComposer _timelineComposer;
    private readonly HomeAssistantService _homeAssistantService;
    private readonly WeekCounterService _weekCounter;
    private readonly HarvestRepository _harvestRepository;

    public GrowsController(
        GrowRepository repository,
        RecommendationEngine recommendationEngine,
        AppPaths paths,
        CultivationKnowledgeService knowledgeService,
        MeasurementSanityService measurementSanityService,
        GrowDashboardComposer composer,
        TaskRepository taskRepository,
        JournalRepository journalRepository,
        AuditRepository auditRepository,
        TemplateRepository templateRepository,
        TimelineComposer timelineComposer,
        HomeAssistantService homeAssistantService,
        WeekCounterService weekCounter,
        HarvestRepository harvestRepository)
    {
        _repository = repository;
        _recommendationEngine = recommendationEngine;
        _paths = paths;
        _knowledgeService = knowledgeService;
        _measurementSanityService = measurementSanityService;
        _composer = composer;
        _taskRepository = taskRepository;
        _journalRepository = journalRepository;
        _auditRepository = auditRepository;
        _templateRepository = templateRepository;
        _timelineComposer = timelineComposer;
        _homeAssistantService = homeAssistantService;
        _weekCounter = weekCounter;
        _harvestRepository = harvestRepository;
    }

    // Route deaktiviert – wird von Blazor Grows.razor übernommen
    [HttpGet("mvc-legacy-list")]
    public IActionResult Index(string? search = null)
    {
        var model = new GrowListPageViewModel
        {
            Title = "Aktive Grows",
            Description = "Nur laufende oder geplante Runs – gruppiert nach Zelt und optimiert für schnellen Alltag.",
            Search = search ?? string.Empty,
            ShowArchived = false,
            Grows = _repository.GetActiveGrows(search),
            Stats = _repository.GetDashboardStats()
        };

        return View(model);
    }

    [HttpGet("archived")]
    public IActionResult Archived(string? search = null)
    {
        var model = new GrowListPageViewModel
        {
            Title = "Archiv",
            Description = "Abgeschlossene und abgebrochene Runs – für Rückblicke, Learnings und Vergleiche.",
            Search = search ?? string.Empty,
            ShowArchived = true,
            Grows = _repository.GetArchivedGrows(search),
            Stats = _repository.GetDashboardStats()
        };

        return View("Index", model);
    }

    [HttpGet("create")]
    public IActionResult Create(int? templateId = null)
    {
        var template = templateId.HasValue ? _templateRepository.Get(templateId.Value) : null;
        var model = template is null ? new GrowFormViewModel() : GrowFormViewModel.FromTemplate(template);
        return View(PrepareGrowForm(model));
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public IActionResult Create(GrowFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(PrepareGrowForm(model));
        }

        var grow = model.ToGrow();
        var growId = _repository.CreateGrow(grow);

        var savedGrow = _repository.GetGrow(growId)!;
        var weekInfo = _weekCounter.Calculate(savedGrow);
        if (savedGrow.Status == GrowStatus.Planning &&
            weekInfo.State != GrowCounterState.WaitingForGermination &&
            weekInfo.State != GrowCounterState.WaitingForRooting &&
            weekInfo.State != GrowCounterState.NoData)
        {
            savedGrow.Status = GrowStatus.Running;
            _repository.UpdateGrow(savedGrow);
        }

        _auditRepository.Add(new AuditEntry
        {
            GrowId = growId,
            EntityType = "Grow",
            Action = "Grow angelegt",
            Summary = $"Setup „{model.Name}“ wurde erstellt{(model.TemplateId.HasValue ? $" auf Basis des Templates #{model.TemplateId}" : string.Empty)}."
        });
        TempData["Flash"] = "Grow angelegt.";
        return Redirect($"/grows/{growId}");
    }

    // Route deaktiviert – wird von Blazor GrowDetail.razor übernommen.
    // Redirects von POST-Actions verwenden jetzt Redirect($"/grows/{id}") direkt.
    [HttpGet("mvc-legacy-detail/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var model = await BuildDetailsViewModelAsync(id, null, null, null, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFound();
        }

        return View(PrepareGrowForm(GrowFormViewModel.FromGrow(grow)));
    }

    [HttpPost("{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, GrowFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(PrepareGrowForm(model));
        }

        var existing = _repository.GetGrow(id);
        if (existing is null)
        {
            return NotFound();
        }

        var grow = model.ToGrow();
        grow.Id = id;
        grow.CreatedAtUtc = existing.CreatedAtUtc;
        _repository.UpdateGrow(grow);
        _auditRepository.Add(new AuditEntry
        {
            GrowId = id,
            EntityType = "Grow",
            EntityId = id,
            Action = "Setup geändert",
            Summary = $"Setup von „{grow.Name}“ aktualisiert. Status: {grow.Status}, Medium: {grow.Profile.Label}."
        });

        TempData["Flash"] = "Grow gespeichert.";
        return Redirect($"/grows/{id}");
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        _repository.DeleteGrow(id);
        TempData["Flash"] = "Grow gelöscht.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/measurements")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMeasurement(int id, MeasurementFormViewModel model)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFound();
        }

        model.GrowId = id;
        model.GrowName = grow.Name;
        model.MediumType = grow.MediumType;
        model.FeedingStyle = grow.FeedingStyle;
        model.HydroStyle = grow.HydroStyle;

        Measurement measurement;
        try
        {
            measurement = model.ToMeasurement();
        }
        catch
        {
            ModelState.AddModelError(nameof(model.TakenAtLocal), "Datum / Uhrzeit konnte nicht gelesen werden.");
            var invalidModel = await BuildDetailsViewModelAsync(id, model, null, null);
            return invalidModel is null ? NotFound() : View("Details", invalidModel);
        }

        measurement.GrowId = id;
        _measurementSanityService.ApplyBlockingValidation(ModelState, grow, measurement);
        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildDetailsViewModelAsync(id, model, null, null);
            return invalidModel is null ? NotFound() : View("Details", invalidModel);
        }

        var measurementId = _repository.CreateMeasurement(measurement);
        await SavePhotosAsync(grow, measurementId, model.Photos, model.PhotoTag, model.PhotoCaption, model.UseAsReferenceShot, model.Source);
        _auditRepository.Add(new AuditEntry
        {
            GrowId = id,
            EntityType = "Measurement",
            EntityId = measurementId,
            Action = "Messung gespeichert",
            Summary = $"{measurement.Stage} am {measurement.TakenAt:dd.MM.yyyy HH:mm} ({measurement.Source})."
        });

        var freshGrow = _repository.GetGrow(id);
        if (freshGrow?.Status == GrowStatus.Planning)
        {
            freshGrow.Status = GrowStatus.Running;
            _repository.UpdateGrow(freshGrow);
        }

        TempData["Flash"] = "Messung gespeichert.";
        return Redirect($"/grows/{id}");
    }

    [HttpGet("measurements/{measurementId:int}/edit")]
    public IActionResult EditMeasurement(int measurementId)
    {
        var measurement = _repository.GetMeasurement(measurementId);
        if (measurement is null)
        {
            return NotFound();
        }

        var grow = _repository.GetGrow(measurement.GrowId);
        if (grow is null)
        {
            return NotFound();
        }

        var photos = _repository.GetPhotosForMeasurement(measurementId);
        var model = MeasurementFormViewModel.FromMeasurement(grow, measurement, photos);
        return View(model);
    }

    [HttpPost("measurements/{measurementId:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMeasurement(int measurementId, MeasurementFormViewModel model)
    {
        var existingMeasurement = _repository.GetMeasurement(measurementId);
        if (existingMeasurement is null)
        {
            return NotFound();
        }

        var grow = _repository.GetGrow(existingMeasurement.GrowId);
        if (grow is null)
        {
            return NotFound();
        }

        model.Id = measurementId;
        model.GrowId = grow.Id;
        model.GrowName = grow.Name;
        model.MediumType = grow.MediumType;
        model.FeedingStyle = grow.FeedingStyle;
        model.HydroStyle = grow.HydroStyle;

        Measurement measurement;
        try
        {
            measurement = model.ToMeasurement();
        }
        catch
        {
            ModelState.AddModelError(nameof(model.TakenAtLocal), "Datum / Uhrzeit konnte nicht gelesen werden.");
            model.ExistingPhotos = _repository.GetPhotosForMeasurement(measurementId);
            return View(model);
        }

        measurement.Id = measurementId;
        measurement.GrowId = grow.Id;
        measurement.CreatedAtUtc = existingMeasurement.CreatedAtUtc;

        _measurementSanityService.ApplyBlockingValidation(ModelState, grow, measurement);
        if (!ModelState.IsValid)
        {
            model.ExistingPhotos = _repository.GetPhotosForMeasurement(measurementId);
            return View(model);
        }

        _repository.UpdateMeasurement(measurement);
        await SavePhotosAsync(grow, measurementId, model.Photos, model.PhotoTag, model.PhotoCaption, model.UseAsReferenceShot, model.Source);
        _auditRepository.Add(new AuditEntry
        {
            GrowId = grow.Id,
            EntityType = "Measurement",
            EntityId = measurementId,
            Action = "Messung geändert",
            Summary = $"Messung vom {measurement.TakenAt:dd.MM.yyyy HH:mm} aktualisiert."
        });

        TempData["Flash"] = "Messung aktualisiert.";
        return Redirect($"/grows/{grow.Id}");
    }

    [HttpPost("measurements/{measurementId:int}/delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteMeasurement(int measurementId)
    {
        var measurement = _repository.GetMeasurement(measurementId);
        if (measurement is null)
        {
            return NotFound();
        }

        var growId = measurement.GrowId;
        _repository.DeleteMeasurement(measurementId);
        _auditRepository.Add(new AuditEntry
        {
            GrowId = growId,
            EntityType = "Measurement",
            EntityId = measurementId,
            Action = "Messung gelöscht",
            Summary = $"Messung vom {measurement.TakenAt:dd.MM.yyyy HH:mm} entfernt."
        });
        TempData["Flash"] = "Messung gelöscht.";
        return Redirect($"/grows/{growId}");
    }

    [HttpPost("{id:int}/tasks")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTask(int id, GrowTaskFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "Bitte gib der Aufgabe einen Titel.");
            var invalidModel = await BuildDetailsViewModelAsync(id, null, null, model);
            return invalidModel is null ? NotFound() : View("Details", invalidModel);
        }

        var task = model.ToTask(id);
        var taskId = _taskRepository.Create(task);
        _auditRepository.Add(new AuditEntry
        {
            GrowId = id,
            EntityType = "Task",
            EntityId = taskId,
            Action = "Aufgabe erstellt",
            Summary = $"Task „{task.Title}“ wurde angelegt."
        });
        TempData["Flash"] = "Aufgabe angelegt.";
        return Redirect($"/grows/{id}");
    }

    [HttpPost("tasks/{taskId:int}/done")]
    [ValidateAntiForgeryToken]
    public IActionResult CompleteTask(int taskId)
    {
        var task = _taskRepository.Get(taskId);
        if (task is null)
        {
            return NotFound();
        }

        _taskRepository.SetStatus(taskId, GrowTaskStatus.Done);
        _auditRepository.Add(new AuditEntry
        {
            GrowId = task.GrowId,
            EntityType = "Task",
            EntityId = taskId,
            Action = "Aufgabe erledigt",
            Summary = task.Title
        });
        TempData["Flash"] = "Aufgabe abgehakt.";
        return Redirect($"/grows/{task.GrowId}");
    }

    [HttpPost("tasks/{taskId:int}/skip")]
    [ValidateAntiForgeryToken]
    public IActionResult SkipTask(int taskId)
    {
        var task = _taskRepository.Get(taskId);
        if (task is null)
        {
            return NotFound();
        }

        _taskRepository.SetStatus(taskId, GrowTaskStatus.Skipped);
        _auditRepository.Add(new AuditEntry
        {
            GrowId = task.GrowId,
            EntityType = "Task",
            EntityId = taskId,
            Action = "Aufgabe übersprungen",
            Summary = task.Title
        });
        TempData["Flash"] = "Aufgabe übersprungen.";
        return Redirect($"/grows/{task.GrowId}");
    }

    [HttpPost("tasks/{taskId:int}/delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteTask(int taskId)
    {
        var task = _taskRepository.Get(taskId);
        if (task is null)
        {
            return NotFound();
        }

        _taskRepository.Delete(taskId);
        TempData["Flash"] = "Aufgabe gelöscht.";
        return Redirect($"/grows/{task.GrowId}");
    }

    [HttpPost("{id:int}/journal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddJournalEntry(int id, JournalEntryFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Body) && string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Body), "Bitte gib mindestens einen Titel oder Text ein.");
            var invalidModel = await BuildDetailsViewModelAsync(id, null, model, null);
            return invalidModel is null ? NotFound() : View("Details", invalidModel);
        }

        var entryId = _journalRepository.Create(model.ToEntry(id));
        _auditRepository.Add(new AuditEntry
        {
            GrowId = id,
            EntityType = "JournalEntry",
            EntityId = entryId,
            Action = "Journal aktualisiert",
            Summary = model.Title ?? model.EntryType.ToString()
        });
        TempData["Flash"] = "Journal-Eintrag gespeichert.";
        return Redirect($"/grows/{id}");
    }

    [HttpGet("compare")]
    public IActionResult Compare(int? leftGrowId = null, int? rightGrowId = null)
    {
        var growOptions = _repository.GetAllGrows();
        var model = new ComparePageViewModel
        {
            GrowOptions = growOptions,
            LeftGrowId = leftGrowId,
            RightGrowId = rightGrowId
        };

        if (leftGrowId.HasValue)
        {
            model.LeftGrow = _repository.GetGrow(leftGrowId.Value);
            model.LeftMeasurement = model.LeftGrow?.LatestMeasurement;
            model.LeftPhoto = model.LeftGrow is null ? null : _repository.GetPhotosForGrow(model.LeftGrow.Id).FirstOrDefault();
            if (model.LeftGrow is not null)
            {
                var previous = model.LeftMeasurement is null ? null : _repository.GetPreviousMeasurement(model.LeftGrow.Id, model.LeftMeasurement.TakenAt, model.LeftMeasurement.Id);
                var lastSolutionChangeAt = _repository.GetMeasurementsForGrow(model.LeftGrow.Id)
                    .Where(x => x.SolutionChange)
                    .OrderByDescending(x => x.TakenAt)
                    .Select(x => (DateTime?)x.TakenAt)
                    .FirstOrDefault();
                model.LeftRecommendations = _recommendationEngine.Evaluate(model.LeftGrow, model.LeftMeasurement, previous, lastSolutionChangeAt);
            }
        }

        if (rightGrowId.HasValue)
        {
            model.RightGrow = _repository.GetGrow(rightGrowId.Value);
            model.RightMeasurement = model.RightGrow?.LatestMeasurement;
            model.RightPhoto = model.RightGrow is null ? null : _repository.GetPhotosForGrow(model.RightGrow.Id).FirstOrDefault();
            if (model.RightGrow is not null)
            {
                var previous = model.RightMeasurement is null ? null : _repository.GetPreviousMeasurement(model.RightGrow.Id, model.RightMeasurement.TakenAt, model.RightMeasurement.Id);
                var lastSolutionChangeAt = _repository.GetMeasurementsForGrow(model.RightGrow.Id)
                    .Where(x => x.SolutionChange)
                    .OrderByDescending(x => x.TakenAt)
                    .Select(x => (DateTime?)x.TakenAt)
                    .FirstOrDefault();
                model.RightRecommendations = _recommendationEngine.Evaluate(model.RightGrow, model.RightMeasurement, previous, lastSolutionChangeAt);
            }
        }

        return View(model);
    }

    [HttpGet("{id:int}/addback")]
    public IActionResult Addback(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null) return NotFound();

        double? suggestedReservoir = null;
        if (!string.IsNullOrWhiteSpace(grow.ReservoirSize))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                grow.ReservoirSize, @"(\d+([.,]\d+)?)");
            if (match.Success &&
                double.TryParse(
                    match.Value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed))
            {
                suggestedReservoir = parsed;
            }
        }

        var measurements = _repository.GetMeasurementsForGrow(id);
        var latestEc = measurements
            .OrderByDescending(m => m.TakenAt)
            .Where(m => m.ReservoirEc.HasValue)
            .Select(m => m.ReservoirEc)
            .FirstOrDefault();

        var latest = measurements.FirstOrDefault();
        var stage = latest?.Stage ?? GrowStage.Veg;
        var targets = TargetValueService.GetTargets(grow.HydroStyle, stage);
        var suggestedEcZiel = targets is not null
            ? Math.Round((targets.EcMin + targets.EcMax) / 2, 2)
            : (double?)null;

        var model = new AddbackViewModel
        {
            GrowId = id,
            GrowName = grow.Name,
            SuggestedReservoirLiters = suggestedReservoir,
            SuggestedEcIst = latestEc,
            SuggestedEcZiel = suggestedEcZiel,
            ReservoirLiters = suggestedReservoir,
            EcIst = latestEc,
            EcZiel = suggestedEcZiel,
            EcStock = 3.0
        };

        return View(model);
    }

    [HttpPost("{id:int}/addback")]
    [ValidateAntiForgeryToken]
    public IActionResult Addback(int id, AddbackViewModel model)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null) return NotFound();

        model.GrowId = id;
        model.GrowName = grow.Name;

        if (model.ReservoirLiters.HasValue
            && model.EcIst.HasValue
            && model.EcZiel.HasValue
            && model.EcStock.HasValue)
        {
            model.Result = AddbackCalculator.Calculate(
                model.ReservoirLiters.Value,
                model.EcIst.Value,
                model.EcZiel.Value,
                model.EcStock.Value);
        }

        return View(model);
    }

    [HttpGet("{id:int}/export")]
    public IActionResult Export(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFound();
        }

        var payload = new
        {
            grow,
            tent = _repository.GetTentForGrow(id),
            measurements = _repository.GetMeasurementsForGrow(id),
            photos = _repository.GetPhotosForGrow(id),
            tasks = _taskRepository.GetOpenForGrow(id),
            journal = _journalRepository.GetForGrow(id),
            audit = _auditRepository.GetRecentForGrow(id, 50)
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"grow-{id}-{DateTime.Now:yyyyMMdd-HHmm}.json");
    }

    [HttpPost("{id:int}/confirm-germination")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmGermination(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null) return NotFound();
        if (grow.GerminatedAt.HasValue)
            return Redirect($"/grows/{id}");

        var now = DateTime.Now;
        grow.GerminatedAt = now;
        _repository.UpdateGrow(grow);
        if (grow.Status == GrowStatus.Planning)
        {
            grow.Status = GrowStatus.Running;
            _repository.UpdateGrow(grow);
        }
        _journalRepository.Create(new JournalEntry
        {
            GrowId = id,
            EntryType = JournalEntryType.GerminationConfirmed,
            Body = "Keimung bestätigt.",
            OccurredAtUtc = DateTime.UtcNow
        });
        TempData["Flash"] = "Keimung bestätigt.";
        return Redirect($"/grows/{id}");
    }

    [HttpPost("{id:int}/confirm-rooting")]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmRooting(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null) return NotFound();
        if (grow.RootedAt.HasValue)
            return Redirect($"/grows/{id}");

        var now = DateTime.Now;
        grow.RootedAt = now;
        grow.CloneIsRooted = true;
        _repository.UpdateGrow(grow);
        if (grow.Status == GrowStatus.Planning)
        {
            grow.Status = GrowStatus.Running;
            _repository.UpdateGrow(grow);
        }
        _journalRepository.Create(new JournalEntry
        {
            GrowId = id,
            EntryType = JournalEntryType.CloneRooted,
            Body = "Bewurzelung bestätigt.",
            OccurredAtUtc = DateTime.UtcNow
        });
        TempData["Flash"] = "Bewurzelung bestätigt.";
        return Redirect($"/grows/{id}");
    }

    [HttpGet("{id:int}/harvest")]
    public IActionResult Harvest(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFound();
        }

        var existing = _harvestRepository.GetForGrow(id);
        var vm = existing is null
            ? new HarvestFormViewModel { GrowId = id, GrowName = grow.Name }
            : HarvestFormViewModel.FromEntry(existing, grow.Name);

        return View(vm);
    }

    [HttpPost("{id:int}/harvest")]
    [ValidateAntiForgeryToken]
    public IActionResult Harvest(int id, HarvestFormViewModel model)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return NotFound();
        }

        model.GrowId = id;
        model.GrowName = grow.Name;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = _harvestRepository.GetForGrow(id);
        if (existing is null)
        {
            _harvestRepository.Create(model.ToEntry());
            if (grow.Status == GrowStatus.Running)
            {
                grow.Status = GrowStatus.Completed;
                grow.EndDate = DateTime.Today;
                _repository.UpdateGrow(grow);
            }
            _auditRepository.Add(new AuditEntry
            {
                GrowId = id,
                EntityType = "Harvest",
                Action = "Ernte dokumentiert",
                Summary = $"Ernte am {model.HarvestedAtLocal} eingetragen."
            });
            TempData["Flash"] = "Ernte gespeichert.";
        }
        else
        {
            var entry = model.ToEntry();
            entry.Id = existing.Id;
            entry.CreatedAtUtc = existing.CreatedAtUtc;
            _harvestRepository.Update(entry);
            TempData["Flash"] = "Ernte aktualisiert.";
        }

        return Redirect($"/grows/{id}");
    }

    [HttpPost("{id:int}/flip-to-flower")]
    [ValidateAntiForgeryToken]
    public IActionResult FlipToFlower(int id)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null) return NotFound();
        if (grow.SeedType == SeedType.Autoflower)
            return BadRequest("Autoflower braucht keinen Flip.");
        if (grow.FlipDate.HasValue)
            return Redirect($"/grows/{id}");

        grow.FlipDate = DateTime.Today;
        _repository.UpdateGrow(grow);
        _journalRepository.Create(new JournalEntry
        {
            GrowId = id,
            EntryType = JournalEntryType.FlipToFlower,
            Body = "Auf 12/12 geflippt.",
            OccurredAtUtc = DateTime.UtcNow
        });
        TempData["Flash"] = "Flip zu 12/12 eingetragen.";
        return Redirect($"/grows/{id}");
    }

    private async Task<GrowDetailsViewModel?> BuildDetailsViewModelAsync(
        int id,
        MeasurementFormViewModel? formOverride,
        JournalEntryFormViewModel? journalOverride,
        GrowTaskFormViewModel? taskOverride,
        CancellationToken cancellationToken = default)
    {
        var grow = _repository.GetGrow(id);
        if (grow is null)
        {
            return null;
        }

        var tent = _repository.GetTentForGrow(id);
        var measurements = _repository.GetMeasurementsForGrow(id);
        var latestMeasurement = measurements.FirstOrDefault();
        var previousMeasurement = latestMeasurement is null ? null : _repository.GetPreviousMeasurement(id, latestMeasurement.TakenAt, latestMeasurement.Id);
        var lastSolutionChangeAt = measurements
            .Where(x => x.SolutionChange)
            .OrderByDescending(x => x.TakenAt)
            .Select(x => (DateTime?)x.TakenAt)
            .FirstOrDefault();

        var nutrientProgram = _knowledgeService.MatchProgram(grow.Nutrients);
        var mediumPlaybook = _knowledgeService.GetMediumPlaybooks().FirstOrDefault(x => x.Key == "hydro");

        var photos = _repository.GetPhotosForGrow(id);
        var journal = _journalRepository.GetForGrow(id);
        var tasks = _taskRepository.GetOpenForGrow(id);
        var audit = _auditRepository.GetRecentForGrow(id);

        return new GrowDetailsViewModel
        {
            Grow = grow,
            Tent = tent,
            MeasurementForm = formOverride ?? await BuildMeasurementFormAsync(grow, tent, cancellationToken),
            JournalForm = journalOverride ?? new JournalEntryFormViewModel(),
            TaskForm = taskOverride ?? new GrowTaskFormViewModel(),
            Measurements = measurements,
            Photos = photos,
            LastSolutionChangeAt = lastSolutionChangeAt,
            NutrientProgram = nutrientProgram,
            MediumPlaybook = mediumPlaybook,
            Recommendations = _recommendationEngine.Evaluate(grow, latestMeasurement, previousMeasurement, lastSolutionChangeAt),
            MainChart = _composer.BuildGrowMainChart(grow, measurements),
            SecondaryChart = _composer.BuildGrowSecondaryChart(grow, measurements),
            WateringChart = _composer.BuildGrowWateringChart(measurements),
            OpenTasks = tasks,
            JournalEntries = journal,
            AuditEntries = audit,
            Timeline = _timelineComposer.Build(grow, measurements, photos, journal, tasks, audit),
            PhotoComparison = _timelineComposer.BuildComparison(photos),
            WeekInfo = _weekCounter.Calculate(grow),
            Harvest = _harvestRepository.GetForGrow(id)
        };
    }

    private async Task<MeasurementFormViewModel> BuildMeasurementFormAsync(
        GrowRun grow, Tent? tent, CancellationToken cancellationToken)
    {
        MeasurementFormViewModel vm;

        if (tent is null)
        {
            vm = MeasurementFormViewModel.ForGrow(grow);
        }
        else
        {
            var settings = _repository.GetHomeAssistantSettings();
            if (!settings.IsConfigured)
            {
                vm = MeasurementFormViewModel.ForGrow(grow);
            }
            else
            {
                try
                {
                    var haStates = await _homeAssistantService.GetStatesAsync(settings, tent, cancellationToken);
                    vm = MeasurementFormViewModel.ForGrowWithHa(grow, haStates);
                }
                catch
                {
                    vm = MeasurementFormViewModel.ForGrow(grow);
                }
            }
        }

        var weekInfo = _weekCounter.Calculate(grow);
        vm.Stage = DetermineStageFromWeekInfo(weekInfo);
        return vm;
    }

    public static GrowStage DetermineStageFromWeekInfo(GrowWeekInfo weekInfo)
    {
        return weekInfo.State switch
        {
            GrowCounterState.WaitingForGermination => GrowStage.Seedling,
            GrowCounterState.WaitingForRooting     => GrowStage.Clone,
            GrowCounterState.Vegetating            => GrowStage.Veg,
            GrowCounterState.Flowering             => GrowStage.Flower,
            GrowCounterState.Autoflowering         => weekInfo.AutoflowerWeek <= 4
                                                          ? GrowStage.Veg
                                                          : GrowStage.Flower,
            _                                      => GrowStage.Veg
        };
    }

    private GrowFormViewModel PrepareGrowForm(GrowFormViewModel model)
    {
        model.NutrientSuggestions = _knowledgeService.GetHydroProgramNames().ToList();
        model.Templates = _templateRepository.GetAll();
        model.TentOptions = _repository.GetTents()
            .Select(t => new SelectListItem(t.Name, t.Id.ToString(), model.TentId == t.Id))
            .ToList();
        return model;
    }

    private async Task SavePhotosAsync(GrowRun grow, int measurementId, IEnumerable<IFormFile>? photos, PhotoTag tag, string? caption, bool useAsReferenceShot, ValueOrigin source)
    {
        if (photos is null)
        {
            return;
        }

        var directory = Path.Combine(_paths.UploadRootPath, grow.Id.ToString());
        Directory.CreateDirectory(directory);

        foreach (var photo in photos)
        {
            if (photo.Length <= 0)
            {
                continue;
            }

            var extension = Path.GetExtension(photo.FileName);
            var safeExtension = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension.ToLowerInvariant();
            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{safeExtension}";
            var physicalPath = Path.Combine(directory, fileName);

            await using (var stream = System.IO.File.Create(physicalPath))
            {
                await photo.CopyToAsync(stream);
            }

            var relativePath = $"/uploads/{grow.Id}/{fileName}".Replace("\\", "/");
            _repository.AddPhoto(new PhotoAsset
            {
                GrowId = grow.Id,
                MeasurementId = measurementId,
                RelativePath = relativePath,
                Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
                Tag = tag,
                Source = source,
                IsReferenceShot = useAsReferenceShot,
                TakenAtUtc = DateTime.UtcNow
            });
        }
    }
}
