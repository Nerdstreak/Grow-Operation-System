import type { MetricPayload } from '../../types'
import type { DashboardTile, EntityValue } from './useTentDashboard'

/** A blank reading, so a tile still renders while its sensor is quiet. */
function placeholder(key: string, label: string, unit: string | null): MetricPayload {
  return { key, label, value: '–', unit, tone: 'muted', hint: null }
}

/** Derives a readable caption from an entity id when the user hasn't set one. */
export function entityLabel(tile: DashboardTile, value: EntityValue | undefined): string {
  if (tile.label) return tile.label
  if (value?.friendlyName) return value.friendlyName
  const id = tile.entityId ?? ''
  const short = id.includes('.') ? id.slice(id.indexOf('.') + 1) : id
  const spaced = short.replace(/[_-]+/g, ' ').trim()
  return spaced ? spaced.charAt(0).toUpperCase() + spaced.slice(1) : 'Sensor'
}

/** Resolves one tile to something the metric tile component can render. */
export function resolveTile(
  tile: DashboardTile,
  metricsByKey: Map<string, MetricPayload>,
  entityValues: Map<string, EntityValue>,
): MetricPayload {
  if (tile.kind === 'Metric' && tile.metricKey) {
    const found = metricsByKey.get(tile.metricKey)
    const base = found ?? placeholder(tile.metricKey, tile.label ?? tile.metricKey, tile.unit)
    return { ...base, label: tile.label ?? base.label, unit: tile.unit ?? base.unit }
  }

  const value = tile.entityId ? entityValues.get(tile.entityId) : undefined
  const raw = value?.state ?? null
  const numeric = raw != null && raw.trim() !== '' && Number.isFinite(Number(raw.replace(',', '.')))
  return {
    key: tile.entityId ?? tile.id,
    label: entityLabel(tile, value),
    // Non-numeric states (on/off, "läuft") are shown as they are — that is the point of
    // letting arbitrary entities in.
    value: raw == null || raw.trim() === '' ? '–' : raw,
    unit: tile.unit ?? value?.unit ?? null,
    tone: raw == null ? 'muted' : numeric ? 'default' : 'default',
    hint: value?.friendlyName ?? null,
  }
}
