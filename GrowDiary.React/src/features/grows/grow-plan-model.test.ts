import { describe, expect, it } from 'vitest'
import { buildTimeline, canCreate, checkPlan, formatShort, type PlanInput } from './grow-plan-model'
import type { GrowSummary, HydroSetupDto, TentDto } from '../../types'

const TODAY = new Date('2026-07-26T12:00:00Z')

const tent = { id: 1, name: 'AC Infinity 120' } as TentDto
const hydro = { id: 7, name: 'RDWC Test Setup', potCount: 6, tentId: 1 } as HydroSetupDto

function input(over: Partial<PlanInput> = {}): PlanInput {
  return {
    plantCount: 6,
    startDate: '2026-07-28',
    vegDays: 28,
    flowerDays: 63,
    tent,
    hydro,
    otherGrows: [],
    programName: null,
    ...over,
  }
}

describe('buildTimeline', () => {
  it('rechnet Flip und Ernte aus den Dauern', () => {
    const timeline = buildTimeline(input())!
    expect(formatShort(timeline.flipDate)).toBe('25.08.')
    expect(formatShort(timeline.harvestDate)).toBe('27.10.')
  })

  it('lässt offen, was ohne Dauer nicht bestimmbar ist', () => {
    const timeline = buildTimeline(input({ flowerDays: null }))!
    expect(timeline.flipDate).not.toBeNull()
    expect(timeline.harvestDate).toBeNull()
  })

  it('zeichnet nichts ohne Startdatum', () => {
    expect(buildTimeline(input({ startDate: null }))).toBeNull()
  })

  it('nimmt das Flipdatum, wenn es eingetragen ist', () => {
    // Unser Formular fragt nach dem Flipdatum statt nach einer Veg-Dauer.
    const timeline = buildTimeline(input({ flipDate: '2026-08-10', vegDays: 28 }))!
    expect(formatShort(timeline.flipDate)).toBe('10.08.')
    expect(timeline.vegDays).toBe(13)
  })
})

describe('checkPlan', () => {
  it('bestätigt, wenn Pflanzenzahl und Sites zusammenpassen', () => {
    const sites = checkPlan(input(), TODAY).find((f) => f.key === 'sites')!
    expect(sites.severity).toBe('ok')
  })

  it('verhindert mehr Pflanzen als Plätze', () => {
    const sites = checkPlan(input({ plantCount: 8 }), TODAY).find((f) => f.key === 'sites')!
    expect(sites.severity).toBe('crit')
    expect(sites.text).toContain('zu wenig Plätze')
  })

  it('nennt leere Plätze, ohne sie zu verbieten', () => {
    // Vier Pflanzen in sechs Sites ist eine Entscheidung, kein Fehler.
    const sites = checkPlan(input({ plantCount: 4 }), TODAY).find((f) => f.key === 'sites')!
    expect(sites.severity).toBe('warn')
  })

  it('warnt, wenn das Zelt schon belegt ist', () => {
    const other = { id: 2, name: 'Run 04', tentId: 1, status: 'Running' } as GrowSummary
    const tentFinding = checkPlan(input({ otherGrows: [other] }), TODAY).find((f) => f.key === 'tent')!
    expect(tentFinding.severity).toBe('warn')
    expect(tentFinding.text).toContain('Run 04')
  })

  it('ignoriert abgeschlossene Grows im selben Zelt', () => {
    const done = { id: 2, name: 'Run 03', tentId: 1, status: 'Completed' } as GrowSummary
    const tentFinding = checkPlan(input({ otherGrows: [done] }), TODAY).find((f) => f.key === 'tent')!
    expect(tentFinding.severity).toBe('ok')
  })

  it('verhindert ein System, das in einem anderen Zelt steht', () => {
    const elsewhere = { ...hydro, tentId: 99 } as HydroSetupDto
    const finding = checkPlan(input({ hydro: elsewhere }), TODAY).find((f) => f.key === 'hydro-tent')!
    expect(finding.severity).toBe('crit')
  })

  it('weist auf ein zurückliegendes Startdatum hin', () => {
    const finding = checkPlan(input({ startDate: '2026-07-01' }), TODAY).find((f) => f.key === 'start')!
    expect(finding.severity).toBe('warn')
    expect(finding.text).toContain('25 Tage')
  })

  it('bemängelt nicht, was noch gar nicht ausgefüllt ist', () => {
    // Beim Ausfüllen sind Felder leer — das ist der Normalfall, keine Warnung.
    const findings = checkPlan(input({ plantCount: null, startDate: null, tent: null, hydro: null }), TODAY)
    expect(findings).toEqual([])
  })

  it('stellt Kritisches nach oben', () => {
    const other = { id: 2, name: 'Run 04', tentId: 1, status: 'Running' } as GrowSummary
    const findings = checkPlan(input({ plantCount: 8, otherGrows: [other], programName: 'Athena' }), TODAY)
    expect(findings[0].severity).toBe('crit')
    expect(findings[findings.length - 1].severity).toBe('ok')
  })
})

describe('canCreate', () => {
  it('erlaubt das Anlegen bei Warnungen', () => {
    // Leere Plätze oder ein geteiltes Zelt sind Entscheidungen des Growers.
    expect(canCreate(checkPlan(input({ plantCount: 4 }), TODAY))).toBe(true)
  })

  it('verhindert es bei einem kritischen Befund', () => {
    expect(canCreate(checkPlan(input({ plantCount: 8 }), TODAY))).toBe(false)
  })
})
