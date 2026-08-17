/** Wie dringend eine Ablesung am Glas ist. */
export type CuringDueLevel = 'Ok' | 'Due' | 'Overdue' | 'Finished'

/** Wie die Feuchte im Glas zu bewerten ist. */
export type CuringHumidityLevel = 'TooDry' | 'Dry' | 'Good' | 'Damp' | 'MoldRisk'

export type CuringDuty = {
  level: CuringDueLevel
  dayInCure: number
  intervalDays: number
  burpMinutesMin: number
  burpMinutesMax: number
  nextDueUtc: string | null
  text: string
  source: string
}

export type CuringHumidity = {
  percent: number
  readAtUtc: string
  source: string
  level: CuringHumidityLevel
  summary: string
  action: string
  ratingSource: string
}

export type CuringJar = {
  id: number
  growId: number
  growName: string
  label: string
  strainId: number | null
  strainName: string | null
  filledAtUtc: string
  weightG: number | null
  hasHumidityPack: boolean
  finishedAtUtc: string | null
  notes: string | null
  duty: CuringDuty
  latestHumidity: CuringHumidity | null
}

export type CuringReading = {
  id: number
  jarId: number
  readAtUtc: string
  humidityPercent: number | null
  burpedMinutes: number | null
  note: string | null
  source: string
}

/** Die Ampelfarbe zur Feuchte — dieselben Namen wie bei der Wasser-Ampel. */
export function feuchteTon(level: CuringHumidityLevel): 'ok' | 'warn' | 'critical' {
  if (level === 'Good') return 'ok'
  if (level === 'MoldRisk' || level === 'TooDry') return 'critical'
  return 'warn'
}

/** „Heute lüften", „seit 2 Tagen überfällig" — was am Glas ansteht, in einem Satz. */
export function faelligText(duty: CuringDuty): string {
  if (duty.level === 'Finished') return 'Fertig ausgehärtet'
  if (duty.level === 'Overdue') return `Überfällig — ${duty.burpMinutesMin}–${duty.burpMinutesMax} min lüften`
  if (duty.level === 'Due') return `Heute lüften — ${duty.burpMinutesMin}–${duty.burpMinutesMax} min`
  // Ab Tag 30 gibt es keinen Termin mehr, sondern nur noch das Hygrometer.
  if (!duty.nextDueUtc) return 'Nach Hygrometer'
  return `Nächstes Lüften ${new Date(duty.nextDueUtc).toLocaleDateString('de-DE', { day: '2-digit', month: '2-digit' })}`
}
