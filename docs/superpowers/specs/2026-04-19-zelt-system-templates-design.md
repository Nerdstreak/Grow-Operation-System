# Design: Zelt & System Templates

**Datum:** 2026-04-19  
**Status:** Genehmigt

---

## Ziel

Der User kann aktuell nur zwei hardcoded Zelte (Hauptzelt, Anzuchtzelt) verwenden und hat keine Möglichkeit, sein physisches Setup (Abmessungen, Licht, Lüftung, CO₂, Hydro-System) zu konfigurieren. Ziele:

1. Beliebig viele Zelte anlegen, umbenennen, löschen
2. Physisches Setup pro Zelt: Abmessungen, Beleuchtung, Lüftung, CO₂
3. Wiederverwendbare Hydro-System-Vorlagen (RDWC 3-Pot, DWC 2-Pot, etc.) als eigenes Konzept
4. Beim Grow-Erstellen: Zelt + System separat wählen; System-Wahl befüllt automatisch HydroStyle und Mengenfelder

---

## Variante B — Zelt & System getrennt

Gewählt: Zwei unabhängige Konzepte. Zelt = physischer Raum + HA-Konfiguration. System = Hydro-Setup-Vorlage. Beim Grow-Erstellen beide separat auswählen, Kombination frei.

---

## Änderungen

### 1. Datenbankschema

**Tabelle `Tents` — neue Spalten (via `EnsureColumn`):**

| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| `WidthCm` | INTEGER NULL | Breite des Zelts in cm |
| `DepthCm` | INTEGER NULL | Tiefe des Zelts in cm |
| `HeightCm` | INTEGER NULL | Höhe des Zelts in cm |
| `LightType` | TEXT NULL | LED / HPS / CMH / T5 |
| `LightWatt` | INTEGER NULL | Lichtstärke in Watt |
| `ExhaustFanCount` | INTEGER NULL | Anzahl Abluft-Lüfter |
| `ExhaustM3h` | INTEGER NULL | Abluftleistung m³/h |
| `CirculationFanCount` | INTEGER NULL | Anzahl Umluft-Lüfter |
| `Co2Type` | TEXT NULL | Keine / Flasche / Generator |
| `Co2TargetPpm` | INTEGER NULL | CO₂-Zielwert in ppm |

**Neue Tabelle `GrowSystems`:**

```sql
CREATE TABLE IF NOT EXISTS GrowSystems (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Name        TEXT    NOT NULL,
    HydroStyle  TEXT    NOT NULL,
    PotCount    INTEGER NULL,
    PotSizeLiters   REAL NULL,
    ReservoirLiters REAL NULL,
    Notes       TEXT    NULL,
    DisplayOrder INTEGER NOT NULL DEFAULT 99,
    CreatedAtUtc TEXT NOT NULL
);
```

---

### 2. Model — `GrowSystem.cs` (neu)

```csharp
namespace GrowDiary.Web.Models;

public sealed class GrowSystem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HydroStyle { get; set; } = string.Empty;
    public int? PotCount { get; set; }
    public double? PotSizeLiters { get; set; }
    public double? ReservoirLiters { get; set; }
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

---

### 3. Model — `Tent.cs` — neue Felder

Folgende Properties werden ergänzt:

```csharp
public int? WidthCm { get; set; }
public int? DepthCm { get; set; }
public int? HeightCm { get; set; }
public string? LightType { get; set; }
public int? LightWatt { get; set; }
public int? ExhaustFanCount { get; set; }
public int? ExhaustM3h { get; set; }
public int? CirculationFanCount { get; set; }
public string? Co2Type { get; set; }
public int? Co2TargetPpm { get; set; }
```

---

### 4. Repository — `GrowRepository.cs`

**Neue Methoden:**

- `DeleteTent(int id)` — löscht Zelt (nur wenn `ActiveGrowCount == 0`)
- `GetSystems()` → `List<GrowSystem>`
- `GetSystem(int id)` → `GrowSystem?`
- `CreateSystem(GrowSystem system)` → `GrowSystem` (mit Id)
- `UpdateSystem(GrowSystem system)`
- `DeleteSystem(int id)`

**Geänderte Methoden:**

- `UpdateTent(Tent tent)` — neuen Felder in UPDATE-Statement + `AddTentParameters` ergänzen
- `MapTent(reader)` — neue Felder lesen

**`CreateTent`** existiert bereits, keine Änderung nötig.

---

### 5. DatabaseInitializer — Schema-Migration

`EnsureColumn`-Aufrufe für die 10 neuen Tent-Spalten. `CREATE TABLE IF NOT EXISTS GrowSystems`.

---

### 6. `Einstellungen.razor` — UI-Änderungen

**Zelte-Sektion:**
- Header-Zeile: "Neues Zelt"-Button (öffnet Inline-Formular für Name → `CreateTent`)
- Jedes Zelt-Panel: "Löschen"-Button (deaktiviert wenn `ActiveGrowCount > 0`, sonst `DeleteTent`)
- Neues Formular-Grid pro Zelt — vier Unterabschnitte:
  - **Abmessungen:** WidthCm, DepthCm, HeightCm
  - **Beleuchtung:** LightType (Select: LED/HPS/CMH/T5), LightWatt
  - **Lüftung:** ExhaustFanCount, ExhaustM3h, CirculationFanCount
  - **CO₂:** Co2Type (Select: Keine/Flasche/Generator), Co2TargetPpm
  - **HA-Entities:** bestehende 16 Felder unverändert

**Neue Systeme-Sektion** (nach Zelte, vor Datenbank):
- "Neues System"-Button → Inline-Formular erscheint oben in der Liste
- Pro System: Name, HydroStyle (Select), PotCount, PotSizeLiters, ReservoirLiters, Notes
- "Speichern" + "Löschen" pro System
- `_systems = List<GrowSystem>` im Code-Block

---

### 7. `GrowForm.razor` — System-Dropdown

- Neues `_systems = List<GrowSystem>` laden in `OnInitializedAsync`
- Dropdown "System (optional)" neben Zelt-Dropdown
- `_model.SystemId` (nullable int) auf `GrowRun`
- Bei System-Auswahl: `_model.HydroStyle`, `_model.PotCount`, `_model.PotSizeLiters`, `_model.ReservoirLiters` automatisch befüllen (überschreibbar)

---

### 8. `GrowRun.cs` + `Grows`-Tabelle

- Neues Property `public int? SystemId { get; set; }` auf `GrowRun`
- `EnsureColumn` für `SystemId INTEGER NULL` in `Grows`-Tabelle
- `GrowRepository`: `SystemId` in INSERT/UPDATE/SELECT für GrowRun ergänzen

---

## Betroffene Dateien

| Datei | Änderung |
|-------|----------|
| `GrowDiary.Web/Models/Tent.cs` | 10 neue physische Felder |
| `GrowDiary.Web/Models/GrowRun.cs` | `SystemId` Property |
| `GrowDiary.Web/Models/GrowSystem.cs` | Neu anlegen |
| `GrowDiary.Web/Infrastructure/GrowRepository.cs` | DeleteTent, CRUD GrowSystems, MapTent/UpdateTent erweitern, GrowRun SystemId |
| `GrowDiary.Web/Infrastructure/DatabaseInitializer.cs` | EnsureColumn ×10, CREATE TABLE GrowSystems, EnsureColumn SystemId in Grows |
| `GrowDiary.Web/Components/Pages/Einstellungen.razor` | Zelt Create/Delete, physische Felder, Systeme-Sektion |
| `GrowDiary.Web/Components/Pages/GrowForm.razor` | System-Dropdown, Auto-Prefill |

---

## Nicht im Scope

- System-Vorlage direkt auf Dashboard oder Timeline anzeigen
- Historische System-Änderungen tracken
- Import/Export von Vorlagen
- Zelt-Reihenfolge per Drag-and-Drop ändern
