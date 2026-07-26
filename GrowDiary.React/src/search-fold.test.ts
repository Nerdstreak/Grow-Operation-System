import { describe, expect, it } from 'vitest'
import { matchesSearchTerm } from './search-fold'

describe('matchesSearchTerm', () => {
  it('findet die Beschriftung so, wie sie geschrieben steht', () => {
    expect(matchesSearchTerm('Zelte & Räume', 'räume')).toBe(true)
  })

  it('findet sie auch ohne Umlaut getippt', () => {
    expect(matchesSearchTerm('Zelte & Räume', 'raume')).toBe(true)
  })

  it('findet sie auch in der ae-Schreibweise', () => {
    expect(matchesSearchTerm('Zelte & Räume', 'raeume')).toBe(true)
  })

  it('findet einen ae-Begriff, wenn der Nutzer den Umlaut tippt', () => {
    expect(matchesSearchTerm('sensoren geraete kalibrierung', 'geräte')).toBe(true)
  })

  it('ignoriert Gross- und Kleinschreibung', () => {
    expect(matchesSearchTerm('Home Assistant', 'ASSIST')).toBe(true)
  })

  it('faltet auch das scharfe s', () => {
    expect(matchesSearchTerm('Grenzwerte & Schlüssel', 'schluessel')).toBe(true)
    expect(matchesSearchTerm('Aussengrenze', 'außen')).toBe(true)
  })

  it('trifft nicht, was nicht vorkommt', () => {
    expect(matchesSearchTerm('Zelte & Räume', 'ernte')).toBe(false)
  })

  it('trifft bei leerer Eingabe nichts', () => {
    expect(matchesSearchTerm('Zelte & Räume', '   ')).toBe(false)
  })
})
