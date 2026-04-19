# Design: Quick Fixes A — Font, Camera, Clone/Keimung

**Datum:** 2026-04-19  
**Status:** Genehmigt

---

## Ziel

Drei kleine aber spürbare UI/UX-Probleme beheben:
1. Zahlen-Font: uneinheitlich, kein numerisches Spacing
2. Kamerabild auf Operations-Seite kollabiert wenn kein Bild vorhanden
3. Keimungs-Logik zeigt sinnlose Optionen/Buttons für Clone/Steckling-Grows

---

## A1 — Zahlen-Font

### Problem
Numerische Werte (`23,2`, `57`, `0,88`, Stat-Chips `3 / 1 / 0`) nutzen keine tabular numerics — Ziffern springen visuell, Weight ist inkonsistent.

### Lösung
Neue Utility-Klasse `.num` in `GrowDiary.Web/wwwroot/css/site.css`:

```css
.num {
    font-variant-numeric: tabular-nums;
    font-feature-settings: "tnum";
    font-weight: 300;
    letter-spacing: -0.02em;
}
```

### Anwendungsstellen
Klasse `.num` zu folgenden Elementen in den Razor-Dateien hinzufügen:

| Element | Datei |
|---------|-------|
| `.grow-kpi-val` — Werte im Grow-Hero | `GrowDetail.razor` |
| `stat-chip strong` — Stat-Chips (3 Aktive Runs etc.) | `Home.razor`, `Grows.razor` |
| Metric-Werte in TentCard (Temp/Hum/VPD/EC/pH) | `TentCard.razor` |
| `.addback-ec-val` — EC-Wert in AddbackPanel | `AddbackPanel.razor` |
| `.row-val` — EC/pH in Grows-Tabelle | `Grows.razor` |

---

## A2 — Kamerabild

### Problem
`CameraPanel.razor` rendert ein `<img>`-Tag. Wenn kein HA-Kamera konfiguriert ist oder der Snapshot-Fetch fehlschlägt, zeigt der Browser nur den Alt-Text — der Container kollabiert auf ~20px Höhe, fast unsichtbar.

### Lösung
Wrapper-Div mit `min-height` und Fallback-Zustand:

```razor
<div class="camera-wrap">
    @if (_hasCamera)
    {
        <img src="@_src" alt="Livebild von @Tent.Name" class="camera-img" @onerror="OnError" />
        <span class="camera-ts">vor @_ageLabel</span>
    }
    else
    {
        <div class="camera-placeholder">
            <span>Kein Livebild</span>
        </div>
    }
</div>
```

CSS in `site.css`:
```css
.camera-wrap {
    position: relative;
    min-height: 220px;
    background: var(--surface-2, oklch(14% 0.01 155));
    border-radius: 6px;
    overflow: hidden;
}
.camera-img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    display: block;
}
.camera-placeholder {
    display: flex;
    align-items: center;
    justify-content: center;
    height: 220px;
    color: var(--faint);
    font-size: 12px;
    letter-spacing: 0.05em;
    text-transform: uppercase;
}
.camera-ts {
    position: absolute;
    bottom: 6px;
    right: 8px;
    font-size: 11px;
    color: var(--faint);
}
```

`_hasCamera` — true wenn `Tent.CameraEntityId` nicht leer ist (prüft nicht ob Bild tatsächlich lädt; `@onerror` setzt `_hasCamera = false` und triggert re-render).

---

## A3 — Clone/Keimung Logik

### Problem
`GrowForm.razor` — Einstiegspunkt-Dropdown enthält "Keimung" auch wenn Startmaterial = Steckling. Der Keimungs-Confirm-Button existiert nicht in der Blazor-Version (`GrowDetail.razor` enthält kein Germination-Markup — war nur im alten MVC-View).

### Lösung

**GrowForm.razor — Einstiegspunkt filtern:**

Der Einstiegspunkt-Select bekommt eine Filterlogik. Wenn `_model.StartMaterial == StartMaterial.Clone`, wird `GrowEntryPoint.Germination` aus der Liste ausgeblendet. Zusätzlich: wenn der User Startmaterial wechselt und aktuell "Keimung" ausgewählt ist → Reset auf `GrowEntryPoint.Seedling`.

```razor
<InputSelect id="grow-entry" @bind-Value="_model.EntryPoint">
    @foreach (var item in EntryPointOptions)
    {
        <option value="@item">@Label(item)</option>
    }
</InputSelect>
```

```csharp
private IEnumerable<GrowEntryPoint> EntryPointOptions =>
    Enum.GetValues<GrowEntryPoint>()
        .Where(e => _model.StartMaterial != StartMaterial.Clone
                    || e != GrowEntryPoint.Germination);
```

Beim Wechsel des Startmaterials:
```csharp
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

Der `InputSelect` wird auf manuelle Bindung umgestellt (`@onchange="OnStartMaterialChanged"` statt `@bind-Value`).

---

## Reihenfolge der Umsetzung

1. CSS: `.num`-Klasse hinzufügen + `site.css` Camera-Styles
2. Razor: `.num` auf alle Ziffern-Elemente anwenden
3. `CameraPanel.razor`: Placeholder + min-height implementieren
4. `GrowForm.razor`: EntryPointOptions-Filter + OnStartMaterialChanged
5. `GrowDetail.razor`: Keimungs-Button konditionalisieren
6. Build + `dotnet run` + Chrome-Check
