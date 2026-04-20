# UI Bug-Fix Paket — Design Spec

**Datum:** 2026-04-20  
**Status:** Approved

---

## Scope

5 konkrete Bugs die nach Live-Test + Chrome-Inspektion identifiziert wurden. Keine neuen Features.

---

## Bug 1 — Dark Mode Toggle kaputt

**Problem:** `Sidebar.razor` speichert den Theme-Wert unter dem localStorage-Key `"theme"`. `site.js` liest beim Seitenload den Key `"growdiary-theme"`. Bei jedem Reload liest `site.js` `null` und fällt auf `dark` zurück — die Einstellung geht verloren.

**Fix:** In `Sidebar.razor` beide Vorkommen von `"theme"` durch `"growdiary-theme"` ersetzen (Zeilen 67 und 75). Kein Umbau der Architektur.

---

## Bug 2 — Kamerabild abgeschnitten / falsche Proportionen

**Problem:** `site.css:222` enthält:
```css
.tent-card .cam-strip > div { height: 180px !important; aspect-ratio: unset !important; }
```
Das überschreibt das korrekte `aspect-ratio: 16/9` aus dem Component mit `!important` und zwingt das Bild in eine fixe Höhe ohne Proportionserhalt. Die Kamera liefert 720p (16:9).

**Fix:** Diese CSS-Zeile entfernen. `width: 100%` sicherstellen damit das Bild den vollen Container füllt.

---

## Bug 3 — Metric-Boxen zu eng

**Problem:** `.tc-metrics-row` verwendet `minmax(130px, 1fr)`. Bei einem 26px großen Messwert (z.B. `22.4`) plus Label (`TEMPERATUR`) plus Einheit plus Target-Text ist 130px zu eng.

**Fix:** `minmax(130px, 1fr)` → `minmax(160px, 1fr)`.

---

## Bug 4 — Einstellungen: kein Seitenmenü

**Problem:** Alle Sektionen (Home Assistant, Zelte, Hydro-Systeme, Datenbank, App) sind in einem einzigen langen `settings-grid` ohne Navigation. Hydro-Systeme und App sind nur durch langen Scroll erreichbar.

**Fix:** Seitenmenü links innerhalb der Einstellungen-Seite — sticky, mit Anker-Links zu den Sektionen. Layout-Änderung: neues `settings-layout` mit `grid-template-columns: 160px 1fr`. Die bisherige `settings-grid` (2-spaltig) bleibt erhalten, wird aber als rechte Spalte eingebettet.

**Sektionen / Anker:**
- `#ha` — Home Assistant  
- `#zelte` — Zelte  
- `#systeme` — Hydro-Systeme  
- `#datenbank` — Datenbank  
- `#app` — App

---

## Bug 5 — Wissen-Seite: Dosierungs-Karten zu klein

**Problem:** `.wissen-stages` verwendet `minmax(120px, 1fr)` was Stage-Karten (Dosierung, Target, Notes) zu eng macht. Padding der Karten zu klein.

**Fix:**
- `minmax(120px, 1fr)` → `minmax(180px, 1fr)`  
- Stage-Card-Padding: `14px 16px` → `16px 20px`  
- Stage-Dose-Schrift: `14px` → `15px` für bessere Lesbarkeit
