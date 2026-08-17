import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

/**
 * Ein Zahlenfeld, in dem keine Zahl steht, darf nicht stillschweigend leer werden.
 *
 * <b>Der Fehler.</b> `parseNullableNumber` gibt für „leer" und für „unlesbar"
 * dasselbe zurück: `null`. Wer sich beim pH vertippt und „6,2x" stehen lässt,
 * speicherte eine Messung ohne pH — und bekam eine Erfolgsmeldung. Der Wert war
 * weg, niemand hat es gesagt, und beim nächsten Blick auf die Kurve fehlt
 * einfach ein Punkt. 21 Felder waren betroffen.
 *
 * Geprüft wird am Quelltext, weil die Seite ohne Backend nicht abschickt: dass
 * die Prüfung existiert, dass sie vor dem Speichern läuft, und dass ihre
 * Feldliste vollständig ist. Der letzte Punkt ist der wichtige — ein Feld, das
 * in der Liste fehlt, verliert seinen Inhalt weiterhin lautlos.
 */
describe('Messungsformular: unlesbare Zahlen', () => {
  const quelle = readFileSync(fileURLToPath(new URL('./ManualMeasurementPage.tsx', import.meta.url)), 'utf8')

  it('prüft vor dem Speichern auf unlesbare Felder', () => {
    const save = quelle.slice(quelle.indexOf('async function save('))
    const pruefung = save.indexOf('unlesbareFelder(draft)')
    const senden = save.indexOf('toPayload(draft)')

    expect(pruefung, 'save() prüft gar nicht auf unlesbare Felder').toBeGreaterThan(-1)
    expect(pruefung, 'die Prüfung steht NACH dem Absenden — dann ist der Wert schon weg')
      .toBeLessThan(senden)
  })

  it('kennt jedes Zahlenfeld, das ins Nichts laufen könnte', () => {
    // Jedes Feld, das durch parseNullableNumber geht, muss in ZAHLENFELDER
    // stehen — sonst wird genau dort weiter stillschweigend verworfen.
    const durchDenParser = [...quelle.matchAll(/parseNullableNumber\(draft\.(\w+)\)/g)].map((m) => m[1])
    const liste = quelle.slice(quelle.indexOf('const ZAHLENFELDER'), quelle.indexOf('function unlesbareFelder'))
    const fehlend = [...new Set(durchDenParser)].filter((feld) => !liste.includes(`'${feld}'`))

    expect(fehlend, 'Diese Felder werden geparst, stehen aber nicht in ZAHLENFELDER:\n' + fehlend.join('\n'))
      .toEqual([])
  })

  it('nennt das Feld beim Namen, nicht beim Schlüssel', () => {
    // „pH (Reservoir)" hilft, „reservoirPh" nicht.
    const liste = quelle.slice(quelle.indexOf('const ZAHLENFELDER'), quelle.indexOf('function unlesbareFelder'))
    expect(liste).toContain("'pH (Reservoir)'")
    expect(liste).toContain("'Luftfeuchte'")
  })
})
