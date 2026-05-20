# Grow OS Mobile-/Tablet-Audit-Plan

## 1. Viewports

Der Audit prüft mindestens diese Viewports:

- 360x800 Android klein
- 375x667 iPhone SE-artig
- 390x844 iPhone 12/13/14/15/16-artig
- 393x852 iPhone 17-nahe Klasse
- 430x932 iPhone Plus/Pro-Max-artig
- 768x1024 iPad Portrait
- 1024x768 iPad Landscape
- 820x1180 iPad Air Portrait
- 1180x820 iPad Air Landscape

Die ersten automatisierten Zusatz-Viewports im Visual Audit sind reportend vorbereitet:

- 393x852
- 430x932
- 768x1024
- 1024x768

## 2. Seiten

Automatisch zu prüfen:

- `/` Live
- `/addback`
- Addback Deep Flow über `/grows/:growId/addback`, wenn ein aktiver Grow eindeutig erreichbar ist
- `/messung`
- `/grows`
- `/grows/new`
- `/zelte`
- `/zelte/new`
- `/hydro`
- `/hydro/new`
- `/home-assistant`
- `/connect`
- `/hardware`
- `/wissen`
- `/settings`
- `/release`
- bestehende Legacy-/Redirect-Flows wie `/action` und `/aufgaben`, solange sie noch Teil der App sind

## 3. Automatische Regeln

Bereits oder vorbereitend automatisch prüfbar:

- Bottom-Nav hat auf Phone genau 4 Items.
- Items heißen `Live`, `Addback`, `Messung`, `Grows`.
- Mehr-Menü enthält sekundäre Seiten.
- Header top position respektiert Safe-Area-Testvariable.
- Bottom-Nav bottom position respektiert Safe-Area-Testvariable.
- Keine horizontalen Overflows.
- Kein sichtbarer Button unter Bottom-Nav.
- Touch targets sind mindestens 44 px.
- `/grows` existiert.
- `/grows/new` Wizard existiert.
- `/hydro` Preview ist overflowfrei.
- `/zelte` Metric Values sind nicht abgeschnitten.

Aktueller Ausbau-Status:

- Harte bestehende Layout-Checks bleiben nur dort hart, wo sie schon vor diesem Contract hart waren.
- Neue Contract-Findings werden zunächst reportend gesammelt.
- Report-Felder: `touchTargetFindings`, `safeAreaFindings`, `navStructureFindings`, `tabletLayoutFindings`.

## 4. Manuelle Checks auf echtem iPhone/iPad

Nur auf echter Hardware belastbar zu prüfen:

- Dynamic Island / Notch wirkt sauber.
- Home Indicator verdeckt nichts.
- PWA Standalone Modus.
- Safari vs Homescreen PWA.
- iPad Portrait und Landscape.
- Bediengefühl mit Daumen.
- Scrollgefühl, Scroll-Traps und Adressleisten-Verhalten in iOS Safari.
- Tap-Fehler bei eng liegenden Aktionen.

## 5. Nötige `data-audit` Hooks

Für stabile Audits sollen diese Hooks ergänzt oder bestätigt werden:

- `data-audit="mobile-shell"` für den responsiven App-Shell-Kontext
- `data-audit="mobile-header"` für den Phone-Header
- `data-audit="mobile-bottom-nav"` für die Phone-Bottom-Nav
- `data-audit="mobile-bottom-nav-item"` je Bottom-Nav-Item
- `data-audit="mobile-more-button"` für den Mehr-Trigger
- `data-audit="mobile-more-menu"` für das Mehr-Menü
- `data-audit="mobile-more-group-setup"` für Zelte/Hydro/Sensoren
- `data-audit="mobile-more-group-integration"` für Home Assistant/Geräte verbinden
- `data-audit="mobile-more-group-system"` für Wissen/Einstellungen/Release
- `data-audit="live-screen"` und `data-audit="live-quick-actions"`
- `data-audit="addback-hub"` und `data-audit="addback-stepper"`
- `data-audit="measurement-form"` und `data-audit="measurement-save-actions"`
- `data-audit="grows-overview"` und `data-audit="grow-list-actions"`
- `data-audit="grow-wizard"` für `/grows/new`
- bestehend weiter nutzen: `hydro-preview`, `tent-delete-button`, `knowledge-search`, `knowledge-topic-nav`, `ha-connection-layout`, `hardware-edit-form`

## 6. Screenshots

Relevante Screenshots:

- je Viewport ein Full-Page-Screenshot für alle Hauptseiten
- Phone: Live, Addback, Messung, Grows, Mehr-Menü geöffnet
- Phone: `/grows/new` je Wizard-Schritt, soweit stabil automatisierbar
- Phone: Addback-Flow mit Stepper, Istwerten, Zielwerten, Dosierung, Nachmessung
- Phone: Messung mit sichtbarer Speichern-Aktion
- iPad Portrait: Startscreen, Grows, Addback, Messung
- iPad Landscape: Startscreen mit adaptiver Navigation, Grows, sekundäre Bereiche
- Problem-Screenshots für Overflow, abgeschnittene Werte und Bottom-Nav-Überdeckung

## 7. Akzeptanzkriterien

Phone:

- Bottom Navigation enthält exakt `Live`, `Addback`, `Messung`, `Grows`.
- Sekundäre Bereiche sind über Mehr erreichbar und gruppiert.
- Keine horizontalen Scrollbars.
- Keine sichtbaren Controls liegen unter Bottom-Nav oder Home Indicator.
- Header liegt nicht unter Notch/Dynamic Island.
- Touch targets sind mindestens 44x44 px.
- Hauptscreens funktionieren als schnelle Bedien-/Monitoring-Schnittstelle.

Tablet/iPad:

- Tablet wirkt nicht wie ein riesiges Handy.
- Navigation ist Sidebar- oder adaptiv-dominant, nicht nur Phone-Bottom-Nav.
- Controls bleiben touchfreundlich.
- Inhalte nutzen zusätzliche Breite für Übersicht, ohne Desktop-Mini-Controls.

Allgemein:

- Keine Fachlogik-Änderungen als Folge des Mobile-Polish.
- Keine Backend-, API-, DB- oder Repository-Klassen-Änderungen.
- Keine Testdaten in normaler DB.
- Keine Secrets in Git oder Publish.
- Audit-Findings sind nachvollziehbar im JSON- und Markdown-Report.
