import { describe, expect, it } from 'vitest'
import { parsePlantWeights, progressLabel, serialisePlantWeights, totals } from './plant-weights-model'

describe('parsePlantWeights', () => {
  it('legt für jede Pflanze eine Zeile an, wenn noch nichts gewogen ist', () => {
    const rows = parsePlantWeights(null, 3)
    expect(rows.map((row) => row.label)).toEqual(['PL-01', 'PL-02', 'PL-03'])
    expect(rows.every((row) => row.wetG === null)).toBe(true)
  })

  it('liest gespeicherte Gewichte zurück', () => {
    const rows = parsePlantWeights('[{"label":"A","wetG":486,"dryG":null}]', 3)
    expect(rows).toEqual([{ label: 'A', wetG: 486, dryG: null }])
  })

  it('lässt sich von kaputtem JSON nicht aufhalten', () => {
    // Eine unlesbare Spalte darf die Erntemaske nicht blockieren.
    expect(parsePlantWeights('{kaputt', 2)).toHaveLength(2)
  })

  it('ignoriert Unsinn in einzelnen Feldern', () => {
    const rows = parsePlantWeights('[{"label":"A","wetG":"viel"}]', 1)
    expect(rows[0].wetG).toBeNull()
  })
})

describe('serialisePlantWeights', () => {
  it('speichert nur, was gewogen wurde', () => {
    const json = serialisePlantWeights([
      { label: 'PL-01', wetG: 486, dryG: null },
      { label: 'PL-02', wetG: null, dryG: null },
    ])
    expect(json).toBe('[{"label":"PL-01","wetG":486,"dryG":null}]')
  })

  it('speichert gar nichts, wenn nichts gewogen wurde', () => {
    expect(serialisePlantWeights([{ label: 'PL-01', wetG: null, dryG: null }])).toBeNull()
  })
})

describe('totals', () => {
  const rows = [
    { label: 'PL-01', wetG: 486, dryG: null },
    { label: 'PL-02', wetG: 512, dryG: null },
    { label: 'PL-03', wetG: null, dryG: null },
  ]

  it('summiert das Nassgewicht', () => {
    expect(totals(rows).wetG).toBe(998)
  })

  it('schätzt das Trockengewicht, solange noch nicht trocken gewogen ist', () => {
    expect(totals(rows).expectedDryG).toBe(219.6)
  })

  it('zählt die gewogenen Pflanzen', () => {
    expect(progressLabel(totals(rows))).toBe('2/3 Pflanzen')
  })

  it('lässt die Schätzung weg, sobald alles trocken gewogen ist', () => {
    // Danach steht die echte Zahl da — eine Schätzung daneben wäre nur Lärm.
    const complete = [
      { label: 'PL-01', wetG: 486, dryG: 108 },
      { label: 'PL-02', wetG: 512, dryG: 113 },
    ]
    const result = totals(complete)
    expect(result.dryG).toBe(221)
    expect(result.expectedDryG).toBeNull()
  })

  it('schätzt weiter, solange nur ein Teil trocken gewogen ist', () => {
    const partial = [
      { label: 'PL-01', wetG: 486, dryG: 108 },
      { label: 'PL-02', wetG: 512, dryG: null },
    ]
    expect(totals(partial).expectedDryG).not.toBeNull()
  })

  it('summiert nichts, wo nichts steht', () => {
    const empty = totals([{ label: 'PL-01', wetG: null, dryG: null }])
    expect(empty.wetG).toBeNull()
    expect(empty.dryG).toBeNull()
    expect(empty.expectedDryG).toBeNull()
  })
})
