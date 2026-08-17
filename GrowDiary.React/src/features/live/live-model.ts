import type { GrowSummary, MetricPayload, TentDto, TentLivePayload } from '../../types'

export type LiveState = {
  tents: TentDto[]
  liveByTentId: Record<number, TentLivePayload>
  grows: GrowSummary[]
  risks: import('../../types').RiskEventDto[]
  /**
   * Überfällige Routinen je Grow.
   *
   * Fehlte hier, und damit fehlten sie im Block „Heute fällig" — der Klick auf
   * „Alle" führte dann auf eine Seite, die Punkte zeigte, die es auf der
   * Startseite angeblich nicht gab.
   */
  faelligeRoutinen: Record<number, Array<{ sopId: string; name: string; severity: string; meldung: string }>>
  issues: string[]
}

export const initialLiveState: LiveState = { tents: [], liveByTentId: {}, grows: [], risks: [], faelligeRoutinen: {}, issues: [] }

export const climateMetricKeys = [
  ['temperature', 'Luft', '°C'],
  ['humidity', 'RLF', '%'],
  ['vpd', 'VPD', 'kPa'],
] as const

export const hydroMetricKeys = [
  ['reservoir-ph', 'pH', null],
  ['reservoir-ec', 'EC', 'mS/cm'],
  ['orp', 'ORP', 'mV'],
  ['dissolved-oxygen', 'DO', 'mg/L'],
  ['reservoir-temp', 'Wassertemp.', '°C'],
  ['reservoir-level', 'Wasserstand', 'L'],
  ['reservoir-level-cm', 'Wasserstand', 'cm'],
] as const

export function mapMetrics(items: MetricPayload[], definitions: readonly (readonly [string, string, string | null])[]): MetricPayload[] {
  return definitions.map(([key, label, unit]) => {
    const found = items.find((item) => item.key === key)
    if (found) return { ...found, label, unit: found.unit ?? unit }
    // Kein Wert vom Server: die Kachel steht trotzdem da, damit das Raster nicht
    // je nach Sensorlage anders aussieht.
    return { key, label, value: '–', unit, tone: 'muted', hint: null, numericValue: null, targetMin: null, targetMax: null }
  })
}

/**
 * Ab wann eine Handmessung als veraltet gilt: 36 Stunden.
 *
 * Die tägliche Messroutine ist auf 24 Stunden getaktet; 36 lässt einen halben
 * Tag Luft, bevor die Kachel mahnt — wer abends statt morgens misst, wird nicht
 * sofort angezählt.
 */
const handVeraltetAbMinuten = 36 * 60

/** „Hand · vor 2 Std“ — die Herkunftszeile einer Handmessung, lesbar statt exakt. */
export function handHerkunft(ageMinutes: number): string {
  const rel = ageMinutes < 1 ? 'gerade eben'
    : ageMinutes < 60 ? `vor ${ageMinutes} Min`
      : ageMinutes < 48 * 60 ? `vor ${Math.round(ageMinutes / 60)} Std`
        : `vor ${Math.round(ageMinutes / (24 * 60))} Tagen`
  return `Hand · ${rel}`
}

export function handVeraltet(ageMinutes: number): boolean {
  return ageMinutes > handVeraltetAbMinuten
}

/**
 * Die beiden Zeilen unter der Kachel: Herkunft (neutral) oder Veraltet (mahnend).
 * Live-Werte bekommen keine — sie sind der Normalfall, den niemand erklärt braucht.
 */
export function metricProvenance(metric: MetricPayload): { sourceNote?: string; stale?: string } {
  if (metric.valueSource !== 'hand' || metric.measuredAgeMinutes == null) return {}
  return handVeraltet(metric.measuredAgeMinutes)
    ? { stale: `${handHerkunft(metric.measuredAgeMinutes)} — nachmessen?` }
    : { sourceNote: handHerkunft(metric.measuredAgeMinutes) }
}

export function findMetric(items: MetricPayload[], keys: string[]) {
  return keys.map((key) => items.find((item) => item.key === key)).find((item): item is MetricPayload => Boolean(item)) ?? null
}

export function riskRank(value: string) {
  return value === 'Critical' ? 0 : value === 'Warning' ? 1 : 2
}

/**
 * Der Grow-Score aus den tatsächlichen Abweichungen.
 *
 * Vorher zählte er `tone === 'warning' | 'danger'` — ein Feld, das der Server
 * für Messwerte nie setzt. Ergebnis: der Ring zeigte 100, während vier Werte
 * ausserhalb ihres Zielbands lagen. Eine Zahl, die der Zeile daneben
 * widerspricht, ist schlimmer als keine.
 *
 * Jetzt zählt, was zählbar ist: jeder Wert mit bekanntem Zielbereich, der
 * daneben liegt, kostet — deutlich ausserhalb mehr als knapp daneben.
 */
export function buildScore(metrics: MetricPayload[], tent: TentDto | null) {
  if (!tent) return { value: null, label: 'Einrichten', tone: 'neutral' as const }

  // Zurueckgerechnete Ziele zaehlen nicht mit: Luft, Feuchte und VPD beschreiben
  // dieselbe Lage, und dreimal abzuziehen macht aus einem Klimaproblem drei. Auf
  // der Kachel steht die Bewertung trotzdem — dort beantwortet sie die Frage
  // „ist dieser Wert gerade gut", und die ist eine andere als „wie steht es".
  const messbar = metrics.filter((metric) =>
    metric.numericValue != null && !metric.targetDerived && (metric.targetMin != null || metric.targetMax != null))
  const brauchbar = metrics.filter((metric) => metric.value && metric.value !== '–').length
  if (brauchbar === 0) return { value: null, label: 'Einrichten', tone: 'neutral' as const }

  // Ohne einen einzigen Wert mit Zielbereich gibt es nichts zu benoten. Vorher
  // kam hier trotzdem eine Zahl heraus — allein aus dem Abzug fuer fehlende
  // Sensoren — und daneben stand „Beobachten", als waere etwas geprueft worden.
  // `value: null`, nicht 0: die 0 landete gross im Ring und las sich wie eine
  // Note — „0 /100" neben dem Wort „Nicht bewertet", zwei Aussagen, die sich
  // widersprechen. Nichts gemessen ist keine schlechte Bewertung, sondern gar
  // keine.
  if (messbar.length === 0) return { value: null, label: 'Nicht bewertet', tone: 'neutral' as const }

  let abzug = 0
  for (const metric of messbar) {
    const wert = metric.numericValue as number
    const unten = metric.targetMin
    const oben = metric.targetMax
    if ((unten == null || wert >= unten) && (oben == null || wert <= oben)) continue

    const abstand = unten != null && wert < unten ? unten - wert : wert - (oben as number)
    const breite = unten != null && oben != null ? Math.abs(oben - unten) : Math.abs(unten ?? oben ?? 1) * 0.2
    // Mehr als eine Zielbreite daneben wiegt doppelt.
    abzug += abstand > Math.max(breite, Number.EPSILON) ? 20 : 10
  }

  // Ohne Sensoren kann man wenig beurteilen — das senkt den Score, statt ihn zu
  // beschönigen.
  abzug += Math.max(0, 6 - brauchbar) * 8

  const value = Math.max(0, Math.min(100, 100 - abzug))
  return value < 55
    ? { value, label: 'Kritisch', tone: 'critical' as const }
    : value < 82
      ? { value, label: 'Beobachten', tone: 'warn' as const }
      : { value, label: 'Stabil', tone: 'ok' as const }
}

export function chooseInitialTent(tents: TentDto[], grows: GrowSummary[]) {
  const running = grows.find((grow) => grow.status === 'Running' && grow.tentId)
  return running?.tentId ?? tents[0]?.id ?? null
}

export function formatTentType(value: string) {
  return value === 'Production' ? 'Blüte / Run' : value === 'Mother' ? 'Mutter' : value === 'Propagation' ? 'Anzucht' : value === 'Quarantine' ? 'Quarantäne' : value === 'MultiPurpose' ? 'Mehrzweck' : value
}

export function formatGrowStatus(value: string) {
  return value === 'Running' ? 'aktiv' : value === 'Planning' ? 'geplant' : value === 'Harvested' ? 'geerntet' : value === 'Archived' ? 'archiviert' : value
}

export function formatGrowHydroMedium(grow: GrowSummary) {
  return grow.hydroSetupName ?? (grow.hydroStyle === 'None' ? 'kein Hydro-Setup' : grow.hydroStyle)
}

