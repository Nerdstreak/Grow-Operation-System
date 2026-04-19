# Handoff: Grow OS – UI Redesign (Blazor Server)

## Übersicht

Dieses Paket enthält alle Designreferenzen für das Redesign der Grow Operation System Web-App. Das bestehende ASP.NET Core MVC-Projekt soll auf **Blazor Server** umgebaut werden. Die HTML-Prototypen in diesem Paket sind **High-Fidelity-Referenzen** — keine Produktionscode. Ziel ist es, das UI 1:1 in Blazor Server-Komponenten nachzubauen und dabei das bestehende C#-Backend (Repositories, Services, Models) unverändert zu übernehmen.

---

## Fidelity

**High-Fidelity.** Die Prototypen zeigen das finale Design inkl. Farben, Typografie, Abstände und Interaktionen. Pixel-genaue Umsetzung ist das Ziel. Alle Design-Tokens (CSS Custom Properties) sind im Abschnitt „Design Tokens" dokumentiert.

---

## Projektstruktur (Blazor Server)

```
GrowDiary.Web/
  Components/
    Layout/
      MainLayout.razor          ← Shell: Sidebar + Content
      Sidebar.razor             ← Navigation
      AlertBar.razor            ← Kritische Alarme
    Shared/
      TentCard.razor            ← Zeltkarte (Ops + Zelte-View)
      MetricBlock.razor         ← Einzelner Messwert (groß)
      MetricCell.razor          ← Einzelner Messwert (klein, in Karte)
      TaskList.razor            ← Aufgabenliste (Seitenpanel)
      AddbackPanel.razor        ← Reservoir/Addback-Panel
      FocusCard.razor           ← Empfehlungs-/Warnkarte
      TimelineList.razor        ← Journal-Timeline
      ChartCard.razor           ← Chart-Wrapper (Chart.js bleibt)
      CameraPanel.razor         ← HA Kamera-Stream (Double-Buffer)
      StatusPill.razor          ← ok / warn / crit Badge
    Pages/
      Home.razor                ← /  (Operations)
      Tents.razor               ← /zelte
      TentDetail.razor          ← /zelte/{id}
      Grows.razor               ← /grows
      GrowDetail.razor          ← /grows/{id}
      MeasurementForm.razor     ← /grows/{id}/messung
      Analyse.razor             ← /analyse
      Wissen.razor              ← /wissen
      Settings.razor            ← /einstellungen
  wwwroot/
    css/
      site.css                  ← Alle Tokens + Klassen (siehe site.css in diesem Paket)
```

---

## Screens / Views

### 1. Operations (`Home.razor`)

**Zweck:** Echtzeit-Übersicht aller aktiven Zelte und priorisierten Aufgaben.

**Layout:**
```
[Alert-Bar — full width, nur wenn kritische Abweichungen]
[Stats-Row: Stat-Chips nebeneinander]
[Ops-Layout: 2-spaltig]
  [Links: Tents-Grid (auto-fill, min 340px)]
  [Rechts: Side-Panel 260px — TaskList + AddbackPanel]
```

**Verhalten:**
- Alert-Bar: Zeigt erste kritische Abweichung + „+N weitere". Pulsierender roter Dot.
- Stats-Row: Aktive Runs / Aktive Zelte / Offene Aufgaben heute.
- Zelt-Karten: Klick → navigiert zu `/zelte/{id}`.
- Run-Pills im Footer der Karte: Klick → navigiert zu `/grows/{id}`.
- Live-Refresh: `InvokeAsync(StateHasChanged)` per Timer alle 10 s (wie bisher `data-live-interval-ms`).
- Tasks: Checkbox anklicken → `TaskRepository.CompleteAsync(id)` → StateHasChanged.

**Live-Daten Pattern (Blazor):**
```csharp
// Home.razor.cs
protected override async Task OnInitializedAsync() {
    await LoadData();
    _timer = new Timer(async _ => {
        await LoadData();
        await InvokeAsync(StateHasChanged);
    }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
}
```

---

### 2. Zelte (`Tents.razor`)

**Zweck:** Verwaltungsansicht aller Zelte (auch inaktive). Gleiche TentCard-Komponente wie Operations.

**Layout:** Stats-Row + Tents-Grid (identisch zu Ops, aber ohne Side-Panel).

---

### 3. Tent-Detail (`TentDetail.razor`)

**Zweck:** Live-Dashboard für ein einzelnes Zelt. Kamera + Metriken + Charts.

**Layout:**
```
[Topbar: Zurück-Button + Titel + Status-Pill + Actions]
[2-spaltig: content 1fr / sidebar 260px]
  [Links]
    [Kamera-Panel (wenn camera_entity_id gesetzt)]
    [Sektion "Klima": MetricBlock × 3]
    [Sektion "Reservoir": MetricBlock × 4 (pH, EC, ORP, W-Temp)]
    [ChartCard: Klima 48h]
    [ChartCard: Reservoir 48h]
  [Rechts: Side-Panel]
    [Aktive Runs (Liste mit Klick → GrowDetail)]
    [FocusCard: wichtigste Warnung]
```

**Kamera (Double-Buffer Pattern):**
```razor
<!-- CameraPanel.razor -->
<div class="cam-large-wrap">
  <img @ref="_bufA" id="cam-buf-a" src="@_srcA" style="opacity:@(_active=='a'?1:0)" />
  <img @ref="_bufB" id="cam-buf-b" src="@_srcB" style="opacity:@(_active=='b'?1:0)" />
  <div class="cam-live-badge"><div class="cam-live-dot"/>Live</div>
  <div class="cam-age">vor @_age s</div>
</div>
```
```csharp
// Refresh alle 30s
void Refresh() {
    if (_active == "a") { _srcB = $"/tents/{TentId}/camera-stream?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"; _active = "b"; }
    else { _srcA = $"/tents/{TentId}/camera-stream?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"; _active = "a"; }
}
```

---

### 4. Aktive Grows (`Grows.razor`)

**Zweck:** Tabellarische Übersicht aller aktiven Grows mit Schnellwerten.

**Layout:** Stats-Chips + `.data-table` mit Spalten: Grow | Phase | pH | EC | ORP | Status.

**Spalten-Grid:** `grid-template-columns: 2fr 1fr 1fr 1fr 1fr 60px`

**Wertfarben:**
- pH > 6.2 oder < 5.8 → `var(--amber)`
- pH im Bereich → `var(--green)`
- EC > 2.0 → `var(--amber)`, sonst `var(--text)`

---

### 5. Grow-Detail (`GrowDetail.razor`)

**Zweck:** Vollständige Detailansicht eines Grow-Runs.

**Layout:**
```
[Grow-Hero: Name, Strain, Phase, KPI-Leiste]
[2-spaltig: content 1fr / sidebar 240px]
  [Links]
    [FocusCard: Empfehlung mit amber-Linksrand]
    [Letzte Messung: 4×2-Grid der Werte]
    [Timeline: journal + measurement + task Einträge]
  [Rechts]
    [TaskList (nur Tasks für diesen Grow)]
    [Sollwerte-Panel für aktuelle Woche/Phase]
```

**Grow-Hero KPIs:** Flex-Row mit border-right-Trenner. Werte in `--mono` Font.

**Timeline-Dots:**
- `measurement` → `var(--green)`
- `journal` → `var(--blue)`
- `task` → `var(--amber)`

---

### 6. Messung eintragen (`MeasurementForm.razor`)

**Zweck:** Schnelle Dateneingabe, optimiert für Nutzung vor der Pflanze.

**Layout:**
```
[Topbar: Grow-Name als Back-Label + "Speichern"-Button]
[2-spaltig: form 1fr / sidebar 280px]
  [Links]
    [Card: Reservoir-Felder (pH, EC, ORP, W-Temp, DO)]
    [Card: Klima-Felder (Temp, Luftfeuchte, VPD, PPFD, CO2)]
    [Card: Journal-Eintrag (Typ-Select, Titel, Notiz-Textarea)]
  [Rechts]
    [Sollwerte für aktuelle Phase]
    [Letzte Messwerte zum Vergleich]
```

**Input-Validierung (Live):**
```csharp
string GetInputStatus(double? val, double min, double max) {
    if (!val.HasValue) return "";
    return val >= min && val <= max ? "ok" : "warn";
}
// CSS-Klasse "ok" → grüner Border, "warn" → amber Border
```

**Input-Styling:** Font-size 22px, font-family Mono, min-height 50px. Fühlt sich wie ein Zahlenpad an.

**Journal-Felder:** Immer sichtbar, optional. Beim Speichern: wenn Notiz-Feld nicht leer → gleichzeitig `JournalRepository.AddAsync()` aufrufen, verknüpft mit der `MeasurementId`.

---

### 7. Analyse (`Analyse.razor`)

**Layout:**
```
[Grow-Picker: 2 Selects mit "vs." dazwischen]
[2-spaltig: Hero-Cards für beide Grows mit Sparkline]
[ChartCard: pH-Verlauf überlagert]
[Compare-Table: Metrik | Grow1 | Grow2 | Sollbereich]
```

---

### 8. Wissen (`Wissen.razor`)

**Layout:**
```
[2-spaltig: Phase-Nav 200px / Content 1fr]
  [Phase-Nav: Keimung / Vegetation / Vorblüte / Blüte / Reife]
  [WissenCard: Sollwerte-Grid + Hinweise-Liste]
```

Daten können hardcoded in C# Records oder JSON-Datei in `/App_Data/` abgelegt werden. Kein DB-Eintrag nötig.

---

### 9. Einstellungen (`Settings.razor`)

**Layout:** Vertikal gestapelt (full-width), dann 2-spaltig für Snapshot + Alarm.

**Sektionen:**
1. HA-Verbindung (URL + Token + Test-Button + Status-Badge)
2. Sensor-Mapping-Tabelle (Metrik | Entity-Input | Letzter Wert | Status)
3. Snapshot-Einstellungen (Intervall, Pfad)
4. Alarm-Info (readonly, erklärt HA-Automationen)

**Speichern:** Schreibt in `ha-config.json` (wie bisher `HaConfigLoader`).

---

## Interaktionen & Verhalten

| Interaktion | Umsetzung |
|---|---|
| Navigation | `NavigationManager.NavigateTo()` oder `<NavLink>` |
| Live-Refresh | `System.Threading.Timer` in `OnInitializedAsync` |
| Kamera-Refresh | Timer alle 30 s, Double-Buffer per Image-Src-Swap |
| Task abhaken | `TaskRepository.CompleteAsync(id)` + StateHasChanged |
| Messung speichern | `GrowRepository.AddMeasurementAsync()` + optional `JournalRepository.AddAsync()` |
| Formular-Validierung | `EditForm` + `DataAnnotationsValidator` oder manuelle live-Validierung |
| Dark/Light Mode | `localStorage` + `document.documentElement.setAttribute('data-theme', ...)` via JSInterop |

---

## Design Tokens

```css
/* Dark Mode (default) */
--bg:       oklch(10% 0.018 155);
--surface:  oklch(14% 0.018 155);
--surface2: oklch(17.5% 0.02 155);
--surface3: oklch(22% 0.022 155);
--border:   oklch(30% 0.02 155 / 0.55);
--border2:  oklch(40% 0.02 155 / 0.7);
--text:     oklch(94% 0.01 150);
--muted:    oklch(58% 0.02 150);
--faint:    oklch(38% 0.015 150);
--green:    oklch(74% 0.19 145);    /* ok / stabil / in Sollbereich */
--amber:    oklch(78% 0.17 72);     /* warn / beobachten */
--red:      oklch(66% 0.22 25);     /* crit / kritisch */
--blue:     oklch(70% 0.16 240);    /* info / Journal */
--radius:   10px;
--sidebar:  228px;
--mono:     'IBM Plex Mono', monospace;

/* Semantic */
--green-bg: oklch(74% 0.19 145 / 0.1);
--amber-bg: oklch(78% 0.17 72 / 0.1);
--red-bg:   oklch(66% 0.22 25 / 0.1);
--blue-bg:  oklch(70% 0.16 240 / 0.1);
```

**Typografie:**
- UI-Text: `'Inter', system-ui, sans-serif`
- Messwerte/Zahlen: `'IBM Plex Mono', monospace` (Größe 18–32px je nach Kontext)
- Base: 14px / line-height 1.5

**Google Fonts Import:**
```html
<link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;500;600&family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet"/>
```

---

## Blazor Server – Setup

```bash
# Kein neues Projekt nötig – bestehende GrowDiary.Web.csproj erweitern
dotnet add package Microsoft.AspNetCore.Components.Web

# Program.cs ergänzen:
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
```

`App.razor`:
```razor
<!DOCTYPE html>
<html lang="de" data-theme="dark">
<head>
    <meta charset="utf-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
    <link rel="stylesheet" href="css/site.css"/>
    <link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;500;600&family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet"/>
    <HeadOutlet/>
</head>
<body>
    <Routes/>
    <script src="_framework/blazor.web.js"></script>
    <script src="js/site.js"></script>
</body>
</html>
```

**Bestehende Services übernehmen (keine Änderung nötig):**
```csharp
// Program.cs – bleibt wie gehabt
builder.Services.AddScoped<GrowRepository>();
builder.Services.AddScoped<TentRepository>();
builder.Services.AddScoped<JournalRepository>();
builder.Services.AddScoped<HomeAssistantService>();
// etc.
```

---

## Komponenten-Referenz (Blazor)

### TentCard.razor
```razor
@* Parameters *@
@code {
    [Parameter] public Tent Tent { get; set; }
    [Parameter] public List<MetricCard> ClimateMetrics { get; set; }
    [Parameter] public List<MetricCard> ReservoirMetrics { get; set; }
    [Parameter] public EventCallback OnOpen { get; set; }
    [Parameter] public EventCallback<int> OnOpenGrow { get; set; }
}
```

### MetricBlock.razor (große Anzeige im Detail)
```razor
@code {
    [Parameter] public string Label { get; set; }
    [Parameter] public string Value { get; set; }
    [Parameter] public string Unit { get; set; }
    [Parameter] public string Status { get; set; }  // "ok" | "warn" | "crit" | "neutral"
    [Parameter] public string Target { get; set; }  // "Soll: 5.8–6.2"
}
```

### MeasurementForm.razor
```razor
@code {
    [Parameter] public int GrowId { get; set; }

    private MeasurementFormModel _model = new();

    private string GetStatus(double? val, double min, double max)
        => val.HasValue ? (val >= min && val <= max ? "ok" : "warn") : "";

    private async Task Submit() {
        await GrowRepository.AddMeasurementAsync(GrowId, _model.ToMeasurement());
        if (!string.IsNullOrEmpty(_model.JournalNote)) {
            await JournalRepository.AddAsync(GrowId, _model.ToJournalEntry());
        }
        NavigationManager.NavigateTo($"/grows/{GrowId}");
    }
}
```

---

## Assets

- **Fonts:** Google Fonts CDN — IBM Plex Mono (400/500/600) + Inter (400/500/600/700)
- **Icons:** Inline SVG — keine Icon-Library. Alle Icons sind im Prototyp als `<svg>`-Elemente vorhanden.
- **Charts:** Chart.js (bereits im Projekt) — Farbpalette anpassen auf neue Tokens.

---

## Dateien in diesem Paket

| Datei | Beschreibung |
|---|---|
| `README.md` | Diese Datei |
| `Grow OS Redesign v2.html` | Vollständiger interaktiver Prototype (alle 9 Screens) |
| `site.css` | Finale CSS-Datei — direkt in `wwwroot/css/` ersetzen |
| `components/` | Blazor-Komponenten-Referenz (`.razor` Dateien als Referenz) |
