import { useEffect, useState } from 'react'
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

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      const issues: string[] = []
      const safe = async <T,>(name: string, path: string, fallback: T): Promise<T> => {
        try { return await apiFetch<T>(path, { signal: controller.signal }) }
        catch (caught) { if (!controller.signal.aborted) issues.push(`${name}: ${formatApiError(caught, 'nicht erreichbar')}`); return fallback }
      }

      const [tents, grows, risks] = await Promise.all([
        safe<TentDto[]>('Zelte', '/api/settings/tents', []),
        safe<GrowSummary[]>('Grows', '/api/grows?archived=false', []),
        safe<RiskEventDto[]>('Risiken', '/api/risk-events?openOnly=true', []),
      ])

      const sorted = [...tents].sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name))
      const livePairs = await Promise.all(sorted.map(async (tent) => {
        try { return [tent.id, await apiFetch<TentLivePayload>(`/api/live/tents/${tent.id}`, { signal: controller.signal })] as const }
        catch { return [tent.id, null] as const }
      }))

      if (controller.signal.aborted) return
      setState({ tents: sorted, grows, risks, liveByTentId: Object.fromEntries(livePairs.filter((pair): pair is readonly [number, TentLivePayload] => pair[1] !== null)), issues })
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
  const hydroMetrics = mapMetrics(live?.metrics ?? [], hydroMetricKeys)
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
      refresh={refresh}
      onRefresh={() => setRefresh((current) => current + 1)}
    />
  )
}

export default LiveDashboardPage
