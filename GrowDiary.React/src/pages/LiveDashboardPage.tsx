import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, MetricPayload, RiskEventDto, TentDto, TentLivePayload } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1LinkButton, V1Page, V1Section, V1Tabs } from '../components/v1'
import { formatDateTime, formatSeverityLabel } from '../utils'

type LiveState = {
  tents: TentDto[]
  liveByTentId: Record<number, TentLivePayload>
  grows: GrowSummary[]
  risks: RiskEventDto[]
  issues: string[]
}

const initialState: LiveState = { tents: [], liveByTentId: {}, grows: [], risks: [], issues: [] }

const climateMetricKeys = [
  ['temperature', 'Luft', '°C'],
  ['humidity', 'RLF', '%'],
  ['vpd', 'VPD', 'kPa'],
] as const

const hydroMetricKeys = [
  ['reservoir-ph', 'pH', null],
  ['reservoir-ec', 'EC', 'mS/cm'],
  ['orp', 'ORP', 'mV'],
  ['dissolved-oxygen', 'DO', 'mg/L'],
  ['reservoir-temp', 'Wassertemp.', '°C'],
  ['reservoir-level', 'Wasserstand', null],
] as const

function LiveDashboardPage() {
  const [state, setState] = useState<LiveState>(initialState)
  const [selectedTentId, setSelectedTentId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [refresh, setRefresh] = useState(0)
  const isPhoneViewport = useIsPhoneViewport()

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
    }
    void load()
    return () => controller.abort()
  }, [refresh])

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
  const pageTitle = selectedTent && primaryGrow ? 'Live Dashboard' : selectedTent ? score.label : 'Live'

  return (
    <V1Page eyebrow="Live" title={pageTitle} className="v1-live-page rc2-live-page">
      {state.issues.length > 0 && <V1Alert title="Teilweise offline" message={state.issues.slice(0, 3).join(' · ')} tone="warn" />}

      {state.tents.length > 1 && (
        <V1Tabs label="Zelt auswählen" active={selectedTent?.id ?? state.tents[0].id} onChange={(id) => setSelectedTentId(Number(id))} items={state.tents.map((tent) => ({ value: tent.id, label: tent.name, meta: formatTentType(tent.tentType) }))} />
      )}

      <div data-audit="live-screen">
        {isPhoneViewport ? (
          <div className="live-mobile-stack" data-audit="live-dashboard-mobile">
            {loading ? (
              <V1Empty title="Lade Live-Daten..." />
            ) : !selectedTent || !primaryGrow ? (
              <div className="live-mobile-empty" data-audit="live-empty-state">
                <V1Empty
                  title="Noch kein aktiver Grow"
                  text="Lege einen Grow an oder richte zuerst ein Zelt ein."
                  action={(
                    <div className="live-empty-actions">
                      <V1LinkButton to="/grows/new" variant="primary">Grow anlegen</V1LinkButton>
                      <V1LinkButton to="/zelte">Zelt anlegen</V1LinkButton>
                    </div>
                  )}
                />
              </div>
            ) : selectedTent && primaryGrow ? (
              <>
                <div data-audit="live-status-card">
                  <V1Card className="live-mobile-card live-status-card" tone={score.tone}>
                    <div className="v1-card-title-row">
                      <div><span className="v1-card-kicker">Aktuell</span><h2>{selectedTent.name}</h2></div>
                      <V1Badge tone={score.tone}>{score.label}</V1Badge>
                    </div>
                    <div className="live-status-summary">
                      <div><span>Grow</span><strong>{primaryGrow.name}</strong></div>
                      <div><span>Status</span><strong>{formatGrowStatus(primaryGrow.status)}</strong></div>
                      <div><span>Hydro</span><strong>{formatGrowHydroMedium(primaryGrow)}</strong></div>
                      <div><span>Phase</span><strong>{primaryGrow.latestStage ?? 'offen'}</strong></div>
                      <div><span>Letzte Messung</span><strong>{formatDateTime(primaryGrow.latestMeasurementAt)}</strong></div>
                    </div>
                  </V1Card>
                </div>

                <div data-audit="live-climate-card">
                  <V1Card className="live-mobile-card live-climate-card">
                    <div className="live-card-head">
                      <span className="v1-card-kicker">Klima</span>
                      <h2>Zustand</h2>
                    </div>
                    <div className="live-mobile-metric-grid">
                      {climateMetrics.map((metric) => <LiveMetric key={metric.key} metric={metric} />)}
                      {lightMetric && <LiveMetric metric={{ ...lightMetric, label: lightMetric.key === 'ppfd' ? 'PPFD' : 'Licht' }} />}
                    </div>
                  </V1Card>
                </div>

                {hasHydroGrow && (
                  <div data-audit="live-hydro-card">
                    <V1Card className="live-mobile-card live-hydro-card">
                      <div className="live-card-head">
                        <span className="v1-card-kicker">Reservoir</span>
                        <h2>Hydro-Werte</h2>
                      </div>
                      <div className="live-mobile-metric-grid">
                        {hydroMetrics.map((metric) => <LiveMetric key={metric.key} metric={metric} />)}
                      </div>
                    </V1Card>
                  </div>
                )}

                <div data-audit="live-sensor-card">
                  <V1Card className="live-mobile-card live-sensor-card" tone={sensorStatus.tone}>
                    <div className="live-card-head">
                      <span className="v1-card-kicker">Sensorstatus</span>
                      <h2>{sensorStatus.label}</h2>
                    </div>
                    <p>{sensorStatus.text}</p>
                  </V1Card>
                </div>

                <RiskSummaryCard risks={risksForContext} />

                {selectedTent.cameraEntityId ? <CameraTile tent={selectedTent} refresh={refresh} /> : <CameraEmptyState tent={selectedTent} />}

                <div className="live-quick-actions" data-audit="live-quick-actions">
                  <V1LinkButton to="/messung" variant="primary">Messung erfassen</V1LinkButton>
                  {hasHydroGrow && <V1LinkButton to={`/grows/${primaryGrow.id}/addback`}>Addback starten</V1LinkButton>}
                  <V1Button onClick={() => setRefresh((current) => current + 1)}>Aktualisieren</V1Button>
                </div>

                <V1Section title={`Aktive Grows in ${selectedTent.name}`} action={<V1LinkButton to="/grows/new">Grow anlegen</V1LinkButton>}>
                  <div className="v1-list">
                    {growsForTent.map((grow) => <Link key={grow.id} className="v1-list-row" to={`/grows/${grow.id}`}><strong>{grow.name}</strong><span>{formatGrowHydroMedium(grow)}</span><em>{grow.latestStage ?? grow.status}</em></Link>)}
                  </div>
                </V1Section>
              </>
            ) : null}
          </div>
        ) : (
          <DesktopLiveDashboard
            loading={loading}
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
            refresh={refresh}
            onRefresh={() => setRefresh((current) => current + 1)}
          />
        )}
      </div>
    </V1Page>
  )
}

type DesktopLiveDashboardProps = {
  loading: boolean
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
  refresh: number
  onRefresh: () => void
}

function DesktopLiveDashboard({
  loading,
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
  refresh,
  onRefresh,
}: DesktopLiveDashboardProps) {
  if (loading) {
    return <V1Empty title="Lade Live-Daten..." />
  }

  if (!selectedTent || !primaryGrow) {
    return (
      <div className="live-desktop-dashboard" data-audit="live-dashboard-desktop">
        <V1Empty
          title="Noch kein aktiver Grow"
          text="Lege einen Grow an oder richte zuerst ein Zelt ein."
          action={(
            <div className="live-empty-actions">
              <V1LinkButton to="/grows/new" variant="primary">Grow anlegen</V1LinkButton>
              <V1LinkButton to="/zelte">Zelt anlegen</V1LinkButton>
            </div>
          )}
        />
      </div>
    )
  }

  return (
    <div className="live-desktop-dashboard" data-audit="live-dashboard-desktop">
      <section className="live-desktop-status-row">
        <div data-audit="live-status-card">
          <V1Card className="live-desktop-status-card" tone={score.tone}>
            <div className="v1-card-title-row">
              <div><span className="v1-card-kicker">Live-Status</span><h2>{selectedTent.name}</h2></div>
              <V1Badge tone={score.tone}>{score.label}</V1Badge>
            </div>
            <div className="live-status-summary">
              <div><span>Grow</span><strong>{primaryGrow.name}</strong></div>
              <div><span>Status</span><strong>{formatGrowStatus(primaryGrow.status)}</strong></div>
              <div><span>Hydro</span><strong>{formatGrowHydroMedium(primaryGrow)}</strong></div>
              <div><span>Phase</span><strong>{primaryGrow.latestStage ?? 'offen'}</strong></div>
              <div><span>Letzte Messung</span><strong>{formatDateTime(primaryGrow.latestMeasurementAt)}</strong></div>
            </div>
          </V1Card>
        </div>
      </section>

      <section className="live-desktop-main-grid">
        <div data-audit="live-climate-card">
          <V1Card className="live-desktop-climate-card">
            <div className="live-card-head">
              <span className="v1-card-kicker">Klima</span>
              <h2>Aktuelle Werte</h2>
            </div>
            <div className="live-desktop-metric-grid">
              {climateMetrics.map((metric) => <LiveMetric key={metric.key} metric={metric} />)}
              {lightMetric && <LiveMetric metric={{ ...lightMetric, label: lightMetric.key === 'ppfd' ? 'PPFD' : 'Licht' }} />}
            </div>
          </V1Card>
        </div>

        {hasHydroGrow && (
          <div data-audit="live-hydro-card">
            <V1Card className="live-desktop-hydro-card">
              <div className="live-card-head">
                <span className="v1-card-kicker">Reservoir</span>
                <h2>Hydro-Werte</h2>
              </div>
              <div className="live-desktop-metric-grid">
                {hydroMetrics.map((metric) => <LiveMetric key={metric.key} metric={metric} />)}
              </div>
            </V1Card>
          </div>
        )}

        <RiskSummaryCard risks={risksForContext} />

        <div data-audit="live-sensor-card">
          <V1Card className="live-desktop-control-card" tone={sensorStatus.tone}>
            <div className="live-card-head">
              <span className="v1-card-kicker">Sensorstatus</span>
              <h2>{sensorStatus.label}</h2>
            </div>
            <p>{sensorStatus.text}</p>
            <div className="live-quick-actions" data-audit="live-quick-actions">
              <V1LinkButton to="/messung" variant="primary">Messung erfassen</V1LinkButton>
              {hasHydroGrow && <V1LinkButton to={`/grows/${primaryGrow.id}/addback`}>Addback starten</V1LinkButton>}
              <V1Button onClick={onRefresh}>Aktualisieren</V1Button>
            </div>
          </V1Card>
        </div>

        {selectedTent.cameraEntityId ? <CameraTile tent={selectedTent} refresh={refresh} /> : <CameraEmptyState tent={selectedTent} />}
      </section>

      <V1Section title={`Aktive Grows in ${selectedTent.name}`} action={<V1LinkButton to="/grows/new">Grow anlegen</V1LinkButton>}>
        <div className="v1-list">
          {growsForTent.map((grow) => <Link key={grow.id} className="v1-list-row" to={`/grows/${grow.id}`}><strong>{grow.name}</strong><span>{formatGrowHydroMedium(grow)}</span><em>{grow.latestStage ?? grow.status}</em></Link>)}
        </div>
      </V1Section>
    </div>
  )
}

function RiskSummaryCard({ risks }: { risks: RiskEventDto[] }) {
  const critical = risks.filter((risk) => risk.severity === 'Critical').length
  const tone = critical > 0 ? 'warn' : risks.length > 0 ? 'neutral' : 'ok'
  return (
    <div data-audit="live-risk-card">
      <V1Card className="live-risk-card" tone={tone}>
        <div className="live-card-head">
          <span className="v1-card-kicker">Aufgaben / Risiken</span>
          <h2>{risks.length > 0 ? `${risks.length} offen` : 'Keine offenen Risiken'}</h2>
        </div>
        {risks.length === 0 ? (
          <p>Keine offenen RiskEvents für diesen Grow-Kontext.</p>
        ) : (
          <div className="live-risk-list">
            {risks.slice(0, 3).map((risk) => (
              <Link key={risk.id} to={risk.growId ? `/grows/${risk.growId}` : '/aufgaben'} className="live-risk-row">
                <strong>{risk.title}</strong>
                <span>{formatSeverityLabel(risk.severity)}</span>
              </Link>
            ))}
          </div>
        )}
        <V1LinkButton to="/aufgaben">Aufgaben öffnen</V1LinkButton>
      </V1Card>
    </div>
  )
}

function useIsPhoneViewport() {
  const [isPhone, setIsPhone] = useState(() => window.matchMedia('(max-width: 767px)').matches)

  useEffect(() => {
    const media = window.matchMedia('(max-width: 767px)')
    const update = () => setIsPhone(media.matches)
    update()
    media.addEventListener('change', update)
    return () => media.removeEventListener('change', update)
  }, [])

  return isPhone
}

function CameraEmptyState({ tent }: { tent: TentDto }) {
  return (
    <div className="live-camera-note" data-audit="live-camera-note">
      <V1Card className="live-camera-empty-card" tone="neutral">
        <div>
          <span className="v1-card-kicker">Kamera</span>
          <h2>Kamera nicht eingerichtet</h2>
          <p>{tent.name} hat aktuell keine Kamera-Entity im Home-Assistant-Mapping.</p>
        </div>
        <V1LinkButton to="/home-assistant">HA-Mapping</V1LinkButton>
      </V1Card>
    </div>
  )
}

function CameraTile({ tent, refresh }: { tent: TentDto; refresh: number }) {
  const [status, setStatus] = useState<'idle' | 'loading' | 'ready' | 'failed'>('idle')
  const [version, setVersion] = useState(0)
  const [lastGoodSrc, setLastGoodSrc] = useState<string | null>(null)
  const [lastGoodAt, setLastGoodAt] = useState<Date | null>(null)
  const src = tent.cameraEntityId ? `/api/live/tents/${tent.id}/camera?t=${refresh}-${version}` : null

  useEffect(() => {
    let active = true
    queueMicrotask(() => {
      if (!active) return
      if (!src) {
        setStatus('idle')
        setLastGoodSrc(null)
        setLastGoodAt(null)
        return
      }
      setStatus('loading')
    })
    return () => { active = false }
  }, [src])

  useEffect(() => {
    if (!tent.cameraEntityId) return
    const interval = window.setInterval(() => setVersion((current) => current + 1), 30000)
    return () => window.clearInterval(interval)
  }, [tent.cameraEntityId, tent.id])

  if (!src && !lastGoodSrc) {
    return (
      <div data-audit="live-camera">
        <V1Card className="v1-camera-empty is-compact live-camera-card" tone="neutral">
          <div><span className="v1-card-kicker">Kamera</span><h2>Nicht eingerichtet</h2><p>{tent.name}</p></div>
        </V1Card>
      </div>
    )
  }

  if (status === 'failed' && !lastGoodSrc) {
    return (
      <div data-audit="live-camera">
        <V1Card className="v1-camera-empty is-compact live-camera-card" tone="warn">
          <div><span className="v1-card-kicker">Kamera-Snapshot</span><h2>Snapshot nicht erreichbar</h2><p>{tent.cameraEntityId ?? tent.name}</p></div>
          <div className="v1-action-row"><V1Button onClick={() => { setVersion((current) => current + 1); setStatus('loading') }}>Neu laden</V1Button><V1LinkButton to="/home-assistant">HA öffnen</V1LinkButton></div>
        </V1Card>
      </div>
    )
  }

  const imageSrc = src ?? lastGoodSrc
  return (
    <div className="v1-camera-card rc2-camera-card live-camera-card" data-audit="live-camera">
      {status === 'loading' && !lastGoodSrc && <div className="v1-camera-loader">Snapshot lädt...</div>}
      {status === 'failed' && lastGoodSrc && <div className="v1-camera-loader">Letzter Snapshot · neuer Snapshot fehlgeschlagen</div>}
      {imageSrc && (
        <img
          src={imageSrc}
          alt={`${tent.name} Kamera-Snapshot`}
          onLoad={() => { setStatus('ready'); setLastGoodSrc(src); setLastGoodAt(new Date()) }}
          onError={() => setStatus('failed')}
          className={lastGoodSrc || status === 'ready' ? 'ready' : ''}
        />
      )}
      <div className="v1-camera-label"><strong>{tent.name}</strong><span>{lastGoodAt ? `Snapshot ${lastGoodAt.toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })}` : 'Snapshot'}</span></div>
    </div>
  )
}

function LiveMetric({ metric }: { metric: MetricPayload }) {
  const tone = metric.tone === 'danger' ? 'critical' : metric.tone === 'warning' ? 'warn' : metric.tone === 'success' ? 'ok' : 'neutral'
  const hasUnit = Boolean(metric.unit && metric.value !== '–')
  return (
    <div className={`live-mobile-metric tone-${tone}`} data-audit={`live-metric-${metric.key}`}>
      <span>{metric.label}</span>
      <strong className="live-metric-value">
        <span className="live-metric-number">{metric.value}</span>
        {hasUnit && <span className="live-metric-unit">{metric.unit}</span>}
      </strong>
      {metric.hint && <small>{metric.hint}</small>}
    </div>
  )
}

function mapMetrics(items: MetricPayload[], definitions: readonly (readonly [string, string, string | null])[]): MetricPayload[] {
  return definitions.map(([key, label, unit]) => {
    const found = items.find((item) => item.key === key)
    return found ? { ...found, label, unit: found.unit ?? unit } : { key, label, value: '–', unit, tone: 'muted', hint: null }
  })
}

function findMetric(items: MetricPayload[], keys: string[]) {
  return keys.map((key) => items.find((item) => item.key === key)).find((item): item is MetricPayload => Boolean(item)) ?? null
}

function riskRank(value: string) {
  return value === 'Critical' ? 0 : value === 'Warning' ? 1 : 2
}

function buildSensorStatus(live: TentLivePayload | undefined, issues: string[]) {
  if (issues.length > 0) return { label: 'Offline', text: 'Ein Teil der Live-Daten ist nicht erreichbar.', tone: 'warn' as const }
  if (!live) return { label: 'Nicht bewertet', text: 'Für dieses Zelt liegen noch keine Live-Werte vor.', tone: 'neutral' as const }
  const values = live.metrics.filter((metric) => metric.value && metric.value !== '–')
  if (values.length === 0) return { label: 'Nicht bewertet', text: 'Sensorwerte fehlen oder sind noch nicht gemappt.', tone: 'neutral' as const }
  const warnings = live.metrics.filter((metric) => metric.tone === 'warning' || metric.tone === 'danger').length
  return warnings > 0
    ? { label: 'Warnung', text: 'Mindestens ein Sensorwert braucht Aufmerksamkeit.', tone: 'warn' as const }
    : { label: 'Aktiv', text: `${values.length} Live-Werte werden ausgewertet.`, tone: 'ok' as const }
}

function buildScore(metrics: MetricPayload[], tent: TentDto | null) {
  const usable = metrics.filter((metric) => metric.value && metric.value !== '–').length
  if (!tent) return { value: 0, label: 'Einrichten', tone: 'neutral' as const }
  if (usable === 0) return { value: 0, label: 'Einrichten', tone: 'neutral' as const }
  const warnings = metrics.filter((metric) => metric.tone === 'warning' || metric.tone === 'danger').length
  const value = Math.max(0, Math.min(100, 100 - warnings * 18 - Math.max(0, 6 - usable) * 8))
  return value < 55 ? { value, label: 'Kritisch', tone: 'critical' as const } : value < 82 ? { value, label: 'Beobachten', tone: 'warn' as const } : { value, label: 'Stabil', tone: 'ok' as const }
}

function chooseInitialTent(tents: TentDto[], grows: GrowSummary[]) {
  const running = grows.find((grow) => grow.status === 'Running' && grow.tentId)
  return running?.tentId ?? tents[0]?.id ?? null
}

function formatTentType(value: string) {
  return value === 'Production' ? 'Blüte / Run' : value === 'Mother' ? 'Mutter' : value === 'Propagation' ? 'Anzucht' : value === 'Quarantine' ? 'Quarantäne' : value === 'MultiPurpose' ? 'Mehrzweck' : value
}

function formatGrowStatus(value: string) {
  return value === 'Running' ? 'aktiv' : value === 'Planning' ? 'geplant' : value === 'Harvested' ? 'geerntet' : value === 'Archived' ? 'archiviert' : value
}

function formatGrowHydroMedium(grow: GrowSummary) {
  return grow.hydroSetupName ?? (grow.hydroStyle === 'None' ? 'kein Hydro-Setup' : grow.hydroStyle)
}

function formatApiError(caught: unknown, fallback: string) {
  return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback
}

export default LiveDashboardPage
