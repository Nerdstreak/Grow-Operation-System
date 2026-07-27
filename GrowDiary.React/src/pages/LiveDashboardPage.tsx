import { useEffect, useRef, useState } from 'react'
import { apiFetch } from '../api'
import type { GrowSummary, RiskEventDto, TentDto, TentLivePayload } from '../types'
import { LiveScreen, type LiveTask } from '../features/live/LiveScreen'
import { buildPhaseTimeline } from '../features/grows/phase-timeline'
import '../features/live/live-screen.css'
import { V1Skeleton } from '../components/v1'
import {
  buildScore,
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
  // Fuellstand wird entweder in Litern ODER in Zentimetern gemessen — beide
  // Kacheln zu zeigen heisst, dass eine davon immer leer ist. Es gewinnt die,
  // die einen Wert hat; ohne beides bleibt die Liter-Kachel als Platzhalter.
  const hydroAlle = mapMetrics(live?.metrics ?? [], hydroMetricKeys)
  const hatWert = (key: string) => {
    const metric = hydroAlle.find((item) => item.key === key)
    return Boolean(metric && metric.value && metric.value !== '–')
  }
  const fuellstandKey = hatWert('reservoir-level') ? 'reservoir-level'
    : hatWert('reservoir-level-cm') ? 'reservoir-level-cm'
      : 'reservoir-level'
  const hydroMetrics = hydroAlle.filter((metric) =>
    !metric.key.startsWith('reservoir-level') || metric.key === fuellstandKey)
  const lightMetric = findMetric(live?.metrics ?? [], ['light-cycle', 'ppfd'])
  const hasHydroGrow = primaryGrow ? primaryGrow.hydroStyle === 'DWC' || primaryGrow.hydroStyle === 'RDWC' : false
  const risksForContext = state.risks
    .filter((risk) => risk.status === 'Open' || risk.status === 'Acknowledged')
    .filter((risk) => (primaryGrow ? risk.growId === primaryGrow.id : false) || (selectedTent ? risk.tentId === selectedTent.id : false))
    .sort((a, b) => riskRank(a.severity) - riskRank(b.severity) || a.startedAtUtc.localeCompare(b.startedAtUtc))

  // --- Ableitungen fuer den Bildschirm ---------------------------------------

  const sensorsLive = (live?.metrics ?? []).filter((metric) => metric.value && metric.value !== '\u2013').length

  // Die Score-Zeile des Entwurfs: "Klima 100 · Naehrloesung 64 (DO -36) · Technik 100".
  // Statt erfundener Teilscores nennt sie, was tatsaechlich danebenliegt — eine
  // Zahl, die niemand nachrechnen kann, waere schlimmer als keine.
  const abweichungen = [...climateMetrics, ...hydroMetrics]
    .filter((metric) => metric.numericValue != null && (metric.targetMin != null || metric.targetMax != null))
    .filter((metric) => (metric.targetMin != null && metric.numericValue! < metric.targetMin)
      || (metric.targetMax != null && metric.numericValue! > metric.targetMax))
  const scoreParts = abweichungen.length === 0
    ? 'Alle Messwerte im Zielband'
    : `${abweichungen.length} ${abweichungen.length === 1 ? 'Wert' : 'Werte'} daneben: ${abweichungen.map((metric) => metric.label).join(', ')}`

  const lastMeasurement = primaryGrow?.latestMeasurementAt
    ? formatTime(primaryGrow.latestMeasurementAt)
    : null
  const stageLine = primaryGrow
    ? [primaryGrow.latestStage, growDayLabel(primaryGrow.startDate)]
      .filter(Boolean).join(' ')
    : null
  const plantLine = primaryGrow?.plantCount ? `${primaryGrow.plantCount} Pflanzen` : null

  // Heute faellig: was die Watchdog-Risiken und der Addback-Bedarf hergeben.
  const tasks: LiveTask[] = []
  if (hasHydroGrow && primaryGrow) {
    const ec = hydroMetrics.find((metric) => metric.key === 'reservoir-ec')
    if (ec?.numericValue != null && ec.targetMax != null && ec.numericValue > ec.targetMax) {
      tasks.push({
        id: 'addback',
        when: 'fällig',
        title: `Addback · EC ${ec.numericValue.toFixed(2).replace('.', ',')} \u2192 ${ec.targetMax.toFixed(2).replace('.', ',')}`,
        action: 'Start',
        to: `/grows/${primaryGrow.id}/addback`,
        due: true,
      })
    }
  }
  for (const risk of risksForContext.slice(0, 3 - tasks.length)) {
    tasks.push({
      id: `risk-${risk.id}`,
      when: risk.severity === 'Critical' ? 'jetzt' : 'offen',
      title: risk.title,
      action: 'Öffnen',
      to: '/diagnose',
      due: risk.severity === 'Critical',
    })
  }

  const timeline = buildPhaseTimeline(primaryGrow)

  if (loading) {
    return (
      <main className="ls">
        <V1Skeleton tiles={4} label="Lade Zelt" />
        <V1Skeleton tiles={5} rows={3} label="Lade Messwerte" />
      </main>
    )
  }

  return (
    <LiveScreen
      tent={selectedTent}
      grow={primaryGrow}
      score={score}
      scoreParts={scoreParts}
      climate={climateMetrics.concat(lightMetric ? [{ ...lightMetric, label: lightMetric.key === 'ppfd' ? 'PPFD' : 'Licht' }] : [])}
      hydro={hydroMetrics}
      sensorsLive={sensorsLive}
      lastMeasurement={lastMeasurement}
      stageLine={stageLine}
      risks={risksForContext}
      tasks={tasks}
      timeline={timeline.phases}
      timelineDates={timeline.dates}
      plantLine={plantLine}
      tents={state.tents}
      onTent={setSelectedTentId}
      onRefresh={() => setRefresh((current) => current + 1)}
    />
  )
}


/** „09:30" — die Uhrzeit reicht, das Datum steht im Journal. */
function formatTime(iso: string): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return ''
  return new Intl.DateTimeFormat('de-DE', { hour: '2-digit', minute: '2-digit' }).format(date)
}

/** „Tag 26" seit dem Start. */
function growDayLabel(startDate: string | null | undefined): string {
  if (!startDate) return ''
  const start = new Date(startDate)
  if (Number.isNaN(start.getTime())) return ''
  const days = Math.floor((Date.now() - start.getTime()) / 86_400_000) + 1
  return days > 0 ? `Tag ${days}` : ''
}


export default LiveDashboardPage
