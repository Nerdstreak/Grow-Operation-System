import { describe, expect, it } from 'vitest'
import { buildPhaseTimeline, flipLabel } from './phase-timeline'

/** 1. Juni 2026, damit die Tagesrechnung nachvollziehbar bleibt. */
const JETZT = new Date('2026-06-01T12:00:00Z').getTime()
const TAG = 86_400_000
const vorTagen = (n: number) => new Date(JETZT - n * TAG).toISOString()
const inTagen = (n: number) => new Date(JETZT + n * TAG).toISOString()

describe('buildPhaseTimeline', () => {
  it('bleibt leer ohne Startdatum', () => {
    expect(buildPhaseTimeline(null, JETZT).phases).toEqual([])
    expect(buildPhaseTimeline({ startDate: null }, JETZT).phases).toEqual([])
    expect(buildPhaseTimeline({ startDate: 'kein Datum' }, JETZT).phases).toEqual([])
  })

  it('zeigt ohne geplante Veg-Dauer nur die laufende Phase — ohne ein Ende zu erfinden', () => {
    const strahl = buildPhaseTimeline({ startDate: vorTagen(20) }, JETZT)

    expect(strahl.phases).toHaveLength(1)
    expect(strahl.phases[0].label).toBe('Veg · Tag 20')
    expect(strahl.phases[0].progress).toBeUndefined()
    expect(strahl.dates.flip).toBe('—')
    expect(strahl.dates.harvest).toBe('—')
    expect(strahl.daysToFlip).toBeNull()
  })

  it('rechnet aus der geplanten Veg-Dauer den Flip und die Ernte', () => {
    const strahl = buildPhaseTimeline({
      startDate: vorTagen(20),
      plannedVegDays: 28,
      breederFlowerWeeksMax: 9,
    }, JETZT)

    // Flip acht Tage voraus, Ernte 63 Tage danach.
    expect(strahl.flipIsPlanned).toBe(true)
    expect(strahl.daysToFlip).toBe(8)
    expect(strahl.dates.flip).toBe('09.06.')
    expect(strahl.dates.harvest).toBe('11.08.')

    const veg = strahl.phases.find((phase) => phase.state === 'current')!
    expect(veg.label).toBe('Veg · Tag 20 von 28')
    expect(veg.progress).toBeCloseTo(20 / 28, 5)

    const bluete = strahl.phases.find((phase) => phase.state === 'planned')!
    expect(bluete.label).toBe('Blüte 63 T geplant')
  })

  it('nennt den Flip überfällig, wenn der Plan verstrichen ist', () => {
    const strahl = buildPhaseTimeline({ startDate: vorTagen(40), plannedVegDays: 28 }, JETZT)

    expect(strahl.daysToFlip).toBe(-12)
    // Der Fortschritt bleibt bei 1 stehen statt über den Balken hinauszulaufen.
    expect(strahl.phases[0].progress).toBe(1)
    expect(strahl.phases[0].label).toBe('Veg · Tag 40 von 28')
  })

  it('setzt die Keimphase vor die Veg-Phase, sobald bewurzelt bekannt ist', () => {
    const strahl = buildPhaseTimeline({
      startDate: vorTagen(30),
      rootedAt: vorTagen(18),
      plannedVegDays: 40,
    }, JETZT)

    expect(strahl.phases[0].label).toBe('Keim 12 T')
    expect(strahl.phases[0].state).toBe('done')
    // Die geplanten 40 Tage zählen ab der Bewurzelung, nicht ab dem Start —
    // gefragt war die Dauer der Veg-Phase.
    expect(strahl.phases[1].label).toBe('Veg · Tag 18 von 40')
  })

  it('nimmt das Bewurzelungsdatum, wenn beides bekannt ist', () => {
    const strahl = buildPhaseTimeline({
      startDate: vorTagen(30),
      germinatedAt: vorTagen(25),
      rootedAt: vorTagen(20),
    }, JETZT)

    expect(strahl.phases[0].label).toBe('Keim 10 T')
  })

  it('schließt Veg ab und zählt die Blüte, sobald geflippt wurde', () => {
    const strahl = buildPhaseTimeline({
      startDate: vorTagen(50),
      flipDate: vorTagen(14),
      breederFlowerWeeksMax: 8,
    }, JETZT)

    expect(strahl.flipIsPlanned).toBe(false)
    const veg = strahl.phases.find((phase) => phase.label.startsWith('Veg'))!
    expect(veg.state).toBe('done')
    expect(veg.label).toBe('Veg 36 T')

    const bluete = strahl.phases.find((phase) => phase.label.startsWith('Blüte'))!
    expect(bluete.state).toBe('current')
    expect(bluete.label).toBe('Blüte · Tag 15 von 56')
    expect(bluete.progress).toBeCloseTo(15 / 56, 5)
  })

  it('behandelt einen künftigen Flip als Plan, nicht als erfolgt', () => {
    const strahl = buildPhaseTimeline({ startDate: vorTagen(10), flipDate: inTagen(5) }, JETZT)

    // Das Datum steht fest, der Flip ist aber noch nicht passiert: Veg läuft.
    const veg = strahl.phases[0]
    expect(veg.state).toBe('current')
    expect(veg.label).toBe('Veg · Tag 10 von 15')
    // Es ist ein gesetztes Datum, kein aus der Dauer errechneter Plan.
    expect(strahl.flipIsPlanned).toBe(false)
  })

  it('fällt ohne Breeder-Angabe auf acht Wochen zurück', () => {
    const strahl = buildPhaseTimeline({ startDate: vorTagen(10), plannedVegDays: 20 }, JETZT)
    expect(strahl.phases.find((phase) => phase.state === 'planned')!.label).toBe('Blüte 56 T geplant')
  })

  it('ignoriert eine unsinnige Veg-Dauer von null oder weniger', () => {
    const strahl = buildPhaseTimeline({ startDate: vorTagen(10), plannedVegDays: 0 }, JETZT)
    expect(strahl.dates.flip).toBe('—')
    expect(strahl.phases[0].label).toBe('Veg · Tag 10')
  })
})

describe('flipLabel', () => {
  it('sagt, was Sache ist — statt immer „Flip geplant"', () => {
    expect(flipLabel(false, null, '—')).toBe('Flip offen')
    expect(flipLabel(false, null, '20.05.')).toBe('Geflippt 20.05.')
    expect(flipLabel(true, 8, '09.06.')).toBe('Flip geplant 09.06. · in 8 T')
    expect(flipLabel(true, 0, '01.06.')).toBe('Flip heute geplant')
    expect(flipLabel(true, -3, '29.05.')).toBe('Flip überfällig seit 3 T')
  })
})
