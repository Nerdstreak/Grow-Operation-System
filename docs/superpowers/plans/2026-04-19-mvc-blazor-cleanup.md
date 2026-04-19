# MVC→Blazor Cleanup + Shared Components Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Alte MVC-Routes per Redirect auf Blazor umleiten, duplizierten Markup in Shared Components kapseln, obsolete MVC-Views löschen und die App starten.

**Architecture:** Redirect-first — GET-Actions in Controllern werden zu Einzeilern, POST-Actions bleiben unberührt. Vier neue Blazor-Components in `Components/Shared/` ersetzen inline-dupliziertes Markup. Danach werden 9 nun tote MVC-View-Dateien gelöscht.

**Tech Stack:** ASP.NET Core 8, Blazor Server, SQLite/ADO.NET, kein ORM, kein Test-Runner konfiguriert (Verifikation via `dotnet build`)

---

## Datei-Übersicht

| Aktion | Datei |
|--------|-------|
| Modify | `GrowDiary.Web/Controllers/TentsController.cs` |
| Modify | `GrowDiary.Web/Controllers/KnowledgeController.cs` |
| Modify | `GrowDiary.Web/Controllers/SettingsController.cs` |
| Modify | `GrowDiary.Web/Controllers/GrowsController.cs` |
| Create | `GrowDiary.Web/Components/Shared/TaskList.razor` |
| Create | `GrowDiary.Web/Components/Shared/FocusCard.razor` |
| Create | `GrowDiary.Web/Components/Shared/AddbackPanel.razor` |
| Create | `GrowDiary.Web/Components/Shared/ChartCard.razor` |
| Modify | `GrowDiary.Web/Components/Pages/Home.razor` |
| Modify | `GrowDiary.Web/Components/Pages/GrowDetail.razor` |
| Delete | `GrowDiary.Web/Views/Home/Index.cshtml` |
| Delete | `GrowDiary.Web/Views/Grows/Index.cshtml` |
| Delete | `GrowDiary.Web/Views/Grows/Details.cshtml` |
| Delete | `GrowDiary.Web/Views/Grows/Create.cshtml` |
| Delete | `GrowDiary.Web/Views/Grows/Edit.cshtml` |
| Delete | `GrowDiary.Web/Views/Tents/Index.cshtml` |
| Delete | `GrowDiary.Web/Views/Tents/Details.cshtml` |
| Delete | `GrowDiary.Web/Views/Knowledge/Index.cshtml` |
| Delete | `GrowDiary.Web/Views/Settings/Index.cshtml` |

---

## Task 1: Controller-Redirects setzen

**Files:**
- Modify: `GrowDiary.Web/Controllers/TentsController.cs`
- Modify: `GrowDiary.Web/Controllers/KnowledgeController.cs`
- Modify: `GrowDiary.Web/Controllers/SettingsController.cs`
- Modify: `GrowDiary.Web/Controllers/GrowsController.cs`

- [ ] **Schritt 1: TentsController — Index und Details ersetzen**

In `Controllers/TentsController.cs` die beiden GET-Actions durch Redirects ersetzen. Die POST/API-Actions (Live, CameraSnapshot, CameraStream, LatestSnapshot) bleiben unverändert.

```csharp
[HttpGet("")]
public IActionResult Index(int? selected, CancellationToken cancellationToken)
    => Redirect("/zelte");

[HttpGet("{id:int}")]
public IActionResult Details(int id, CancellationToken cancellationToken)
    => Redirect($"/zelte/{id}");
```

Die Signaturen (Parameter) bleiben erhalten damit keine Routing-Konflikte entstehen; die Parameter werden einfach ignoriert.

- [ ] **Schritt 2: KnowledgeController — Index ersetzen**

In `Controllers/KnowledgeController.cs`:

```csharp
[HttpGet("")]
public IActionResult Index(string? key = null)
    => Redirect("/wissen");
```

Den `_knowledgeService`-Inject-Konstruktor und das Private-Feld können stehen bleiben — kein Aufwand, kein Risiko.

- [ ] **Schritt 3: SettingsController — Index ersetzen**

In `Controllers/SettingsController.cs`:

```csharp
[HttpGet("")]
public IActionResult Index()
    => Redirect("/einstellungen");
```

Die POST-Actions `SaveHomeAssistant` und `SaveTent` bleiben unverändert. Ihr `RedirectToAction(nameof(Index))` führt über `/settings` → `/einstellungen` (eine Redirect-Kette, funktioniert korrekt).

- [ ] **Schritt 4: GrowsController — Create GET, Edit GET, Delete POST**

In `Controllers/GrowsController.cs` drei Stellen ändern:

**Create GET** (war: baut GrowFormViewModel und gibt View zurück):
```csharp
[HttpGet("create")]
public IActionResult Create(int? templateId = null)
    => Redirect("/grows/new");
```

**Edit GET** (war: lädt Grow und gibt Edit-View zurück):
```csharp
[HttpGet("{id:int}/edit")]
public IActionResult Edit(int id)
    => Redirect($"/grows/{id}/setup");
```

**Delete POST** (war: `return RedirectToAction(nameof(Index))` → ging auf `mvc-legacy-list`):
```csharp
[HttpPost("{id:int}/delete")]
[ValidateAntiForgeryToken]
public IActionResult Delete(int id)
{
    _repository.DeleteGrow(id);
    TempData["Flash"] = "Grow gelöscht.";
    return Redirect("/grows");
}
```

- [ ] **Schritt 5: Build prüfen**

```bash
cd "D:/Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
```

Erwartet: `Build succeeded. 0 Error(s)`

- [ ] **Schritt 6: Commit**

```bash
cd "D:/Grow Operation System new"
git add GrowDiary.Web/Controllers/TentsController.cs \
        GrowDiary.Web/Controllers/KnowledgeController.cs \
        GrowDiary.Web/Controllers/SettingsController.cs \
        GrowDiary.Web/Controllers/GrowsController.cs
git commit -m "feat: redirect legacy MVC routes to Blazor equivalents"
```

---

## Task 2: TaskList.razor erstellen + Home.razor und GrowDetail.razor refaktorieren

**Files:**
- Create: `GrowDiary.Web/Components/Shared/TaskList.razor`
- Modify: `GrowDiary.Web/Components/Pages/Home.razor`
- Modify: `GrowDiary.Web/Components/Pages/GrowDetail.razor`

- [ ] **Schritt 1: TaskList.razor anlegen**

Neue Datei `GrowDiary.Web/Components/Shared/TaskList.razor`:

```razor
@foreach (var task in Tasks.Take(6))
{
    <div class="task-item">
        <button type="button"
                class="task-check @(task.Status == GrowTaskStatus.Done ? "done" : "")"
                aria-label="@ToggleLabel(task)"
                aria-pressed="@(task.Status == GrowTaskStatus.Done)"
                @onclick="() => OnToggle.InvokeAsync(task)">
            @if (task.Status == GrowTaskStatus.Done)
            {
                <svg aria-hidden="true" width="8" height="8" viewBox="0 0 8 8">
                    <path d="M1 4l2 2 4-4" stroke="oklch(10% 0.02 155)" stroke-width="1.5" fill="none" stroke-linecap="round" />
                </svg>
            }
        </button>
        <div style="flex:1; @(task.Status == GrowTaskStatus.Done ? "opacity:0.4" : "")">
            <div class="task-title" style="@(task.Status == GrowTaskStatus.Done ? "text-decoration:line-through" : "")">
                @task.Title
            </div>
            <div class="task-sub">@SubLabel(task)</div>
        </div>
        @if (ShowPriority)
        {
            <div class="prio-dot @PrioClass(task.Priority)"></div>
        }
    </div>
}

@code {
    [Parameter] public List<GrowTask> Tasks { get; set; } = new();
    [Parameter] public EventCallback<GrowTask> OnToggle { get; set; }
    /// <summary>
    /// true  → zeigt GrowName + HH:mm (Home-Dashboard)
    /// false → zeigt dd.MM · HH:mm    (GrowDetail)
    /// </summary>
    [Parameter] public bool ShowGrowName { get; set; } = false;
    [Parameter] public bool ShowPriority { get; set; } = false;

    private string SubLabel(GrowTask task)
    {
        var time = task.DueAtUtc?.ToLocalTime();
        if (ShowGrowName)
            return $"{task.GrowName ?? "–"} · {(time.HasValue ? time.Value.ToString("HH:mm") : "ohne Termin")}";
        return time.HasValue ? time.Value.ToString("dd.MM · HH:mm") : "ohne Termin";
    }

    private static string PrioClass(TaskPriority p) => p switch
    {
        TaskPriority.Critical or TaskPriority.High => "prio-high",
        TaskPriority.Normal                         => "prio-med",
        _                                           => "prio-low",
    };

    private static string ToggleLabel(GrowTask task)
        => task.Status == GrowTaskStatus.Done
            ? $"Aufgabe {task.Title} wieder oeffnen"
            : $"Aufgabe {task.Title} als erledigt markieren";
}
```

- [ ] **Schritt 2: Home.razor — Task-Loop ersetzen**

In `Components/Pages/Home.razor` den foreach-Block für Tasks (ca. Zeile 79–105) durch die Component ersetzen. Der `panel-card`-Header (`panel-card-header`, Titel, Count) bleibt unverändert.

Alten Block:
```razor
@foreach (var task in _openTasks.Take(6))
{
    <div class="task-item">
        <button type="button"
                class="task-check @(task.Status == GrowTaskStatus.Done ? "done" : "")"
                aria-label="@TaskToggleLabel(task)"
                aria-pressed="@(task.Status == GrowTaskStatus.Done)"
                @onclick="() => ToggleTask(task)">
            @if (task.Status == GrowTaskStatus.Done)
            {
                <svg aria-hidden="true" width="8" height="8" viewBox="0 0 8 8">
                    <path d="M1 4l2 2 4-4" stroke="oklch(10% 0.02 155)" stroke-width="1.5" fill="none" stroke-linecap="round" />
                </svg>
            }
        </button>
        <div style="flex:1; @(task.Status == GrowTaskStatus.Done ? "opacity:0.4" : "")">
            <div class="task-title" style="@(task.Status == GrowTaskStatus.Done ? "text-decoration:line-through" : "")">
                @task.Title
            </div>
            <div class="task-sub">
                @(task.GrowName ?? "–") · @(task.DueAtUtc?.ToLocalTime().ToString("HH:mm") ?? "ohne Termin")
            </div>
        </div>
        <div class="prio-dot @PrioClass(task.Priority)"></div>
    </div>
}
```

Durch folgendes ersetzen:
```razor
<TaskList Tasks="_openTasks" OnToggle="ToggleTask" ShowGrowName="true" ShowPriority="true" />
```

- [ ] **Schritt 3: Home.razor — PrioClass und TaskToggleLabel aus @code entfernen**

Im `@code`-Block von `Home.razor` die beiden jetzt unbenutzten static-Methoden entfernen:

```csharp
// ENTFERNEN:
private static string PrioClass(TaskPriority p) => p switch { ... };
private static string TaskToggleLabel(GrowTask task) => ...;
```

- [ ] **Schritt 4: GrowDetail.razor — Task-Loop ersetzen**

In `Components/Pages/GrowDetail.razor` den foreach-Block für Tasks (ca. Zeile 138–158) ersetzen.

Alten Block:
```razor
@foreach (var task in _tasks.Take(6))
{
    <div class="task-item">
        <button type="button"
                class="task-check @(task.Status == GrowTaskStatus.Done ? "done" : "")"
                aria-label="@TaskToggleLabel(task)"
                aria-pressed="@(task.Status == GrowTaskStatus.Done)"
                @onclick="() => ToggleTask(task)">
            @if (task.Status == GrowTaskStatus.Done)
            {
                <svg aria-hidden="true" width="8" height="8" viewBox="0 0 8 8">
                    <path d="M1 4l2 2 4-4" stroke="oklch(10% 0.02 155)" stroke-width="1.5" fill="none" stroke-linecap="round" />
                </svg>
            }
        </button>
        <div style="flex:1; @(task.Status == GrowTaskStatus.Done ? "opacity:0.4" : "")">
            <div class="task-title" style="@(task.Status == GrowTaskStatus.Done ? "text-decoration:line-through" : "")">@task.Title</div>
            <div class="task-sub">@(task.DueAtUtc?.ToLocalTime().ToString("dd.MM · HH:mm") ?? "ohne Termin")</div>
        </div>
    </div>
}
```

Durch folgendes ersetzen:
```razor
<TaskList Tasks="_tasks" OnToggle="ToggleTask" />
```

- [ ] **Schritt 5: GrowDetail.razor — TaskToggleLabel aus @code entfernen**

Im `@code`-Block von `GrowDetail.razor` diese Methode entfernen:

```csharp
// ENTFERNEN:
private static string TaskToggleLabel(GrowTask task) => ...;
```

- [ ] **Schritt 6: Build prüfen**

```bash
cd "D:/Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
```

Erwartet: `Build succeeded. 0 Error(s)`

- [ ] **Schritt 7: Commit**

```bash
cd "D:/Grow Operation System new"
git add GrowDiary.Web/Components/Shared/TaskList.razor \
        GrowDiary.Web/Components/Pages/Home.razor \
        GrowDiary.Web/Components/Pages/GrowDetail.razor
git commit -m "feat: extract TaskList shared component, remove duplicated task markup"
```

---

## Task 3: FocusCard.razor erstellen + GrowDetail.razor refaktorieren

**Files:**
- Create: `GrowDiary.Web/Components/Shared/FocusCard.razor`
- Modify: `GrowDiary.Web/Components/Pages/GrowDetail.razor`

- [ ] **Schritt 1: FocusCard.razor anlegen**

Neue Datei `GrowDiary.Web/Components/Shared/FocusCard.razor`:

```razor
<div class="focus-card @(Severity == DeviationSeverity.Critical ? "crit-border" : "warn-border")">
    <div class="focus-label">@Label</div>
    <div class="focus-body">@Body</div>
</div>

@code {
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string Body { get; set; } = "";
    [Parameter] public DeviationSeverity Severity { get; set; }
}
```

- [ ] **Schritt 2: GrowDetail.razor — Abweichungs-Loop ersetzen**

In `Components/Pages/GrowDetail.razor` innerhalb des Hinweise-`panel-card`-Blocks (ca. Zeile 162–176) den foreach ersetzen.

Alten Block:
```razor
@foreach (var d in _deviations.Take(5))
{
    <div class="focus-card @(d.Severity == DeviationSeverity.Critical ? "crit-border" : "warn-border")">
        <div class="focus-label">@d.Metric</div>
        <div class="focus-body">@d.Recommendation</div>
    </div>
}
```

Durch folgendes ersetzen:
```razor
@foreach (var d in _deviations.Take(5))
{
    <FocusCard Label="@d.Metric.ToString()" Body="@d.Recommendation" Severity="@d.Severity" />
}
```

- [ ] **Schritt 3: Build prüfen**

```bash
cd "D:/Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
```

Erwartet: `Build succeeded. 0 Error(s)`

- [ ] **Schritt 4: Commit**

```bash
cd "D:/Grow Operation System new"
git add GrowDiary.Web/Components/Shared/FocusCard.razor \
        GrowDiary.Web/Components/Pages/GrowDetail.razor
git commit -m "feat: extract FocusCard shared component for deviation hints"
```

---

## Task 4: AddbackPanel.razor erstellen + Home.razor refaktorieren

**Files:**
- Create: `GrowDiary.Web/Components/Shared/AddbackPanel.razor`
- Modify: `GrowDiary.Web/Components/Pages/Home.razor`

- [ ] **Schritt 1: AddbackPanel.razor anlegen**

Das `AddbackEntry`-Record wird direkt in der Component definiert — `Home.razor` referenziert es als `AddbackPanel.AddbackEntry`.

Neue Datei `GrowDiary.Web/Components/Shared/AddbackPanel.razor`:

```razor
@if (Items.Any())
{
    <div class="panel-card">
        <div class="panel-card-header">
            <span class="panel-card-title">Reservoir / Addback</span>
        </div>
        @foreach (var item in Items)
        {
            <div class="addback-item">
                <div class="addback-name">@item.GrowName</div>
                <div class="addback-detail">@item.TentName · @item.HydroStyle</div>
                <div class="addback-ec">
                    <span class="addback-ec-val @item.EcTrend">@item.Ec?.ToString("0.00")</span>
                    <span class="addback-ec-unit">mS/cm @(item.EcTrend == "up" ? "↑" : item.EcTrend == "down" ? "↓" : "–")</span>
                </div>
                @if (!string.IsNullOrEmpty(item.Recommendation))
                {
                    <div class="addback-rec">@item.Recommendation</div>
                }
            </div>
        }
    </div>
}

@code {
    [Parameter] public IEnumerable<AddbackEntry> Items { get; set; } = Array.Empty<AddbackEntry>();

    public record AddbackEntry(
        string GrowName,
        string TentName,
        string HydroStyle,
        double? Ec,
        string EcTrend,
        string? Recommendation);
}
```

- [ ] **Schritt 2: Home.razor — Addback-Block ersetzen**

In `Components/Pages/Home.razor` den gesamten Addback-`panel-card`-Block (ca. Zeile 107–130) durch die Component ersetzen:

Alten Block:
```razor
@if (_addbackItems.Any())
{
    <div class="panel-card">
        <div class="panel-card-header">
            <span class="panel-card-title">Reservoir / Addback</span>
        </div>
        @foreach (var item in _addbackItems)
        {
            <div class="addback-item">
                <div class="addback-name">@item.GrowName</div>
                <div class="addback-detail">@item.TentName · @item.HydroStyle</div>
                <div class="addback-ec">
                    <span class="addback-ec-val @item.EcTrend">@item.Ec?.ToString("0.00")</span>
                    <span class="addback-ec-unit">mS/cm @(item.EcTrend == "up" ? "↑" : item.EcTrend == "down" ? "↓" : "–")</span>
                </div>
                @if (!string.IsNullOrEmpty(item.Recommendation))
                {
                    <div class="addback-rec">@item.Recommendation</div>
                }
            </div>
        }
    </div>
}
```

Durch folgendes ersetzen:
```razor
<AddbackPanel Items="@_addbackItems.Select(i => new AddbackPanel.AddbackEntry(i.GrowName, i.TentName, i.HydroStyle, i.Ec, i.EcTrend, i.Recommendation))" />
```

- [ ] **Schritt 3: Build prüfen**

```bash
cd "D:/Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
```

Erwartet: `Build succeeded. 0 Error(s)`

- [ ] **Schritt 4: Commit**

```bash
cd "D:/Grow Operation System new"
git add GrowDiary.Web/Components/Shared/AddbackPanel.razor \
        GrowDiary.Web/Components/Pages/Home.razor
git commit -m "feat: extract AddbackPanel shared component"
```

---

## Task 5: ChartCard.razor erstellen

**Files:**
- Create: `GrowDiary.Web/Components/Shared/ChartCard.razor`

- [ ] **Schritt 1: ChartCard.razor anlegen**

Nur die strukturelle Hülle — kein JS-Interop in diesem Schritt. Der `<canvas>`-Tag ist bereit für zukünftiges Chart-Rendering via `data-chart`.

Neue Datei `GrowDiary.Web/Components/Shared/ChartCard.razor`:

```razor
<div class="panel-card">
    <div class="panel-card-header">
        <span class="panel-card-title">@Title</span>
    </div>
    @if (!string.IsNullOrEmpty(ChartJson))
    {
        <div class="card-pad">
            <canvas data-chart="@ChartJson" style="width:100%;height:160px"></canvas>
        </div>
    }
    else
    {
        <div style="padding:14px; font-size:12px; color:var(--faint)">Keine Daten.</div>
    }
</div>

@code {
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string? ChartJson { get; set; }
}
```

- [ ] **Schritt 2: Build prüfen**

```bash
cd "D:/Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
```

Erwartet: `Build succeeded. 0 Error(s)`

- [ ] **Schritt 3: Commit**

```bash
cd "D:/Grow Operation System new"
git add GrowDiary.Web/Components/Shared/ChartCard.razor
git commit -m "feat: add ChartCard shared component shell"
```

---

## Task 6: Obsolete MVC-Views löschen

**Files:**
- Delete: 9 View-Dateien (alle Controller-Routen dazu sind jetzt Redirects oder dead)

- [ ] **Schritt 1: Views mit git rm löschen**

```bash
cd "D:/Grow Operation System new"
git rm "GrowDiary.Web/Views/Home/Index.cshtml" \
       "GrowDiary.Web/Views/Grows/Index.cshtml" \
       "GrowDiary.Web/Views/Grows/Details.cshtml" \
       "GrowDiary.Web/Views/Grows/Create.cshtml" \
       "GrowDiary.Web/Views/Grows/Edit.cshtml" \
       "GrowDiary.Web/Views/Tents/Index.cshtml" \
       "GrowDiary.Web/Views/Tents/Details.cshtml" \
       "GrowDiary.Web/Views/Knowledge/Index.cshtml" \
       "GrowDiary.Web/Views/Settings/Index.cshtml"
```

Diese Views bleiben **unberührt** (aktive MVC-Routes ohne Blazor-Ersatz):
- `Views/Grows/Addback.cshtml`
- `Views/Grows/Compare.cshtml`
- `Views/Grows/Harvest.cshtml`
- `Views/Grows/EditMeasurement.cshtml`
- `Views/Grows/_GrowForm.cshtml`
- `Views/Grows/_MeasurementFormFields.cshtml`
- `Views/Shared/_Layout.cshtml`, `Error.cshtml`, `_ChartCard.cshtml`, `_OpsChartCard.cshtml`, `_GrowCard.cshtml`
- `Views/_ViewImports.cshtml`, `Views/_ViewStart.cshtml`

- [ ] **Schritt 2: Build prüfen**

```bash
cd "D:/Grow Operation System new"
dotnet build GrowDiary.Web/GrowDiary.Web.csproj -v:minimal
```

Erwartet: `Build succeeded. 0 Error(s)`

Falls der Build fehlschlägt: Eine View wurde gelöscht deren Controller-Route noch aktiv ist. Dann die gelöschte View mit `git restore` wiederherstellen und Task 1 nochmals prüfen.

- [ ] **Schritt 3: Commit**

```bash
cd "D:/Grow Operation System new"
git add -A
git commit -m "chore: delete obsolete MVC views replaced by Blazor"
```

---

## Task 7: App starten

- [ ] **Schritt 1: dotnet run**

```bash
cd "D:/Grow Operation System new/GrowDiary.Web"
dotnet run
```

Erwartet:
- Kein Build-Fehler
- Ausgabe enthält `Now listening on: http://localhost:5076`
- Browser öffnen auf `http://localhost:5076`
- Sidebar-Navigation funktioniert (Operations, Zelte, Grows, Einstellungen, Wissen)
- `/tents` → Redirect auf `/zelte` ✓
- `/settings` → Redirect auf `/einstellungen` ✓
- `/knowledge` → Redirect auf `/wissen` ✓
