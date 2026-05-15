import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, MetricPayload, RiskEventDto, TentDto, TentLivePayload } from '../types'
import { classNames, formatDateTime } from '../utils'

type LoadIssue = {
  area: string
  message: string
}

type LiveDashboardData = {
  tents: TentDto[]
  liveByTentId: Record<number, TentLivePayload>
  grows: GrowSummary[]
  riskEvents: RiskEventDto[]
  issues: LoadIssue[]
  refreshedAtUtc: string | null
}

type MetricDefinition = {
  key: string
  label: string
  unit: string | null
}

type MetricGroup = {
  title: string
  description: string
  metrics: MetricPayload[]
}

const emptyData: LiveDashboardData = {
  tents: [],
  liveByTentId: {},
  grows: [],
  riskEvents: [],
  issues: [],
  refreshedAtUtc: null,
}

const riskRank: Record<string, number> = {
  Critical: 0,
  Warning: 1,
  Info: 2,
}

const tentMetricDefinitions: MetricDefinition[] = [
  { key: 'temperature', label: 'Lufttemperatur', unit: '°C' },
  { key: 'humidity', label: 'Luftfeuchte', unit: '%' },
  { key: 'vpd', label: 'VPD', unit: 'kPa' },
  { key: 'ppfd', label: 'PPFD', unit: 'µmol/m²/s' },
  { key: 'co2', label: 'CO₂', unit: 'ppm' },
  { key: 'light-cycle', label: 'Licht', unit: null },
]

const reservoirMetricDefinitions: MetricDefinition[] = [
  { key: 'reservoir-ph', label: 'pH', unit: null },
  { key: 'reservoir-ec', label: 'EC', unit: 'mS/cm' },
  { key: 'orp', label: 'ORP', unit: 'mV' },
  { key: 'dissolved-oxygen', label: 'Sauerstoff', unit: 'mg/L' },
  { key: 'reservoir-temp', label: 'Wasser °C', unit: '°C' },
  { key: 'reservoir-level', label: 'Wasserstand', unit: null },
]

function LiveDashboardPage() {
  const [data, setData] = useState<LiveDashboardData>(emptyData)
  const [loading, setLoading] = useState(true)
  const [refreshTick, setRefreshTick] = useState(0)

  useEffect(() => {
    const intervalId = window.setInterval(() => {
      setRefreshTick((current) => current + 1)
    }, 60_000)

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
          if (!controller.signal.aborted) {
            issues.push({ area, message: formatApiError(caught, `${area} konnten nicht geladen werden.`) })
          }
          return fallback
        }
      }

      try {
        const [allTents, grows, riskEvents] = await Promise.all([
          fetchOptional<TentDto[]>('Zelte', '/api/settings/tents', []),
          fetchOptional<GrowSummary[]>('Grows', '/api/grows?archived=false', []),
          fetchOptional<RiskEventDto[]>('RiskEvents', '/api/risk-events?status=Open', []),
        ])

        const activeTents = allTents.filter((tent) => tent.activeGrowCount > 0 || tent.activeSetupCount > 0)
        const liveEntries = await Promise.all(
          activeTents.map(async (tent) => {
            try {
              const payload = await apiFetch<TentLivePayload>(`/api/live/tents/${tent.id}`, { signal: controller.signal })
              return [tent.id, payload] as const
            } catch (caught) {
              if (!controller.signal.aborted) {
                issues.push({ area: tent.name, message: formatApiError(caught, 'Live-Daten konnten nicht geladen werden.') })
              }
              return [tent.id, null] as const
            }
          }),
        )

        if (controller.signal.aborted) return

        setData({
          tents: activeTents,
          liveByTentId: Object.fromEntries(liveEntries.filter((entry): entry is readonly [number, TentLivePayload] => entry[1] !== null)),
          grows,
          riskEvents: riskEvents.filter((event) => event.status === 'Open'),
          issues,
          refreshedAtUtc: new Date().toISOString(),
        })
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void load()
    return () => controller.abort()
  }, [refreshTick])

  const activeGrows = useMemo(() => data.grows.filter((grow) => grow.status === 'Running'), [data.grows])
  const sortedRisks = useMemo(
    () => [...data.riskEvents].sort((a, b) => (riskRank[a.severity] ?? 99) - (riskRank[b.severity] ?? 99) || a.startedAtUtc.localeCompare(b.startedAtUtc)),
    [data.riskEvents],
  )
  const criticalRisks = sortedRisks.filter((event) => event.severity === 'Critical')
  const warningRisks = sortedRisks.filter((event) => event.severity === 'Warning')
  const cameraSource = findPrimaryCamera(data.tents, data.liveByTentId)
  const visibleTents = data.tents

  return (
    <div className="page-scroll">
      <div className="live-dashboard live-dashboard-home">
        <section className="live-hero-panel">
          <div>
            <div className="live-kicker">Grow-Zentrale</div>
            <h1>Dashboard</h1>
          </div>
          <div className="live-hero-actions">
            <span className="text-muted">Refresh {formatDateTime(data.refreshedAtUtc)}</span>
            <button type="button" className="btn btn-primary" onClick={() => setRefreshTick((current) => current + 1)} disabled={loading}>
              {loading ? 'Aktualisiert...' : 'Aktualisieren'}
            </button>
          </div>
        </section>

        {cameraSource && <LiveCameraPanel camera={cameraSource} />}

        <RiskSummary criticalRisks={criticalRisks} warningRisks={warningRisks} sortedRisks={sortedRisks} />

        {data.issues.length > 0 && (
          <div className="alert-bar">
            <div className="alert-dot" />
            <strong>Teilweise geladen</strong>
            <span>{data.issues.map((issue) => `${issue.area}: ${issue.message}`).join(' | ')}</span>
          </div>
        )}

        <section>
          <div className="section-label">Zelte & Systeme</div>
          {loading && visibleTents.length === 0 ? (
            <div className="empty-hint">Lade Live-Dashboard...</div>
          ) : visibleTents.length === 0 ? (
            <div className="empty-hint">Keine aktiven Zelte oder Hydro-Setups gefunden.</div>
          ) : (
            <div className="live-grid live-grid-separated">
              {visibleTents.map((tent) => (
                <LiveTentCard key={tent.id} tent={tent} live={data.liveByTentId[tent.id]} />
              ))}
            </div>
          )}
        </section>

        <section>
          <div className="section-label">Aktive Grows</div>
          {activeGrows.length === 0 ? (
            <div className="empty-hint">Keine aktiven Grows gefunden.</div>
          ) : (
            <div className="live-grow-grid compact">
              {activeGrows.map((grow) => (
                <Link key={grow.id} to={`/grows/${grow.id}`} className="live-grow-card">
                  <div>
                    <div className="live-grow-name">{grow.name}</div>
                    <div className="live-grow-meta">{grow.strain ?? 'Unbekannter Strain'} · {grow.tentName ?? 'Ohne Zelt'}</div>
                  </div>
                  <span className="badge badge-neutral">{grow.latestStage ?? grow.status}</span>
                </Link>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  )
}

function LiveCameraPanel({ camera }: { camera: { tent: TentDto; live: TentLivePayload } }) {
  return (
    <section className="live-camera-panel" aria-label={`Live-Kamera ${camera.tent.name}`}>
      <div className="live-camera-frame">
        <img src={camera.live.cameraUrl ?? ''} alt={`Livebild ${camera.tent.name}`} loading="lazy" />
        <div className="cam-live-badge"><span className="cam-live-dot" />Live</div>
      </div>
      <div className="live-camera-meta">
        <div className="live-kicker">Livebild</div>
        <h2>{camera.tent.name}</h2>
        <p>Aktualisiert {formatDateTime(camera.live.refreshedAtUtc)} · {camera.live.stateLabel}</p>
        <Link to={`/zelte/${camera.tent.id}`} className="btn">Zelt öffnen</Link>
      </div>
    </section>
  )
}

function RiskSummary({ criticalRisks, warningRisks, sortedRisks }: { criticalRisks: RiskEventDto[]; warningRisks: RiskEventDto[]; sortedRisks: RiskEventDto[] }) {
  const hasEvents = sortedRisks.length > 0

  if (!hasEvents) {
    return (
      <section className="live-status-strip is-quiet">
        <span className="badge badge-ok">Stabil</span>
        <span>Keine kritischen RiskEvents.</span>
      </section>
    )
  }

  return (
    <section className={classNames('live-alarm-band', criticalRisks.length > 0 && 'is-critical', criticalRisks.length === 0 && warningRisks.length > 0 && 'is-warning')}>
      <div>
        <div className="live-kicker">Alarme</div>
        <div className="live-alarm-title">
          {criticalRisks.length > 0
            ? `${criticalRisks.length} kritische RiskEvents`
            : warningRisks.length > 0
              ? `${warningRisks.length} Warnungen offen`
              : `${sortedRisks.length} Hinweise offen`}
        </div>
      </div>
      <div className="live-alarm-list">
        {sortedRisks.slice(0, 3).map((event) => (
          <div key={event.id} className="live-alarm-item">
            <span className={classNames('badge', event.severity === 'Critical' ? 'badge-crit' : event.severity === 'Warning' ? 'badge-warn' : 'badge-info')}>
              {event.severity}
            </span>
            <span>{event.title}</span>
          </div>
        ))}
      </div>
      <Link className="btn action-primary" to="/hardware">Hardware öffnen</Link>
    </section>
  )
}

function LiveTentCard({ tent, live }: { tent: TentDto; live: TentLivePayload | undefined }) {
  const groups = buildMetricGroups(live?.metrics ?? [])

  return (
    <article className={classNames('live-card live-card-separated', live?.stateTone === 'critical' && 'is-critical', live?.stateTone === 'attention' && 'is-warning')}>
      <div className="live-card-header">
        <div>
          <div className="live-card-title">{tent.name}</div>
          <div className="live-card-meta">{formatTentType(tent.tentType)} · {formatTentSize(tent)} · {formatTentActivity(tent)}</div>
        </div>
        <span className={classNames('badge', live?.stateTone === 'critical' ? 'badge-crit' : live?.stateTone === 'attention' ? 'badge-warn' : live ? 'badge-ok' : 'badge-neutral')}>
          {live?.stateLabel ?? 'offline'}
        </span>
      </div>

      {groups.map((group) => (
        <section key={group.title} className="live-metric-section">
          <div className="live-metric-section-header">
            <h3>{group.title}</h3>
            <p>{group.description}</p>
          </div>
          <div className="live-metric-grid live-metric-grid-compact">
            {group.metrics.map((metric) => (
              <MetricTile key={metric.key} metric={metric} />
            ))}
          </div>
        </section>
      ))}

      <div className="live-card-footer">
        <span>Aktualisiert {formatDateTime(live?.refreshedAtUtc)}</span>
        <Link to={`/zelte/${tent.id}`} className="btn">Zelt öffnen</Link>
      </div>
    </article>
  )
}

function MetricTile({ metric }: { metric: MetricPayload }) {
  return (
    <div className={classNames('live-metric', metric.tone === 'danger' && 'is-critical', metric.tone === 'warning' && 'is-warning', metric.tone === 'success' && 'is-ok')}>
      <div className="live-metric-label">{metric.label}</div>
      <div className="live-metric-value">{metric.value}</div>
      <div className="live-metric-unit">{metric.hint ?? metric.unit ?? ' '}</div>
    </div>
  )
}

function buildMetricGroups(metrics: MetricPayload[]): MetricGroup[] {
  return [
    {
      title: 'Zelt / Umgebung',
      description: 'Klima, Licht und Transpiration.',
      metrics: mapMetricDefinitions(metrics, tentMetricDefinitions),
    },
    {
      title: 'RDWC/DWC / Reservoir',
      description: 'Nährlösung, Sauerstoff und Wasserstand.',
      metrics: mapMetricDefinitions(metrics, reservoirMetricDefinitions),
    },
  ]
}

function mapMetricDefinitions(metrics: MetricPayload[], definitions: MetricDefinition[]): MetricPayload[] {
  const byKey = new Map(metrics.map((metric) => [metric.key, metric]))
  return definitions.map((definition) => {
    const mapped = byKey.get(definition.key)
    return mapped ? normalizeMetric(mapped, definition) : createMissingMetric(definition)
  })
}

function normalizeMetric(metric: MetricPayload, definition: MetricDefinition): MetricPayload {
  return {
    ...metric,
    label: definition.label,
    unit: metric.unit ?? definition.unit,
  }
}

function createMissingMetric(definition: MetricDefinition): MetricPayload {
  return {
    key: definition.key,
    label: definition.label,
    value: '–',
    unit: definition.unit,
    tone: 'neutral',
    hint: 'Kein Wert vorhanden',
  }
}

function findPrimaryCamera(tents: TentDto[], liveByTentId: Record<number, TentLivePayload>): { tent: TentDto; live: TentLivePayload } | null {
  for (const tent of tents) {
    const live = liveByTentId[tent.id]
    if (live?.cameraUrl) {
      return { tent, live }
    }
  }

  return null
}

function formatTentActivity(tent: TentDto): string {
  const parts: string[] = []
  if (tent.activeGrowCount > 0) {
    parts.push(`${tent.activeGrowCount} ${tent.activeGrowCount === 1 ? 'Grow' : 'Grows'}`)
  }
  if (tent.activeSetupCount > 0) {
    parts.push(`${tent.activeSetupCount} ${tent.activeSetupCount === 1 ? 'Setup' : 'Setups'}`)
  }
  return parts.length > 0 ? parts.join(' · ') : 'Keine aktive Nutzung'
}

function formatTentSize(tent: TentDto): string {
  if (!tent.widthCm && !tent.depthCm && !tent.tentHeightCm) {
    return 'Größe offen'
  }

  const width = tent.widthCm ?? '–'
  const depth = tent.depthCm ?? '–'
  const height = tent.tentHeightCm ?? '–'
  return `${width}×${depth}×${height} cm`
}

function formatTentType(value: string): string {
  switch (value) {
    case 'Flower': return 'Blüte'
    case 'Mother': return 'Mutter'
    case 'Propagation': return 'Anzucht'
    case 'Quarantine': return 'Quarantäne'
    case 'MultiPurpose': return 'Mehrzweck'
    default: return value
  }
}

function formatApiError(caught: unknown, fallback: string): string {
  if (caught instanceof ApiRequestError) {
    return caught.message
  }

  return caught instanceof Error ? caught.message : fallback
}

export default LiveDashboardPage
