# Design: MVC→Blazor Cleanup + Shared Components

**Datum:** 2026-04-19  
**Status:** Genehmigt

---

## Ziel

Drei offene Punkte aus dem UI-Review vom 19.04.2026 schließen:

1. Alte MVC-Routes per Redirect auf Blazor-Äquivalente umleiten
2. Dupliziertes/fehlendes Blazor-Markup in Shared Components kapseln
3. Ersetzte MVC-Views löschen

---

## Abschnitt 1 — Routing-Fix

### Strategie

**Redirect-first:** GET-Actions in betroffenen Controllern werden zu `Redirect()`-Einzeilern. POST-Actions bleiben unverändert — ihre Formular-Redirects zeigen bereits auf Blazor-URLs (`/grows/{id}` etc.).

### Änderungen pro Controller

**TentsController** (`Controllers/TentsController.cs`)
- `Index` (GET `/tents`) → `return Redirect("/zelte");`
- `Details` (GET `/tents/{id}`) → `return Redirect($"/zelte/{id}");`
- Alle anderen Actions (Live, CameraSnapshot, CameraStream, LatestSnapshot) bleiben unverändert.

**KnowledgeController** (`Controllers/KnowledgeController.cs`)
- `Index` (GET `/knowledge`) → `return Redirect("/wissen");`

**SettingsController** (`Controllers/SettingsController.cs`)
- `Index` (GET `/settings`) → `return Redirect("/einstellungen");`
- POST-Actions (`SaveHomeAssistant`, `SaveTent`) bleiben. `RedirectToAction(nameof(Index))` dort führt über `/settings` → `/einstellungen` (einmalige Redirect-Kette, akzeptabel).

**GrowsController** (`Controllers/GrowsController.cs`)
- `Create` GET (`/grows/create`) → `return Redirect("/grows/new");`
- `Edit` GET (`/grows/{id}/edit`) → `return Redirect($"/grows/{id}/setup");`
- `Delete` POST: `RedirectToAction(nameof(Index))` → `return Redirect("/grows");`

---

## Abschnitt 2 — Shared Components

Alle vier Components kommen nach `GrowDiary.Web/Components/Shared/`.

### TaskList.razor

**Zweck:** Ersetzt das identisch duplizierte Task-Markup in `Home.razor` und `GrowDetail.razor`.

**Parameter:**
```csharp
[Parameter] public List<GrowTask> Tasks { get; set; } = new();
[Parameter] public EventCallback<GrowTask> OnToggle { get; set; }
[Parameter] public bool ShowGrowName { get; set; } = false;
```

`ShowGrowName = true` zeigt die `task.GrowName`-Zeile (nur in `Home.razor` benötigt).  
`ShowGrowName = false` zeigt `task.DueAtUtc` (nur in `GrowDetail.razor`).

### FocusCard.razor

**Zweck:** Ersetzt die inline `.focus-card`-Divs in `GrowDetail.razor` (Abweichungs-Hinweise).

**Parameter:**
```csharp
[Parameter] public string Label { get; set; } = "";
[Parameter] public string Body { get; set; } = "";
[Parameter] public DeviationSeverity Severity { get; set; }
```

CSS-Klasse: `Severity == Critical` → `"crit-border"`, sonst `"warn-border"`.

### AddbackPanel.razor

**Zweck:** Ersetzt den Reservoir/Addback-Block in `Home.razor`.

**Parameter:**
```csharp
[Parameter] public IEnumerable<AddbackItem> Items { get; set; } = Array.Empty<AddbackItem>();
```

`AddbackItem` bleibt als `private sealed class` in `Home.razor` — kein separates ViewModel nötig.  
Die Component bekommt `IEnumerable<object>` oder — besser — ein eigenes Interface/Record in der Component selbst, das `Home.razor` befüllt.

> **Konkret:** Ein `public record AddbackEntry` direkt in `AddbackPanel.razor` definieren mit den nötigen Feldern. `Home.razor` mappt seine lokale `AddbackItem`-Klasse darauf.

**Record:**
```csharp
public record AddbackEntry(
    string GrowName, string TentName, string HydroStyle,
    double? Ec, string EcTrend, string? Recommendation);
```

### ChartCard.razor

**Zweck:** Leere Blazor-Hülle für zukünftiges Chart-Rendering (JS-Interop kommt später).

**Parameter:**
```csharp
[Parameter] public string Title { get; set; } = "";
[Parameter] public string? ChartJson { get; set; }
```

Rendert ein `<div class="panel-card">` mit Titel und einem `<canvas data-chart="@ChartJson">`.  
Kein JS-Interop in diesem Schritt — nur die strukturelle Hülle.

---

## Abschnitt 3 — MVC Views löschen

### Löschen (nach Redirect-Fix)

| Datei | Grund |
|---|---|
| `Views/Home/Index.cshtml` | HomeController.Index bei `/mvc-home-legacy` |
| `Views/Grows/Index.cshtml` | GrowsController.Index bei `/grows/mvc-legacy-list` |
| `Views/Grows/Details.cshtml` | GrowsController.Details bei `/grows/mvc-legacy-detail/{id}` |
| `Views/Grows/Create.cshtml` | Redirect → `/grows/new` |
| `Views/Grows/Edit.cshtml` | Redirect → `/grows/{id}/setup` |
| `Views/Tents/Index.cshtml` | Redirect → `/zelte` |
| `Views/Tents/Details.cshtml` | Redirect → `/zelte/{id}` |
| `Views/Knowledge/Index.cshtml` | Redirect → `/wissen` |
| `Views/Settings/Index.cshtml` | Redirect → `/einstellungen` |

### Behalten (aktive MVC-Routes ohne Blazor-Ersatz)

- `Views/Grows/Addback.cshtml` — `GET /grows/{id}/addback`
- `Views/Grows/Compare.cshtml` — `GET /grows/compare`
- `Views/Grows/Harvest.cshtml` — `GET /grows/{id}/harvest`
- `Views/Grows/EditMeasurement.cshtml` — `GET /grows/measurements/{id}/edit`
- `Views/Grows/_GrowForm.cshtml`, `_MeasurementFormFields.cshtml` — Partials für obige
- `Views/Shared/_Layout.cshtml`, `Error.cshtml`, `_ChartCard.cshtml`, `_OpsChartCard.cshtml`, `_GrowCard.cshtml`
- `Views/_ViewImports.cshtml`, `Views/_ViewStart.cshtml`

---

## Reihenfolge der Umsetzung

1. Redirects in Controllern setzen
2. Build prüfen (`dotnet build`)
3. Shared Components anlegen
4. `Home.razor` und `GrowDetail.razor` auf neue Components umstellen
5. Build prüfen
6. Obsolete MVC-Views löschen
7. Finaler Build + `dotnet run`

---

## Was nicht in diesem Schritt gemacht wird

- Blazor-Ersatz für Addback, Compare, Harvest, EditMeasurement (aktive MVC-Routes — separater Schritt)
- JS-Interop in ChartCard (nur Hülle)
- Einstellungen.razor eigene POST-Logik (SettingsController.SaveHomeAssistant bleibt aktiv)
