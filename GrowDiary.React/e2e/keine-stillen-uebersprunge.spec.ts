import { expect, test } from '@playwright/test'
import { readFileSync, readdirSync } from 'node:fs'
import { darfUeberspringen, streng } from './pflicht'

/**
 * Kein Test darf sich still selbst überspringen.
 *
 * <b>Der Anlass.</b> Der Bericht sagte „275 passed" und niemand las die Zeile
 * darüber: „33 skipped". Von 34 Fällen aus vier Dateien liefen im Tor <b>drei</b>.
 * Übersprungen waren genau die, die etwas über die laufende App gesagt hätten —
 * darunter die Prüfung auf leere Seiten, die den Befund des Testers
 * („es fehlen wieder mock daten") gefunden hätte, wenn sie gelaufen wäre.
 *
 * <b>Die Grundmenge</b> sind die Dateien im Ordner, nicht eine Liste im Code.
 * Eine Liste könnte genau die Datei vergessen, die jemand neu anlegt.
 */

const ORDNER = new URL('.', import.meta.url)

/**
 * Dateien, die ein rohes <c>test.skip(</c> tragen dürfen — jede mit Grund.
 *
 * Die Namen sind aus dem Ordner abgeschrieben. Ein falsch getippter Name würde
 * hier stumm ins Leere greifen; dagegen prüft der Test darunter.
 */
const HILFSLAEUFE: Record<string, string> = {
  'collect-classes.spec.ts':
    'Kein Test, sondern ein Sammler: schreibt die verwendeten Klassennamen heraus. Läuft nur mit COLLECT_CLASSES=1 und soll sonst nichts tun.',
  'computed-snapshot.spec.ts':
    'Kein Test, sondern ein Abzug der gerechneten Stile für den Vorher-Nachher-Vergleich. Läuft nur mit SNAPSHOT=<name>.',
}

/**
 * Alle Prüfdateien — <b>ausser dieser hier</b>.
 *
 * Ein erster Anlauf hat sich selbst gemeldet: der Suchtext `test.skip(` steht
 * in dieser Datei, weil sie danach sucht. Das ist genau die Falle, gegen die
 * sie gebaut ist — `routes-reachable` liest `App.tsx` mit, wo die Routen
 * stehen, und eine erfundene Route belegt sich dadurch selbst.
 */
const EIGENE = 'keine-stillen-uebersprunge.spec.ts'

function specDateien(): string[] {
  return readdirSync(ORDNER).filter((n) => n.endsWith('.spec.ts') && n !== EIGENE)
}

function ohneKommentare(inhalt: string): string[] {
  return inhalt
    .split('\n')
    .map((z) => z.trim())
    .filter((z) => z.length > 0 && !z.startsWith('//') && !z.startsWith('*') && !z.startsWith('/*'))
}

test.describe('Keine stillen Übersprünge', () => {
  test('der Test sieht die Mappe überhaupt', () => {
    // Sonst läuft die Schleife null Mal und der Test ist grün — genau die
    // Falle, gegen die er gebaut ist.
    expect(specDateien().length).toBeGreaterThan(10)
  })

  test('jeder Hilfslauf in der Ausnahmeliste gibt es wirklich', () => {
    // Sonst schützt eine Ausnahme eine Datei, die es nicht gibt, während die
    // echte durchfällt. Genau so sind schon drei erfundene Kennungen in eine
    // Ausnahmeliste geraten.
    const vorhanden = specDateien()
    for (const name of Object.keys(HILFSLAEUFE)) {
      expect(vorhanden, `${name} steht in HILFSLAEUFE, aber nicht im Ordner.`).toContain(name)
    }
  })

  test('kein rohes test.skip ausserhalb der Hilfsläufe', () => {
    const treffer: string[] = []

    for (const name of specDateien()) {
      if (HILFSLAEUFE[name]) continue

      const zeilen = ohneKommentare(readFileSync(new URL(name, ORDNER), 'utf8'))
      const rohe = zeilen.filter((z) => z.includes('test.skip('))
      for (const z of rohe) treffer.push(`${name}: ${z.slice(0, 90)}`)
    }

    expect(treffer,
      'Diese Stellen überspringen sich still:\n' + treffer.join('\n')
      + '\n\nBenutze stattdessen darfUeberspringen(bedingung, grund) aus ./pflicht — dann wird der '
      + 'Übersprung im strengen Lauf (E2E_STRENG=1) zum Fehler statt zu einer Zeile, die niemand liest.')
      .toEqual([])
  })

  test('die Hilfe wird auch wirklich benutzt', () => {
    // Eine Regel, der niemand folgt, ist keine. Wenn kein Test darfUeberspringen
    // aufruft, prüft die Regel oben nur, dass niemand überspringt — und das
    // wäre auch wahr, wenn jemand die Prüfungen ganz gelöscht hätte.
    const nutzer = specDateien().filter((name) =>
      ohneKommentare(readFileSync(new URL(name, ORDNER), 'utf8'))
        .some((z) => z.includes('darfUeberspringen(')))

    expect(nutzer.length, 'Niemand benutzt darfUeberspringen — die Regel läuft ins Leere.')
      .toBeGreaterThan(0)
  })

  test('im strengen Lauf wirft die Hilfe, statt zu überspringen', () => {
    // <b>Der Beweis, dass sie beisst.</b> Ohne ihn wäre nicht zu unterscheiden,
    // ob E2E_STRENG wirkt oder ob die Variable nur herumliegt.
    if (!streng) {
      // Nicht streng: sie darf NICHT werfen. Das ist die andere Hälfte.
      expect(() => darfUeberspringen(false, 'trifft nicht zu')).not.toThrow()
      return
    }

    expect(() => darfUeberspringen(true, 'Probe'),
      'E2E_STRENG=1 ist gesetzt, aber darfUeberspringen wirft nicht — dann sind alle '
      + 'Übersprünge weiterhin still.')
      .toThrow(/strengen Lauf/)
  })
})
