import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync } from 'node:fs'

/**
 * Jede Seite im Menü kommt in der Referenz vor — und jede Referenz-Seite hat
 * ihre sechs Abschnitte.
 *
 * **Der Anlass.** `docs/architecture.md` ist vom 18.05.2026 und führt `/live`,
 * `/action` und `/analyse` als Oberflächen, die es seit Monaten nur noch als
 * Weiterleitung gibt. Niemand hat es gemerkt, weil nichts es geprüft hat. Die
 * neue Mappe `docs/referenz/` würde denselben Weg gehen: eine Seite dazubauen
 * kostet Minuten, die Referenz mitzuziehen vergisst man.
 *
 * Diese Zählung geht über die **Grundmenge** — `navigation.ts`, also das, was
 * der Nutzer sieht — und verlangt für jeden Eintrag entweder eine Fundstelle
 * in der Referenz oder eine Ausnahme mit ausgeschriebenem Grund.
 */

const REFERENZ = new URL('../../docs/referenz/', import.meta.url)
const NAVIGATION = new URL('./navigation.ts', import.meta.url)

/** Die sechs Abschnitte, auf die sich alle zehn Seiten geeinigt haben. */
const ABSCHNITTE = [
  '## Wo in der App',
  '## Was es tut',
  '## Die Zahlen und woher sie kommen',
  '## Was es bewusst NICHT tut',
  '## Im Code',
]

/**
 * Menüpunkte ohne eigenen Referenz-Eintrag — jeder mit Grund.
 *
 * Die Routen sind aus `navigation.ts` abgeschrieben; ein Tippfehler machte die
 * Ausnahme wirkungslos, dagegen prüft der Test darunter.
 */
const OHNE_REFERENZ: Record<string, string> = {
  '/handy': 'Eine Anleitung mit QR-Code, keine Funktion mit Regeln oder Zahlen. '
    + 'Was dort steht, steht auf der Seite selbst — eine Referenz dafür wäre eine Kopie.',
}

function referenzSeiten(): { name: string; inhalt: string }[] {
  return readdirSync(REFERENZ)
    .filter((n) => n.endsWith('.md') && n !== 'README.md')
    .map((name) => ({ name, inhalt: readFileSync(new URL(name, REFERENZ), 'utf8') }))
}

/** Alle Routen aus dem Menü — die Grundmenge. */
function menueRouten(): string[] {
  const quelle = readFileSync(NAVIGATION, 'utf8')
  return [...quelle.matchAll(/to:\s*'([^']+)'/g)].map((treffer) => treffer[1])
}

describe('Referenz-Mappe', () => {
  it('sieht ihre Grundmenge überhaupt', () => {
    // Ohne diesen Wächter läuft die Zählung bei einem verschobenen Ordner
    // null Mal durch und ist grün — der Fehler, gegen den CLAUDE.md eine
    // eigene Regel hat.
    expect(referenzSeiten().length,
      'Keine Referenz-Seite gefunden — der Pfad stimmt nicht, und alles darunter '
      + 'wäre grundlos grün.').toBeGreaterThanOrEqual(10)

    expect(menueRouten().length,
      'Keine Route in navigation.ts gefunden — die Grundmenge ist leer.')
      .toBeGreaterThan(15)
  })

  it('jede ausgenommene Route gibt es wirklich im Menü', () => {
    const vorhanden = menueRouten()
    for (const route of Object.keys(OHNE_REFERENZ)) {
      expect(vorhanden,
        `${route} steht in OHNE_REFERENZ, aber nicht im Menü — die Ausnahme schützt `
        + 'eine Seite, die es nicht gibt, während die echte durchfällt.')
        .toContain(route)
    }
  })

  it('jede Seite im Menü kommt in der Referenz vor oder hat einen Grund', () => {
    const seiten = referenzSeiten()
    const fehlend: string[] = []

    for (const route of menueRouten()) {
      if (OHNE_REFERENZ[route]) continue
      // In Backticks, damit ein zufälliger Fließtext-Treffer nicht zählt:
      // eine Erwähnung ist keine Verwendung.
      const gesucht = '`' + route + '`'
      if (!seiten.some((s) => s.inhalt.includes(gesucht))) fehlend.push(route)
    }

    expect(fehlend,
      'Diese Seiten gibt es in der App, aber in keiner Referenz-Seite:\n'
      + fehlend.join('\n')
      + '\n\nEntweder in die passende Seite unter docs/referenz/ aufnehmen — oder mit '
      + 'ausgeschriebenem Grund in OHNE_REFERENZ eintragen. Eine Doku, die eine halbe '
      + 'App beschreibt, kostet beim Nachschlagen mehr Zeit als sie spart.')
      .toEqual([])
  })

  it('jede Referenz-Seite trägt ihre sechs Abschnitte', () => {
    const luecken: string[] = []

    for (const { name, inhalt } of referenzSeiten()) {
      for (const abschnitt of ABSCHNITTE) {
        if (!inhalt.includes(abschnitt)) luecken.push(`${name}: ${abschnitt}`)
      }
    }

    expect(luecken,
      'Diese Abschnitte fehlen:\n' + luecken.join('\n')
      + '\n\nBesonders „Was es bewusst NICHT tut" — das ist der Abschnitt, wegen dem '
      + 'die Mappe gebaut wurde. Er beantwortet „kann die App auch …", ohne dass '
      + 'jemand raten muss.')
      .toEqual([])
  })

  it('das Register nennt jede Seite', () => {
    // Sonst gibt es eine Seite, die niemand findet — genau der Fehler, den die
    // Einkaufsliste schon einmal hatte (Visual Audit beta.42).
    const register = readFileSync(new URL('README.md', REFERENZ), 'utf8')
    const ungenannt = referenzSeiten()
      .map((s) => s.name)
      .filter((name) => !register.includes(name))

    expect(ungenannt,
      'Diese Seiten stehen in keinem Register — sie sind nur da, wer sie kennt:\n'
      + ungenannt.join('\n')).toEqual([])
  })
})
