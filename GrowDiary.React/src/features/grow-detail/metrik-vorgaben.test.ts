import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { autoMeasurementFields, defaultMetricKeyByField } from './grow-detail-model'

/**
 * Jede vorgeschlagene Metrik gibt es auch wirklich.
 *
 * **Der Anlass (02.09.2026).** Wer eine Automessung einrichtet, wählt ein Feld
 * („Reservoir-pH") und bekommt eine Metrik vorgeschlagen (`reservoir-ph`). Die
 * Vorschläge stehen in `defaultMetricKeyByField` — im **Frontend**, als
 * Zeichenketten. Welche Metriken es gibt, weiss das **Backend**.
 *
 * Ein Tippfehler in einem Vorschlag (`reservoir_ph` statt `reservoir-ph`) fällt
 * niemandem auf: das Feld ist ausgefüllt, die Seite sieht richtig aus, und die
 * Automessung schreibt danach nichts. Der Nutzer sucht den Fehler in Home
 * Assistant.
 *
 * **TypeScript hilft hier nur halb.** `Record<AutoMeasurementField, string>`
 * erzwingt, dass jedes Feld einen Eintrag hat — nicht, dass der Wert etwas
 * bedeutet. Und `autoMeasurementFields: AutoMeasurementField[]` erzwingt gar
 * nichts: eine Liste darf Werte auslassen, und dann fehlt das Feld im
 * Auswahlfeld.
 */

const BACKEND = new URL('../../../../GrowDiary.Web/Models/DashboardLayout.cs', import.meta.url)
const TYPEN = new URL('../../types/shared.ts', import.meta.url)

/** Die Metriken, die das Backend in seinem Standard-Dashboard aufführt. */
function metrikenDesBackends(): string[] {
  const quelle = readFileSync(BACKEND, 'utf8')
  const raus = new Set<string>()

  for (const aufruf of quelle.matchAll(/Metrics\(([^)]*)\)/g)) {
    for (const treffer of aufruf[1].matchAll(/"([a-z0-9-]+)"/g)) {
      raus.add(treffer[1])
    }
  }

  return [...raus]
}

/** Die Werte eines Vereinigungstyps aus `types/shared.ts`. */
function werteVon(typ: string): string[] {
  const quelle = readFileSync(TYPEN, 'utf8')
  const block = new RegExp(`export type ${typ} =([\\s\\S]*?)(?:\\n\\n|export )`).exec(quelle)
  if (!block) return []
  return [...block[1].matchAll(/'([^']+)'/g)].map((m) => m[1])
}

describe('Die vorgeschlagenen Metriken', () => {
  it('werden überhaupt gefunden', () => {
    // Mengenwächter: ohne Grundmenge liefe die Prüfung darunter null Mal durch
    // und wäre grün, egal was drinsteht.
    expect(
      metrikenDesBackends().length,
      'Im Backend wurden keine Metriken gefunden — heisst der Aufruf nicht mehr `Metrics(...)`, '
      + 'oder liegt DashboardLayout.cs woanders? Dann prüft diese Datei nichts.',
    ).toBeGreaterThanOrEqual(10)
  })

  it('gibt es alle im Backend', () => {
    const bekannt = metrikenDesBackends()
    const erfunden = Object.entries(defaultMetricKeyByField)
      .filter(([, metrik]) => !bekannt.includes(metrik))
      .map(([feld, metrik]) => `${feld} → "${metrik}"`)

    expect(
      erfunden,
      'Diese Vorschläge zeigen auf eine Metrik, die es im Backend nicht gibt:\n  '
      + erfunden.join('\n  ')
      + '\n\nDas Feld ist ausgefüllt, die Seite sieht richtig aus, und die Automessung schreibt '
      + `danach nichts. Bekannt sind: ${bekannt.join(', ')}.`,
    ).toEqual([])
  })
})

describe('Die angebotenen Felder', () => {
  it('sind vollständig', () => {
    // Ein fehlendes Feld kann man nicht auswaehlen — ein stiller
    // Funktionsverlust, genau wie bei PHASEN und FOTO_TAGS.
    const ausTyp = werteVon('AutoMeasurementField')

    expect(ausTyp.length, 'AutoMeasurementField wurde in types/shared.ts nicht gefunden.')
      .toBeGreaterThanOrEqual(10)
    expect(
      [...autoMeasurementFields].sort(),
      'Die Liste im Auswahlfeld weicht vom Typ ab. Was fehlt, kann der Nutzer nicht auswählen; '
      + 'was zu viel ist, nimmt das Backend nicht an.',
    ).toEqual([...ausTyp].sort())
  })

  it('haben alle einen Vorschlag', () => {
    // Record<AutoMeasurementField, string> erzwingt das zwar — aber nur, solange
    // der Typ stimmt. Hier steht es als Zusage, nicht als Nebenwirkung.
    const ohne = autoMeasurementFields.filter((feld) => !defaultMetricKeyByField[feld])

    expect(ohne, `Ohne vorgeschlagene Metrik: ${ohne.join(', ')}`).toEqual([])
  })
})
