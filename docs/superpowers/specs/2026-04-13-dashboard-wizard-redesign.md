# Design Spec – Dashboard & Wizard Redesign
**Datum:** 2026-04-13  
**Sprint:** 17  
**Scope:** Operations Dashboard (Zelt-Cards) + Grow-Wizard

---

## Ziel

Zwei Bereiche der App bekommen ein überarbeitetes Layout das ruhiger wirkt, bessere Kontraste hat und sowohl visuell als auch in der Nutzbarkeit verbessert ist:

1. **Operations Dashboard** – die Zelt-Cards zeigen jetzt alle relevanten Messwerte (Klima + Reservoir) klar gruppiert, mit der Kamera als optionalem Panel
2. **Grow-Wizard** – der vertikale Sidebar-Stepper wird durch einen horizontalen Stepper oben ersetzt

---

## 1. Dashboard – Zelt-Card

### Struktur

```
┌─────────────────────────────────────────────────────┐
│ HEADER: Eyebrow · Name · Run-Anzahl+Typ  [Pill] [Btn]│
├───────────────────────────────────────┬─────────────┤
│ BODY – Metriken                       │  KAMERA     │
│  Klima                                │  (optional) │
│  ┌──────────┬──────────┬──────────┐   │             │
│  │ Temp     │ Feuchte  │ VPD      │   │             │
│  └──────────┴──────────┴──────────┘   │             │
│  Reservoir                            │             │
│  ┌──────┬──────┬──────┬──────────┐    │             │
│  │ pH   │ EC   │ ORP  │ H₂O      │    │             │
│  └──────┴──────┴──────┴──────────┘    │             │
├───────────────────────────────────────┴─────────────┤
│ FOOTER: Status-Dot + Text                 Run-Pills │
└─────────────────────────────────────────────────────┘
```

### Details

**Header**
- Eyebrow: `tent.Kind` (Hauptzelt / Anzuchtzelt)
- Titelzeile: `tent.Name` (fett, ~1rem)
- Subzeile: `{N} aktive Runs · {HydroStyle}` (nur wenn ActiveHydro)
- Rechts: State-Pill (stable/warn/critical) + Ghost-Button „Öffnen"

**Body – Metriken (links)**
- Zwei benannte Sektionen: **Klima** und **Reservoir**
- Jede Sektion: Zweispalten-Tabelle (`mlist`-Pattern), jede Zelle mit Label (klein, muted) + Wert (groß, fett) + Einheit (klein, muted)
- **Klima:** Temperatur, Luftfeuchte, VPD (3 Werte → 2+1 oder 3-spaltig)
- **Reservoir:** pH, EC, ORP, Wassertemp (4 Werte → 2×2)
- Werte die außerhalb des Sollbereichs liegen: `color: var(--warning)` bzw. `var(--danger)` direkt an der Zahl
- Werte fehlen (kein HA / kein Hydro-Grow): `–` in muted, kein leeres Layout

**Body – Kamera (rechts, optional)**
- Nur wenn `tent.CameraEntityId` gesetzt: schmales Panel (~150px breit), `border-left`, dunkler Hintergrund
- Live-Dot oben links (roter Pulse)
- Kamerabild per `<img>` mit `ops-cam-img` / `ops-cam-img--hidden` Double-Buffer (bereits in Sprint 16 implementiert)
- Ohne Kamera: Metriken nehmen volle Breite, kein Panel

**Footer**
- Status-Dot + Text: grün „Alle Werte im Sollbereich" / gelb „{Metrik} {Abweichung}" / rot „Kritisch: {Metrik}"
- Run-Pills: max. 3, Font-Size klein, `border-radius: 7px`, muted

### CSS-Änderungen

- Neue Klassen: `.card-head`, `.card-name`, `.card-sub`, `.card-body`, `.card-body.no-cam`, `.metrics`, `.section-lbl`, `.mlist`, `.mi`, `.mi.warn`, `.mi.danger`, `.cam`, `.card-foot`
- Bestehende `.ops-tent-card`-Regeln in `site.css` werden ersetzt / überarbeitet
- Keine inline-styles

### JS-Änderungen

- Double-Buffer-Logik (bereits in `@section Scripts` von `Home/Index.cshtml`) bleibt, IDs `ops-cam-a-{id}` / `ops-cam-b-{id}` bleiben
- Live-Refresh (`data-live-tent-card`) bleibt unverändert

---

## 2. Grow-Wizard – Horizontaler Stepper

### Struktur

```
┌────────────────────────────────────────────────────┐
│ STEPPER: ①Genetik ── ②System ── ③Nährstoffe ── ④Einstieg │
├────────────────────────────────────────────────────┤
│ FORMULAR-PANEL (aktiver Schritt)                   │
│  h3: Schrittname · "Schritt N von 4"               │
│  Felder (form-grid two / one wie bisher)           │
│  Radio-Gruppen (btn-group-radio wie bisher)        │
├────────────────────────────────────────────────────┤
│ AKTIONEN: [Abbrechen]          [← Zurück] [Weiter →]│
└────────────────────────────────────────────────────┘
```

### Details

**Stepper**
- Horizontal, oben, volle Breite
- 4 Step-Items: `step-circle` (Nummer) + `step-text` (Name + Subtext)
- Zwischen Items: `step-connector` (Linie, `height: 1px`)
- Status: `.done` (grün, Checkmark), `.active` (primary-Farbe), default (muted)
- Kein Subtext auf mobil (`display: none` unter 600px), nur Nummer + Name

**Formular-Panel**
- Bestehende Felder und Validierung bleiben exakt gleich
- Kein `<aside class="wizard-sidebar">` mehr
- `<div class="wizard-content">` nimmt volle Breite
- Panels (`wizard-panel`) weiterhin via `is-active` ein-/ausgeblendet

**Aktionen**
- Footer-Zeile: Links `Abbrechen` (Ghost), rechts `← Zurück` (Ghost) + `Weiter →` / `Grow anlegen` (Primary)
- `data-step-prev` / `data-step-next` Attribute bleiben, JS-Logik unverändert

**Mobile (< 600px)**
- Stepper: nur Nummer-Circles + aktiver Step-Name, kein Subtext
- Formular: einspaltig (`form-grid` → `grid-template-columns: 1fr`)

### CSS-Änderungen

- Neue Klassen: `.wizard-stepper`, `.step-item`, `.step-circle`, `.step-circle.done`, `.step-circle.active`, `.step-connector`, `.step-text`
- `.wizard-layout` (Grid mit Sidebar) wird durch `.wizard-layout` (Stack: Stepper + Panel) ersetzt
- `.wizard-sidebar` entfällt komplett

### JS-Änderungen

- `initWizard()` in `site.js`: Step-Navigation bleibt identisch (data-step-next / data-step-prev), nur der visuelle Update der Step-Circles kommt hinzu
- `updateWizardConditionals()` bleibt unverändert

---

## Nicht in Scope

- Grows-Liste, Tent-Details, Settings: keine Änderungen
- Chart-Redesign: separate Überlegung
- Mobile-Navigation / Sidebar: eigener Sprint
- CSS-Gesamtbereinigung (3.200 Zeilen): eigener Sprint

---

## Reihenfolge der Umsetzung

1. CSS: neue Klassen für Zelt-Card in `site.css`
2. View: `Views/Home/Index.cshtml` – Tent-Card HTML ersetzen
3. CSS: Wizard-Stepper-Klassen in `site.css`
4. View: `Views/Grows/_GrowForm.cshtml` – Sidebar durch Stepper ersetzen
5. JS: `site.js` – `initWizard()` um visuelles Step-Update erweitern
6. Build + alle Tests grün
