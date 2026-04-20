# Quick Fixes A — Font, Camera, Clone/Keimung Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Drei UI-Probleme beheben: Zahlen-Font auf tabular-nums/leichter, Kamera-Placeholder wenn kein Bild, Keimungs-Option bei Steckling ausblenden.

**Architecture:** Reine UI-Fixes — CSS-Anpassungen an bestehenden Selektoren, Blazor-Komponenten-Logik ohne API-Änderungen.

**Tech Stack:** Blazor Server (.NET 8), CSS custom properties, no ORM, no test framework.

---

### Task 1: Zahlen-Font — tabular-nums in bestehende CSS-Selektoren einbauen

**Files:**
- Modify: `GrowDiary.Web/wwwroot/css/site.css:193`
- Modify: `GrowDiary.Web/wwwroot/css/site.css:214`
- Modify: `GrowDiary.Web/wwwroot/css/site.css:260`
- Modify: `GrowDiary.Web/wwwroot/css/site.css:297`
- Modify: `GrowDiary.Web/wwwroot/css/site.css:326`

- [ ] **Step 1: `.stat-chip strong` anpassen (Zeile 193)**

Aktuelle Zeile:
```css
.stat-chip strong { color: var(--text); font-size: 20px; font-family: var(--mono); display: block; line-height: 1.1; margin-bottom: 2px; }
```

Ersetzen durch:
```css
.stat-chip strong { color: var(--text); font-size: 20px; font-family: var(--mono); font-weight: 300; font-variant-numeric: tabular-nums; font-feature-settings: "tnum"; letter-spacing: -0.02em; display: block; line-height: 1.1; margin-bottom: 2px; }
```

- [ ] **Step 2: `.tc-metric-value` anpassen (Zeile 214)**

Aktuelle Zeile:
```css
.tc-metric-value  { font-family: var(--mono); font-size: 26px; font-weight: 500; letter-spacing: -0.04em; line-height: 1; color: var(--text); }
```

Ersetzen durch:
```css
.tc-metric-value  { font-family: var(--mono); font-size: 26px; font-weight: 300; font-variant-numeric: tabular-nums; font-feature-settings: "tnum"; letter-spacing: -0.04em; line-height: 1; color: var(--text); }
```

- [ ] **Step 3: `.grow-kpi-val` anpassen (Zeile 260)**

Aktuelle Zeile:
```css
.grow-kpi-val   { font-family: var(--mono); font-size: 24px; font-weight: 500; letter-spacing: -0.03em; line-height: 1; }
```

Ersetzen durch:
```css
.grow-kpi-val   { font-family: var(--mono); font-size: 24px; font-weight: 300; font-variant-numeric: tabular-nums; font-feature-settings: "tnum"; letter-spacing: -0.03em; line-height: 1; }
```

- [ ] **Step 4: `.addback-ec-val` anpassen (Zeile 297)**

Aktuelle Zeile:
```css
.addback-ec-val { font-family: var(--mono); font-size: 20px; font-weight: 500; }
```

Ersetzen durch:
```css
.addback-ec-val { font-family: var(--mono); font-size: 20px; font-weight: 300; font-variant-numeric: tabular-nums; font-feature-settings: "tnum"; letter-spacing: -0.02em; }
```

- [ ] **Step 5: `.row-val` anpassen (Zeile 326)**

Aktuelle Zeile:
```css
.row-val   { font-family: var(--mono); font-size: 13px; }
```

Ersetzen durch:
```css
.row-val   { font-family: var(--mono); font-size: 13px; font-variant-numeric: tabular-nums; font-feature-settings: "tnum"; }
```

- [ ] **Step 6: Build prüfen**

```bash
dotnet build "Grow Operation System new/GrowDiary.slnx" -m:1 -v:minimal
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 7: Commit**

```bash
git add "Grow Operation System new/GrowDiary.Web/wwwroot/css/site.css"
git commit -m "style: tabular-nums und font-weight 300 fuer alle Ziffernanzeigen"
```

---

### Task 2: CameraPanel — Fehler-Placeholder wenn Bild nicht lädt

**Files:**
- Modify: `GrowDiary.Web/Components/Shared/CameraPanel.razor`

Hintergrund: `CameraPanel.razor` rendert aktuell zwei `<img>`-Tags (A/B-Swap für smooth refresh). Wenn HA nicht konfiguriert ist oder der `/tents/{id}/camera-stream`-Endpoint 404 zurückgibt, zeigen beide Bilder Broken-Image-Icons. Die `.cam-placeholder`-CSS-Klasse existiert bereits in `site.css` (Zeile 233). Ein `_loadFailed`-Flag stoppt den Refresh-Timer und zeigt den Placeholder.

- [ ] **Step 1: `_loadFailed`-Feld und `OnImageError`-Methode hinzufügen**

Im `@code`-Block nach `private bool _disposed;` einfügen:

```csharp
private bool _loadFailed;
```

Neue Methode nach `Dispose()`:

```csharp
private void OnImageError()
{
    _loadFailed = true;
    _refreshTimer?.Dispose();
    _refreshTimer = null;
    _ageTimer?.Dispose();
    _ageTimer = null;
    _ = InvokeAsync(StateHasChanged);
}
```

- [ ] **Step 2: Markup anpassen — Placeholder + onerror-Handler**

Aktuelle Template-Sektion (Zeilen 3–30):

```razor
<div class="cam-strip">
    <div style="position:relative; aspect-ratio:16/9; overflow:hidden;">
        @if (!string.IsNullOrWhiteSpace(_srcA))
        {
            <img class="cam-img"
                 src="@_srcA"
                 alt="@Alt"
                 width="1280"
                 height="720"
                 fetchpriority="high"
                 style="opacity:@(_active == "a" ? 1 : 0)" />
        }
        @if (!string.IsNullOrWhiteSpace(_srcB))
        {
            <img class="cam-img"
                 src="@_srcB"
                 alt=""
                 width="1280"
                 height="720"
                 style="opacity:@(_active == "b" ? 1 : 0)" />
        }
        <div class="cam-live-badge">
            <div class="cam-live-dot"></div>
            Live
        </div>
        <div class="cam-age">vor @_ageSeconds s</div>
    </div>
</div>
```

Ersetzen durch:

```razor
<div class="cam-strip">
    @if (_loadFailed)
    {
        <div class="cam-placeholder">
            <span>Kein Livebild verfügbar</span>
        </div>
    }
    else
    {
        <div style="position:relative; aspect-ratio:16/9; overflow:hidden;">
            @if (!string.IsNullOrWhiteSpace(_srcA))
            {
                <img class="cam-img"
                     src="@_srcA"
                     alt="@Alt"
                     width="1280"
                     height="720"
                     fetchpriority="high"
                     style="opacity:@(_active == "a" ? 1 : 0)"
                     @onerror="OnImageError" />
            }
            @if (!string.IsNullOrWhiteSpace(_srcB))
            {
                <img class="cam-img"
                     src="@_srcB"
                     alt=""
                     width="1280"
                     height="720"
                     style="opacity:@(_active == "b" ? 1 : 0)"
                     @onerror="OnImageError" />
            }
            <div class="cam-live-badge">
                <div class="cam-live-dot"></div>
                Live
            </div>
            <div class="cam-age">vor @_ageSeconds s</div>
        </div>
    }
</div>
```

- [ ] **Step 3: Build prüfen**

```bash
dotnet build "Grow Operation System new/GrowDiary.slnx" -m:1 -v:minimal
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 4: Commit**

```bash
git add "Grow Operation System new/GrowDiary.Web/Components/Shared/CameraPanel.razor"
git commit -m "fix: CameraPanel zeigt Placeholder wenn Kamerabild nicht laedt"
```

---

### Task 3: GrowForm — Keimung bei Steckling ausblenden

**Files:**
- Modify: `GrowDiary.Web/Components/Pages/GrowForm.razor`

Hintergrund: Das Startmaterial-Dropdown (`InputSelect @bind-Value="_model.StartMaterial"`, Zeile 89) und das Einstiegspunkt-Dropdown (`InputSelect @bind-Value="_model.EntryPoint"`, Zeile 166) sind unabhängig. Wenn StartMaterial = Steckling (Clone), darf `GrowEntryPoint.Germination` nicht auswählbar sein. Lösung: StartMaterial-Select auf manuelles `@onchange` umstellen + `EntryPointOptions`-Property filtern.

- [ ] **Step 1: StartMaterial-InputSelect auf manuelles onchange umstellen**

Aktuelles Markup (Zeilen 88–95):

```razor
<div class="field">
    <label for="grow-start-material">Startmaterial</label>
    <InputSelect id="grow-start-material" @bind-Value="_model.StartMaterial">
        @foreach (var item in Enum.GetValues<StartMaterial>())
        {
            <option value="@item">@Label(item)</option>
        }
    </InputSelect>
</div>
```

Ersetzen durch:

```razor
<div class="field">
    <label for="grow-start-material">Startmaterial</label>
    <select id="grow-start-material" value="@_model.StartMaterial" @onchange="OnStartMaterialChanged">
        @foreach (var item in Enum.GetValues<StartMaterial>())
        {
            <option value="@item">@Label(item)</option>
        }
    </select>
</div>
```

- [ ] **Step 2: EntryPoint-InputSelect auf EntryPointOptions umstellen**

Aktuelles Markup (Zeilen 165–171):

```razor
<div class="field">
    <label for="grow-entry">Einstiegspunkt</label>
    <InputSelect id="grow-entry" @bind-Value="_model.EntryPoint">
        @foreach (var item in Enum.GetValues<GrowEntryPoint>())
        {
            <option value="@item">@Label(item)</option>
        }
    </InputSelect>
</div>
```

Ersetzen durch:

```razor
<div class="field">
    <label for="grow-entry">Einstiegspunkt</label>
    <InputSelect id="grow-entry" @bind-Value="_model.EntryPoint">
        @foreach (var item in EntryPointOptions)
        {
            <option value="@item">@Label(item)</option>
        }
    </InputSelect>
</div>
```

- [ ] **Step 3: `EntryPointOptions` Property und `OnStartMaterialChanged` Methode hinzufügen**

Im `@code`-Block, direkt vor der `SaveAsync`-Methode einfügen:

```csharp
private IEnumerable<GrowEntryPoint> EntryPointOptions =>
    Enum.GetValues<GrowEntryPoint>()
        .Where(e => _model.StartMaterial != StartMaterial.Clone
                    || e != GrowEntryPoint.Germination);

private void OnStartMaterialChanged(ChangeEventArgs e)
{
    if (Enum.TryParse<StartMaterial>(e.Value?.ToString(), out var val))
    {
        _model.StartMaterial = val;
        if (val == StartMaterial.Clone && _model.EntryPoint == GrowEntryPoint.Germination)
            _model.EntryPoint = GrowEntryPoint.Seedling;
    }
}
```

- [ ] **Step 4: Build prüfen**

```bash
dotnet build "Grow Operation System new/GrowDiary.slnx" -m:1 -v:minimal
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [ ] **Step 5: Commit**

```bash
git add "Grow Operation System new/GrowDiary.Web/Components/Pages/GrowForm.razor"
git commit -m "fix: Keimung als Einstiegspunkt bei Steckling-Startmaterial ausblenden"
```
