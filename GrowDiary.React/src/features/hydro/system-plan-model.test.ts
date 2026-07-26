/* src/features/hydro/system-plan-model.test.ts
   Die Geometrie ist jetzt testbar — vorher steckte sie in CSS-Klassen.
   Passt zum bestehenden Test-Setup des Projekts (vitest-Syntax). */

import { describe, expect, it } from 'vitest'
import { buildSystemPlan, circleDiameterForLiters, layoutTypeFromRows, rowsFromLayoutType } from './system-plan-model'

const base = {
  hydroStyle: 'RDWC' as const,
  siteCount: 6,
  rows: 2,
  potLiters: 19,
  tankLiters: 60,
  reservoirPosition: 'Left' as const,
  tentWidthCm: 120,
  tentDepthCm: 120,
}

describe('system-plan-model', () => {
  it('leitet das Raster aus Sites und Reihen ab (kein 6-Sites-in-2x2-Widerspruch)', () => {
    const plan = buildSystemPlan(base)
    expect(plan.cols).toBe(3)
    expect(plan.rows).toBe(2)
    expect(plan.sites).toHaveLength(6)
  })

  it('rechnet den Eimerdurchmesser aus dem Volumen', () => {
    expect(Math.round(circleDiameterForLiters(19))).toBe(28)
    expect(circleDiameterForLiters(30)).toBeGreaterThan(circleDiameterForLiters(19))
  })

  it('meldet, wenn das System nicht ins Zelt passt', () => {
    expect(buildSystemPlan(base).fits).toBe(true)
    expect(buildSystemPlan({ ...base, siteCount: 12, rows: 2 }).fits).toBe(false)
  })

  it('haelt alle Eimer innerhalb des Zeltrahmens', () => {
    const plan = buildSystemPlan(base)
    for (const site of plan.sites) {
      expect(site.cx - site.diameterCm / 2).toBeGreaterThanOrEqual(plan.tent.x - 0.01)
      expect(site.cx + site.diameterCm / 2).toBeLessThanOrEqual(plan.tent.x + plan.tent.w + 0.01)
      expect(site.cy - site.diameterCm / 2).toBeGreaterThanOrEqual(plan.tent.y - 0.01)
      expect(site.cy + site.diameterCm / 2).toBeLessThanOrEqual(plan.tent.y + plan.tent.h + 0.01)
    }
  })

  it('DWC hat einen Eimer, keinen Tank und keine Rohre', () => {
    const plan = buildSystemPlan({ ...base, hydroStyle: 'DWC' })
    expect(plan.sites).toHaveLength(1)
    expect(plan.tank).toBeNull()
    expect(plan.pipes).toHaveLength(0)
  })

  it('zeichnet Ruecklauf UND Zulauf', () => {
    const plan = buildSystemPlan(base)
    expect(plan.pipes.some((pipe) => pipe.kind === 'return')).toBe(true)
    expect(plan.pipes.some((pipe) => pipe.kind === 'feed')).toBe(true)
  })

  it('bleibt zum Legacy-Layouttyp kompatibel (Round-Trip)', () => {
    expect(layoutTypeFromRows(2, 6)).toBe('Grid2x3')
    expect(rowsFromLayoutType('Grid2x3', 6)).toBe(2)
    expect(layoutTypeFromRows(1, 4)).toBe('Row')
    expect(rowsFromLayoutType('Row', 4)).toBe(1)
  })

  it('rechnet Systemvolumen und Tankanteil', () => {
    const plan = buildSystemPlan(base)
    expect(plan.totalLiters).toBe(174)
    expect(plan.tankSharePct).toBe(34)
  })
})
