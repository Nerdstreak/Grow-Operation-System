import { formatNumber } from '../utils'
import { feldText } from '../zahlenfeld'

/** Zahl und Einheit gehoeren zusammen — deshalb `\u00A0`, ein geschuetztes Leerzeichen.
 *  Mit einem gewoehnlichen stand das „L" am Telefon allein in der naechsten Zeile. */
export function formatLiters(value: number | null | undefined) {
  return value == null ? '–' : `${formatNumber(value, 1)}\u00A0L`
}

export function toNullableString(value: string | null | undefined): string | null {
  const trimmed = (value ?? '').trim()
  return trimmed.length === 0 ? null : trimmed
}

export function toNullableInt(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number.parseInt(trimmed, 10)
  return Number.isFinite(parsed) ? parsed : null
}

/**
 * Eine Zahl für ein Eingabefeld.
 *
 * Die Umwandlung steht in <code>zahlenfeld.ts</code> und nur dort — sie stand
 * am 01.09.2026 fünfmal in der Oberfläche, jedes Mal mit
 * <code>String(value)</code> und damit mit englischem Punkt.
 */
export function draftNumber(value: number | null | undefined) {
  return feldText(value)
}
