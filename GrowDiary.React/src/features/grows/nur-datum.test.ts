import { describe, expect, it } from 'vitest'
import { nurDatum } from './nur-datum'

/**
 * Der Rundweg-Fehler aus dem Feld: Startdatum gesetzt, gespeichert, beim
 * Bearbeiten „weg". Die API liefert Datum MIT Uhrzeit, `input[type="date"]`
 * zeigt bei allem außer yyyy-MM-dd leer an. Diese Tests halten das Format an
 * beiden Enden fest.
 */
describe('nurDatum', () => {
  it('schneidet das API-Format auf das, was ein Datumsfeld anzeigen kann', () => {
    expect(nurDatum('2026-05-20T00:00:00')).toBe('2026-05-20')
    expect(nurDatum('2026-05-20T14:30:12.123Z')).toBe('2026-05-20')
    expect(nurDatum('2026-05-20')).toBe('2026-05-20')
  })

  it('macht aus Unlesbarem null statt eines kaputten Feldes', () => {
    expect(nurDatum(null)).toBeNull()
    expect(nurDatum(undefined)).toBeNull()
    expect(nurDatum('')).toBeNull()
    expect(nurDatum('kein Datum')).toBeNull()
  })
})
