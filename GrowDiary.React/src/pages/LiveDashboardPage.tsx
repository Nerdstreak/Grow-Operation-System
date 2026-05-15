import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, MetricPayload, RiskEventDto, TentDto, TentLivePayload } from '../types'
import { classNames, formatDateTime } from '../utils'

type LoadIssue = { area: string; message: string }
type LiveDashboardData = {
  tents: TentDto[]
  liveByTentId: Record<number, TentLivePayload>
  grows: GrowSummary[]
  riskEvents: RiskEventDto[]
  issues: LoadIssue[]
  refreshedAtUtc: string | null
}
type MetricDefinition = { key: string; label: string; unit: string | null; priority?: boolean }

const emptyData: LiveDashboardData = { tents: [], liveByTentId: {}, grows: [], riskEvents: [], issues: [], refreshedAtUtc: null }
const riskRank: Record<string, number> = { Critical: 0, Warning: 1, Info: 2 }

const tentMetrics: MetricDefinition[] = [
  { key: 'temperature', label: 'Luft', unit: '°C', priority: true },
  { key: 'humidity', label: 'RLF', unit: '%', priority: true },
  { key: 'vpd', label: 'VPD', unit: 'kPa', priority: true },
  { key: 'ppfd', label: 'PPFD', unit: 'µmol/m²/s', priority: true },
  { key: 'co2', label: 'CO₂', unit: 'ppm' },
  { key: 'light-cycle', label: 'Licht', unit: null },
]

const reservoirMetrics: MetricDefinition[] = [
  { key: 'reservoir-ph', label: 'pH', unit: null, priority: true },
  { key: 'reservoir-ec', label: 'EC', unit: 'mS/cm', priority: true },
  { key: 'reservoir-temp', label: 'Wasser', unit: '°C', priority: true },
  { key: 'reservoir-level', label: 'Level', unit: 'L/cm', priority: true },
  { key: 'orp', label: 'ORP', unit: 'mV' },
  { key: 'dissolved-oxygen', label: 'DO', unit: 'mg/L' },
]

function LiveDashboardPage() {
  const [data, setData] = useState<LiveDashboardData>(emptyData)
  const [loading, setLoading] = useState(true)
  const [refreshTick, setRefreshTick] = useState(0)

  useEffect(() => {
    const intervalId = window.setInterval(() => setRefreshTick((current) => current + 1), 60_000)
    return () => window.clearInterval(intervalId)
  }, [])

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      const issues: LoadIssue[] = []
      const fetchOptional = async <T,>(area: string, path: string, fallback: T): Promise<T> => {
        try {
          return await apiFetch<T>(path, { signal: controller.signal })
        } catch (caught) {
          if (!controller.signal.aborted) issues.push({ area, message: formatApiError(caught, `${area} konnten nicht geladen werden.`) })
          return fallback
        }
      }

      const [tents, grows, riskEvents] = await Promise.all([
        fetchOptional<TentDto[]>('Zelte', '/api/settings/tents', []),
        fetchOptional<GrowSummary[]>('Grows', '/api/grows?archived=false', []),
        fetchOptional<RiskEventDto[]>('RiskEvents', '/api/risk-events?status=Open', []),
      ])

      const liveEntries = await Promise.all(
        tents.map(async (tent) => {
          try {
            const payload = await apiFetch<TentLivePayload>(`/api/live/tents/${tent.id}`, { signal: controller.signal })
            return [tent.id, payload] as const
          } catch (caught) {
            if (!controller.signal.aborted) issues.push({ area: tent.name, message: formatApiError(caught, 'Live-Daten fehlen.') })
            return [tent.id, null] as const
          }
        }),
      )

      if (controller.signal.aborted) return
      setData({
        tents: [...tents].sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name)),
        liveByTentId: Object.fromEntries(liveEntries.filter((entry): entry is readonly [number, TentLivePayload] => entry[1] !== null)),
        grows,
        riskEvents: riskEvents.filter((event) => event.status === 'Open'),
        issues,
        refreshedAtUtc: new Date().toISOString(),
      })
      setLoading(false)
    }

    void load()
    return () => controller.abort()
  }, [refreshTick])

  const activeGrows = useMemo(() => data.grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning'), [data.grows])
  const sortedRisks = useMemo(() => [...data.riskEvents].sort((a, b) => (riskRank[a.severity] ?? 99) - (riskRank[b.severity] ?? 99) || b.startedAtUtc.localeCompare(a.startedAtUtc)), [data.riskEvents])
  const primaryCamera = useMemo(() => findPrimaryCamera(data.tents, data.liveByTentId), [data.tents, data.liveByTentId])
  const criticalCount = sortedRisks.filter((risk) => risk.severity === 'Critical').length
  const warningCount = sortedRisks.filter((risk) => risk.severity === 'Warning').length
  const statusLabel = loading ? 'Lädt' : criticalCount > 0 ? 'Kritisch' : warningCount > 0 ? 'Beobachten' : 'Stabil'

  return (
    <main className="page-scroll app-page live-home-page">
      <section className="control-header">
        <div>
          <span className="control-kicker">Live</span>
          <h1>{statusLabel}</h1>
        </div>
        <button className="btn btn-primary" type="button" onClick={() => setRefreshTick((current) => current + 1)}>Aktualisieren</button>
      </section>

      {data.issues.length > 0 && <InlineIssues issues={data.issues} />}
      <RiskStrip risks={sortedRisks} />

      {data.tents.length === 0 ? (
        <section className="start-grid">
          <StartCard title="Zelt" to="/zelte" action="Anlegen" />
          <StartCard title="Home Assistant" to="/home-assistant" action="Verbinden" />
          <StartCard title="Grow" to="/grows/new" action="Starten" />
        </section>
      ) : (
        <>
          {primaryCamera && <CameraPanel camera={primaryCamera} />}

          <section className="live-tent-grid">
            {data.tents.map((tent) => (
              <LiveTentPanel key={tent.id} tent={tent} live={data.liveByTentId[tent.id]} activeGrows={activeGrows.filter((grow) => grow.tentId === tent.id)} />
            ))}
          </section>
        </>
      )}

      <section className="compact-section">
        <div className="section-headline">
          <h2>Grows</h2>
          <Link to="/grows/new" className="btn">Starten</Link>
        </div>
        {activeGrows.length === 0 ? (
          <div className="empty-hint tight">Keine aktiven Grows.</div>
        ) : (
          <div className="compact-list">
            {activeGrows.slice(0, 5).map((grow) => (
              <Link key={grow.id} to={`/grows/${grow.id}`} className="compact-row">
                <strong>{grow.name}</strong>
                <span>{grow.strain ?? 'Sorte offen'}</span>
                <span>{grow.tentName ?? 'Ohne Zelt'}</span>
              </Link>
            ))}
          </div>
        )}
      </section>
    </main>
  )
}

function LiveTentPanel({ tent, live, activeGrows }: { tent: TentDto; live: TentLivePayload | undefined; activeGrows: GrowSummary[] }) {
  return (
    <article className={classNames('ops-card tent-ops-card', live?.stateTone === 'critical' && 'is-critical', live?.stateTone === 'attention' && 'is-warning')}>
      <header className="ops-card-header">
        <div>
          <h2>{tent.name}</h2>
          <span>{formatTentType(tent.tentType)} · {formatTentSize(tent)}</span>
        </div>
        <span className={classNames('badge', live?.stateTone === 'critical' ? 'badge-crit' : live?.stateTone === 'attention' ? 'badge-warn' : live ? 'badge-ok' : 'badge-neutral')}>
          {live?.stateLabel ?? 'offline'}
        </span>
      </header>

      <MetricGroup title="Zelt" metrics={mapMetrics(live?.metrics ?? [], tentMetrics)} />
      <MetricGroup title="RDWC/DWC" metrics={mapMetrics(live?.metrics ?? [], reservoirMetrics)} />

      <footer className="ops-card-footer">
        <div className="mini-grow-list">
          {activeGrows.length === 0 ? <span>Kein aktiver Grow</span> : activeGrows.slice(0, 2).map((grow) => <Link key={grow.id} to={`/grows/${grow.id}`}>{grow.name}</Link>)}
        </div>
        <Link to={`/zelte/${tent.id}`} className="btn">Öffnen</Link>
      </footer>
    </article>
  )
}

function MetricGroup({ title, metrics }: { title: string; metrics: MetricPayload[] }) {
  return (
    <section className="ops-metric-group">
      <h3>{title}</h3>
      <div className="ops-metric-grid">
        {metrics.map((metric) => <MetricTile key={metric.key} metric={metric} />)}
      </div>
    </section>
  )
}

function MetricTile({ metric }: { metric: MetricPayload }) {
  const showUnit = Boolean(metric.unit && metric.value !== '–')
  return (
    <div className={classNames('ops-metric', metric.tone === 'danger' && 'is-critical', metric.tone === 'warning' && 'is-warning', metric.tone === 'success' && 'is-ok')}>
      <span>{metric.label}</span>
      <div className="ops-metric-value">
        <strong>{metric.value}</strong>
        {showUnit && <em>{metric.unit}</em>}
      </div>
      <small>{metric.hint ?? ' '}</small>
    </div>
  )
}

function CameraPanel({ camera }: { camera: { tent: TentDto; live: TentLivePayload } }) {
  if (!camera.live.cameraUrl) return null
  const cameraUrl = resolveCameraUrl(camera.live.cameraUrl)
  return (
    <section className="camera-panel">
      <div className="camera-frame">
        <img src={cameraUrl} alt={camera.tent.name} />
        <span className="camera-badge">Live</span>
      </div>
      <div className="camera-side">
        <h2>{camera.tent.name}</h2>
        <span>{formatDateTime(camera.live.refreshedAtUtc)}</span>
        <Link className="btn" to="/home-assistant">HA</Link>
      </div>
    </section>
  )
}

function RiskStrip({ risks }: { risks: RiskEventDto[] }) {
  if (risks.length === 0) return null
  const critical = risks.filter((risk) => risk.severity === 'Critical').length
  return (
    <section className={classNames('risk-strip', critical > 0 && 'is-critical')}>
      <strong>{critical > 0 ? `${critical} kritisch` : `${risks.length} offen`}</strong>
      <span>{risks[0]?.title}</span>
      <Link to="/hardware" className="btn">Prüfen</Link>
    </section>
  )
}

function InlineIssues({ issues }: { issues: LoadIssue[] }) {
  return (
    <section className="inline-issues">
      <strong>Teilweise offline</strong>
      <span>{issues.slice(0, 2).map((issue) => issue.area).join(' · ')}</span>
    </section>
  )
}

function StartCard({ title, to, action }: { title: string; to: string; action: string }) {
  return (
    <Link to={to} className="start-card">
      <strong>{title}</strong>
      <span>{action}</span>
    </Link>
  )
}

function mapMetrics(metrics: MetricPayload[], definitions: MetricDefinition[]): MetricPayload[] {
  const byKey = new Map(metrics.map((metric) => [metric.key, metric]))
  return definitions.map((definition) => {
    const mapped = byKey.get(definition.key)
    if (!mapped) return { key: definition.key, label: definition.label, value: '–', unit: definition.unit, tone: 'neutral', hint: null }
    return { ...mapped, label: definition.label, unit: mapped.unit ?? definition.unit }
  })
}

function resolveCameraUrl(url: string): string {
  if (/^https?:\/\//i.test(url)) return url
  if (import.meta.env.DEV && window.location.port === '5173') {
    return `http://${window.location.hostname}:5076${url}`
  }
  return url
}

function findPrimaryCamera(tents: TentDto[], liveByTentId: Record<number, TentLivePayload>): { tent: TentDto; live: TentLivePayload } | null {
  for (const tent of tents) {
    const live = liveByTentId[tent.id]
    if (live?.cameraUrl) return { tent, live }
  }
  return null
}

function formatTentSize(tent: TentDto): string {
  if (!tent.widthCm && !tent.depthCm && !tent.tentHeightCm) return 'Größe offen'
  return `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm`
}

function formatTentType(value: string): string {
  switch (value) {
    case 'Flower': return 'Blüte'
    case 'Production': return 'Blüte'
    case 'Mother': return 'Mutter'
    case 'Propagation': return 'Anzucht'
    case 'Quarantine': return 'Quarantäne'
    case 'MultiPurpose': return 'Mehrzweck'
    default: return value
  }
}

function formatApiError(caught: unknown, fallback: string): string {
  if (caught instanceof ApiRequestError) return caught.message
  return caught instanceof Error ? caught.message : fallback
}

export default LiveDashboardPage
