import { useCallback, useEffect, useState } from 'react'
import { apiFetch } from '../../api'

export type DashboardTile = {
  id: string
  kind: 'Metric' | 'Entity'
  metricKey: string | null
  entityId: string | null
  label: string | null
  unit: string | null
}

export type DashboardSection = { id: string; title: string; tiles: DashboardTile[] }
export type DashboardLayout = { tentId: number; sections: DashboardSection[] }
export type EntityValue = { entityId: string; friendlyName: string | null; state: string | null; unit: string | null }

/**
 * The tent's dashboard arrangement plus the readings for tiles that point at arbitrary
 * Home Assistant entities. Grow OS's own metrics keep coming from the live payload, so
 * their status colouring stays intact.
 */
export function useTentDashboard(tentId: number | null) {
  const [layout, setLayout] = useState<DashboardLayout | null>(null)
  const [entityValues, setEntityValues] = useState<Map<string, EntityValue>>(new Map())
  const [reloadKey, setReloadKey] = useState(0)

  const reload = useCallback(() => setReloadKey((key) => key + 1), [])

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      if (tentId == null) {
        setLayout(null)
        return
      }
      try {
        const data = await apiFetch<DashboardLayout>(`/api/tents/${tentId}/dashboard`, { signal: controller.signal })
        if (!controller.signal.aborted) setLayout(data)
      } catch {
        // Without a layout the dashboard falls back to its built-in sections.
      }
    }

    void load()
    return () => controller.abort()
  }, [tentId, reloadKey])

  // Custom entities refresh on their own cadence — they are extras, not the core metrics.
  useEffect(() => {
    const controller = new AbortController()
    const hasCustom = (layout?.sections ?? []).some((section) => section.tiles.some((tile) => tile.kind === 'Entity'))

    async function loadValues() {
      if (tentId == null || !hasCustom) {
        setEntityValues(new Map())
        return
      }
      try {
        const values = await apiFetch<EntityValue[]>(`/api/tents/${tentId}/dashboard/values`, { signal: controller.signal })
        if (!controller.signal.aborted) setEntityValues(new Map(values.map((value) => [value.entityId, value])))
      } catch {
        // Leave the previous readings in place rather than blanking the tiles.
      }
    }

    void loadValues()
    const timer = window.setInterval(() => void loadValues(), 30_000)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [tentId, layout])

  return { layout, entityValues, reload }
}
