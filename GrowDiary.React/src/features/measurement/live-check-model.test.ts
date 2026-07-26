import { describe, expect, it } from 'vitest'
import { checkDraft, summariseOk } from './live-check-model'
import type { MetricPayload } from '../../types'

function metric(key: string, targetMin: number | null, targetMax: number | null): MetricPayload {
  return { key, label: key, value: '–', unit: null, tone: 'default', hint: null, numericValue: null, targetMin, targetMax }
}

const TARGETS = [
  metric('reservoir-ph', 5.8, 6.2),
  metric('reservoir-ec', 1.5, 1.8),
  metric('dissolved-oxygen', 7, null),
  metric('reservoir-temp', 18, 21),
]

describe('checkDraft', () => {
  it('meldet einen Wert im Zielband als in Ordnung', () => {
    const [finding] = checkDraft({ reservoirPh: '6,0' }, TARGETS)
    expect(finding.severity).toBe('ok')
    expect(finding.delta).toBeNull()
  })

  it('versteht das Komma als Dezimaltrenner', () => {
    // Auf einer deutschen Tastatur tippt man 6,02.
    expect(checkDraft({ reservoirPh: '6,02' }, TARGETS)[0].severity).toBe('ok')
  })

  it('nennt einen knapp danebenliegenden Wert auffällig', () => {
    const [finding] = checkDraft({ reservoirPh: '6,3' }, TARGETS)
    expect(finding.severity).toBe('warn')
    expect(finding.delta).toBeCloseTo(0.1)
    expect(finding.text).toContain('über 6,2')
  })

  it('nennt einen weit entfernten Wert kritisch', () => {
    // Mehr als eine Zielbreite daneben ist nicht mehr „knapp".
    const [finding] = checkDraft({ reservoirPh: '7,5' }, TARGETS)
    expect(finding.severity).toBe('crit')
  })

  it('kommt mit einer reinen Untergrenze klar und schlägt die Prozedur vor', () => {
    const [finding] = checkDraft({ dissolvedOxygenMgL: '5,1' }, TARGETS)
    expect(finding.severity).toBe('crit')
    expect(finding.text).toContain('unter 7')
    expect(finding.hint).toContain('SOP-S1')
  })

  it('schweigt zu leeren Feldern', () => {
    expect(checkDraft({ reservoirPh: '', reservoirEc: '   ' }, TARGETS)).toEqual([])
  })

  it('schweigt zu Unsinn statt eine Zahl zu erfinden', () => {
    expect(checkDraft({ reservoirPh: 'abc' }, TARGETS)).toEqual([])
  })

  it('schweigt, wo es keinen Zielbereich gibt', () => {
    // Ohne laufenden Grow kennt niemand die Phase — dann gibt es nichts zu pruefen.
    expect(checkDraft({ reservoirPh: '6,0' }, [metric('reservoir-ph', null, null)])).toEqual([])
  })

  it('stellt nach oben, was Aufmerksamkeit braucht', () => {
    const findings = checkDraft(
      { reservoirPh: '6,0', reservoirEc: '1,9', dissolvedOxygenMgL: '5,1' },
      TARGETS,
    )
    expect(findings.map((f) => f.severity)).toEqual(['crit', 'warn', 'ok'])
  })
})

describe('summariseOk', () => {
  it('fasst mehrere gute Werte zu einer Zeile zusammen', () => {
    const findings = checkDraft({ reservoirPh: '6,0', reservoirEc: '1,6', reservoirWaterTempC: '19' }, TARGETS)
    // Alphabetisch, damit dieselben Werte immer dieselbe Zeile ergeben.
    expect(summariseOk(findings)).toBe('EC, pH und Wassertemperatur im Zielband')
  })

  it('bleibt bei einem einzelnen im Singular', () => {
    expect(summariseOk(checkDraft({ reservoirPh: '6,0' }, TARGETS))).toBe('pH im Zielband')
  })

  it('schweigt, wenn nichts in Ordnung ist', () => {
    expect(summariseOk(checkDraft({ reservoirPh: '7,5' }, TARGETS))).toBeNull()
  })
})
