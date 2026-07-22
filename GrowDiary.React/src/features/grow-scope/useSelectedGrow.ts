import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../../api'
import type { GrowSummary } from '../../types'

// Shared grow scope for the top-level, single-purpose pages that used to be tabs
// inside a grow (measurements, diagnosis, journal, automation, SOPs). Each such page
// gets a grow switcher instead of forcing the user to open a grow first. The choice
// lives in the URL (?growId=) so it survives refresh and is shareable, and defaults
// to the first active grow.
export function useSelectedGrow() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const urlGrowId = searchParams.get('growId')

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      try {
        const list = await apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal })
        if (controller.signal.aborted) return
        setGrows(list)
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Grows konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  // Default to the first active grow when nothing is pinned in the URL yet.
  const growId = urlGrowId ?? (grows[0] ? String(grows[0].id) : undefined)

  const setGrowId = useCallback((id: string) => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current)
      next.set('growId', id)
      return next
    }, { replace: true })
  }, [setSearchParams])

  return { grows, growId, setGrowId, loading, error }
}
