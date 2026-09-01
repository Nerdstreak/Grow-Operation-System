import { readFileSync, readdirSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

/**
 * Jede E2E-Datei, die schreibt, nimmt das Schloss.
 *
 * <b>Der Anlass.</b> Playwright fährt mit <c>fullyParallel: true</c>; die
 * Dateien laufen in verschiedenen Prozessen auf <b>demselben</b> Bestand. Vier
 * Dateien hatten deshalb ein Lockfile bekommen (<c>e2e/schloss.ts</c>). Am
 * 01.09.2026 kam eine fünfte dazu — und der zweite volle Lauf hintereinander
 * meldete prompt einen Fehlschlag in einer der vier, während dieselbe Datei
 * allein dreimal grün lief.
 *
 * <b>Warum eine Zählung.</b> Der Kommentar in <c>schloss.ts</c> sagte „vier
 * Dateien". Eine Zahl im Fließtext altert; sie hat die fünfte nicht bemerkt und
 * hätte die sechste auch nicht bemerkt. Diese Prüfung geht über die
 * <b>Grundmenge</b> — alle <c>*.spec.ts</c> in <c>e2e/</c> — und verlangt für
 * jede schreibende Datei entweder das Schloss oder einen ausgeschriebenen Grund.
 *
 * <b>Was „schreibend" heisst.</b> Ein Aufruf, der den Bestand verändert:
 * <c>request.post/put/patch/delete</c>. Reines Lesen und reines Anklicken zählt
 * nicht — ein Klick, der ein Formular abschickt, läuft über eine dieser
 * Methoden oder über die Seite selbst; für den zweiten Fall gibt es die
 * Ausnahmeliste mit Grund.
 */

const E2E = new URL('../e2e/', import.meta.url)

/**
 * Der Aufruf, der das Schloss WIRKLICH holt.
 *
 * <b>Die erste Fassung suchte nur den Namen</b> — und ein unbenutzter Import
 * reichte, um durchzukommen. Der Prüfer hat es nachgestellt: `beforeEach` und
 * `afterEach` entfernt, den Import stehen lassen, Prüfung grün. Eine Erwähnung
 * ist keine Verwendung; genau diese Falle ist in diesem Projekt mehrfach
 * zugeschnappt. Gesucht wird deshalb der AUFRUF mit Klammern.
 */
const SCHLOSS = /\bnimmSchloss\s*\(/

/** Schreibende Aufrufe über die Playwright-Anfrage. */
const SCHREIBT = /\brequest\.(post|put|patch|delete)\s*\(/

const Blockkommentar = /\/\*[\s\S]*?\*\//g
const Zeilenkommentar = /\/\/.*$/gm

/**
 * Schreibende Dateien ohne Schloss — je mit ausgeschriebenem Grund.
 *
 * Ein Eintrag ohne Grund ist keine Ausnahme, sondern eine Lücke mit Deckel.
 */
const OHNE_SCHLOSS: Record<string, string> = {
  // Noch keine. Die erste Fassung dieser Prüfung trug hier eine Datei ein, die
  // es gar nicht gibt — aus dem Kopf geschrieben statt aus dem Verzeichnis
  // gelesen. Die dritte Prüfung darunter hat es gefunden; deshalb gibt es sie.
}

function ohneKommentare(code: string): string {
  return code.replace(Blockkommentar, '').replace(Zeilenkommentar, '')
}

describe('E2E-Schloss', () => {
  const dateien = readdirSync(E2E).filter((name) => name.endsWith('.spec.ts'))

  it('sieht ueberhaupt E2E-Dateien', () => {
    /* Mengenwächter: ohne Grundmenge liefe die Schleife null Mal durch und wäre
       grün. Genau so war die Kontrast-Prüfung dieses Projekts dreimal blind. */
    expect(dateien.length, `Nur ${dateien.length} .spec.ts in e2e/ gefunden — `
      + 'die Prüfung sieht ihre Grundmenge nicht.').toBeGreaterThanOrEqual(10)
  })

  it('jede schreibende Datei nimmt das Schloss', () => {
    const verstoesse: string[] = []
    let schreibende = 0

    for (const name of dateien) {
      const roh = readFileSync(new URL(name, E2E), 'utf8')
      const code = ohneKommentare(roh)
      if (!SCHREIBT.test(code)) continue

      schreibende += 1
      if (SCHLOSS.test(code)) continue
      if (OHNE_SCHLOSS[name]) continue

      verstoesse.push(name)
    }

    /* Zweiter Mengenwächter: findet der Suchausdruck überhaupt schreibende
       Dateien? Eine kaputte Regex wäre sonst nicht von „alles sauber" zu
       unterscheiden. */
    expect(schreibende, 'Keine einzige schreibende E2E-Datei gefunden — der '
      + 'Suchausdruck greift nicht mehr. Er ist damit blind, nicht zufrieden.')
      .toBeGreaterThanOrEqual(3)

    expect(verstoesse,
      'Diese E2E-Dateien schreiben in den geteilten Bestand, ohne das Schloss zu '
      + 'nehmen:\n  ' + verstoesse.join('\n  ')
      + '\n\nPlaywright läuft parallel — zwei Dateien am selben Grow ergeben einen '
      + 'Test, dessen Ausgang vom Zeitpunkt abhängt, und der hat nichts geprüft. '
      + "Richtig ist `test.beforeEach(async () => { await nimmSchloss() })` plus "
      + '`test.afterEach(() => { gibSchloss() })`. Wer wirklich ohne auskommt, '
      + 'trägt sich mit Grund in OHNE_SCHLOSS ein.')
      .toEqual([])
  })

  it('keine Ausnahme zeigt ins Leere', () => {
    /* Ein Tippfehler im Dateinamen machte die Ausnahme wirkungslos — und
       niemand hätte es gemerkt, weil eine Ausnahme, die auf nichts passt,
       genauso aussieht wie eine, die greift. */
    for (const name of Object.keys(OHNE_SCHLOSS)) {
      expect(dateien, `Ausnahme für „${name}", aber diese Datei gibt es nicht. `
        + 'Entweder umbenannt oder gelöscht — weg damit.').toContain(name)
    }
  })
})
