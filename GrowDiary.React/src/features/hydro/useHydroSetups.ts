/* src/features/hydro/useHydroSetups.ts
   Datenzugriff aus HydroPage herausgezogen (war Teil der 448-Zeilen-Seite). */

import { useCallback, useEffect, useMemo, useState } from 'react'
import { apiFetch, ApiRequestError, formatApiError } from '../../api'
import type { GrowSummary, HydroSetupDto, TentDto } from '../../types'

export function isNotFound(caught: unknown): boolean {
  return caught instanceof ApiRequestError && caught.status === 404
}

function sortSetups(items: HydroSetupDto[]): HydroSetupDto[] {
  return [...items].sort((a, b) =>
    a.status.localeCompare(b.status) || a.displayOrder - b.displayOrder || a.name.localeCompare(b.name))
}

export function useHydroSetups() {
  const [tents, setTents] = useState<TentDto[]>([])
  const [setups, setSetups] = useState<HydroSetupDto[]>([])
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [reloadKey, setReloadKey] = useState(0)

  /** Neu laden, ohne dass der Effekt von einer Funktionsreferenz abhaengt. */
  const reload = useCallback(() => setReloadKey((key) => key + 1), [])

  // Das Laden lebt im Effekt selbst: so ruft nichts synchron setState auf, und der
  // erste Render loest keinen zweiten aus. `loading` startet auf true, der Fehler
  // wird erst nach erfolgreichem Laden zurueckgesetzt.
  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      try {
        const [tentData, setupData] = await Promise.all([
          apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal }),
          apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true', { signal: controller.signal }),
        ])
        const growData = await apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }).catch(() => [])
        if (controller.signal.aborted) return
        setError(null)
        setTents(tentData)
        setSetups(sortSetups(setupData))
        setGrows(growData)
      } catch (caught) {
        if (!controller.signal.aborted) {
          setError(formatApiError(caught, 'Hydro-Daten konnten nicht geladen werden.'))
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [reloadKey])

  const activeSetups = useMemo(() => setups.filter((setup) => setup.status === 'Active'), [setups])

  const replaceSetup = useCallback((saved: HydroSetupDto) => {
    setSetups((current) => sortSetups(current.some((item) => item.id === saved.id)
      ? current.map((item) => (item.id === saved.id ? saved : item))
      : [...current, saved]))
  }, [])

  const removeSetup = useCallback((id: number) => {
    setSetups((current) => current.filter((item) => item.id !== id))
  }, [])

  /** Aktive/geplante Grows, die an diesem Setup haengen. Blockiert das Loeschen. */
  const growsForSetup = useCallback((setup: HydroSetupDto) => {
    const active = grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning')
    const direct = active.filter((grow) => grow.systemId === setup.id || grow.setupId === setup.id)
    if (direct.length > 0 || !setup.activeGrowCount) return direct
    return active.filter((grow) => grow.tentId === setup.tentId)
  }, [grows])

  return { tents, setups, activeSetups, grows, loading, error, setError, reload, replaceSetup, removeSetup, growsForSetup }
}
