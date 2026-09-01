import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync } from 'node:fs'
import { istLeer, istUnlesbar, unlesbarMeldung, unlesbareFelder, zahlOderNull } from './zahlenfeld'

/**
 * Keine Seite baut sich ihre eigene Zahlen-Umwandlung.
 *
 * **Der Anlass.** Derselbe Fehler kam dreimal, in drei Fassungen:
 *
 * | Seite | Fassung | Folge |
 * |---|---|---|
 * | `ManualMeasurementPage` | `Number.isFinite`, meldet Unlesbares | 2026-08 behoben |
 * | `MeasurementEditPage` | `Number.isNaN` | „6,2x" wurde still zu `null` |
 * | `DosingPumpSetupPage` | **keine Leerprüfung** | leeres Feld wurde zu `0` |
 *
 * Der dritte war der teuerste: `Number('')` ist `0` und `Number.isFinite(0)`
 * ist `true`. Ein geleerter Mindestabstand wurde damit zur Null, und
 * `DosingService` prüft `seit < TimeSpan.FromMinutes(0)` — das ist nie wahr.
 * Die Pumpe hätte ohne jede Mischpause dosiert, still, mit Erfolgsmeldung.
 *
 * Behoben wurde jedes Mal nur die eine Seite, auf der es auffiel.
 */

const QUELLE = new URL('./', import.meta.url)

/**
 * Wie viele eigene Umwandlungen es noch gibt — **diese Zahl darf nur sinken.**
 *
 * Zwanzig Stellen wandeln getippten deutschen Text selbst in Zahlen um. Alle an
 * einem Tag umzustellen hiesse, zwanzig funktionierende Seiten anzufassen; eine
 * Ausnahmeliste mit zwanzig Namen wäre ein Feigenblatt, das nur an dem
 * scheitern kann, was schon draufsteht.
 *
 * Deshalb eine Ratsche: eine NEUE eigene Fassung fällt sofort auf, und wer eine
 * alte umstellt, muss die Zahl mitsenken — sonst wird der Test rot und erinnert
 * ihn daran. Der Fortschritt ist damit sichtbar statt behauptet.
 *
 * Stand 2026-08-23: 16. Zwei sind an diesem Tag verschwunden
 * (`MeasurementEditPage`, `DosingPumpSetupPage`) — beide wegen eines echten,
 * lebenden Fehlers.
 *
 * **Stand 2026-09-01: 24 — und das ist ein Anstieg auf dem Papier, kein
 * Rückschritt.** Der Suchausdruck sah nur `Number(…)` und übersah damit zehn
 * Stellen, darunter `Number.parseFloat(x.replace(…))` in `PhenoSheetEditor`,
 * `StrainsPage` und `TentsPage` — zwei davon mit genau dem Fehler, gegen den
 * diese Datei angetreten ist. Eine Ratsche, die einen Teil ihrer Grundmenge
 * nicht sieht, misst den Fortschritt an der falschen Zahl.
 *
 * Am selben Tag sind zwei verschwunden: `HarvestPage` und `AlertsPage`. Dort wurden
 * aus getippten „21,5" die Zahl 215, weil das Feld an einer Zahl hing und die
 * Zwischenform bei jedem Tastendruck wegwarf.
 *
 * 01.09.2026, später: 23 → 22. `HardwarePage` trug eine EIGENE Fassung von
 * `zahlOderNull` — die vierte Abschrift derselben fünf Zeilen. Sie wurde
 * unbenutzt, als die Kalibrierpunkte auf `zahlenfeld.ts` umgestellt wurden.
 */
const HOECHSTENS = 22

/**
 * Das Kennzeichen: eine Komma-Ersetzung ergibt nur bei getipptem Text Sinn.
 *
 * <b>Die erste Fassung sah nur `Number(…)`.</b> Sie hing an `Number\(` und an
 * `[^)]*` — damit fielen `Number.parseFloat(x.replace(',', '.'))` und jede
 * Fassung mit einer inneren Klammer heraus. Zehn Stellen blieben unsichtbar,
 * zwei davon mit genau dem Fehler, gegen den diese Datei angetreten ist:
 * `num()` in `PhenoSheetEditor` und `StrainsPage` liefert für „6,2x" die 6,2
 * und meldet nichts.
 *
 * Gesucht wird jetzt die Komma-Ersetzung selbst — die ist das Kennzeichen, und
 * sie steht in jeder Fassung.
 */
const EIGENE_FASSUNG = /\.replace\(\s*['"],['"]\s*,\s*['"]\.['"]\s*\)/

function alleQuellen(ordner = QUELLE, pfad = ''): string[] {
  const raus: string[] = []
  for (const eintrag of readdirSync(ordner, { withFileTypes: true })) {
    if (eintrag.name === 'node_modules') continue
    if (eintrag.isDirectory()) {
      raus.push(...alleQuellen(new URL(eintrag.name + '/', ordner), pfad + eintrag.name + '/'))
    } else if (eintrag.name.endsWith('.tsx') || eintrag.name.endsWith('.ts')) {
      if (eintrag.name.includes('.test.')) continue
      // zahlenfeld.ts IST die Antwort — dort MUSS die Umwandlung stehen.
      if (eintrag.name === 'zahlenfeld.ts') continue
      raus.push(pfad + eintrag.name)
    }
  }
  return raus
}

/** Alle Fundstellen mit Datei und Zeile. */
function eigeneFassungen(): string[] {
  const treffer: string[] = []

  for (const name of alleQuellen()) {
    const inhalt = readFileSync(new URL(name, QUELLE), 'utf8')

    inhalt.split(String.fromCharCode(10)).forEach((zeile, i) => {
      const roh = zeile.trim()
      // Kommentare zählen nicht — dort DARF die alte Schreibweise stehen,
      // das ist ja die Begründung.
      if (roh.startsWith('//') || roh.startsWith('*') || roh.startsWith('/*')) return
      if (EIGENE_FASSUNG.test(roh)) treffer.push(`${name}:${i + 1}  ${roh.slice(0, 90)}`)
    })
  }

  return treffer
}

describe('Zahlenfelder', () => {
  /* ---------------- Die geteilte Fassung selbst ---------------- */

  it('unterscheidet leer von unlesbar', () => {
    // Der ganze Sinn der Übung. Beides ergibt `null`, meint aber Verschiedenes:
    // „nicht gemessen" gegen „vertippt".
    expect(zahlOderNull('')).toBeNull()
    expect(zahlOderNull('   ')).toBeNull()
    expect(zahlOderNull('6,2x')).toBeNull()

    expect(istUnlesbar('')).toBe(false)
    expect(istUnlesbar('   ')).toBe(false)
    expect(istUnlesbar('6,2x')).toBe(true)
  })

  it('macht aus einem leeren Feld KEINE Null', () => {
    // Der Fehler in der Dosierpumpe: `Number('')` ist 0 und gilt als endlich.
    expect(zahlOderNull('')).not.toBe(0)
    expect(zahlOderNull('0')).toBe(0)   // eine getippte Null bleibt eine Null
  })

  it('lässt Unendlich nicht durch', () => {
    // `Number.isNaN` hätte hier ja gesagt — die Fassung der Bearbeiten-Seite.
    expect(zahlOderNull('Infinity')).toBeNull()
    expect(zahlOderNull('1e400')).toBeNull()
  })

  it('nimmt das deutsche Komma', () => {
    expect(zahlOderNull('6,2')).toBe(6.2)
    expect(zahlOderNull(' 6,2 ')).toBe(6.2)
    expect(istLeer(' ')).toBe(true)
  })

  it('nennt unlesbare Felder beim Namen', () => {
    const namen = unlesbareFelder([['6,2x', 'Reservoir-pH'], ['', 'EC'], ['1,2', 'Wassertemperatur']])
    expect(namen).toEqual(['Reservoir-pH'])

    const meldung = unlesbarMeldung(namen)
    expect(meldung).toContain('Reservoir-pH')
    // Die Meldung muss sagen, was sonst passiert — „ungültige Eingabe" allein
    // lässt offen, ob gespeichert wurde.
    expect(meldung).toContain('verloren')
    expect(unlesbarMeldung([])).toBeNull()
  })

  /* ---------------- Die Ratsche ---------------- */

  it('sieht ihre Grundmenge überhaupt', () => {
    expect(alleQuellen().length,
      'Keine Quelldatei gefunden — dann liefe alles darunter null Mal durch.')
      .toBeGreaterThan(50)
  })

  it('keine NEUE eigene Zahlen-Umwandlung', () => {
    const treffer = eigeneFassungen()

    expect(treffer.length,
      `Es gibt jetzt ${treffer.length} eigene Zahlen-Umwandlungen, erlaubt sind ${HOECHSTENS}. `
      + 'Benutze `zahlOderNull` und `istUnlesbar` aus `src/zahlenfeld.ts`. Drei eigene '
      + 'Fassungen haben drei verschiedene Fehler ergeben — zuletzt eine Dosierpumpe '
      + 'ohne Mischpause. Fundstellen: ' + treffer.join(' | '))
      .toBeLessThanOrEqual(HOECHSTENS)
  })

  it('und die alten werden weniger, nicht heimlich mehr', () => {
    const treffer = eigeneFassungen()

    expect(treffer.length,
      `Es sind nur noch ${treffer.length}. Setz HOECHSTENS in dieser Datei auf diese Zahl — `
      + 'sonst ist wieder Platz für eine neue, ohne dass es auffällt.')
      .toBeGreaterThanOrEqual(HOECHSTENS)
  })

  /* ---------------- Dass die Ratsche beisst ---------------- */

  it('erkennt die alte Schreibweise und lässt Kommentare in Ruhe', () => {
    // Die echten Fassungen, wörtlich aus dem Verlauf.
    expect(EIGENE_FASSUNG.test("const parsed = Number(value.replace(',', '.'))")).toBe(true)
    expect(EIGENE_FASSUNG.test("const parsed = Number(trimmed.replace(',', '.'))")).toBe(true)
    expect(EIGENE_FASSUNG.test("weightG: Number(neu.weightG.replace(',', '.'))")).toBe(true)

    // Datums-Prüfungen und schon-numerische Werte gehen NICHT ins Netz. Eine
    // erste Fassung suchte auch nach `Number.isFinite`/`Number.isNaN` und fand
    // 25 Stellen, davon 17 Unbeteiligte. Eine Prüfung, die überwiegend
    // Unschuldige meldet, wird abgeschaltet — dann prüft sie gar nichts mehr.
    expect(EIGENE_FASSUNG.test('if (Number.isNaN(date.getTime())) return null')).toBe(false)
    expect(EIGENE_FASSUNG.test('if (typeof value === "number" && Number.isFinite(value))')).toBe(false)
  })
})
