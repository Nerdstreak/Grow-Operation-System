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

export type PhaseName = 'Keim' | 'Sämling' | 'Veg' | 'Blüte'

export type Phase = {
  /** Für kurze Anzeigen („Veg Tag 20") — ohne den Text zerlegen zu müssen. */
  name: PhaseName
  label: string
  /** Kurzfassung für enge Stellen wie die Grow-Karten: „Veg 22/28". */
  short: string
  days: number
  state: PhaseState
  /** Nur gesetzt, solange die Phase läuft: 0–1 des geplanten Anteils. */
  progress?: number
  /** Der wievielte Tag in dieser Phase heute ist; nur für die laufende. */
  dayInPhase?: number
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
  /** Wann der Sämling zur Veg wurde — beobachtet, nicht gerechnet. */
  vegStartedAt?: string | null
  /** Nach so vielen Tagen ohne Eintrag gilt der Sämling als durch (Schätzung). */
  seedlingDays?: number
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
  // Bewurzelt schlägt gekeimt: ab da hört das Keimen auf.
  const keimEnde = parse(grow.rootedAt) ?? parse(grow.germinatedAt)

  // Der Sämling: zwischen Keimung und den ersten echten Blättern. Er stand
  // vorher nicht im Strahl, obwohl die Zielwerte ihn längst kannten — der
  // Balken sagte „Veg Tag 8", die Kacheln zeigten Sämlings-Ziele. Zwei
  // Phasenmodelle nebeneinander, und keins verriet das andere.
  //
  // Der Übergang hängt nicht am Kalender, sondern am Aussehen: echte gezackte
  // Blätter statt der zwei runden Keimblätter, dickerer Stängel, regelmäßig
  // neue Blattpaare. Steht kein Eintrag, wird geschätzt — und das steht dann
  // auch dran.
  const saemlingStart = keimEnde?.getTime() ?? start.getTime()
  const saemlingTage = grow.seedlingDays ?? 14
  const vegEingetragen = parse(grow.vegStartedAt)
  const vegGeschaetzt = new Date(saemlingStart + saemlingTage * TAG)
  const vegBeginn = vegEingetragen ?? vegGeschaetzt
  const imSaemling = flip == null && jetzt < vegBeginn.getTime()

  // Blütedauer aus den Breeder-Angaben; ohne sie der übliche Richtwert von acht
  // Wochen. Das Erntedatum trägt deshalb ein „~" — es ist eine Schätzung.
  const bluetewochen = grow.breederFlowerWeeksMax ?? grow.breederFlowerWeeksMin ?? 8
  const bluetetage = bluetewochen * 7

  // Veg beginnt, wo der Sämling endet.
  const vegStart = vegBeginn.getTime()

  // Der Flip: entweder erfolgt, oder aus der geplanten Veg-Dauer errechnet.
  // Die Dauer zählt ab Beginn der Veg-Phase — gefragt war „wie lange will ich
  // in der Veg bleiben", nicht „wie lange ab Aussaat".
  const geplanterFlip = grow.plannedVegDays != null && grow.plannedVegDays > 0
    ? new Date(vegStart + grow.plannedVegDays * TAG)
    : null
  const flipFuerRechnung = flip ?? geplanterFlip
  const inBluete = flip != null && jetzt >= flip.getTime()
  // Geplant ist alles, was noch nicht passiert ist — auch ein fest gesetztes
  // Datum in der Zukunft. Vorher stand unter dem Strahl "Geflippt 06.08.",
  // obwohl der 06.08. erst kommt.
  const flipIsPlanned = flipFuerRechnung != null && !inBluete

  const harvest = flipFuerRechnung ? new Date(flipFuerRechnung.getTime() + bluetetage * TAG) : null

  // Alle drei Phasen erscheinen IMMER. Vorher fehlten Keim und Blüte, solange
  // kein Bewurzelungsdatum und kein Flip erfasst war — dann stand da ein
  // einzelner Balken „Veg", und wo man im Lauf steckt, war nicht zu sehen.
  // `days: 0` heißt „Dauer unbekannt": die Anzeige gibt dem Abschnitt dann nur
  // einen schmalen Streifen, statt eine Länge zu behaupten.
  const phases: Phase[] = []

  // ---------- Keim ----------
  if (keimEnde && keimEnde.getTime() > start.getTime()) {
    const dauer = tage(start.getTime(), keimEnde.getTime())
    phases.push({ name: 'Keim', label: `Keim ${dauer} T`, short: `Keim ${dauer} T`, days: dauer, state: 'done' })
  } else {
    phases.push({ name: 'Keim', label: 'Keim · nicht erfasst', short: 'Keim —', days: 0, state: 'done' })
  }

  // ---------- Sämling ----------
  {
    const dauer = tage(saemlingStart, vegBeginn.getTime())
    const gelaufen = tage(saemlingStart, Math.min(jetzt, vegBeginn.getTime()))
    const geschaetzt = vegEingetragen == null
    phases.push(imSaemling
      ? {
          name: 'Sämling',
          label: geschaetzt ? `Sämling · Tag ${gelaufen} (geschätzt)` : `Sämling · Tag ${gelaufen}`,
          short: `Sämling ${gelaufen}`,
          days: dauer,
          state: 'current',
          dayInPhase: gelaufen,
        }
      : {
          name: 'Sämling',
          label: `Sämling ${dauer} T`,
          short: `Sämling ${dauer} T`,
          days: dauer,
          state: 'done',
        })
  }

  // ---------- Veg ----------
  if (inBluete && flip) {
    const dauer = tage(vegStart, flip.getTime())
    phases.push({ name: 'Veg', label: `Veg ${dauer} T`, short: `Veg ${dauer} T`, days: dauer, state: 'done' })
  } else {
    const gelaufen = tage(vegStart, jetzt)
    const geplant = flipFuerRechnung ? tage(vegStart, flipFuerRechnung.getTime()) : null
    phases.push(imSaemling
      ? {
          // Noch im Sämling: die Veg steht bevor, sie läuft nicht.
          name: 'Veg',
          label: geplant ? `Veg · ${geplant} T geplant` : 'Veg · offen',
          short: geplant ? `Veg ${geplant} T` : 'Veg —',
          days: geplant ?? 0,
          state: 'planned',
        }
      : {
          name: 'Veg',
          label: geplant ? `Veg · Tag ${gelaufen} von ${geplant}` : `Veg · Tag ${gelaufen}`,
          short: geplant ? `Veg ${gelaufen}/${geplant}` : `Veg ${gelaufen} T`,
          days: geplant ?? gelaufen,
          state: 'current',
          progress: geplant ? Math.min(1, gelaufen / geplant) : undefined,
          dayInPhase: gelaufen,
        })
  }

  // ---------- Blüte ----------
  if (inBluete && flip) {
    const tagInBluete = Math.floor((jetzt - flip.getTime()) / TAG) + 1
    phases.push({
      name: 'Blüte',
      label: `Blüte · Tag ${tagInBluete} von ${bluetetage}`,
      short: `Blüte ${tagInBluete}/${bluetetage}`,
      days: bluetetage,
      state: 'current',
      progress: Math.min(1, tagInBluete / bluetetage),
      dayInPhase: tagInBluete,
    })
  } else if (flipFuerRechnung) {
    phases.push({ name: 'Blüte', label: `Blüte ${bluetetage} T geplant`, short: `Blüte ${bluetetage} T`, days: bluetetage, state: 'planned' })
  } else {
    phases.push({ name: 'Blüte', label: 'Blüte · offen', short: 'Blüte —', days: 0, state: 'planned' })
  }

  return {
    phases,
    dates: {
      start: shortDate(start),
      flip: shortDate(flipFuerRechnung),
      harvest: shortDate(harvest),
    },
    flipIsPlanned,
    daysToFlip: flipIsPlanned && flipFuerRechnung
      ? Math.round((flipFuerRechnung.getTime() - jetzt) / TAG)
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

/**
 * Die Kurzform für Kartenköpfe: „Veg Tag 20", „Blüte Tag 22".
 *
 * Kommt aus demselben Strahl wie alles andere. Die Grow-Karten hatten dafür
 * eine eigene Rechnung, die ab Startdatum zählte — also die Keimzeit
 * mitzählte — und jede laufende Phase „Veg" nannte.
 */
export function currentPhaseLabel(timeline: PhaseTimeline): string | null {
  const laufend = timeline.phases.find((phase) => phase.state === 'current')
  if (!laufend || laufend.dayInPhase == null) return null
  return `${laufend.name} Tag ${laufend.dayInPhase}`
}
