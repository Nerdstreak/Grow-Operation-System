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

type MetricDefinition = {
  key: string
  label: string
  unit: string | null
}

const coreMetricDefinitions: MetricDefinition[] = [
  { key: 'temperature', label: 'Temperatur', unit: '°C' },
  { key: 'humidity', label: 'Luftfeuchte', unit: '%' },
  { key: 'vpd', label: 'VPD', unit: 'kPa' },
  { key: 'reservoir-ph', label: 'pH', unit: null },
  { key: 'reservoir-ec', label: 'EC', unit: 'mS/cm' },
  { key: 'orp', label: 'ORP', unit: 'mV' },
  { key: 'dissolved-oxygen', label: 'DO', unit: 'mg/L' },
  { key: 'reservoir-temp', label: 'Wasser °C', unit: '°C' },
]

const optionalMetricDefinitions: MetricDefinition[] = [
  { key: 'ppfd', label: 'PPFD', unit: 'µmol/m²/s' },
  { key: 'co2', label: 'CO2', unit: 'ppm' },
  { key: 'light-cycle', label: 'Lichtstatus', unit: null },
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

  return (
    <>
      <div className="topbar">
        <span className="topbar-title">Live Dashboard</span>
        <div className="topbar-right">
          <span className="text-muted" style={{ fontSize: 12 }}>Refresh {formatDateTime(data.refreshedAtUtc)}</span>
          <button type="button" className="btn btn-primary" onClick={() => setRefreshTick((current) => current + 1)} disabled={loading}>
            {loading ? 'Aktualisiert...' : 'Aktualisieren'}
          </button>
        </div>
      </div>

      <div className="page-scroll">
        <div className="live-dashboard">
          <section className={classNames('live-alarm-band', criticalRisks.length > 0 && 'is-critical', criticalRisks.length === 0 && warningRisks.length > 0 && 'is-warning')}>
            <div>
              <div className="live-kicker">Alarme</div>
              <div className="live-alarm-title">
                {criticalRisks.length > 0
                  ? `${criticalRisks.length} kritische RiskEvents`
                  : warningRisks.length > 0
                    ? `${warningRisks.length} Warnungen offen`
                    : 'Keine kritischen RiskEvents'}
              </div>
            </div>
            {sortedRisks.length > 0 ? (
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
            ) : (
              <div className="text-muted">System im Beobachtungsfenster stabil.</div>
            )}
            <Link className="btn action-primary" to="/hardware">Hardware öffnen</Link>
          </section>

          {data.issues.length > 0 && (
            <div className="alert-bar">
              <div className="alert-dot" />
              <strong>Teilweise geladen</strong>
              <span>{data.issues.map((issue) => `${issue.area}: ${issue.message}`).join(' | ')}</span>
            </div>
          )}

          <section>
            <div className="section-label">Tent Live Grid</div>
            {loading && data.tents.length === 0 ? (
              <div className="empty-hint">Lade Live-Dashboard...</div>
            ) : data.tents.length === 0 ? (
              <div className="empty-hint">Keine aktiven Zelte oder Setups gefunden.</div>
            ) : (
              <div className="live-grid">
                {data.tents.map((tent) => (
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
              <div className="live-grow-grid">
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
    </>
  )
}

function LiveTentCard({ tent, live }: { tent: TentDto; live: TentLivePayload | undefined }) {
  const orderedMetrics = buildDashboardMetrics(live?.metrics ?? [])

  return (
    <article className={classNames('live-card', live?.stateTone === 'critical' && 'is-critical', live?.stateTone === 'attention' && 'is-warning')}>
      <div className="live-card-header">
        <div>
          <div className="live-card-title">{tent.name}</div>
          <div className="live-card-meta">{tent.tentType} · {formatTentActivity(tent)}</div>
        </div>
        <span className={classNames('badge', live?.stateTone === 'critical' ? 'badge-crit' : live?.stateTone === 'attention' ? 'badge-warn' : live ? 'badge-ok' : 'badge-neutral')}>
          {live?.stateLabel ?? 'offline'}
        </span>
      </div>

      <div className="live-metric-grid">
        {orderedMetrics.map((metric) => (
          <MetricTile key={metric.key} metric={metric} />
        ))}
      </div>

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

function buildDashboardMetrics(metrics: MetricPayload[]): MetricPayload[] {
  const byKey = new Map(metrics.map((metric) => [metric.key, metric]))
  const coreMetrics = coreMetricDefinitions.map((definition) => {
    const mapped = byKey.get(definition.key)
    return mapped ? normalizeMetric(mapped, definition) : createMissingMetric(definition)
  })
  const optionalMetrics = optionalMetricDefinitions
    .map((definition) => {
      const mapped = byKey.get(definition.key)
      return mapped ? normalizeMetric(mapped, definition) : null
    })
    .filter((metric): metric is MetricPayload => metric !== null)
  const knownKeys = new Set([...coreMetricDefinitions, ...optionalMetricDefinitions].map((definition) => definition.key))
  const otherMetrics = metrics.filter((metric) => !knownKeys.has(metric.key))

  return [...coreMetrics, ...optionalMetrics, ...otherMetrics]
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

function formatApiError(caught: unknown, fallback: string): string {
  if (caught instanceof ApiRequestError) {
    return caught.message
  }

  return caught instanceof Error ? caught.message : fallback
}

export default LiveDashboardPage
