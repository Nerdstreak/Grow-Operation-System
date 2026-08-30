/* src/release-notes-deutsch.node.test.ts
   Die Release Notes stehen auf Deutsch.

   <b>Der Anlass (28.08.2026).</b> Gemeldet: „einmal müssen die Release Notes
   auf Deutsch sein weil das unsere Hauptsprache ist." Der Changelog des
   Add-ons war bis dahin durchgehend englisch — und das ist der Text, den Home
   Assistant beim Update anzeigt.

   <b>Warum eine Prüfung und nicht nur eine Regel.</b> „Alles auf Deutsch"
   steht seit Langem in CLAUDE.md, und trotzdem sind 114 Versionen auf Englisch
   entstanden. Eine Regel, die niemand misst, wird beim nächsten Release wieder
   gebrochen.

   <b>Was gemessen wird.</b> Nur der NEUESTE Eintrag. Die älteren sind
   Geschichte; sie nachzuübersetzen ändert nichts an dem, was jemand liest,
   wenn er aktualisiert.
*/

import { readFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const WURZEL = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..')

/**
 * Wörter, die es im Deutschen so nicht gibt und die in Fließtext auffallen.
 *
 * Bewusst kurz und eindeutig: „New", „Fixed", „Changed" sind die Marker der
 * Einträge, der Rest sind Allerweltswörter, die in keinem deutschen Satz
 * stehen. Ein Wort wie „Server" oder „Update" fehlt hier mit Absicht — das
 * sagt man auf Deutsch auch.
 */
const ENGLISCH = [
  'New —', 'Fixed —', 'Changed —', 'Removed —',
  ' the ', ' and ', ' with ', ' that ', ' this ', ' from ', ' were ',
  ' does ', ' could ', ' would ', ' every ', ' each ', ' which ',
  /* Das englische „was" fehlt mit Absicht: dieselbe Buchstabenfolge ist ein
     deutsches Wort („und sagt was", „was sich ändert"). Die erste Fassung
     meldete es zweimal im übersetzten Text — ein Fehlalarm, der die Prüfung
     binnen einer Woche übergangen hätte. */
]

/** Der neueste Eintrag: von der ersten `## `-Zeile bis zur zweiten. */
function neuesterEintrag(text: string): { version: string; inhalt: string } {
  const zeilen = text.split('\n')
  const erste = zeilen.findIndex((z) => z.startsWith('## '))
  if (erste < 0) return { version: '(keine)', inhalt: '' }
  const zweite = zeilen.findIndex((z, i) => i > erste && z.startsWith('## '))
  return {
    version: zeilen[erste].replace(/^##\s*/, '').trim(),
    inhalt: zeilen.slice(erste + 1, zweite < 0 ? undefined : zweite).join('\n'),
  }
}

describe('Release Notes', () => {
  const text = readFileSync(join(WURZEL, 'grow-os', 'CHANGELOG.md'), 'utf8')
  const { version, inhalt } = neuesterEintrag(text)

  it('sieht ueberhaupt einen Eintrag', () => {
    /* Mengenwaechter: ohne Inhalt liefe die Suche null Mal durch und waere
       gruen. Genau so war die erste Fassung einer aehnlichen Zaehlung in
       diesem Projekt blind. */
    expect(version, 'Kein Versionseintrag im Changelog gefunden.').not.toBe('(keine)')
    expect(inhalt.length, `Der Eintrag „${version}" ist leer.`).toBeGreaterThan(200)
  })

  it('der neueste Eintrag steht auf Deutsch', () => {
    const gefunden = ENGLISCH
      .map((wort) => ({ wort, treffer: inhalt.split(wort).length - 1 }))
      .filter((x) => x.treffer > 0)

    expect(gefunden.map((x) => `${x.wort.trim()} (${x.treffer}×)`),
      `Der Eintrag „${version}" enthält englische Wendungen. Home Assistant zeigt `
      + 'genau diesen Text beim Update — und Deutsch ist die Sprache dieses '
      + 'Projekts.').toEqual([])
  })

  it('nennt den Grund, warum die alten Eintraege englisch bleiben', () => {
    /* Ohne den Hinweis sieht der Changelog nach einem halben Umbau aus. Mit
       ihm ist es eine Entscheidung, die jemand nachlesen kann. */
    const kopf = text.slice(0, text.indexOf('## '))
    expect(kopf.toLowerCase(), 'Der Changelog wechselt mitten im Dokument die Sprache, '
      + 'ohne das zu erklären.').toContain('deutsch')
  })
})
