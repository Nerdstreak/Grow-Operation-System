/* src/features/hydro/system-plan-model.ts
   Geometrie der RDWC/DWC-Draufsicht. Reine Funktion, kein React —
   damit testbar (siehe HANDOFF: system-plan-model.test.ts).

   Alles in Zentimetern. Der Aufrufer rechnet nur noch in Prozent
   der Rahmenbox um. */

import type { ReservoirPosition, SelectableHydroStyle } from '../../types'

/** Annahme fuer die Umrechnung Liter -> Grundflaeche. */
const BUCKET_HEIGHT_CM = 30
const TANK_HEIGHT_CM = 50
/** Platz zwischen Zeltwand und Tank; darin liegen Sammel- und Zulaufleitung. */
const GUTTER_CM = 34
const PIPE_RETURN_CM = 5      // 50 mm Rohr
const PIPE_FEED_CM = 2.6      // 25 mm Schlauch
const MIN_AISLE_CM = 4        // darunter passt keine Hand mehr dazwischen

export type PlanInput = {
  hydroStyle: SelectableHydroStyle
  siteCount: number
  /** Reihen; Spalten ergeben sich daraus und aus siteCount. */
  rows: number
  potLiters: number
  tankLiters: number
  reservoirPosition: ReservoirPosition
  tentWidthCm: number | null
  tentDepthCm: number | null
}

export type PlanRect = { x: number; y: number; w: number; h: number }
export type PlanPipe = PlanRect & { kind: 'return' | 'feed' }
export type PlanSite = { index: number; cx: number; cy: number; diameterCm: number }

export type SystemPlan = {
  frame: { w: number; h: number }
  tent: PlanRect
  tank: PlanRect | null
  sites: PlanSite[]
  pipes: PlanPipe[]
  cols: number
  rows: number
  bucketDiameterCm: number
  aisleCm: number
  rowGapCm: number
  fits: boolean
  totalLiters: number
  tankSharePct: number
}

/** Fallback, wenn am Zelt keine Maße hinterlegt sind. */
const DEFAULT_TENT = { w: 120, d: 120 }

export function circleDiameterForLiters(liters: number, heightCm = BUCKET_HEIGHT_CM): number {
  return 2 * Math.sqrt((Math.max(0, liters) * 1000) / (Math.PI * heightCm))
}

export function squareSideForLiters(liters: number, heightCm = TANK_HEIGHT_CM): number {
  return Math.sqrt((Math.max(0, liters) * 1000) / heightCm)
}

/** Legacy-Layouttyp -> Reihenanzahl. Das UI waehlt nur noch Reihen. */
export function rowsFromLayoutType(layout: string, siteCount: number): number {
  if (layout === 'SingleBucket' || layout === 'Row') return 1
  if (layout === 'Grid2x2' || layout === 'Grid2x3' || layout === 'Grid2x4') return 2
  return siteCount > 6 ? 2 : 1
}

/** Reihenanzahl -> Legacy-Layouttyp, damit das bestehende DTO/Backend
    unveraendert bleiben kann. */
export function layoutTypeFromRows(rows: number, siteCount: number): string {
  if (siteCount <= 1) return 'SingleBucket'
  if (rows === 1) return 'Row'
  const cols = Math.ceil(siteCount / rows)
  if (rows === 2 && cols === 2) return 'Grid2x2'
  if (rows === 2 && cols === 3) return 'Grid2x3'
  if (rows === 2 && cols === 4) return 'Grid2x4'
  return 'Custom'
}

export function buildSystemPlan(input: PlanInput): SystemPlan {
  const isDwc = input.hydroStyle === 'DWC'
  const siteCount = isDwc ? 1 : Math.max(1, Math.round(input.siteCount))
  const rows = isDwc ? 1 : Math.max(1, Math.min(Math.round(input.rows), siteCount))
  const cols = Math.ceil(siteCount / rows)

  const tentW = input.tentWidthCm && input.tentWidthCm > 0 ? input.tentWidthCm : DEFAULT_TENT.w
  const tentD = input.tentDepthCm && input.tentDepthCm > 0 ? input.tentDepthCm : DEFAULT_TENT.d
  const dia = circleDiameterForLiters(input.potLiters)

  const aisle = (tentW - cols * dia) / (cols + 1)
  const rowGap = (tentD - rows * dia) / (rows + 1)

  const pos: ReservoirPosition = isDwc ? 'None' : input.reservoirPosition
  const tankSide = pos === 'None' ? 0 : Math.max(20, squareSideForLiters(input.tankLiters))

  let frameW = tentW
  let frameH = tentD
  let tentX = 0
  let tentY = 0

  if (pos === 'Left' || pos === 'External') {
    frameW = tentW + tankSide + GUTTER_CM
    tentX = tankSide + GUTTER_CM
  } else if (pos === 'Right') {
    frameW = tentW + tankSide + GUTTER_CM
  } else if (pos === 'Top' || pos === 'Bottom') {
    frameH = tentD + tankSide + GUTTER_CM
    if (pos === 'Top') tentY = tankSide + GUTTER_CM
    // Plan quer halten, damit ein Tank oben/unten keinen Turm erzeugt
    frameW = Math.max(tentW, frameH * 1.15)
    tentX = (frameW - tentW) / 2
  }

  let tank: PlanRect | null = null
  if (pos === 'Left' || pos === 'External') tank = { x: 0, y: tentY + tentD / 2 - tankSide / 2, w: tankSide, h: tankSide }
  else if (pos === 'Right') tank = { x: tentX + tentW + GUTTER_CM, y: tentD / 2 - tankSide / 2, w: tankSide, h: tankSide }
  else if (pos === 'Top') tank = { x: tentX + tentW / 2 - tankSide / 2, y: 0, w: tankSide, h: tankSide }
  else if (pos === 'Bottom') tank = { x: tentX + tentW / 2 - tankSide / 2, y: tentY + tentD + GUTTER_CM, w: tankSide, h: tankSide }

  const sites: PlanSite[] = []
  for (let i = 0; i < siteCount; i += 1) {
    const row = Math.floor(i / cols)
    const col = i % cols
    const inRow = Math.min(cols, siteCount - row * cols)
    const gapForRow = (tentW - inRow * dia) / (inRow + 1)
    sites.push({
      index: i + 1,
      cx: tentX + gapForRow * (col + 1) + dia * (col + 0.5),
      cy: tentY + rowGap * (row + 1) + dia * (row + 0.5),
      diameterCm: dia,
    })
  }

  const pipes: PlanPipe[] = tank && !isDwc && sites.length > 1
    ? buildPipes({ sites, cols, rows, tank, tentX, tentY, tentW, tentD, pos, dia })
    : []

  const totalLiters = Math.round(siteCount * input.potLiters + (isDwc ? 0 : input.tankLiters))
  return {
    frame: { w: frameW, h: frameH },
    tent: { x: tentX, y: tentY, w: tentW, h: tentD },
    tank,
    sites,
    pipes,
    cols,
    rows,
    bucketDiameterCm: dia,
    aisleCm: aisle,
    rowGapCm: rowGap,
    fits: aisle >= MIN_AISLE_CM && rowGap >= MIN_AISLE_CM,
    totalLiters,
    tankSharePct: totalLiters > 0 && !isDwc ? Math.round((input.tankLiters / totalLiters) * 100) : 0,
  }
}

type PipeArgs = {
  sites: PlanSite[]
  cols: number
  rows: number
  tank: PlanRect
  tentX: number
  tentY: number
  tentW: number
  tentD: number
  pos: ReservoirPosition
  dia: number
}

function buildPipes(a: PipeArgs): PlanPipe[] {
  const pipes: PlanPipe[] = []
  const thick = PIPE_RETURN_CM
  const thin = PIPE_FEED_CM
  const xs = a.sites.map((s) => s.cx)
  const minX = Math.min(...xs)
  const maxX = Math.max(...xs)
  const rowYs = Array.from(new Set(a.sites.map((s) => s.cy)))
  const topY = Math.min(...rowYs)
  const botY = Math.max(...rowYs)
  const tankCx = a.tank.x + a.tank.w / 2
  const tankCy = a.tank.y + a.tank.h / 2
  const vertical = a.pos === 'Top' || a.pos === 'Bottom'
  const near = a.pos === 'Right' || a.pos === 'Bottom'

  if (!vertical) {
    // Ruecklauf-Manifold durch die Eimermitten, aus der Zeltwand heraus
    const trunkX = near ? a.tentX + a.tentW + GUTTER_CM * 0.32 : a.tentX - GUTTER_CM * 0.32
    const feedX = near ? a.tentX + a.tentW + GUTTER_CM * 0.66 : a.tentX - GUTTER_CM * 0.66
    const endX = near ? a.tank.x : a.tank.x + a.tank.w
    for (const y of rowYs) {
      const x0 = Math.min(trunkX, minX)
      const x1 = Math.max(trunkX, maxX)
      pipes.push({ x: x0, y: y - thick / 2, w: x1 - x0, h: thick, kind: 'return' })
    }
    pipes.push({ x: trunkX - thick / 2, y: Math.min(topY, tankCy), w: thick, h: Math.abs(Math.max(botY, tankCy) - Math.min(topY, tankCy)), kind: 'return' })
    pipes.push({ x: Math.min(endX, trunkX), y: tankCy - thick / 2, w: Math.abs(trunkX - endX), h: thick, kind: 'return' })
    // Zulauf: Pumpenleitung entlang der ersten Reihe
    const feedY = topY - a.dia * 0.34
    pipes.push({ x: Math.min(endX, feedX), y: tankCy + 10 - thin / 2, w: Math.abs(feedX - endX), h: thin, kind: 'feed' })
    pipes.push({ x: feedX - thin / 2, y: Math.min(feedY, tankCy + 10), w: thin, h: Math.abs(tankCy + 10 - feedY), kind: 'feed' })
    pipes.push({ x: Math.min(feedX, maxX), y: feedY - thin / 2, w: Math.abs(maxX - feedX), h: thin, kind: 'feed' })
    return pipes
  }

  // Transponiert: Manifolds laufen die Spalten hinunter
  const trunkY = near ? a.tentY + a.tentD + GUTTER_CM * 0.32 : a.tentY - GUTTER_CM * 0.32
  const feedLine = near ? a.tentY + a.tentD + GUTTER_CM * 0.66 : a.tentY - GUTTER_CM * 0.66
  const endY = near ? a.tank.y : a.tank.y + a.tank.h
  const colXs = Array.from(new Set(xs))
  for (const x of colXs) {
    const ys = a.sites.filter((s) => s.cx === x).map((s) => s.cy)
    const y0 = Math.min(Math.min(...ys), trunkY)
    const y1 = Math.max(Math.max(...ys), trunkY)
    pipes.push({ x: x - thick / 2, y: y0, w: thick, h: y1 - y0, kind: 'return' })
  }
  pipes.push({ x: Math.min(minX, tankCx), y: trunkY - thick / 2, w: Math.abs(Math.max(maxX, tankCx) - Math.min(minX, tankCx)), h: thick, kind: 'return' })
  pipes.push({ x: tankCx - thick / 2, y: Math.min(trunkY, endY), w: thick, h: Math.abs(endY - trunkY), kind: 'return' })
  const feedX = minX - a.dia * 0.34
  pipes.push({ x: Math.min(tankCx, feedX), y: feedLine - thin / 2, w: Math.abs(tankCx - feedX), h: thin, kind: 'feed' })
  pipes.push({ x: tankCx - thin / 2, y: Math.min(endY, feedLine), w: thin, h: Math.abs(feedLine - endY), kind: 'feed' })
  pipes.push({ x: feedX - thin / 2, y: Math.min(feedLine, near ? topY : botY), w: thin, h: Math.abs(feedLine - (near ? topY : botY)), kind: 'feed' })
  return pipes
}
