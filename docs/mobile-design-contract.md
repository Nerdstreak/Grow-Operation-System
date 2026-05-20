# Grow OS Mobile-/Tablet-Design-Contract

## 1. Zielbild

Grow OS Mobile ist eine Companion App und Bedienoberfläche für Monitoring und schnelle Aktionen, keine verkleinerte Desktop-Verwaltung.

Der Phone-Fokus liegt verbindlich auf vier Kernen:

- Live
- Addback
- Messung
- Grows

Desktop bleibt die vollständige Verwaltungsoberfläche für Setup, Integration, Wissen, System und längere Bearbeitungsabläufe. Tablet/iPad ist die Zwischenform: mehr Übersicht als Phone, aber weiterhin touchfreundlich und nicht als Mini-Desktop mit kleinen Controls.

## 2. Navigationsmodell

### Phone

Phone verwendet eine Bottom Navigation mit exakt vier Punkten:

- Live
- Addback
- Messung
- Grows

Sekundäre Bereiche bleiben über ein Mehr-Menü erreichbar, aber nicht in der primären Bottom Navigation:

- Setup: Zelte, Hydro, Sensoren
- Integration: Home Assistant, Geräte verbinden
- Wissen & System: Wissen, Einstellungen, Release

### iPad / Tablet

Tablet muss nicht die Phone-Bottom-Nav übernehmen. Bevorzugt ist:

- Sidebar oder adaptive Navigation
- sichtbare Hauptbereiche
- gruppierte sekundäre Bereiche
- touchfreundliche Abstände und Trefferflächen
- keine Desktop-Mini-Controls

iPad darf dashboard-artiger wirken als Phone, soll aber keine gequetschte Desktop-App sein.

## 3. Breakpoints

Verbindliche Breakpoints:

- `phone-small`: 360-374 px
- `phone`: 375-430 px
- `phone-large`: 431-767 px
- `tablet`: 768-1023 px
- `tablet-large / desktop-like`: ab 1024 px

Wichtig:

- iPhone 17 wird nicht über physische Pixel abgeleitet.
- Relevant sind CSS-Viewport und DPR.
- Tests müssen mehrere iPhone-nahe Viewports prüfen.

## 4. Test-Viewports

Mindestens zu prüfen:

- 360x800 Android klein
- 375x667 iPhone SE-artig
- 390x844 iPhone 12/13/14/15/16-artig
- 393x852 iPhone 17-nahe Klasse
- 430x932 iPhone Plus/Pro-Max-artig
- 768x1024 iPad Portrait
- 1024x768 iPad Landscape
- 820x1180 iPad Air Portrait
- 1180x820 iPad Air Landscape

## 5. Safe-Area-Regeln

Zentrale CSS-Variablen:

- `--safe-top`
- `--safe-right`
- `--safe-bottom`
- `--safe-left`
- `--mobile-header-height`
- `--mobile-bottom-nav-height`
- `--mobile-page-padding`

Regeln:

- `index.html` muss `viewport-fit=cover` enthalten.
- Header darf nie unter Notch/Dynamic Island liegen.
- Bottom-Nav darf nie vom Home Indicator verdeckt werden.
- Content darf nie unter Bottom-Nav verschwinden.
- Keine floating Pill-Bar mit sichtbarem Spalt unten.
- Bottom-Nav-Hintergrund läuft bis ganz unten durch.
- `100dvh` oder `min-height: 100dvh` verwenden, wo sinnvoll.
- Keine `100vw`-Elemente innerhalb gepaddeter Container.

## 6. Touch-Regeln

- Buttons haben mindestens 44 px Höhe, besser 48 px.
- Inputs haben mindestens 48 px Höhe.
- Karten-Aktionen sind touchfreundlich.
- Kein Button hat eine aktive Touchfläche kleiner als 44x44 px.
- Buttons dürfen umbrechen, aber nicht gequetscht werden.

## 7. Mobile Content-Regeln

- Keine Desktop-Tabellen auf Phone.
- Keine 3-/4-Spalten-Metric-Grids auf Phone.
- Phone nutzt meistens 1 Spalte, maximal 2 kurze Metric-Spalten.
- Cards sind nicht überladen.
- CTAs sind klar und nicht textlastig.
- Empty States sind kurz.
- Keine langen Erklärtexte oben.
- Wenn langer Text zum Verstehen nötig ist, muss das Design geprüft werden.

## 8. Hauptscreens Phone

### Live

- Monitoring zuerst
- aktives Zelt
- aktiver Grow
- Klima/Sensorstatus
- Kamera optional
- Schnellaktionen: Aktualisieren, Messung, Addback

### Addback

- Hydro/Grow schnell wählen
- Istwerte
- Zielwerte
- Dosierung
- Nachmessung
- Stepper kompakt

### Messung

- schnelle Werteingabe
- Foto optional
- Speichern immer erreichbar
- kein horizontaler Overflow

### Grows

- aktive Grows zuerst
- Neuen Grow anlegen
- Öffnen/Bearbeiten/Beenden/Löschen
- Wizard unter `/grows/new` vollständig

## 9. Sekundäre Screens

Diese Bereiche bleiben mobil bedienbar und über das Mehr-Menü erreichbar, sind aber nicht Bottom-Nav-Fokus:

- Zelte
- Hydro
- Sensoren
- Home Assistant
- Geräte verbinden
- Wissen
- Einstellungen
- Release

## 10. No-Go-Regeln

- Keine abgeschnittenen Buttons
- Keine Inhalte unter Notch
- Keine Inhalte unter Home Indicator
- Keine horizontalen Scrollbars
- Keine gequetschten Desktop-Karten
- Keine fehlenden Wizard-Inhalte
- Keine ASCII-Umlaute in sichtbaren UI-Texten
- Keine Testdaten in normaler DB
- Keine Secrets in Git oder Publish

## Ist-Zustand / technische Risiken

### Aktuelle Navigation

`GrowDiary.React/src/App.tsx` definiert aktuell `coreNav` als `Live`, `Addback`, `Zelte`, `Hydro`. Diese Liste wird sowohl für die Desktop-Core-Navigation als auch für die mobile Bottom Navigation verwendet. Damit entspricht die aktuelle Phone-Bottom-Nav noch nicht dem Contract, weil `Messung` und `Grows` im Mehr-Menü liegen und `Zelte`/`Hydro` primär sind.

`moreNav` enthält aktuell `Aufgaben`, `Grows`, `Messung`, `Home Assistant`, `Gerät verbinden`, `Sensoren`, `Wissen`, `Vergleich`, `Archiv`, `Einstellungen`. Die Route `/release` existiert, ist aber in `moreNav` nicht sichtbar verlinkt. Für den Contract muss Release später in die Gruppe "Wissen & System".

### Aktuelle Safe-Area-Umsetzung

`GrowDiary.React/index.html` enthält bereits `viewport-fit=cover` und PWA-Metadaten inklusive `apple-mobile-web-app-capable`.

Safe-Area wird aktuell nicht über die zentralen Contract-Variablen geführt. Stattdessen gibt es punktuelle Regeln in `src/index.css` und `src/rc2-overrides.css`, vor allem `env(safe-area-inset-bottom)` für Bottom-Nav und Sticky Actions. Für `safe-area-inset-top` gibt es noch keinen belastbaren Header-Contract.

### Riskante `100vh` / `100vw` / `fixed` / `sticky`-Stellen

- `src/index.css` enthält Legacy-Layouts mit `height: 100vh` und V1-Layouts mit `min-height: 100vh`.
- `src/rc2-overrides.css` setzt `.v1-app-shell` auf `height: 100dvh` in kleinen Viewports, was sinnvoll sein kann, aber in Kombination mit verschachteltem Scrolling geprüft werden muss.
- `.v1-desktop-nav`, `.v1-mobile-topbar`, `.v1-mobile-more-panel` und `.v1-bottom-nav` verwenden `fixed` in den bestehenden Styles.
- `.sticky-actions`, `.ops1b-sticky-actions`, `.rc2-sticky-card`, `.grow-wizard-context` und mehrere Navigationsbereiche verwenden `sticky`.
- `src/rc2-overrides.css` setzt `.v1-route-frame` auf `width: 100vw` und `max-width: 100vw`; das ist innerhalb gepaddeter Container ein Contract-Risiko und muss bei der späteren UI-Umsetzung entfernt oder eingegrenzt werden.

### Screens mit voraussichtlich erster Anpassung

- `App.tsx`: Navigation in Phone, Tablet und Mehr-Menü an den Contract anpassen.
- `LiveDashboardPage.tsx`: Phone-Startscreen konsequent auf Monitoring und Schnellaktionen ausrichten.
- `AddbackHubPage.tsx` und `AddbackPage.tsx`: Addback als schnellen Bedienflow sichern; vorhandener Stepper muss kompakt bleiben.
- `ManualMeasurementPage.tsx`: Speichern/Abbrechen und Fotoeingabe gegen Bottom-Nav und Overflow absichern.
- `GrowsPage.tsx`, `GrowSetupPage.tsx`, `GrowDetailPage.tsx`: aktive Grows zuerst, Wizard vollständig, Actions touchfreundlich.
- `TentsPage.tsx` und `HydroPage.tsx`: sekundär erreichbar, aber mobile Karten/Metrics/Preview weiterhin overflowfrei.

### Daten- und Test-Isolation

Diese Aufgabe darf keine Backend-, API-, Datenbank-, Repository- oder Reset-Script-Änderungen enthalten. Die vorhandenen Audit-Testdaten-Flows in `visual-audit.spec.ts` und `workflow-audit.spec.ts` bleiben fachlich unangetastet. Test-Isolation, normale Datenbank, Addback-Rechenlogik, Messlogik, Home-Assistant-Logik, Grow-Wizard-Inhalte und Knowledge-Content dürfen im nächsten UI-Schritt nicht als Nebenwirkung verändert werden.
