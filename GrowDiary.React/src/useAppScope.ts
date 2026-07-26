import { useCallback, useEffect, useMemo, useState } from 'react'
import { apiFetch } from './api'

type TentOption = { id: number; name: string }
type GrowOption = { id: number; name: string; tentId?: number | null; status?: string }

const TENT_KEY = 'growos.scope.tent'
const GROW_KEY = 'growos.scope.grow'

function readStored(key: string): number | null {
  try {
    const raw = localStorage.getItem(key)
    const value = raw ? Number(raw) : Number.NaN
    return Number.isFinite(value) ? value : null
  } catch {
    return null
  }
}

function store(key: string, value: number | null) {
  try {
    if (value == null) localStorage.removeItem(key)
    else localStorage.setItem(key, String(value))
  } catch {
    /* Privatmodus */
  }
}

/**
 * Tent and grow, chosen once for the whole app.
 *
 * Every grow-related page used to carry its own picker, so the same choice had to be
 * made again on each of them and they could disagree. The choice lives here now, is
 * remembered across restarts, and the badges in the navigation refer to it — which is
 * also why the open-task count is per grow rather than a total: a number that mixes
 * every grow tells you nothing about the one you are looking at.
 */
export function useAppScope() {
  const [tents, setTents] = useState<TentOption[]>([])
  const [grows, setGrows] = useState<GrowOption[]>([])
  const [tentId, setTentIdState] = useState<number | null>(() => readStored(TENT_KEY))
  const [growId, setGrowIdState] = useState<number | null>(() => readStored(GROW_KEY))
  const [openTasks, setOpenTasks] = useState(0)
  const [addbackDue, setAddbackDue] = useState(false)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      try {
        const [tentData, growData] = await Promise.all([
          apiFetch<TentOption[]>('/api/settings/tents', { signal: controller.signal }).catch(() => []),
          apiFetch<GrowOption[]>('/api/grows?archived=false', { signal: controller.signal }).catch(() => []),
        ])
        if (controller.signal.aborted) return
        setTents(tentData)
        setGrows(growData)
      } catch {
        /* Ohne Auswahlliste bleibt die Leiste leer — die Seiten funktionieren weiter. */
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  // Badge data follows the selected grow; without one there is nothing to count.
  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      if (growId == null) {
        setOpenTasks(0)
        setAddbackDue(false)
        return
      }
      try {
        const [tasks, deviations] = await Promise.all([
          apiFetch<Array<{ status: string }>>(`/api/grows/${growId}/tasks`, { signal: controller.signal }).catch(() => []),
          apiFetch<Array<{ stableKey: string }>>(`/api/grows/${growId}/deviations`, { signal: controller.signal }).catch(() => []),
        ])
        if (controller.signal.aborted) return
        setOpenTasks(tasks.filter((task) => task.status === 'Open').length)
        setAddbackDue(deviations.some((deviation) => deviation.stableKey === 'hydro.ec'))
      } catch {
        /* Ein fehlendes Badge ist besser als ein falsches. */
      }
    }

    void load()
    return () => controller.abort()
  }, [growId])

  const setTentId = useCallback((value: number | null) => {
    setTentIdState(value)
    store(TENT_KEY, value)
  }, [])

  const setGrowId = useCallback((value: number | null) => {
    setGrowIdState(value)
    store(GROW_KEY, value)
  }, [])

  // Picking a tent narrows the grow list; a grow from another tent would be misleading.
  const growsForTent = useMemo(
    () => (tentId == null ? grows : grows.filter((grow) => grow.tentId == null || grow.tentId === tentId)),
    [grows, tentId],
  )

  const scope = useMemo(
    () => ({ tents, grows: growsForTent, tentId, growId, onTent: setTentId, onGrow: setGrowId }),
    [tents, growsForTent, tentId, growId, setTentId, setGrowId],
  )

  return { scope, counts: { openTasks, addbackDue } }
}
