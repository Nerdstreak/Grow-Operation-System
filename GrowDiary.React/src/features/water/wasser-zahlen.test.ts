import { describe, expect, it } from 'vitest'
import { textZuZahl, zahlZuText } from './wasser-zahlen'

/**
 * Der Fehler, den diese Tests festnageln: 1234 µS/cm wurde als „1.234"
 * angezeigt und beim Speichern als 1,234 gelesen — ein Tausendstel.
 * Hartes Wasser gibt es wirklich; der Rundweg muss verlustfrei sein.
 */
describe('wasser-zahlen', () => {
  it('überlebt den Rundweg für jeden realistischen Berichtswert', () => {
    for (const wert of [0, 5.6, 7.2, 12, 276, 999, 1000, 1234, 1234.5, 2500]) {
      expect(textZuZahl(zahlZuText(wert))).toBe(wert)
    }
  })

  it('schreibt große Werte ohne Tausenderpunkt', () => {
    expect(zahlZuText(1234)).toBe('1234')
    expect(zahlZuText(5.6)).toBe('5,6')
    expect(zahlZuText(null)).toBe('')
  })

  it('liest deutsche und technische Schreibweisen gleich richtig', () => {
    expect(textZuZahl('5,6')).toBe(5.6)
    expect(textZuZahl('5.6')).toBe(5.6) // Messgeraete-Anzeige mit Punkt
    expect(textZuZahl('1.234')).toBe(1234) // deutsch gruppiert
    expect(textZuZahl('1.234,5')).toBe(1234.5)
    expect(textZuZahl('12.345.678')).toBe(12_345_678)
    expect(textZuZahl('')).toBeNull()
    expect(textZuZahl('abc')).toBeNull()
  })
})
