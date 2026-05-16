import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { CalibrationEventDto, GrowSummary, HardwareItemDto, MaintenanceEventDto, MetricPayload, RiskEventDto, TentDto, TentLivePayload } from '../types'
import { classNames, formatDateTime } from '../utils'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1LinkButton, V1Page, V1Section, V1Stat, V1Tabs } from '../components/v1'

type LiveState = {
  tents: TentDto[]
  liveByTentId: Record<number, TentLivePayload>
  grows: GrowSummary[]
  risks: RiskEventDto[]
  hardware: HardwareItemDto[]
  calibration: CalibrationEventDto[]
  maintenance: MaintenanceEventDto[]
  issues: string[]
}

type MetricDefinition = { key: string; label: string; unit: string | null }

type CameraState = 'hidden' | 'loading' | 'ready' | 'failed'

const initialState: LiveState = { tents: [], liveByTentId: {}, grows: [], risks: [], hardware: [], calibration: [], maintenance: [], issues: [] }
const riskRank: Record<string, number> = { Critical: 0, Warning: 1, Info: 2 }

const tentMetricDefs: MetricDefinition[] = [
  { key: 'temperature', label: 'Luft', unit: '°C' },
  { key: 'humidity', label: 'RLF', unit: '%' },
  { key: 'vpd', label: 'VPD', unit: 'kPa' },
  { key: 'ppfd', label: 'PPFD', unit: 'µmol/m²/s' },
  { key: 'co2', label: 'CO₂', unit: 'ppm' },
  { key: 'light-cycle', label: 'Licht', unit: null },
]

const hydroMetricDefs: MetricDefinition[] = [
  { key: 'reservoir-ph', label: 'pH', unit: null },
  { key: 'reservoir-ec', label: 'EC', unit: 'mS/cm' },
  { key: 'reservoir-temp', label: 'Wasser', unit: '°C' },
  { key: 'reservoir-level', label: 'Level', unit: 'L/cm' },
  { key: 'orp', label: 'ORP', unit: 'mV' },
  { key: 'dissolved-oxygen', label: 'DO', unit: 'mg/L' },
]

function LiveDashboardPage() {
  const [state, setState] = useState<LiveState>(initialState)
  const [loading, setLoading] = useState(true)
  const [selectedTentId, setSelectedTentId] = useState<number | null>(null)
  const [refreshToken, setRefreshToken] = useState(0)

  useEffect(() => {
    const id = window.setInterval(() => setRefreshToken((current) => current + 1), 60_000)
    return () => window.clearInterval(id)
  }, [])

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      const issues: string[] = []
      const fetchOptional = async <T,>(name: string, path: string, fallback: T): Promise<T> => {
        try {
          return await apiFetch<T>(path, { signal: controller.signal })
        } catch (caught) {
          if (!controller.signal.aborted) issues.push(`${name}: ${formatApiError(caught, 'nicht erreichbar')}`)
          return fallback
        }
      }

      const dueBeforeUtc = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString()
      const [tents, grows, risks, hardware, calibration, maintenance] = await Promise.all([
        fetchOptional<TentDto[]>('Zelte', '/api/settings/tents', []),
        fetchOptional<GrowSummary[]>('Grows', '/api/grows?archived=false', []),
        fetchOptional<RiskEventDto[]>('Risiken', '/api/risk-events?status=Open', []),
        fetchOptional<HardwareItemDto[]>('Hardware', '/api/hardware-items', []),
        fetchOptional<CalibrationEventDto[]>('Kalibrierung', `/api/calibration-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
        fetchOptional<MaintenanceEventDto[]>('Wartung', `/api/maintenance-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
      ])

      const sortedTents = [...tents].sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name))
      const liveEntries = await Promise.all(sortedTents.map(async (tent) => {
        try {
          const live = await apiFetch<TentLivePayload>(`/api/live/tents/${tent.id}`, { signal: controller.signal })
          return [tent.id, live] as const
        } catch (caught) {
          if (!controller.signal.aborted) issues.push(`${tent.name}: ${formatApiError(caught, 'Live-Daten fehlen')}`)
          return [tent.id, null] as const
        }
      }))

      if (controller.signal.aborted) return
      const liveByTentId = Object.fromEntries(liveEntries.filter((entry): entry is readonly [number, TentLivePayload] => entry[1] !== null))
      setState({ tents: sortedTents, grows, risks: risks.filter((risk) => risk.status === 'Open'), hardware, calibration, maintenance, liveByTentId, issues })
      setSelectedTentId((current) => current ?? chooseInitialTent(sortedTents, grows) ?? null)
      setLoading(false)
    }

    void load()
    return () => controller.abort()
  }, [refreshToken])

  const selectedTent = useMemo(() => state.tents.find((tent) => tent.id === selectedTentId) ?? state.tents[0] ?? null, [selectedTentId, state.tents])
  const live = selectedTent ? state.liveByTentId[selectedTent.id] : undefined
  const activeGrows = useMemo(() => state.grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning'), [state.grows])
  const growsForTent = selectedTent ? activeGrows.filter((grow) => grow.tentId === selectedTent.id) : []
  const primaryGrow = growsForTent[0] ?? activeGrows[0] ?? null
  const sortedRisks = useMemo(() => [...state.risks].sort((a, b) => (riskRank[a.severity] ?? 9) - (riskRank[b.severity] ?? 9) || b.startedAtUtc.localeCompare(a.startedAtUtc)), [state.risks])
  const status = getStatusLabel(sortedRisks, live, loading)
  const statusTone = status === 'Kritisch' ? 'critical' : status === 'Beobachten' ? 'warn' : status === 'Lädt' ? 'neutral' : 'ok'
  const sensorTrust = useMemo(() => buildLiveSensorTrust(state.hardware, state.calibration, state.maintenance, sortedRisks), [state.hardware, state.calibration, state.maintenance, sortedRisks])

  return (
    <V1Page
      className="v1-live-page"
      eyebrow="Live"
      title={status}
      action={<V1Button variant="primary" onClick={() => setRefreshToken((current) => current + 1)}>Aktualisieren</V1Button>}
    >
      {state.issues.length > 0 && <V1Alert title="Teilweise offline" message={state.issues.slice(0, 3).join(' · ')} tone="warn" />}

      {state.tents.length > 1 && (
        <V1Tabs
          label="Zelt auswählen"
          active={selectedTent?.id ?? state.tents[0].id}
          onChange={(id) => setSelectedTentId(Number(id))}
          items={state.tents.map((tent) => ({ value: tent.id, label: tent.name, meta: formatTentSize(tent) }))}
        />
      )}

      {!selectedTent ? (
        <V1Empty title="Noch kein Zelt angelegt" action={<V1LinkButton to="/zelte" variant="primary">Zelt anlegen</V1LinkButton>} />
      ) : (
        <>
          <section className="v1-live-hero-grid">
            <CameraTile tent={selectedTent} live={live} />
            <V1Card className="v1-live-now-card" tone={statusTone}>
              <div className="v1-card-title-row">
                <div>
                  <span className="v1-card-kicker">{selectedTent.name}</span>
                  <h2>{primaryGrow?.name ?? 'Kein aktiver Grow'}</h2>
                </div>
                <V1Badge tone={statusTone}>{live?.stateLabel ?? status}</V1Badge>
              </div>
              <div className="v1-now-list">
                <Row label="Zelt" value={formatTentType(selectedTent.tentType)} />
                <Row label="Größe" value={formatTentSize(selectedTent)} />
                <Row label="Grow" value={primaryGrow?.strain ?? 'offen'} />
                <Row label="Phase" value={primaryGrow?.latestStage ?? 'offen'} />
                <Row label="Letzte Messung" value={formatDateTime(primaryGrow?.latestMeasurementAt)} />
                <Row label="Sensoren" value={sensorTrust.label} />
              </div>
              <div className="v1-action-row">
                {primaryGrow ? <V1LinkButton to={`/grows/${primaryGrow.id}/addback`} variant="primary">Addback</V1LinkButton> : <V1LinkButton to="/grows/new" variant="primary">Grow starten</V1LinkButton>}
                <V1LinkButton to="/hardware">Sensoren</V1LinkButton>
                <V1LinkButton to="/home-assistant">HA</V1LinkButton>
              </div>
            </V1Card>
          </section>

          {sortedRisks.length > 0 && (
            <V1Section title="Risiken">
              <div className="v1-risk-list">
                {sortedRisks.slice(0, 3).map((risk) => <div key={risk.id} className={classNames('v1-risk-row', risk.severity.toLowerCase())}><strong>{risk.title}</strong><span>{risk.severity}</span></div>)}
              </div>
            </V1Section>
          )}

          <div className="v1-live-metrics-pair">
            <V1Section title="Zelt">
              <div className="v1-metric-grid compact">{mapMetrics(live?.metrics ?? [], tentMetricDefs).map((metric) => <MetricCard key={metric.key} metric={metric} />)}</div>
            </V1Section>

            <V1Section title="RDWC/DWC">
              <div className="v1-metric-grid compact">{mapMetrics(live?.metrics ?? [], hydroMetricDefs).map((metric) => <MetricCard key={metric.key} metric={metric} />)}</div>
            </V1Section>
          </div>

          <V1Section title="Grows" action={<V1LinkButton to="/grows/new">Starten</V1LinkButton>}>
            {activeGrows.length === 0 ? <V1Empty title="Keine aktiven Grows" /> : (
              <div className="v1-list">
                {activeGrows.slice(0, 5).map((grow) => <Link key={grow.id} className="v1-list-row" to={`/grows/${grow.id}`}><strong>{grow.name}</strong><span>{grow.strain ?? 'Sorte offen'}</span><em>{grow.tentName ?? 'Ohne Zelt'}</em></Link>)}
              </div>
            )}
          </V1Section>
        </>
      )}
    </V1Page>
  )
}

function CameraTile({ tent, live }: { tent: TentDto; live: TentLivePayload | undefined }) {
  const [state, setState] = useState<CameraState>('hidden')
  const source = live?.cameraUrl ? resolveCameraUrl(live.cameraUrl) : null

  useEffect(() => {
    setState(source ? 'loading' : 'hidden')
  }, [source])

  if (!source || state === 'failed' || state === 'hidden') {
    return (
      <V1Card className="v1-camera-empty is-compact">
        <div>
          <span className="v1-card-kicker">Kamera</span>
          <h2>{tent.cameraEntityId ? 'Nicht erreichbar' : 'Nicht eingerichtet'}</h2>
          <p>{tent.name}</p>
        </div>
        <V1LinkButton to="/home-assistant">HA öffnen</V1LinkButton>
      </V1Card>
    )
  }

  return (
    <div className="v1-camera-card">
      {state === 'loading' && <div className="v1-camera-loader">Kamera lädt...</div>}
      <img src={source} alt={`${tent.name} Kamera`} onLoad={() => setState('ready')} onError={() => setState('failed')} className={state === 'ready' ? 'ready' : ''} />
      <div className="v1-camera-label"><strong>{tent.name}</strong><span>Live</span></div>
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

function mapMetrics(items: MetricPayload[], definitions: MetricDefinition[]): MetricPayload[] {
  return definitions.map((definition) => {
    const found = items.find((item) => item.key === definition.key)
    return found ? { ...found, label: definition.label, unit: found.unit ?? definition.unit } : { key: definition.key, label: definition.label, value: '–', unit: definition.unit, tone: 'muted', hint: null }
  })
}

function chooseInitialTent(tents: TentDto[], grows: GrowSummary[]) {
  const running = grows.find((grow) => grow.status === 'Running' && grow.tentId)
  return running?.tentId ?? tents[0]?.id ?? null
}

function getStatusLabel(risks: RiskEventDto[], live: TentLivePayload | undefined, loading: boolean) {
  if (loading) return 'Lädt'
  if (risks.some((risk) => risk.severity === 'Critical')) return 'Kritisch'
  if (risks.some((risk) => risk.severity === 'Warning') || live?.stateTone === 'attention') return 'Beobachten'
  if (live?.stateTone === 'critical') return 'Kritisch'
  return 'Stabil'
}


function buildLiveSensorTrust(hardware: HardwareItemDto[], calibration: CalibrationEventDto[], maintenance: MaintenanceEventDto[], risks: RiskEventDto[]) {
  const sensorHardware = hardware.filter((item) => {
    const haystack = `${item.name} ${item.category} ${item.wearTemplateId ?? ''}`.toLowerCase()
    return ['sensor', 'sonde', 'probe', 'ph', 'ec', 'orp', 'do', 'temperatur', 'level', 'wasserstand'].some((term) => haystack.includes(term))
  })
  const offline = sensorHardware.filter((item) => item.status === 'Offline' || item.status === 'Retired').length
  const plannedCalibration = calibration.filter((event) => event.status === 'Planned').length
  const plannedMaintenance = maintenance.filter((event) => event.status === 'Planned').length
  const criticalRisks = risks.filter((risk) => risk.severity === 'Critical' && (risk.hardwareItemId != null || risk.eventType === 'SensorUnavailable')).length
  const score = Math.max(0, 100 - offline * 25 - plannedCalibration * 15 - plannedMaintenance * 10 - criticalRisks * 20)
  const label = score < 55 ? 'kritisch' : score < 82 ? 'prüfen' : 'stabil'
  return { score, label }
}

function resolveCameraUrl(value: string) {
  if (/^https?:\/\//i.test(value)) return value
  if (window.location.port === '5173') return `${window.location.protocol}//${window.location.hostname}:5076${value}`
  return value
}

function formatTentSize(tent: TentDto) {
  if (!tent.widthCm && !tent.depthCm && !tent.tentHeightCm) return 'Größe offen'
  return `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm`
}

function formatTentType(value: string) {
  switch (value) {
    case 'Production': return 'Blüte / Run'
    case 'Mother': return 'Mutter'
    case 'Propagation': return 'Anzucht'
    case 'Quarantine': return 'Quarantäne'
    case 'MultiPurpose': return 'Mehrzweck'
    default: return value
  }
}

function formatApiError(caught: unknown, fallback: string) {
  if (caught instanceof ApiRequestError) return caught.message
  return caught instanceof Error ? caught.message : fallback
}

export default LiveDashboardPage
