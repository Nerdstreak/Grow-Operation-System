import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, MetricPayload, TentDto, TentLivePayload } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1LinkButton, V1Page, V1Section, V1Stat, V1Tabs } from '../components/v1'
import { formatDateTime } from '../utils'

type LiveState = {
  tents: TentDto[]
  liveByTentId: Record<number, TentLivePayload>
  grows: GrowSummary[]
  issues: string[]
}

const initialState: LiveState = { tents: [], liveByTentId: {}, grows: [], issues: [] }

const tentMetrics = [
  ['temperature', 'Luft', '°C'],
  ['humidity', 'RLF', '%'],
  ['vpd', 'VPD', 'kPa'],
  ['ppfd', 'PPFD', 'µmol/m²/s'],
  ['co2', 'CO₂', 'ppm'],
  ['light-cycle', 'Licht', null],
] as const

const hydroMetrics = [
  ['reservoir-ph', 'pH', null],
  ['reservoir-ec', 'EC', 'mS/cm'],
  ['reservoir-temp', 'Wasser', '°C'],
  ['reservoir-level', 'Level', 'L/cm'],
  ['orp', 'ORP', 'mV'],
  ['dissolved-oxygen', 'DO', 'mg/L'],
] as const

function LiveDashboardPage() {
  const [state, setState] = useState<LiveState>(initialState)
  const [selectedTentId, setSelectedTentId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [refresh, setRefresh] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      const issues: string[] = []
      const safe = async <T,>(name: string, path: string, fallback: T): Promise<T> => {
        try { return await apiFetch<T>(path, { signal: controller.signal }) }
        catch (caught) { if (!controller.signal.aborted) issues.push(`${name}: ${formatApiError(caught, 'nicht erreichbar')}`); return fallback }
      }

      const [tents, grows] = await Promise.all([
        safe<TentDto[]>('Zelte', '/api/settings/tents', []),
        safe<GrowSummary[]>('Grows', '/api/grows?archived=false', []),
      ])

      const sorted = [...tents].sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name))
      const livePairs = await Promise.all(sorted.map(async (tent) => {
        try { return [tent.id, await apiFetch<TentLivePayload>(`/api/live/tents/${tent.id}`, { signal: controller.signal })] as const }
        catch { return [tent.id, null] as const }
      }))

      if (controller.signal.aborted) return
      setState({ tents: sorted, grows, liveByTentId: Object.fromEntries(livePairs.filter((pair): pair is readonly [number, TentLivePayload] => pair[1] !== null)), issues })
      setSelectedTentId((current) => current ?? chooseInitialTent(sorted, grows))
      setLoading(false)
    }
    void load()
    return () => controller.abort()
  }, [refresh])

  const selectedTent = state.tents.find((tent) => tent.id === selectedTentId) ?? state.tents[0] ?? null
  const live = selectedTent ? state.liveByTentId[selectedTent.id] : undefined
  const growsForTent = selectedTent ? state.grows.filter((grow) => (grow.status === 'Running' || grow.status === 'Planning') && grow.tentId === selectedTent.id) : []
  const primaryGrow = growsForTent[0] ?? null
  const score = buildScore(live?.metrics ?? [], selectedTent)

  return (
    <V1Page eyebrow="Live" title={score.label} className="v1-live-page rc2-live-page" action={<V1Button variant="primary" onClick={() => setRefresh((current) => current + 1)}>Aktualisieren</V1Button>}>
      {state.issues.length > 0 && <V1Alert title="Teilweise offline" message={state.issues.slice(0, 3).join(' · ')} tone="warn" />}

      {state.tents.length > 1 && (
        <V1Tabs label="Zelt auswählen" active={selectedTent?.id ?? state.tents[0].id} onChange={(id) => setSelectedTentId(Number(id))} items={state.tents.map((tent) => ({ value: tent.id, label: tent.name, meta: formatTentType(tent.tentType) }))} />
      )}

      {!selectedTent ? <V1Empty title={loading ? 'Lade Live-Daten...' : 'Noch kein Zelt'} action={<V1LinkButton to="/zelte" variant="primary">Zelt anlegen</V1LinkButton>} /> : (
        <>
          <section className="v1-live-hero-grid">
            <CameraTile tent={selectedTent} refresh={refresh} />
            <V1Card className="v1-live-now-card" tone={score.tone}>
              <div className="v1-card-title-row">
                <div><span className="v1-card-kicker">{selectedTent.name}</span><h2>{primaryGrow?.name ?? 'Kein aktiver Grow in diesem Zelt'}</h2></div>
                <V1Badge tone={score.tone}>{score.label}</V1Badge>
              </div>
              <div className="rc2-score-line"><strong>{score.value}</strong><span>%</span></div>
              <div className="v1-now-list">
                <Row label="Zelt" value={formatTentType(selectedTent.tentType)} />
                <Row label="Grow" value={primaryGrow?.strain ?? 'kein Grow'} />
                <Row label="Phase" value={primaryGrow?.latestStage ?? 'offen'} />
                <Row label="Messung" value={formatDateTime(primaryGrow?.latestMeasurementAt)} />
              </div>
              <div className="v1-action-row">
                {primaryGrow ? <V1LinkButton to={`/grows/${primaryGrow.id}/addback`} variant="primary">Addback</V1LinkButton> : <V1LinkButton to="/grows/new" variant="primary">Grow starten</V1LinkButton>}
                <V1LinkButton to="/messung">Messung</V1LinkButton>
                <V1LinkButton to="/home-assistant">HA</V1LinkButton>
              </div>
            </V1Card>
          </section>

          <div className="v1-live-metrics-pair">
            <V1Section title="Zelt"><div className="v1-metric-grid compact">{mapMetrics(live?.metrics ?? [], tentMetrics).map((metric) => <MetricCard key={metric.key} metric={metric} />)}</div></V1Section>
            <V1Section title="RDWC/DWC"><div className="v1-metric-grid compact">{mapMetrics(live?.metrics ?? [], hydroMetrics).map((metric) => <MetricCard key={metric.key} metric={metric} />)}</div></V1Section>
          </div>

          <V1Section title={`Aktive Grows in ${selectedTent.name}`} action={<V1LinkButton to="/grows/new">Starten</V1LinkButton>}>
            {growsForTent.length === 0 ? <V1Empty title="Kein Grow in diesem Zelt" text="Beim Zeltwechsel zeigt diese Liste nur noch Grows des ausgewählten Zelts." /> : (
              <div className="v1-list">
                {growsForTent.map((grow) => <Link key={grow.id} className="v1-list-row" to={`/grows/${grow.id}`}><strong>{grow.name}</strong><span>{grow.strain ?? 'Sorte offen'}</span><em>{grow.latestStage ?? grow.status}</em></Link>)}
              </div>
            )}
          </V1Section>
        </>
      )}
    </V1Page>
  )
}

function CameraTile({ tent, refresh }: { tent: TentDto; refresh: number }) {
  const [status, setStatus] = useState<'idle' | 'loading' | 'ready' | 'failed'>('idle')
  const [version, setVersion] = useState(0)
  const [lastGoodSrc, setLastGoodSrc] = useState<string | null>(null)
  const [lastGoodAt, setLastGoodAt] = useState<Date | null>(null)
  const src = tent.cameraEntityId ? `/api/live/tents/${tent.id}/camera?t=${refresh}-${version}` : null

  useEffect(() => {
    if (!src) {
      setStatus('idle')
      setLastGoodSrc(null)
      setLastGoodAt(null)
      return
    }
    setStatus('loading')
  }, [src, tent.id])

  useEffect(() => {
    if (!tent.cameraEntityId) return
    const interval = window.setInterval(() => setVersion((current) => current + 1), 30000)
    return () => window.clearInterval(interval)
  }, [tent.cameraEntityId, tent.id])

  if (!src && !lastGoodSrc) {
    return (
      <V1Card className="v1-camera-empty is-compact" tone="neutral">
        <div><span className="v1-card-kicker">Kamera</span><h2>Nicht eingerichtet</h2><p>{tent.name}</p></div>
        <div className="v1-action-row"><V1LinkButton to="/home-assistant">HA öffnen</V1LinkButton></div>
      </V1Card>
    )
  }

  if (status === 'failed' && !lastGoodSrc) {
    return (
      <V1Card className="v1-camera-empty is-compact" tone="warn">
        <div><span className="v1-card-kicker">Kamera-Snapshot</span><h2>Snapshot nicht erreichbar</h2><p>{tent.cameraEntityId ?? tent.name}</p></div>
        <div className="v1-action-row"><V1Button onClick={() => { setVersion((current) => current + 1); setStatus('loading') }}>Neu laden</V1Button><V1LinkButton to="/home-assistant">HA öffnen</V1LinkButton></div>
      </V1Card>
    )
  }

  const imageSrc = src ?? lastGoodSrc
  return (
    <div className="v1-camera-card rc2-camera-card">
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

function MetricCard({ metric }: { metric: MetricPayload }) {
  const tone = metric.tone === 'danger' ? 'critical' : metric.tone === 'warning' ? 'warn' : metric.tone === 'success' ? 'ok' : 'neutral'
  return <V1Stat label={metric.label} value={metric.value} unit={metric.unit} hint={metric.hint ?? undefined} tone={tone} />
}

function Row({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>
}

function mapMetrics(items: MetricPayload[], definitions: readonly (readonly [string, string, string | null])[]): MetricPayload[] {
  return definitions.map(([key, label, unit]) => {
    const found = items.find((item) => item.key === key)
    return found ? { ...found, label, unit: found.unit ?? unit } : { key, label, value: '–', unit, tone: 'muted', hint: null }
  })
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

function formatApiError(caught: unknown, fallback: string) {
  return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback
}

export default LiveDashboardPage