import { useEffect, useRef, useState } from 'react'
import { apiFetch } from '../api'
import type { GrowSummary, RiskEventDto, TentDto, TentLivePayload } from '../types'
import { LiveDashboard } from '../features/live/DesktopLiveDashboard'
import {
  buildScore,
  buildSensorStatus,
  chooseInitialTent,
  climateMetricKeys,
  findMetric,
  formatApiError,
  hydroMetricKeys,
  initialLiveState,
  mapMetrics,
  riskRank,
  type LiveState,
} from '../features/live/live-model'

function LiveDashboardPage() {
  const [state, setState] = useState<LiveState>(initialLiveState)
  const [selectedTentId, setSelectedTentId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [refresh, setRefresh] = useState(0)
  const [lastUpdated, setLastUpdated] = useState<number | null>(null)

  // Mirror the latest committed state so a background refresh can fall back to the
  // last good data instead of blanking out when a request fails transiently.
  const stateRef = useRef(state)
  useEffect(() => { stateRef.current = state }, [state])

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      // Note: don't flip `loading` back on for background refreshes — the
      // initial useState(true) covers first paint, and keeping it false on the
      // 30s tick lets the dashboard update in place instead of blanking out.
      const previous = stateRef.current
      const issues: string[] = []
      // Report whether the call succeeded so a transient failure keeps the last
      // good value instead of overwriting it with an empty fallback (which made
      // sensor values vanish until the page was re-opened).
      const attempt = async <T,>(name: string, path: string): Promise<{ ok: boolean; value: T | null }> => {
        try { return { ok: true, value: await apiFetch<T>(path, { signal: controller.signal }) } }
        catch (caught) {
          if (!controller.signal.aborted) issues.push(`${name}: ${formatApiError(caught, 'nicht erreichbar')}`)
          return { ok: false, value: null }
        }
      }

      const [tentsResult, growsResult, risksResult] = await Promise.all([
        attempt<TentDto[]>('Zelte', '/api/settings/tents'),
        attempt<GrowSummary[]>('Grows', '/api/grows?archived=false'),
        attempt<RiskEventDto[]>('Risiken', '/api/risk-events?openOnly=true'),
      ])

      const sorted = tentsResult.ok
        ? [...(tentsResult.value ?? [])].sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name))
        : previous.tents
      const grows = growsResult.ok ? (growsResult.value ?? []) : previous.grows
      const risks = risksResult.ok ? (risksResult.value ?? []) : previous.risks

      const livePairs = await Promise.all(sorted.map(async (tent) => {
        try { return [tent.id, await apiFetch<TentLivePayload>(`/api/live/tents/${tent.id}`, { signal: controller.signal })] as const }
        catch { return [tent.id, null] as const }
      }))

      if (controller.signal.aborted) return
      // Merge into the previous live map so a tent whose refresh failed keeps its
      // last good payload instead of dropping to empty. Only keep entries for
      // tents that still exist.
      const freshLive = Object.fromEntries(livePairs.filter((pair): pair is readonly [number, TentLivePayload] => pair[1] !== null))
      const liveByTentId: Record<number, TentLivePayload> = {}
      for (const tent of sorted) {
        const merged = freshLive[tent.id] ?? previous.liveByTentId[tent.id]
        if (merged) liveByTentId[tent.id] = merged
      }
      setState({ tents: sorted, grows, risks, liveByTentId, issues })
      setSelectedTentId((current) => current ?? chooseInitialTent(sorted, grows))
      setLoading(false)
      setLastUpdated(Date.now())
    }
    void load()
    return () => controller.abort()
  }, [refresh])

  useEffect(() => {
    const id = window.setInterval(() => setRefresh((value) => value + 1), 30000)
    return () => window.clearInterval(id)
  }, [])

  const selectedTent = state.tents.find((tent) => tent.id === selectedTentId) ?? state.tents[0] ?? null
  const live = selectedTent ? state.liveByTentId[selectedTent.id] : undefined
  const activeGrows = state.grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning')
  const growsForTent = selectedTent ? activeGrows.filter((grow) => grow.tentId === selectedTent.id) : []
  const primaryGrow = growsForTent[0] ?? null
  const score = buildScore(live?.metrics ?? [], selectedTent)
  const climateMetrics = mapMetrics(live?.metrics ?? [], climateMetricKeys)
  // The cm water-level slot only renders when its sensor actually reports — most
  // setups measure either liters OR centimeters, so no permanent empty tile.
  const hydroMetrics = mapMetrics(live?.metrics ?? [], hydroMetricKeys)
    .filter((metric) => metric.key !== 'reservoir-level-cm' || (metric.value && metric.value !== '–'))
  const lightMetric = findMetric(live?.metrics ?? [], ['light-cycle', 'ppfd'])
  const sensorStatus = buildSensorStatus(live, state.issues)
  const hasHydroGrow = primaryGrow ? primaryGrow.hydroStyle === 'DWC' || primaryGrow.hydroStyle === 'RDWC' : false
  const risksForContext = state.risks
    .filter((risk) => risk.status === 'Open' || risk.status === 'Acknowledged')
    .filter((risk) => (primaryGrow ? risk.growId === primaryGrow.id : false) || (selectedTent ? risk.tentId === selectedTent.id : false))
    .sort((a, b) => riskRank(a.severity) - riskRank(b.severity) || a.startedAtUtc.localeCompare(b.startedAtUtc))

  return (
    <LiveDashboard
      loading={loading}
      tents={state.tents}
      selectedTentId={selectedTent?.id ?? null}
      onSelectTent={(id) => setSelectedTentId(id)}
      selectedTent={selectedTent}
      primaryGrow={primaryGrow}
      growsForTent={growsForTent}
      score={score}
      climateMetrics={climateMetrics}
      hydroMetrics={hydroMetrics}
      lightMetric={lightMetric}
      sensorStatus={sensorStatus}
      hasHydroGrow={hasHydroGrow}
      risksForContext={risksForContext}
      issues={state.issues}
      lastUpdated={lastUpdated}
      onRefresh={() => setRefresh((current) => current + 1)}
    />
  )
}

export default LiveDashboardPage
