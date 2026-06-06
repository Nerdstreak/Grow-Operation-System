import { ApiRequestError } from '../../api'
import type { GrowSummary, MetricPayload, TentDto, TentLivePayload } from '../../types'

export type LiveState = {
  tents: TentDto[]
  liveByTentId: Record<number, TentLivePayload>
  grows: GrowSummary[]
  risks: import('../../types').RiskEventDto[]
  issues: string[]
}

export const initialLiveState: LiveState = { tents: [], liveByTentId: {}, grows: [], risks: [], issues: [] }

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
  ['reservoir-level', 'Wasserstand', null],
] as const

export function mapMetrics(items: MetricPayload[], definitions: readonly (readonly [string, string, string | null])[]): MetricPayload[] {
  return definitions.map(([key, label, unit]) => {
    const found = items.find((item) => item.key === key)
    return found ? { ...found, label, unit: found.unit ?? unit } : { key, label, value: '–', unit, tone: 'muted', hint: null }
  })
}

export function findMetric(items: MetricPayload[], keys: string[]) {
  return keys.map((key) => items.find((item) => item.key === key)).find((item): item is MetricPayload => Boolean(item)) ?? null
}

export function riskRank(value: string) {
  return value === 'Critical' ? 0 : value === 'Warning' ? 1 : 2
}

export function buildSensorStatus(live: TentLivePayload | undefined, issues: string[]) {
  if (issues.length > 0) return { label: 'Offline', text: 'Ein Teil der Live-Daten ist nicht erreichbar.', tone: 'warn' as const }
  if (!live) return { label: 'Nicht bewertet', text: 'Für dieses Zelt liegen noch keine Live-Werte vor.', tone: 'neutral' as const }
  const values = live.metrics.filter((metric) => metric.value && metric.value !== '–')
  if (values.length === 0) return { label: 'Nicht bewertet', text: 'Sensorwerte fehlen oder sind noch nicht gemappt.', tone: 'neutral' as const }
  const warnings = live.metrics.filter((metric) => metric.tone === 'warning' || metric.tone === 'danger').length
  return warnings > 0
    ? { label: 'Warnung', text: 'Mindestens ein Sensorwert braucht Aufmerksamkeit.', tone: 'warn' as const }
    : { label: 'Aktiv', text: `${values.length} Live-Werte werden ausgewertet.`, tone: 'ok' as const }
}

export function buildScore(metrics: MetricPayload[], tent: TentDto | null) {
  const usable = metrics.filter((metric) => metric.value && metric.value !== '–').length
  if (!tent) return { value: 0, label: 'Einrichten', tone: 'neutral' as const }
  if (usable === 0) return { value: 0, label: 'Einrichten', tone: 'neutral' as const }
  const warnings = metrics.filter((metric) => metric.tone === 'warning' || metric.tone === 'danger').length
  const value = Math.max(0, Math.min(100, 100 - warnings * 18 - Math.max(0, 6 - usable) * 8))
  return value < 55 ? { value, label: 'Kritisch', tone: 'critical' as const } : value < 82 ? { value, label: 'Beobachten', tone: 'warn' as const } : { value, label: 'Stabil', tone: 'ok' as const }
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

export function formatApiError(caught: unknown, fallback: string) {
  return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback
}
