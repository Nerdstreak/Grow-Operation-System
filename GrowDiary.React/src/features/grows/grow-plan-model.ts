import type { GrowSummary, HydroSetupDto, TentDto } from '../../types'

/**
 * Die Prüfung neben dem Formular „Grow anlegen".
 *
 * Der Assistent hatte sechs Schritte, und ob die Pflanzenzahl zu den Sites passt
 * oder das Zelt am Starttag noch belegt ist, merkte man erst am Ende — oder gar
 * nicht. Auf einer Seite steht die Prüfung neben der Eingabe und rechnet mit,
 * während man tippt.
 *
 * Die Regeln stehen hier und nicht in der Komponente, weil sie Datumsarithmetik
 * enthalten und weil „belegt" mehrere Fälle hat: der andere Grow läuft noch, er
 * beginnt später, oder er ist längst archiviert.
 */

export type PlanSeverity = 'ok' | 'warn' | 'crit'

export type PlanFinding = {
  key: string
  severity: PlanSeverity
  text: string
}

export type PlanInput = {
  plantCount: number | null
  startDate: string | null
  /**
   * Das Formular kennt ein Flipdatum, der Entwurf eine Veg-Dauer — dieselbe
   * Information in zwei Schreibweisen. Ist ein Flipdatum da, gewinnt es; sonst
   * wird aus der Dauer gerechnet.
   */
  flipDate?: string | null
  vegDays: number | null
  flowerDays: number | null
  tent: TentDto | null
  hydro: HydroSetupDto | null
  /** Andere Grows, die dasselbe Zelt belegen könnten. */
  otherGrows: GrowSummary[]
  programName: string | null
}

export type PlanTimeline = {
  startDate: Date
  flipDate: Date | null
  harvestDate: Date | null
  vegDays: number | null
  flowerDays: number | null
}

function parseDate(value: string | null): Date | null {
  if (!value) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

function addDays(date: Date, days: number): Date {
  const next = new Date(date)
  next.setDate(next.getDate() + days)
  return next
}

export function formatShort(date: Date | null): string {
  if (!date) return '—'
  return new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit' }).format(date)
}

/** Start → Flip → Ernte, sofern die Dauern eingetragen sind. */
export function buildTimeline(input: PlanInput): PlanTimeline | null {
  const start = parseDate(input.startDate)
  if (!start) return null
  const explicitFlip = parseDate(input.flipDate ?? null)
  const veg = explicitFlip != null
    ? Math.max(0, Math.round((explicitFlip.getTime() - start.getTime()) / 86_400_000))
    : (input.vegDays != null && input.vegDays > 0 ? input.vegDays : null)
  const flower = input.flowerDays != null && input.flowerDays > 0 ? input.flowerDays : null
  const flip = explicitFlip ?? (veg != null ? addDays(start, veg) : null)
  const harvest = flip != null && flower != null ? addDays(flip, flower) : null
  return { startDate: start, flipDate: flip, harvestDate: harvest, vegDays: veg, flowerDays: flower }
}

/**
 * Prüft, was sich vor dem Anlegen prüfen lässt. Was fehlt, wird nicht bemängelt —
 * ein leeres Feld ist beim Ausfüllen der Normalfall, keine Warnung.
 */
export function checkPlan(input: PlanInput, today: Date = new Date()): PlanFinding[] {
  const findings: PlanFinding[] = []

  // --- Pflanzen und Sites ---
  const sites = input.hydro?.potCount ?? null
  if (input.plantCount != null && sites != null) {
    if (input.plantCount > sites) {
      findings.push({
        key: 'sites',
        severity: 'crit',
        text: `${input.plantCount} Pflanzen auf ${sites} Sites — ${input.hydro?.name ?? 'das System'} hat zu wenig Plätze.`,
      })
    } else if (input.plantCount < sites) {
      findings.push({
        key: 'sites',
        severity: 'warn',
        text: `${input.plantCount} Pflanzen auf ${sites} Sites — ${sites - input.plantCount} Plätze bleiben leer.`,
      })
    } else {
      findings.push({
        key: 'sites',
        severity: 'ok',
        text: `${input.plantCount} Pflanzen auf ${sites} Sites — ${input.hydro?.name ?? 'das System'} passt.`,
      })
    }
  }

  // --- Zelt zum Startzeitpunkt frei? ---
  const start = parseDate(input.startDate)
  if (input.tent && start) {
    const blocking = input.otherGrows.filter((grow) =>
      grow.tentId === input.tent!.id
      && (grow.status === 'Running' || grow.status === 'Planning'))
    if (blocking.length > 0) {
      const names = blocking.map((grow) => grow.name).join(', ')
      findings.push({
        key: 'tent',
        severity: 'warn',
        text: `${input.tent.name} ist durch ${names} belegt — zwei Läufe im selben Zelt teilen Klima und Licht.`,
      })
    } else {
      findings.push({ key: 'tent', severity: 'ok', text: `${input.tent.name} ist frei.` })
    }
  }

  // --- Start in der Vergangenheit ---
  if (start) {
    const daysAgo = Math.floor((today.getTime() - start.getTime()) / 86_400_000)
    if (daysAgo > 1) {
      findings.push({
        key: 'start',
        severity: 'warn',
        text: `Start liegt ${daysAgo} Tage zurück — die Timeline rechnet ab diesem Datum.`,
      })
    }
  }

  // --- Hydro passt zum Zelt? ---
  if (input.tent && input.hydro && input.hydro.tentId != null && input.hydro.tentId !== input.tent.id) {
    findings.push({
      key: 'hydro-tent',
      severity: 'crit',
      text: `${input.hydro.name} steht in einem anderen Zelt.`,
    })
  }

  // --- Programm ---
  if (input.programName) {
    findings.push({
      key: 'program',
      severity: 'ok',
      text: `Zielwerte aus ${input.programName} werden als Grenzwerte übernommen.`,
    })
  }

  const rank = { crit: 0, warn: 1, ok: 2 } as const
  return findings.sort((a, b) => rank[a.severity] - rank[b.severity])
}

/** Ob der Grow angelegt werden darf: bei einem kritischen Befund nicht. */
export function canCreate(findings: PlanFinding[]): boolean {
  return !findings.some((finding) => finding.severity === 'crit')
}
