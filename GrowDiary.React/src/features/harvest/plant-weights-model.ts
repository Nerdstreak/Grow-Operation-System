/**
 * Ernte pro Pflanze.
 *
 * Am Trockenregal wiegt man Pflanze für Pflanze — vorher gab es ein Feld für den
 * ganzen Grow, also musste man im Kopf addieren und verlor, welche Pflanze wie
 * viel gebracht hat. Genau diese Aufschlüsselung ist beim nächsten Lauf die
 * interessante: welches Pheno hat getragen.
 *
 * Die Summe bleibt die Wahrheit für Auswertungen, damit ältere Ernten ohne
 * Aufschlüsselung weiterzählen. Die Einzelwerte hängen als JSON daneben.
 */

export type PlantWeight = {
  /** Pflanzenkennung, z. B. „PL-01". */
  label: string
  wetG: number | null
  dryG: number | null
}

export type WeightTotals = {
  wetG: number | null
  dryG: number | null
  /** Erwartetes Trockengewicht, solange nicht alles trocken gewogen ist. */
  expectedDryG: number | null
  /** Wie viele Pflanzen schon nass gewogen sind. */
  weighed: number
  total: number
}

/** Übliche Trockenausbeute; dient nur der Erwartung, nie dem gespeicherten Wert. */
export const TYPICAL_DRY_RATIO = 0.22

export function parsePlantWeights(json: string | null | undefined, fallbackCount: number): PlantWeight[] {
  if (json) {
    try {
      const parsed = JSON.parse(json) as unknown
      if (Array.isArray(parsed)) {
        return parsed
          .filter((entry): entry is Record<string, unknown> => typeof entry === 'object' && entry !== null)
          .map((entry, index) => ({
            label: typeof entry.label === 'string' && entry.label ? entry.label : defaultLabel(index),
            wetG: numberOrNull(entry.wetG),
            dryG: numberOrNull(entry.dryG),
          }))
      }
    } catch {
      // Kaputtes JSON darf die Seite nicht blockieren — dann eben leere Zeilen.
    }
  }
  return Array.from({ length: Math.max(0, fallbackCount) }, (_, index) => ({
    label: defaultLabel(index),
    wetG: null,
    dryG: null,
  }))
}

function defaultLabel(index: number): string {
  return `PL-${String(index + 1).padStart(2, '0')}`
}

function numberOrNull(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value
  return null
}

/** Leere Zeilen werden nicht gespeichert — sie sagen nichts. */
export function serialisePlantWeights(weights: PlantWeight[]): string | null {
  const filled = weights.filter((weight) => weight.wetG != null || weight.dryG != null)
  return filled.length > 0 ? JSON.stringify(filled) : null
}

export function totals(weights: PlantWeight[]): WeightTotals {
  const wet = weights.filter((weight) => weight.wetG != null)
  const dry = weights.filter((weight) => weight.dryG != null)

  const wetSum = wet.length > 0 ? round(wet.reduce((sum, weight) => sum + (weight.wetG as number), 0)) : null
  const drySum = dry.length > 0 ? round(dry.reduce((sum, weight) => sum + (weight.dryG as number), 0)) : null

  // Die Erwartung gilt nur, solange noch nicht alles trocken gewogen ist —
  // danach steht die echte Zahl da, und eine Schätzung daneben wäre nur Lärm.
  const complete = dry.length > 0 && dry.length === weights.length
  const expected = !complete && wetSum != null ? round(wetSum * TYPICAL_DRY_RATIO) : null

  return { wetG: wetSum, dryG: drySum, expectedDryG: expected, weighed: wet.length, total: weights.length }
}

function round(value: number): number {
  return Math.round(value * 10) / 10
}

/** „2/6 Pflanzen" */
export function progressLabel(result: WeightTotals): string {
  return `${result.weighed}/${result.total} Pflanzen`
}
