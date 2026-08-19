# Regeln für die Arbeit an Grow Operation System

## DIE ERGEBNIS-REGEL

**Prüfe nie die Änderung. Prüfe immer das Ergebnis am laufenden Stand.**

Diese Regel steht hier, weil an einem einzigen Tag (19.08.2026) vier Fehler
ausgeliefert wurden, die alle dieselbe Form hatten: geprüft wurde das, was ich
geschrieben hatte — nicht das, was der Nutzer sieht.

| geprüft | hätte geprüft gehört |
|---|---|
| der Menü-Link funktioniert | was auf der Zielseite steht |
| die CSS-Regel wirkt (per `addStyleTag` eingespielt) | ob die **gebaute** Datei gewinnt |
| der Test läuft durch | ob sein Treffer ein **Kommentar** war |
| die Ausnahmeliste steht im Code | ob die Kennungen darin **echt** sind |

Ergebnis: eine Seite mit einem zweiten, verkrüppelten Messformular kam ins
Menü, eine Regel lag wochenlang wirkungslos im Stylesheet, ein Test war grün
ohne zu prüfen, und drei Ausnahmen griffen nie.

### Die fünf Prüfungen, die vor jedem „fertig" laufen

1. **Ansehen, nicht nur messen.** Bei allem, was der Nutzer sieht: die Seite
   rendern und den Inhalt **lesen**. Zahlen erheben ist keine Prüfung —
   `−500 ppm` rendert tadellos, ein doppeltes Formular auch.

2. **Nur gegen den gebauten Stand.** `npm run build`, dann an der laufenden App
   messen. **Niemals** eine Regel per `addStyleTag`/`evaluate` einspielen und
   das als Beleg nehmen: so etwas hängt am Dokumentende und gewinnt immer. Es
   belegt, dass der Gedanke stimmt — nicht, dass er ankommt.

3. **Bezeichner nie aus dem Kopf.** Klassennamen, Enum-Werte, Datei-Kennungen,
   Routen: immer aus dem laufenden Baum oder der Datei holen. In diesem Projekt
   ist das über 30-mal schiefgegangen.

4. **Eine Erwähnung ist keine Verwendung.** Ein Kommentar, der einen Namen
   nennt, ist kein Import. Eine XML-Doku, die einen Enum-Wert nennt, ist kein
   Erzeuger. Ein Link irgendwo im Quelltext ist kein Weg im Menü. Jede Prüfung,
   die per Textsuche arbeitet, muss Kommentare und Doku ausschließen.

5. **Zeigen, dass die Prüfung beißt.** Eine Prüfung, von der niemand
   nachgewiesen hat, dass sie den Fehler findet, ist kein Beleg. Fehler
   wiedereinbauen, Prüfung laufen lassen, muss rot werden.

### Zusätzlich bei bestimmten Änderungen

- **Etwas auffindbar machen** (Menü, Link, Suchbegriff): die Zielseite vorher
  **ganz** lesen. Erreichbar zu machen ist die Behauptung, dass es das wert ist.
  Führt die Seite dieselbe Hauptaktion wie eine andere, ist das ein Befund und
  kein Feature.
- **Layout**: nicht nur bei Scrollstand 0 messen. Klebende Elemente wandern erst
  beim Scrollen über ihre Nachbarn
  (`GrowDiary.React/zz-plausibel/ueberlappung-beim-scrollen.mjs`).
- **Text**: nicht den Element-Kasten messen, sondern den Textinhalt
  (`Range.getClientRects()`, siehe `zz-schrift/text-gegenprobe.mjs`). Ein
  `nowrap`-Element bleibt mit seinem Kasten brav in der Spalte, während die
  Buchstaben abgeschnitten werden.
- **CSS-Regel greift nicht?** Erst Spezifität und Ladereihenfolge prüfen. Bei
  gleicher Spezifität gewinnt die später geladene Datei (Reihenfolge in
  `src/App.tsx`). Zwei Klassen im Selektor gewinnen unabhängig davon.

### Prüfungen, die in diesem Projekt nichts prüfen

Belegte Fälle. Wer eine davon benutzt und „grün" meldet, hat nichts gemessen:

- **`tsc --noEmit` prüft NULL Dateien.** `tsconfig.json` hat `"files": []` und
  nur `references`. Richtig ist **`tsc -b`** (oder `npm run typecheck`). Am
  19.08.2026 mehrfach als „Typen ok" gemeldet, ohne eine Datei anzusehen.
- **Ein übersprungener Test ist kein bestandener.** Die E2E-Mappe fährt im Tor
  ohne Backend; von 34 Fällen aus vier Dateien sind dort 31 übersprungen. Genau
  die Prüfung, die das leere Archiv gefunden hätte, lief nie mit.
- **Eine Zählung ohne Mengenwächter** läuft bei leerer Grundmenge null Mal
  durch und ist grün. Jede braucht `Assert.True(menge.Count >= n)`.
- **Eine Textsuche darf die Datei nicht mitlesen, die sie prüft.**
  `routes-reachable` sucht Links im ganzen Quelltext einschließlich `App.tsx`,
  wo die Routen stehen — die Route belegt sich selbst.

### Die Hooks nehmen mir zwei Prüfungen ab

Eingerichtet in `.claude/settings.json`, die Skripte in `.claude/hooks/`:

| Wann | Was | Blockt? |
|---|---|---|
| nach jeder `.cs`/`.ts`/`.tsx`-Änderung | `dotnet build` bzw. `tsc -b` | ja |
| vor `git commit` | das volle Tor (Backend, Typen, Lint, Vitest) | ja |

Beide sind nachgewiesen: Fehler eingebaut, Hook wurde rot. **Sie ersetzen die
fünf Prüfungen nicht** — sie fangen Tippfehler und rotes CI, nicht ein
doppeltes Formular oder eine Seite, die niemand liest.

### Vor „fertig": den Prüfer laufen lassen

`.claude/agents/pruefer.md` — ein Agent in eigenem Kontext, der die Änderung
gegen die fünf Regeln ansieht. Er hat sie nicht gebaut und keinen Grund, sie zu
mögen. Aufrufen **bevor** „fertig" gesagt wird.

Für ein Release: `/release` — die Reihenfolge steht dort, nicht im Kopf.

## ZÄHLUNGEN STATT LISTEN

Eine handgeschriebene Liste kann nur an dem scheitern, was schon draufsteht.
Deshalb prüft dieses Projekt über die **Grundmenge** (Reflexion, Enum,
Verzeichnis) und verlangt für jeden Fall entweder eine Behandlung oder eine
Ausnahme **mit ausgeschriebenem Grund**.

Jede Zählung braucht einen **Selbsttest**: sieht sie ihre Grundmenge überhaupt?
Ohne den läuft sie bei einer leeren Menge null Mal durch und ist grün.

Vorhandene Zählungen (Muster zum Abschauen):
- `GrowDiary.Web.Tests/MessfelderVollstaendigTests.cs` — jedes Zahlenfeld hat
  eine Sperre
- `GrowDiary.Web.Tests/AutoFelderVollstaendigTests.cs` — kein Auto-Band ist
  weiter als die Physik
- `GrowDiary.Web.Tests/RisikoTypenVollstaendigTests.cs` — kein Empfehler
  verzweigt auf einen Typ, den niemand erzeugt
- `GrowDiary.Web.Tests/SollwertKetteVollstaendigTests.cs` — niemand umgeht die
  Profil-Kette
- `GrowDiary.Web.Tests/WissenErreichbarkeitTests.cs` — jeder Ablauf wird von
  etwas vorgeschlagen
- `GrowDiary.React/src/menue-vollstaendig.node.test.ts` — jede eigenständige
  Seite steht im Menü
- `GrowDiary.React/src/deutsche-woerter.node.test.ts` — kein Enum-Wert steht
  roh auf dem Schirm

## EINE WAHRHEIT JE ZAHL

Steht dieselbe Zahl an zwei Stellen, laufen sie auseinander — das ist kein
Risiko, sondern eine Frage der Zeit. Belegte Fälle: das EC-Ziel (Diagnose
0,6–0,8 gegen Kachel 0,9–1,1 für denselben Grow), die physikalischen Grenzen
(drei Tabellen, sieben Widersprüche), die Sauerstoff-Schwelle (viermal 6,5).

Wo eine Zahl gebraucht wird, die es schon gibt: **verweisen, nicht abtippen.**

## FACHLICHE REGELN

- **Keine KI in der App.** Nie vorschlagen.
- **Faustregeln nur mit Etikett**, Empfehlungen nur mit Quelle. Eine Zahl, die
  niemand nachprüfen kann, ist schlechter als „zu wenig Daten".
- **Alles auf Deutsch** — Kommentare, Bezeichner, Oberflächentexte.
- **Beide Themen prüfen.** Das helle wird beim Bauen regelmäßig vergessen; die
  Falle ist dreimal zugeschnappt.
- **Das Telefon ist sauber** (320–768 px, null Überlauf über zwölf Breiten).
  Jede Änderung wird dagegen gemessen.
- **Athena-PDFs gehören nicht ins Repository.**

## RELEASE

CI grün **vor** dem Image: `ci.yml` → `gh workflow run docker-publish.yml -f
version=X` → GHCR-Manifest HTTP 200 → erst dann `config.yaml` hochzählen. Der
Docker-Build führt keine Tests aus.
