/**
 * Die Phasen-Timeline eines Grows: Start → Flip → Ernte.
 *
 * Wird auf der Live-Seite und im Grow-Detail gezeichnet — deshalb liegt die
 * Rechnung hier und nicht doppelt in beiden Seiten. Dieselbe Zahl an zwei
 * Stellen zu pflegen ist in diesem Projekt schon dreimal schiefgegangen.
 *
 * Ohne Flipdatum gibt es keine Blütephase zu zeichnen — dann steht nur die
 * laufende Phase da, statt eine Dauer zu erfinden.
 */

export type PhaseState = 'done' | 'current' | 'planned'

export type Phase = { label: string; days: number; state: PhaseState }

export type PhaseTimeline = {
  phases: Phase[]
  dates: { start: string; flip: string; harvest: string }
}

/** Nur die Felder, die die Rechnung braucht — GrowSummary und GrowDetail passen beide. */
export type PhaseTimelineInput = {
  startDate: string | null
  flipDate?: string | null
  breederFlowerWeeksMin?: number | null
  breederFlowerWeeksMax?: number | null
}

const EMPTY: PhaseTimeline = { phases: [], dates: { start: '—', flip: '—', harvest: '—' } }

export function buildPhaseTimeline(grow: PhaseTimelineInput | null): PhaseTimeline {
  if (!grow?.startDate) return EMPTY

  const start = new Date(grow.startDate)
  if (Number.isNaN(start.getTime())) return EMPTY

  const flipRaw = grow.flipDate ? new Date(grow.flipDate) : null
  const flip = flipRaw && !Number.isNaN(flipRaw.getTime()) ? flipRaw : null

  // Blütedauer aus den Breeder-Angaben; ohne sie der übliche Richtwert von acht
  // Wochen. Das Erntedatum trägt deshalb ein „~" — es ist eine Schätzung.
  const flowerWeeks = grow.breederFlowerWeeksMax ?? grow.breederFlowerWeeksMin ?? 8
  const harvest = flip ? new Date(flip.getTime() + flowerWeeks * 7 * 86_400_000) : null

  const today = Date.now()
  const vegDays = flip
    ? Math.max(1, Math.round((flip.getTime() - start.getTime()) / 86_400_000))
    : Math.max(1, Math.round((today - start.getTime()) / 86_400_000))
  const inFlower = flip != null && today >= flip.getTime()

  const phases: Phase[] = [
    { label: `Veg ${vegDays} T`, days: vegDays, state: inFlower ? 'done' : 'current' },
  ]
  if (flip) {
    const flowerDays = flowerWeeks * 7
    phases.push({
      label: inFlower ? `Blüte · Tag ${Math.floor((today - flip.getTime()) / 86_400_000) + 1}` : `Blüte ${flowerDays} T geplant`,
      days: flowerDays,
      state: inFlower ? 'current' : 'planned',
    })
  }

  return { phases, dates: { start: shortDate(start), flip: shortDate(flip), harvest: shortDate(harvest) } }
}

/** „20.05." — Intl setzt den Punkt am Ende bei de-DE selbst. */
export function shortDate(value: Date | null): string {
  return value ? new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit' }).format(value) : '—'
}
