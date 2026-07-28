import { describe, expect, it } from 'vitest'
import {
  addSection,
  addTile,
  encodeDropTarget,
  entityTile,
  parseDropTarget,
  layoutIsEmpty,
  metricTile,
  moveSection,
  moveTile,
  removeSection,
  removeTile,
  renameSection,
  resolveTile,
  type DashboardLayout,
  type EntityValue,
} from './dashboard-layout'
import type { MetricPayload } from '../../types'

function tile(id: string, metricKey: string) {
  return { id, kind: 'Metric' as const, metricKey, entityId: null, label: null, unit: null, span: 1 }
}

function layout(): DashboardLayout {
  return {
    tentId: 1,
    isCustom: true,
    sections: [
      { id: 'klima', title: 'Klima', tiles: [tile('a', 'temperature'), tile('b', 'humidity'), tile('c', 'vpd')] },
      { id: 'hydro', title: 'Nährlösung', tiles: [tile('d', 'reservoir-ph')] },
    ],
  }
}

const keys = (l: DashboardLayout, sectionId: string) =>
  l.sections.find((section) => section.id === sectionId)!.tiles.map((item) => item.id)

describe('Kacheln verschieben', () => {
  it('sortiert innerhalb eines Bereichs nach hinten korrekt', () => {
    // Der Fall, der beim Entfernen-und-Einsetzen um eins danebengeht.
    const next = moveTile(layout(), { sectionId: 'klima', index: 0 }, { sectionId: 'klima', index: 2 })

    expect(keys(next, 'klima')).toEqual(['b', 'a', 'c'])
  })

  it('sortiert innerhalb eines Bereichs nach vorn korrekt', () => {
    const next = moveTile(layout(), { sectionId: 'klima', index: 2 }, { sectionId: 'klima', index: 0 })

    expect(keys(next, 'klima')).toEqual(['c', 'a', 'b'])
  })

  it('an dieselbe Stelle ziehen ändert nichts', () => {
    const before = layout()
    const next = moveTile(before, { sectionId: 'klima', index: 1 }, { sectionId: 'klima', index: 1 })

    expect(keys(next, 'klima')).toEqual(keys(before, 'klima'))
  })

  it('verschiebt zwischen zwei Bereichen', () => {
    const next = moveTile(layout(), { sectionId: 'klima', index: 1 }, { sectionId: 'hydro', index: 0 })

    expect(keys(next, 'klima')).toEqual(['a', 'c'])
    expect(keys(next, 'hydro')).toEqual(['b', 'd'])
  })

  it('hängt hinten an, wenn das Ziel hinter dem Ende liegt', () => {
    const next = moveTile(layout(), { sectionId: 'klima', index: 0 }, { sectionId: 'hydro', index: 99 })

    expect(keys(next, 'hydro')).toEqual(['d', 'a'])
  })

  it('lässt ein unbekanntes Ziel unverändert', () => {
    const before = layout()
    const next = moveTile(before, { sectionId: 'gibtsnicht', index: 0 }, { sectionId: 'klima', index: 0 })

    expect(next).toBe(before)
  })
})

describe('Bereiche und Kacheln ändern', () => {
  it('fügt eine Kachel in den gewünschten Bereich ein', () => {
    const next = addTile(layout(), 'hydro', metricTile('orp'))

    expect(keys(next, 'hydro')).toHaveLength(2)
    expect(next.sections[1].tiles[1].metricKey).toBe('orp')
  })

  it('fällt auf den ersten Bereich zurück, wenn das Ziel nicht existiert', () => {
    const next = addTile(layout(), 'weg', metricTile('co2'))

    expect(keys(next, 'klima')).toHaveLength(4)
  })

  it('entfernt eine Kachel über alle Bereiche hinweg', () => {
    expect(keys(removeTile(layout(), 'd'), 'hydro')).toEqual([])
  })

  it('benennt einen Bereich um', () => {
    expect(renameSection(layout(), 'hydro', 'Technik').sections[1].title).toBe('Technik')
  })

  it('legt einen Bereich an und entfernt ihn wieder', () => {
    const grown = addSection(layout(), 'Technik')
    expect(grown.sections).toHaveLength(3)

    const shrunk = removeSection(grown, grown.sections[2].id)
    expect(shrunk.sections).toHaveLength(2)
  })

  it('verschiebt Bereiche und ignoriert Ziele ausserhalb', () => {
    expect(moveSection(layout(), 1, 0).sections.map((s) => s.id)).toEqual(['hydro', 'klima'])
    expect(moveSection(layout(), 0, -1).sections.map((s) => s.id)).toEqual(['klima', 'hydro'])
    expect(moveSection(layout(), 0, 9).sections.map((s) => s.id)).toEqual(['klima', 'hydro'])
  })

  it('erkennt ein Layout ohne jede Kachel', () => {
    // Der Server wirft ein leeres Layout weg — Speichern muss das vorher merken.
    expect(layoutIsEmpty(layout())).toBe(false)
    expect(layoutIsEmpty({ tentId: 1, isCustom: true, sections: [{ id: 'x', title: 'Leer', tiles: [] }] })).toBe(true)
  })
})

describe('Kachel auflösen', () => {
  const metric: MetricPayload = {
    key: 'temperature', label: 'Luft', value: '24,1', unit: '°C', tone: 'ok', hint: null,
    numericValue: 24.1, targetMin: 22, targetMax: 26,
  }
  const metricsByKey = new Map([['temperature', metric]])

  it('behält Zielbereich und Wert eigener Messwerte', () => {
    const resolved = resolveTile(tile('a', 'temperature'), metricsByKey, new Map())

    expect(resolved.numericValue).toBe(24.1)
    expect(resolved.targetMin).toBe(22)
  })

  it('zeigt einen fehlenden eigenen Messwert als Platzhalter statt zu verschwinden', () => {
    const resolved = resolveTile(tile('z', 'co2'), metricsByKey, new Map())

    expect(resolved.value).toBe('–')
    expect(resolved.numericValue).toBeNull()
  })

  it('zeigt fremde Entitäten so, wie sie sind — auch nicht-numerisch', () => {
    const values = new Map<string, EntityValue>([
      ['switch.eheim_uv', { entityId: 'switch.eheim_uv', friendlyName: 'EHEIM UV-Klärer', state: 'on', unit: null }],
    ])
    const resolved = resolveTile(entityTile('switch.eheim_uv', null, null), metricsByKey, values)

    expect(resolved.value).toBe('on')
    expect(resolved.label).toBe('EHEIM UV-Klärer')
    // Ohne Zielbereich keine erfundene Ampelfarbe.
    expect(resolved.targetMin).toBeNull()
  })

  it('leitet einen Namen aus der Entity-Id ab, wenn Home Assistant keinen liefert', () => {
    const resolved = resolveTile(entityTile('sensor.steckdose_leistung', null, null), metricsByKey, new Map())

    expect(resolved.label).toBe('Steckdose leistung')
    expect(resolved.value).toBe('–')
  })

  it('nimmt einen eigenen Namen vor allem anderen', () => {
    const values = new Map<string, EntityValue>([
      ['sensor.x', { entityId: 'sensor.x', friendlyName: 'Von HA', state: '5', unit: 'W' }],
    ])
    const resolved = resolveTile(entityTile('sensor.x', 'Mein Name', null), metricsByKey, values)

    expect(resolved.label).toBe('Mein Name')
    expect(resolved.unit).toBe('W')
    expect(resolved.numericValue).toBe(5)
  })
})

describe('Ziel beim Ziehen mit dem Finger', () => {
  it('kodiert und liest ein Ziel zurück', () => {
    const kodiert = encodeDropTarget('klima', 2)

    expect(parseDropTarget(kodiert)).toEqual({ sectionId: 'klima', index: 2 })
  })

  it('verträgt ein Trennzeichen in der Bereichs-Kennung', () => {
    // Eigene Bereiche bekommen ihren Namen vom Nutzer — dort darf alles stehen.
    expect(parseDropTarget(encodeDropTarget('mein|technik', 0)))
      .toEqual({ sectionId: 'mein|technik', index: 0 })
  })

  it('gibt bei Unsinn nichts zurück', () => {
    // Unter dem Finger liegt oft irgendein Element ohne Ziel — das darf keine
    // Kachel an Position NaN schieben.
    expect(parseDropTarget(null)).toBeNull()
    expect(parseDropTarget('')).toBeNull()
    expect(parseDropTarget('klima')).toBeNull()
    expect(parseDropTarget('|klima')).toBeNull()
    expect(parseDropTarget('x|klima')).toBeNull()
    expect(parseDropTarget('2|')).toBeNull()
    expect(parseDropTarget('-1|klima')).toBeNull()
  })
})
