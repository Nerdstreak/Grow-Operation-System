import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { resolveUrl } from '../../base'
import type { GrowSummary, MetricPayload, RiskEventDto, TentDto } from '../../types'
import { formatDateTime } from '../../utils'
import { buildScore, buildSensorStatus, formatGrowHydroMedium, formatGrowStatus, formatTentType } from './live-model'
import './live-instrument.css'

type DesktopLiveDashboardProps = {
  loading: boolean
  tents: TentDto[]
  selectedTentId: number | null
  onSelectTent: (tentId: number) => void
  selectedTent: TentDto | null
  primaryGrow: GrowSummary | null
  growsForTent: GrowSummary[]
  score: ReturnType<typeof buildScore>
  climateMetrics: MetricPayload[]
  hydroMetrics: MetricPayload[]
  lightMetric: MetricPayload | null
  sensorStatus: ReturnType<typeof buildSensorStatus>
  hasHydroGrow: boolean
  risksForContext: RiskEventDto[]
  issues: string[]
  lastUpdated: number | null
  onRefresh: () => void
}

const GAUGE_CIRC = 2 * Math.PI * 69

function toneColor(tone: string) {
  return tone === 'ok' ? 'var(--ix-phos)' : tone === 'warn' ? 'var(--ix-amber)' : tone === 'critical' ? 'var(--ix-red)' : 'var(--ix-faint)'
}
function toneBadge(tone: string) {
  return tone === 'ok' ? 'ix-b-ok' : tone === 'warn' ? 'ix-b-warn' : tone === 'critical' ? 'ix-b-crit' : 'ix-b-neutral'
}
function metricClass(metric: MetricPayload) {
  if (!metric.value || metric.value === '–') return 'ix-m muted'
  if (metric.tone === 'danger') return 'ix-m crit'
  if (metric.tone === 'warning') return 'ix-m warn'
  return 'ix-m ok'
}

function Metric({ metric }: { metric: MetricPayload }) {
  return (
    <div className={metricClass(metric)}>
      <span className="pip" />
      <div className="lab">{metric.label}</div>
      <div className="val">{metric.value}{metric.unit && <u>{metric.unit}</u>}</div>
      <div className="bar"><i /></div>
    </div>
  )
}

function formatClock(value: number | string | null): string {
  if (value == null) return '—'
  const date = typeof value === 'number' ? new Date(value) : new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('de-DE', { hour: '2-digit', minute: '2-digit', second: '2-digit' }).format(date)
}

// Fetches the proxied camera frame as a blob so it can read the capture-time and
// live/stale headers, and keeps the previous frame on a failed refresh so the
// grow is never blank once a valid image has been seen.
function CameraScreen({ tent }: { tent: TentDto }) {
  const [src, setSrc] = useState<string | null>(null)
  const [meta, setMeta] = useState<{ capturedAt: string | null; live: boolean } | null>(null)
  const [unavailable, setUnavailable] = useState(false)
  const urlRef = useRef<string | null>(null)

  // Near-live: schedule the next frame ~1s after the previous one *completes*, so a
  // slow (cold-start) camera never has its in-flight request aborted by a fixed timer
  // — it just refreshes a little less often instead of never showing an image.
  useEffect(() => {
    if (!tent.cameraEntityId) return
    let active = true
    let timer: number | undefined

    async function loop() {
      try {
        const response = await fetch(resolveUrl(`/api/live/tents/${tent.id}/camera?t=${Date.now()}`))
        if (!active) return
        if (response.ok) {
          const blob = await response.blob()
          if (!active) return
          const next = URL.createObjectURL(blob)
          if (urlRef.current) URL.revokeObjectURL(urlRef.current)
          urlRef.current = next
          setSrc(next)
          setMeta({ capturedAt: response.headers.get('X-Camera-Captured-At'), live: response.headers.get('X-Camera-Live') !== 'false' })
          setUnavailable(false)
        } else if (!urlRef.current) {
          setUnavailable(true)
        }
      } catch {
        if (active && !urlRef.current) setUnavailable(true)
      } finally {
        if (active) timer = window.setTimeout(loop, 1000)
      }
    }

    void loop()
    return () => { active = false; if (timer !== undefined) window.clearTimeout(timer) }
  }, [tent.id, tent.cameraEntityId])

  useEffect(() => () => { if (urlRef.current) URL.revokeObjectURL(urlRef.current) }, [])

  if (!tent.cameraEntityId) {
    return <div className="ix-screen"><div className="ico">⬡</div><div className="l">Kein Stream gemappt</div></div>
  }
  if (!src && unavailable) {
    return <div className="ix-screen"><div className="ico">⬡</div><div className="l">Kamera nicht erreichbar</div></div>
  }
  return (
    <div className="ix-screen ix-screen-cam">
      {src && <img src={src} alt={`Kamera ${tent.name}`} />}
      {meta && <div className={`ix-cam-stamp${meta.live ? '' : ' stale'}`} data-audit="camera-stamp">{meta.live ? '● LIVE' : '○ veraltet'} · {formatClock(meta.capturedAt)}</div>}
    </div>
  )
}

export function LiveDashboard({
  loading,
  tents,
  selectedTentId,
  onSelectTent,
  selectedTent,
  primaryGrow,
  growsForTent,
  score,
  climateMetrics,
  hydroMetrics,
  lightMetric,
  sensorStatus,
  hasHydroGrow,
  risksForContext,
  issues,
  lastUpdated,
  onRefresh,
}: DesktopLiveDashboardProps) {
  const [arc, setArc] = useState(0)
  useEffect(() => {
    const handle = window.setTimeout(() => setArc(score.value), 80)
    return () => window.clearTimeout(handle)
  }, [score.value])

  const TopBar = (
    <div className="ix-top ix-rise">
      <div className="ix-brand"><span className="dot" /><b>GROW OS</b></div>
      <div className="ix-tents">
        {tents.map((tent) => (
          <button
            key={tent.id}
            type="button"
            className={`ix-tent ${tent.id === selectedTentId ? 'on' : ''}`}
            onClick={() => onSelectTent(tent.id)}
          >
            {tent.name} · {formatTentType(tent.tentType)}
          </button>
        ))}
      </div>
      <div className="ix-livechip"><span className="rec" />LIVE{lastUpdated ? ` · ${formatClock(lastUpdated)}` : ''}</div>
    </div>
  )

  if (loading) {
    return (
      <div className="ix-live" data-audit="live-dashboard-desktop">
        {TopBar}
        <div className="ix-empty-state ix-panel"><h2>Lade Live-Daten…</h2></div>
      </div>
    )
  }

  if (!selectedTent || !primaryGrow) {
    return (
      <div className="ix-live" data-audit="live-dashboard-desktop">
        {TopBar}
        <div className="ix-empty-state ix-panel ix-rise ix-d1">
          <h2>Noch kein aktiver Grow</h2>
          <p>Neu hier? Die „Erste Schritte" führen dich durch Setup und Funktionen.</p>
          <div className="ix-empty-actions">
            <Link className="ix-btn pri" to="/start">Erste Schritte</Link>
            <Link className="ix-btn" to="/grows/new">Grow anlegen</Link>
            <Link className="ix-btn" to="/zelte">Zelt anlegen</Link>
          </div>
        </div>
      </div>
    )
  }

  const gaugeColor = toneColor(score.tone)

  return (
    <div className="ix-live" data-audit="live-dashboard-desktop">
      {TopBar}

      <section className="ix-hero">
        <div className="ix-panel ix-status ix-rise ix-d1" data-audit="live-status-card">
          <span className="ix-corner ix-tl" /><span className="ix-corner ix-tr" />
          <span className="ix-corner ix-bl" /><span className="ix-corner ix-br" />
          <div className="ix-gauge">
            <svg width="158" height="158" viewBox="0 0 158 158" preserveAspectRatio="xMidYMid meet">
              <circle className="ring-bg" cx="79" cy="79" r="69" />
              <circle
                className="ring"
                cx="79"
                cy="79"
                r="69"
                transform="rotate(-90 79 79)"
                style={{ stroke: gaugeColor, strokeDasharray: GAUGE_CIRC, strokeDashoffset: GAUGE_CIRC * (1 - arc / 100), filter: `drop-shadow(0 0 8px ${gaugeColor})` }}
              />
            </svg>
            <div className="mid">
              <b>{score.value}</b>
              <em style={{ color: gaugeColor }}>{score.label}</em>
            </div>
          </div>
          <div>
            <div className="ix-kick">Live-Status · {selectedTent.name}</div>
            <h1>{primaryGrow.name}</h1>
            <div className="sub">{formatGrowHydroMedium(primaryGrow)} · {primaryGrow.latestStage ?? formatGrowStatus(primaryGrow.status)}</div>
            <div className="ix-facts">
              <div><span>Hydro</span><strong>{formatGrowHydroMedium(primaryGrow)}</strong></div>
              <div><span>Phase</span><strong>{primaryGrow.latestStage ?? 'offen'}</strong></div>
              <div><span>Status</span><strong>{formatGrowStatus(primaryGrow.status)}</strong></div>
              <div><span>Letzte Messung</span><strong>{formatDateTime(primaryGrow.latestMeasurementAt)}</strong></div>
            </div>
          </div>
        </div>

        <div className="ix-panel ix-sensor ix-rise ix-d2" data-audit="live-sensor-card">
          <span className="ix-corner ix-tl" /><span className="ix-corner ix-br" />
          <div className="ix-sensor-head">
            <div><div className="ix-kick">Sensorstatus</div><h2>{sensorStatus.label}</h2></div>
            <span className={`ix-badge ${toneBadge(sensorStatus.tone)}`}>{score.label}</span>
          </div>
          <p>{sensorStatus.text}</p>
          <div className="ix-actions">
            <Link className="ix-btn pri" to="/messung">Messung erfassen</Link>
            {hasHydroGrow && <Link className="ix-btn" to={`/grows/${primaryGrow.id}/addback`}>Addback starten</Link>}
            <button type="button" className="ix-btn" onClick={onRefresh}>Aktualisieren</button>
          </div>
        </div>
      </section>

      <section className="ix-clusters">
        <div className="ix-panel ix-cluster ix-rise ix-d3" data-audit="live-climate-card">
          <div className="ix-cluster-head"><div className="t"><span className="ix-kick">Sektion 01</span><h3>Klima</h3></div></div>
          <div className="ix-grid-3">
            {climateMetrics.map((metric) => <Metric key={metric.key} metric={metric} />)}
            {lightMetric && <Metric metric={{ ...lightMetric, label: lightMetric.key === 'ppfd' ? 'PPFD' : 'Licht' }} />}
          </div>
        </div>

        {hasHydroGrow && (
          <div className="ix-panel ix-cluster ix-rise ix-d4" data-audit="live-hydro-card">
            <div className="ix-cluster-head"><div className="t"><span className="ix-kick">Sektion 02</span><h3>Reservoir</h3></div></div>
            <div className="ix-grid-3">
              {hydroMetrics.map((metric) => <Metric key={metric.key} metric={metric} />)}
            </div>
          </div>
        )}
      </section>

      <section className="ix-lower">
        <div className="ix-panel ix-alerts ix-rise ix-d5" data-audit="live-risk-card">
          <div className="ix-alerts-head">
            <h3>Risiken · offen</h3>
            {risksForContext.length > 0 && <span className="ix-badge ix-b-crit">{risksForContext.length} aktiv</span>}
          </div>
          {risksForContext.length === 0 ? (
            <div className="ix-empty-line">Keine offenen Risiken für diesen Grow-Kontext.</div>
          ) : (
            risksForContext.slice(0, 5).map((risk) => (
              <Link key={risk.id} className={`ix-alert ${risk.severity === 'Critical' ? 'crit' : 'warn'}`} to={risk.growId ? `/grows/${risk.growId}?section=diagnosis` : '/aufgaben'}>
                <div className="sev" />
                <div>
                  <div className="ttl">{risk.title}</div>
                  <div className="meta">{risk.description ?? risk.eventType}</div>
                </div>
                <div className="go">→</div>
              </Link>
            ))
          )}
        </div>

        <div className="ix-panel ix-feed ix-rise ix-d6" data-audit="live-camera-card">
          <h3>Kamera · {selectedTent.name}</h3>
          <CameraScreen key={selectedTent.id} tent={selectedTent} />
        </div>
      </section>

      <div className="ix-rise ix-d6">
        <div className="ix-grows-head"><h3>Aktive Grows · {selectedTent.name}</h3><Link className="ix-btn" to="/grows/new">Grow anlegen</Link></div>
        {growsForTent.map((grow) => (
          <Link key={grow.id} className="ix-grow-row" to={`/grows/${grow.id}`}>
            <strong>{grow.name}</strong>
            <span>{formatGrowHydroMedium(grow)}</span>
            <em>{grow.latestStage ?? grow.status}</em>
          </Link>
        ))}
      </div>

      {issues.length > 0 && (
        <div className="ix-empty-line">⚠ Teilweise offline: {issues.slice(0, 3).join(' · ')}</div>
      )}
    </div>
  )
}
