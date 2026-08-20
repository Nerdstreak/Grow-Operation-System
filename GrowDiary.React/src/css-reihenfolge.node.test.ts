import { readFileSync, readdirSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

/**
 * Eine Regel in `@media` oder `@container` darf nicht von einer späteren
 * Grundregel überstimmt werden.
 *
 * <b>Der Anlass — zweimal in zwei Tagen.</b>
 * <ul>
 *   <li>19.08.: `.rc2-sticky-card` aus `widgets.css` schlug
 *       `.rc2-measurement-context` aus `measurement.css`. Gleiche Spezifität,
 *       spätere Datei gewinnt — die Korrektur lag wochenlang wirkungslos im
 *       Stylesheet, und der Nutzer schickte ein Bild von der Überlappung.</li>
 *   <li>20.08.: `.hw-actions { flex-wrap: wrap }` stand ZWEI ZEILEN unter
 *       `@container (min-width: 761px) { .hw-actions { flex-wrap: nowrap } }`
 *       und gewann. Eine Container-Abfrage erhöht die Spezifität <b>nicht</b>.
 *       Am Schreibtisch stapelten sich dadurch drei Knöpfe senkrecht, die
 *       Zeile wurde dreimal so hoch — „das ist verzogen".</li>
 * </ul>
 *
 * <b>Was hier geprüft wird.</b> Je Datei: steht ein Selektor mit einer
 * Eigenschaft in einem Bedingungsblock, und <i>danach</i> derselbe Selektor
 * mit derselben Eigenschaft ohne Bedingung — dann gewinnt die zweite immer,
 * und die Bedingung war umsonst.
 *
 * <b>Was hier NICHT geprüft wird.</b> Dateiübergreifend (der Fall vom 19.08.):
 * dafür müsste die Ladereihenfolge aus `App.tsx` mitgelesen und über alle
 * Dateien hinweg verglichen werden. Der häufigere und heimtückischere Fall ist
 * der innerhalb einer Datei — dort stehen die zwei Regeln oft direkt
 * untereinander und sehen aus, als gehörten sie zusammen.
 */
describe('CSS-Reihenfolge', () => {
  const WURZEL = new URL('./', import.meta.url)

  /** Alle .css unterhalb von src, rekursiv. */
  function alleStile(ordner = WURZEL, pfad = ''): string[] {
    const raus: string[] = []
    for (const eintrag of readdirSync(ordner, { withFileTypes: true })) {
      if (eintrag.isDirectory()) {
        raus.push(...alleStile(new URL(eintrag.name + '/', ordner), pfad + eintrag.name + '/'))
      } else if (eintrag.name.endsWith('.css')) {
        raus.push(pfad + eintrag.name)
      }
    }
    return raus
  }

  /**
   * Regeln einer Datei einsammeln — je Eintrag Selektor, Eigenschaft,
   * Position und ob sie in einem Bedingungsblock steht.
   *
   * <b>Kein CSS-Zerteiler.</b> Das Projekt hat keinen als Abhängigkeit, und
   * einen zu bauen wäre mehr Fehlerquelle als Nutzen. Diese Klammerzählung
   * reicht für das, was gesucht wird: sie muss nur wissen, ob sie sich
   * gerade innerhalb eines `@`-Blocks befindet.
   */
  function regeln(inhalt: string) {
    const raus: { selektor: string; eigenschaft: string; zeile: number; bedingt: boolean; bedingung: string }[] = []
    const zeilen = inhalt.split('\n')

    // Ein Stapel der offenen Blöcke: '@' für Bedingungsblöcke, sonst der Selektor.
    const stapel: string[] = []
    let selektor = ''

    zeilen.forEach((roh, index) => {
      const zeile = roh.trim()
      if (zeile.startsWith('/*') || zeile.startsWith('*') || zeile.startsWith('//')) return

      // Eine Zeile kann öffnen, schliessen und Deklarationen tragen.
      const auf = zeile.includes('{')
      const zu = zeile.includes('}')

      if (auf) {
        const kopf = zeile.slice(0, zeile.indexOf('{')).trim()
        if (kopf.startsWith('@')) {
          stapel.push('@' + kopf)
        } else {
          stapel.push('sel')
          selektor = kopf
        }
      }

      // Deklarationen dieser Zeile — nur, wenn wir in einem Selektorblock sind.
      if (stapel[stapel.length - 1] === 'sel' || (auf && !zeile.slice(0, zeile.indexOf('{')).trim().startsWith('@'))) {
        const rumpf = auf ? zeile.slice(zeile.indexOf('{') + 1) : zeile
        for (const teil of rumpf.split(';')) {
          const doppelpunkt = teil.indexOf(':')
          if (doppelpunkt < 1) continue
          const eigenschaft = teil.slice(0, doppelpunkt).trim().replace('}', '').trim()
          if (!/^[a-z-]+$/.test(eigenschaft)) continue
          const bedingung = stapel.find((e) => e.startsWith('@')) ?? ''
          raus.push({ selektor, eigenschaft, zeile: index + 1, bedingt: bedingung !== '', bedingung })
        }
      }

      if (zu) {
        for (let i = 0; i < (zeile.match(/\}/g)?.length ?? 0); i++) stapel.pop()
      }
    })

    return raus
  }

  it('findet die Stildateien überhaupt', () => {
    // Sonst läuft die Prüfung darunter null Mal und ist grün — die Falle, in
    // die in diesem Projekt schon mehrere Zählungen gelaufen sind.
    const dateien = alleStile()
    expect(dateien.length, 'Keine .css unter src/ gefunden.').toBeGreaterThan(10)
  })

  it('erkennt eine überstimmte Bedingung', () => {
    // <b>Der Beweis, dass sie beisst.</b> Genau der Fehler vom 20.08., als
    // Text — ohne ihn wäre nicht zu unterscheiden, ob nichts gefunden wurde
    // oder ob die Suche ins Leere greift.
    const probe = [
      '@container (min-width: 761px) {',
      '  .hw-actions { flex-wrap: nowrap; }',
      '}',
      '.hw-actions { display: flex; flex-wrap: wrap; gap: 6px; }',
    ].join('\n')

    const gefunden = regeln(probe)
    const bedingt = gefunden.filter((r) => r.bedingt && r.eigenschaft === 'flex-wrap')
    const spaeter = gefunden.filter((r) => !r.bedingt && r.eigenschaft === 'flex-wrap')

    expect(bedingt.length, 'Die bedingte Regel wurde nicht erkannt.').toBe(1)
    expect(spaeter.length, 'Die spätere Grundregel wurde nicht erkannt.').toBe(1)
    expect(spaeter[0].zeile).toBeGreaterThan(bedingt[0].zeile)
  })

  it('keine Bedingung wird von einer späteren Grundregel überstimmt', () => {
    const befunde: string[] = []

    for (const datei of alleStile()) {
      const gefunden = regeln(readFileSync(new URL(datei, WURZEL), 'utf8'))

      for (const bedingt of gefunden.filter((r) => r.bedingt)) {
        const ueberstimmt = gefunden.find((r) =>
          !r.bedingt
          && r.selektor === bedingt.selektor
          && r.eigenschaft === bedingt.eigenschaft
          && r.zeile > bedingt.zeile)

        if (ueberstimmt) {
          befunde.push(
            `${datei}:${bedingt.zeile} — "${bedingt.selektor} { ${bedingt.eigenschaft} }" in `
            + `${bedingt.bedingung.slice(0, 40)} wird in Zeile ${ueberstimmt.zeile} von derselben `
            + 'Regel ohne Bedingung überstimmt.')
        }
      }
    }

    expect(befunde,
      'Diese Bedingungen laufen ins Leere:\n' + befunde.join('\n')
      + '\n\nEine @media- oder @container-Abfrage erhöht die Spezifität NICHT. Steht dieselbe '
      + 'Eigenschaft danach ohne Bedingung, gewinnt sie immer. Die Grundform gehört VOR den '
      + 'Bedingungsblock.')
      .toEqual([])
  })
})
