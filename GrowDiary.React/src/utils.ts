export function formatDate(value: string | null | undefined, options?: Intl.DateTimeFormatOptions): string {
  if (!value) {
    return '–'
  }

  return new Intl.DateTimeFormat('de-DE', {
    dateStyle: 'medium',
    ...(options ?? {}),
  }).format(new Date(value))
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '–'
  }

  return new Intl.DateTimeFormat('de-DE', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

export function formatNumber(value: number | null | undefined, fractionDigits = 1): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '–'
  }

  return new Intl.NumberFormat('de-DE', {
    minimumFractionDigits: 0,
    maximumFractionDigits: fractionDigits,
  }).format(value)
}

/**
 * Ein Wert aus Home Assistant, wie ihn ein deutscher Bildschirm zeigt.
 *
 * **Home Assistant liefert Zahlen IMMER mit Punkt** — „5.78", „1.02", „17.8".
 * Das gilt nicht nur im Testbetrieb, sondern in jeder echten Anlage. Roh
 * ausgegeben stand es an drei Stellen: in der Wert-Spalte auf „Sensoren &
 * Wartung", in der Live-Wert-Anzeige der Zuordnung und in der Auswahlliste der
 * Entitäten („Demo EC — 1.02 mS/cm").
 *
 * Text bleibt Text: `on`, `off`, `unavailable` werden nicht angefasst.
 */
export function haWert(state: string | null | undefined, unit?: string | null): string | null {
  if (state == null || state === '') return null

  const zahl = Number(state)
  const wert = state.trim() !== '' && Number.isFinite(zahl)
    ? formatNumber(zahl, Math.abs(zahl) < 10 ? 2 : 1)
    : state

  return `${wert}${unit ? ` ${unit}` : ''}`
}

export function toLocalInputValue(date = new Date()): string {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 16)
}

export function classNames(...values: Array<string | false | null | undefined>): string {
  return values.filter(Boolean).join(' ')
}

const severityLabels: Record<string, string> = {
  Critical: 'Kritisch',
  Warning: 'Warnung',
  // „Info" fehlte — und wo ein Wort fehlt, faellt der englische Wert durch:
  // auf der Diagnose stand deshalb „Info" zwischen lauter deutschen Stufen.
  Info: 'Hinweis',
  High: 'Hoch',
  Medium: 'Mittel',
  Low: 'Niedrig',
  Normal: 'Normal',
}

export function formatSeverityLabel(value: string | null | undefined): string {
  if (!value) return '–'
  return severityLabels[value] ?? value
}
