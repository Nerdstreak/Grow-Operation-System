import { formatDate, formatNumber } from '../utils'

export function formatLiters(value: number | null | undefined) {
  return value == null ? '–' : `${formatNumber(value, 1)} L`
}

export function formatDateShort(value: string | null | undefined) {
  return formatDate(value)
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

export function toNullableFloat(value: string): number | null {
  const trimmed = value.trim().replace(',', '.')
  if (!trimmed) return null
  const parsed = Number.parseFloat(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

export function draftNumber(value: number | null | undefined) {
  return value == null || Number.isNaN(value) ? '' : String(value)
}
