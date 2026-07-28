import { describe, expect, it } from 'vitest'
import { MAX_CALIBRATION_SECONDS, runSecondsForPump, secondsForTarget, targetForPump } from './calibration'

describe('Kalibrieren auf Zielmenge', () => {
  it('rechnet die Zielmenge in Laufzeit um', () => {
    // 100 ml bei 46 ml/min sind 130,4 s.
    expect(secondsForTarget(100, 46)).toBeCloseTo(130.4, 1)
  })

  it('nennt ohne Fördermenge keine Zeit', () => {
    // Beim allerersten Mal weiss niemand, wie lange 100 ml dauern.
    expect(secondsForTarget(100, null)).toBeNull()
    expect(secondsForTarget(100, 0)).toBeNull()
  })

  it('nimmt bei einer normalen Pumpe die grosse Menge', () => {
    expect(targetForPump(46)).toBe(100)
  })

  it('wird kleiner, wenn 100 ml nicht in die erlaubte Zeit passen', () => {
    // 15 ml/min: 100 ml waeren 400 s, 50 ml sind 200 s.
    expect(targetForPump(15)).toBe(50)
    expect(runSecondsForPump(15)).toBeLessThanOrEqual(MAX_CALIBRATION_SECONDS)
  })

  it('bleibt auch bei einer sehr langsamen Pumpe bei der kleinsten Menge', () => {
    // 3 ml/min: selbst 25 ml sind 500 s. Dann wird gekappt — was im Becher
    // steht, wird trotzdem gemessen, und daraus rechnet sich die Foerdermenge.
    expect(targetForPump(3)).toBe(25)
    expect(runSecondsForPump(3)).toBe(MAX_CALIBRATION_SECONDS)
  })

  it('nennt eine Laufzeit, auch wenn die Pumpe noch unkalibriert ist', () => {
    // Der Knopf steht dann zwar nicht da, aber die Anzeige darf nicht NaN sein.
    expect(runSecondsForPump(null)).toBe(30)
  })
})
