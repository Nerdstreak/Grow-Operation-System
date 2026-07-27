/**
 * Der Zeitstrahl eines Grows: Keim → Veg → Blüte → Ernte.
 *
 * Wird auf Live, in der Grow-Liste und im Grow-Detail gezeichnet — deshalb
 * liegt die Rechnung hier und nicht dreifach in den Seiten. Dieselbe Zahl an
 * mehreren Stellen zu pflegen ist in diesem Projekt schon dreimal
 * schiefgegangen.
 *
 * Der Strahl zeigt den **Plan** und wo man **heute** darin steht:
 *
 *   Keim    Start → Bewurzelt/Gekeimt (steht das nicht fest, entfällt die Phase)
 *   Veg     bis zum Flip; davor bis zum geplanten Flip (Bewurzelung + plannedVegDays)
 *   Blüte   Flip + Blütewochen des Breeders
 *
 * Vorher fehlte die geplante Veg-Dauer. Ohne sie konnte der Strahl vor dem
 * Flip nichts als „Veg, 68 Tage und läuft" sagen — kein Ziel, kein Ende, keine
 * Ernteschätzung. Der Entwurf zeigt dagegen „Flip geplant 04.08.", also war
 * die Absicht immer, den Plan zu sehen. Wer keine Veg-Dauer angibt, bekommt
 * weiterhin den offenen Strahl; erfunden wird nichts.
 */

export type PhaseState = 'done' | 'current' | 'planned'

export type Phase = {
  label: string
  days: number
  state: PhaseState
  /** Nur gesetzt, solange die Phase läuft: 0–1 des geplanten Anteils. */
  progress?: number
}

export type PhaseTimeline = {
  phases: Phase[]
  dates: { start: string; flip: string; harvest: string }
  /** true, sobald der Flip nur geplant und noch nicht erfolgt ist. */
  flipIsPlanned: boolean
  /** Tage bis zum geplanten Flip; negativ heißt überfällig. Null ohne Plan. */
  daysToFlip: number | null
}

/** Nur die Felder, die die Rechnung braucht — GrowSummary und GrowDetail passen beide. */
export type PhaseTimelineInput = {
  startDate: string | null
  flipDate?: string | null
  germinatedAt?: string | null
  rootedAt?: string | null
  plannedVegDays?: number | null
  breederFlowerWeeksMin?: number | null
  breederFlowerWeeksMax?: number | null
}

const TAG = 86_400_000
const EMPTY: PhaseTimeline = {
  phases: [],
  dates: { start: '—', flip: '—', harvest: '—' },
  flipIsPlanned: false,
  daysToFlip: null,
}

function parse(value: string | null | undefined): Date | null {
  if (!value) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

/** Ganze Tage zwischen zwei Zeitpunkten, mindestens 1 — eine Phase dauert nie 0 Tage. */
function tage(von: number, bis: number): number {
  return Math.max(1, Math.round((bis - von) / TAG))
}

export function buildPhaseTimeline(grow: PhaseTimelineInput | null, jetzt = Date.now()): PhaseTimeline {
  const start = parse(grow?.startDate ?? null)
  if (!grow || !start) return EMPTY

  const flip = parse(grow.flipDate)
  // Bewurzelt schlägt gekeimt: ab da wächst sie wirklich vegetativ.
  const keimEnde = parse(grow.rootedAt) ?? parse(grow.germinatedAt)

  // Blütedauer aus den Breeder-Angaben; ohne sie der übliche Richtwert von acht
  // Wochen. Das Erntedatum trägt deshalb ein „~" — es ist eine Schätzung.
  const bluetewochen = grow.breederFlowerWeeksMax ?? grow.breederFlowerWeeksMin ?? 8
  const bluetetage = bluetewochen * 7

  // Veg beginnt mit der Bewurzelung; ohne dieses Datum mit dem Grow-Start.
  const vegStart = keimEnde?.getTime() ?? start.getTime()

  // Der Flip: entweder erfolgt, oder aus der geplanten Veg-Dauer errechnet.
  // Die Dauer zählt ab Beginn der Veg-Phase — gefragt war „wie lange will ich
  // in der Veg bleiben", nicht „wie lange ab Aussaat".
  const geplanterFlip = grow.plannedVegDays != null && grow.plannedVegDays > 0
    ? new Date(vegStart + grow.plannedVegDays * TAG)
    : null
  const flipFuerRechnung = flip ?? geplanterFlip
  const flipIsPlanned = flip == null && geplanterFlip != null

  const harvest = flipFuerRechnung ? new Date(flipFuerRechnung.getTime() + bluetetage * TAG) : null
  const inBluete = flip != null && jetzt >= flip.getTime()

  const phases: Phase[] = []

  // ---------- Keim ----------
  if (keimEnde && keimEnde.getTime() > start.getTime()) {
    phases.push({
      label: `Keim ${tage(start.getTime(), keimEnde.getTime())} T`,
      days: tage(start.getTime(), keimEnde.getTime()),
      state: 'done',
    })
  }

  // ---------- Veg ----------
  if (inBluete && flip) {
    const dauer = tage(vegStart, flip.getTime())
    phases.push({ label: `Veg ${dauer} T`, days: dauer, state: 'done' })
  } else {
    const gelaufen = tage(vegStart, jetzt)
    const geplant = flipFuerRechnung ? tage(vegStart, flipFuerRechnung.getTime()) : null
    phases.push({
      label: geplant ? `Veg · Tag ${gelaufen} von ${geplant}` : `Veg · Tag ${gelaufen}`,
      days: geplant ?? gelaufen,
      state: 'current',
      progress: geplant ? Math.min(1, gelaufen / geplant) : undefined,
    })
  }

  // ---------- Blüte ----------
  if (inBluete && flip) {
    const tagInBluete = Math.floor((jetzt - flip.getTime()) / TAG) + 1
    phases.push({
      label: `Blüte · Tag ${tagInBluete} von ${bluetetage}`,
      days: bluetetage,
      state: 'current',
      progress: Math.min(1, tagInBluete / bluetetage),
    })
  } else if (flipFuerRechnung) {
    phases.push({ label: `Blüte ${bluetetage} T geplant`, days: bluetetage, state: 'planned' })
  }

  return {
    phases,
    dates: {
      start: shortDate(start),
      flip: shortDate(flipFuerRechnung),
      harvest: shortDate(harvest),
    },
    flipIsPlanned,
    daysToFlip: flipIsPlanned && geplanterFlip
      ? Math.round((geplanterFlip.getTime() - jetzt) / TAG)
      : null,
  }
}

/** „20.05." — Intl setzt den Punkt am Ende bei de-DE selbst. */
export function shortDate(value: Date | null): string {
  return value ? new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit' }).format(value) : '—'
}

/**
 * Die Beschriftung des Flip-Termins: „Flip geplant 09.06. · in 8 T",
 * „Geflippt 20.05." oder „Flip überfällig seit 3 T".
 *
 * Steht hier und nicht in den Seiten, weil Live und Grow-Detail denselben
 * Strahl zeichnen. Vorher stand dort schlicht immer „Flip geplant" — auch
 * lange nach dem Flip, und ohne Plan sogar „Flip geplant —", was nach einem
 * fehlenden Wert aussah statt nach einer offenen Entscheidung.
 */
export function flipLabel(geplant: boolean, tage: number | null, datum: string): string {
  if (datum === '—') return 'Flip offen'
  if (!geplant) return `Geflippt ${datum}`
  if (tage == null) return `Flip geplant ${datum}`
  if (tage < 0) return `Flip überfällig seit ${Math.abs(tage)} T`
  if (tage === 0) return 'Flip heute geplant'
  return `Flip geplant ${datum} · in ${tage} T`
}
