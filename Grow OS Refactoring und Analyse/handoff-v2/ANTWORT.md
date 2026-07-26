# Antwort an Claude Code · Entwurf + die vier Fragen

Die Datei liegt bei: `Grow OS Redesign.dc.html` (+ `support.js`, muss daneben liegen,
dann öffnet sie direkt im Browser). Alle Maße, Abstände und Farben stehen inline im
Quelltext — Draufsicht-Geometrie wird in der `Component`-Klasse in **Zentimetern**
gerechnet und in Prozent der Rahmenbox ausgegeben (entspricht `system-plan-model.ts`).

**Wichtig, bevor du misst:**

1. **Der Google-Fonts-`<link>` im `<helmet>` ist nur Preview-Komfort.** Produktion:
   die gebündelten Archivo/JetBrains-Mono-Dateien. Keine neuen Schriften nötig.
2. **Der Tab-Umschalter oben rechts (SOLL/IST) und die klickbare Seitenleiste sind
   Mock-Navigation**, kein Teil des Entwurfs. Die echte App navigiert über Router.
3. **Zwei Stellen sind von eurer IA-Änderung überholt** — die neue IA gewinnt:
   - Grow-Detail zeigt im Mock noch Tabs (Diagnose, Messungen, Journal …). Gilt nur
     noch die Ansicht **Überblick**: Kopfzeile mit Aktionen, Phasen-Timeline,
     Fakten-Leiste, Diagnose-Kurzliste (max. 2, Link zur Top-Seite), letzte 4 Messungen.
   - Die Kontextleiste im Mock zeigt Zelt/Grow statisch — genau so ist sie gemeint:
     einmal oben, nie pro Seite.
4. Farbtokens: es kommt **keine neue Farbe** dazu. Alles im Mock ist `--accent`/
   `--warn`/`--danger`/`--info` (+ deren `-text`/`-wash`) — Hell-Varianten stehen
   in `tokens.css`.

---

## a) 924 px — was bricht, was schrumpft

Regel im ganzen Entwurf: **Nebenspalten brechen um, Hauptinhalte schrumpfen nie
unter ihre Mindestbreite.** Konkret:

- Alle Zwei-Spalten-Layouts sind `repeat(auto-fit, minmax(<min>, 1fr))` mit
  min = 300–340 px. Bei 924 px minus Sidebar (236) minus Padding bleiben ~640 px:
  die Prüf-/Faktenspalte rutscht **unter** den Hauptblock. Kein Zwischenzustand,
  in dem beide gequetscht nebeneinander stehen.
- KPI-/Messwert-Leisten sind `flex-wrap` mit `flex: 1 1 150px` — bei 924 px stehen
  4 pro Zeile statt 8, Trennlinien laufen über die geerbten `border-left/top` weiter.
- Die RDWC-Draufsicht hält ihr Seitenverhältnis (`aspect-ratio` aus Zeltmaß) und
  ist auf `max-width: 560px` gedeckelt — sie wird bei 924 px **nicht** kleiner,
  die Faktenspalte weicht nach unten aus.
- Sidebar bleibt bis 860 px, darunter Bottom-Nav (steht so in `shell.css`).
- Tabellen (Sensoren, Protokoll, Ernte): ab dem Umbruchpunkt horizontal scrollbar
  innerhalb der Karte — nie die Seite selbst verbreitern. Erste Spalte bleibt lesbar,
  weil sie die breiteste `fr`-Einheit hat.

## b) Leer · lädt · kaputt — eine Regel statt pro Seite

- **Leer:** pro Sektion genau ein `.v1-empty` mit **einer** Aktion. Live ohne
  Messung: Score-Ring zeigt „—", darunter „Erste Messung erfassen". Hydro ohne
  System: die Draufsicht-Fläche selbst ist der Empty-State („+ System anlegen").
  Kein Illustrations-Placeholder, keine zweite Aktion.
- **Laden:** Skeleton = dieselben Hairline-Boxen wie der Inhalt, Werte als
  `--hint`-Balken (60 % Breite). Keine Spinner, kein Layout-Sprung — die Boxen
  haben schon die endgültige `min-height`.
- **HA nicht erreichbar:** ein `--warn`-Banner **unter der Kontextleiste** (global,
  nicht pro Karte), alle Live-Werte zeigen den letzten bekannten Stand plus
  Zeitstempel („25,4 °C · vor 8 min") in `--muted`. Nie leere Felder — ein RDWC
  läuft weiter, auch wenn HA hustet. Manuelle Eingabe (Messen, Addback) bleibt
  voll funktionsfähig.
- **Einzelner Sensor gestört:** nur dessen Kachel: Wert in `--danger`, Status
  „Wert unplausibel" (siehe DO-Zeile in der Sensoren-Tabelle im Mock).

## c) Wenn es zu viel wird

- **Messwert-Kacheln (Live):** wachsen per `flex-wrap` — 8, 10, 12 Werte sind
  einfach mehr Zeilen. Reihenfolge fix: erst Klima, dann Nährlösung, Abweichungen
  behalten ihre Position (nicht nach vorn sortieren — Muskelgedächtnis schlägt
  Priorisierung, die Statusfarbe macht das Highlighting).
- **12+ Sites im RDWC:** die Draufsicht rechnet in cm, sie wird also automatisch
  dichter, nie abgeschnitten. Unter ~24 px gerendertem Eimerdurchmesser die
  Sitenummern ausblenden (nur Kreise), die Prüfzeile meldet ohnehin „zu eng",
  bevor es unleserlich wird.
- **3 Kameras:** eine große Bühne + Thumbnail-Zeile darunter (Klick tauscht),
  nicht drei gleich große Karten.
- **8+ Sensoren:** die Tabelle bleibt eine Tabelle — Filterchips oben (ALLE /
  MESSGERÄTE / TECHNIK / FÄLLIG) sind bereits im Mock; ab ~20 Zeilen zusätzlich
  Suchfeld, keine Pagination im Heimnetz-Maßstab.
- **Journal:** monatsweise nachladen („Ältere Einträge laden"), kein Infinite Scroll.

## d) Die eine Sache pro Ansicht

| Ansicht | Das Wichtigste | Die eine Handlung |
|---|---|---|
| Live | Statuswort + schlimmste Abweichung (z. B. „DO 5,1 kritisch") | die vorgeschlagene SOP/Aktion des Risikos |
| Messen | die Nährlösungs-Felder pH/EC/DO (Klima kommt eh aus HA) | Messung speichern |
| Addback | die drei Dosier-Zahlen in ml — groß, mono | „Dosiert & protokollieren" |
| Grow anlegen | die Prüfung rechts (Kollision, Site-Zahl) | Grow anlegen (erst aktiv, wenn Prüfung grün/gelb) |
| Ernte | die Gewichts-Eingabe pro Pflanze | Ernte speichern |
| Grow-Detail | Phasen-Timeline mit „Tag X" | kontextabhängig: fällige Aktion (Addback/Flip/Ernte) |
| Hydro | die Draufsicht + „passt es?"-Zeile | System anlegen/bearbeiten |
| Aufgaben | das oberste kritische Risiko | dessen SOP starten |

Hierarchie-Faustregel im Entwurf: pro Karte genau **ein** Primärbutton
(`--accent`-Füllung), alles andere Hairline. Wo im Mock zwei gefüllte Buttons
nebeneinander stehen, ist der linke gemeint.

---

Wenn beim Nachbauen etwas zwischen zwei Auslegungen hängt: im Zweifel die Variante
mit weniger Rahmen, weniger Füllfarbe, mehr Hairline.
