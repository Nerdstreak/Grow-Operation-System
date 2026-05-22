import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, HydroSetupDto, MetricPayload, PlantInstanceDto, SetupDto, TentDto, TentLivePayload } from '../types'
import { V1Alert, V1Badge, V1Card, V1Empty, V1LinkButton, V1Page, V1Section, V1Stat } from '../components/v1'

const tentMetricDefinitions = [
  ['temperature', 'Temp', '°C'],
  ['humidity', 'RLF', '%'],
  ['vpd', 'VPD', 'kPa'],
  ['light-cycle', 'Licht', null],
  ['ppfd', 'PPFD', 'µmol/m²/s'],
  ['co2', 'CO₂', 'ppm'],
] as const

const hydroMetricDefinitions = [
  ['reservoir-ph', 'pH', null],
  ['reservoir-ec', 'EC', 'mS/cm'],
  ['reservoir-temp', 'Wasser', '°C'],
  ['reservoir-level', 'Level', 'L/cm'],
  ['orp', 'ORP', 'mV'],
  ['dissolved-oxygen', 'DO', 'mg/L'],
] as const

function TentDetailPage() {
  const { tentId } = useParams()
  const [tent, setTent] = useState<TentDto | null>(null)
  const [live, setLive] = useState<TentLivePayload | null>(null)
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [setups, setSetups] = useState<SetupDto[]>([])
  const [hydroSetups, setHydroSetups] = useState<HydroSetupDto[]>([])
  const [plantsBySetupId, setPlantsBySetupId] = useState<Record<number, PlantInstanceDto[]>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      if (!tentId) return
      setLoading(true)
      setError(null)

      try {
        const tentIdNumber = Number(tentId)
        const [tents, livePayload, activeGrows, setupList, hydroSetupList] = await Promise.all([
          apiFetch<TentDto[]>('/api/settings/tents?includeArchived=true', { signal: controller.signal }),
          apiFetch<TentLivePayload>(`/api/live/tents/${tentId}`, { signal: controller.signal }).catch(() => null),
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }),
          apiFetch<SetupDto[]>('/api/setups', { signal: controller.signal }),
          apiFetch<HydroSetupDto[]>(`/api/hydro-setups?tentId=${tentIdNumber}&includeArchived=true`, { signal: controller.signal }).catch(() => []),
        ])

        if (controller.signal.aborted) return

        const selectedTent = tents.find((item) => item.id === tentIdNumber) ?? null
        const activeSetups = setupList.filter((setup) => setup.tentId === tentIdNumber && isActiveSetup(setup))
        const plantEntries = await fetchPlantsForSetups(activeSetups, controller.signal)

        if (controller.signal.aborted) return

        setTent(selectedTent)
        setLive(livePayload)
        setGrows(activeGrows.filter((grow) => grow.tentId === tentIdNumber))
        setSetups(activeSetups)
        setHydroSetups(hydroSetupList)
        setPlantsBySetupId(Object.fromEntries(plantEntries))
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Zelt-Details konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [tentId])

  const activeHydroSetups = useMemo(() => hydroSetups.filter((setup) => setup.status === 'Active'), [hydroSetups])
  const score = buildScore(live?.metrics ?? [], tent)

  if (loading) return <V1Page eyebrow="Zelt" title="Lade Zelt..."><V1Empty title="Live-Daten werden geladen..." /></V1Page>
  if (!tent) return <V1Page eyebrow="Zelt" title="Nicht gefunden" action={<V1LinkButton to="/zelte">Zurück</V1LinkButton>}><V1Empty title="Zelt nicht gefunden." /></V1Page>

  return (
    <V1Page
      eyebrow={formatTentType(tent.tentType)}
      title={tent.name}
      className="tent-detail-v2"
      action={<div className="v1-action-row"><V1LinkButton to="/zelte" variant="ghost">Zelte</V1LinkButton><V1LinkButton to="/home-assistant">HA</V1LinkButton><V1LinkButton to="/hydro">Hydro</V1LinkButton></div>}
    >
      {error && <V1Alert title="Fehler" message={error} tone="warn" />}

      <section className="v1-live-hero-grid">
        <V1Card tone={score.tone} className="v1-live-now-card">
          <div className="v1-card-title-row"><div><span className="v1-card-kicker">Live</span><h2>{score.label}</h2></div><V1Badge tone={score.tone}>{score.value}%</V1Badge></div>
          <div className="v1-info-grid compact">
            {mapMetrics(live?.metrics ?? [], tentMetricDefinitions.slice(0, 5)).map((metric) => <Info key={metric.key} label={metric.label} value={formatMetricValue(metric)} />)}
          </div>
          <div className="v1-action-row">{grows[0] ? <V1LinkButton to={`/grows/${grows[0].id}/addback`} variant="primary">Addback</V1LinkButton> : <V1LinkButton to="/grows/new" variant="primary">Grow starten</V1LinkButton>}<V1LinkButton to="/messung">Messung</V1LinkButton></div>
        </V1Card>

        <V1Card className="v1-live-now-card">
          <div className="v1-card-title-row"><div><span className="v1-card-kicker">Raum</span><h2>{formatSize(tent)}</h2></div><V1Badge tone={tent.status === 'Active' ? 'ok' : 'neutral'}>{tent.status === 'Active' ? 'aktiv' : 'Archiv'}</V1Badge></div>
          <div className="v1-info-grid compact tent-detail-room-grid">
            <Info label="Grows" value={String(grows.length)} />
            <Info label="Hydro" value={String(activeHydroSetups.length)} />
            <Info label="Setups" value={String(setups.length)} />
            <Info label="Sensoren" value={String(tent.sensors.filter((sensor) => sensor.isActive).length)} />
            <Info label="Licht" value={tent.lightWatt ? `${tent.lightWatt} W` : tent.lightType ?? 'offen'} />
            <Info label="Klima" value={`${tent.exhaustFanCount ?? 0} Abluft · ${tent.circulationFanCount ?? 0} Umluft`} />
          </div>
        </V1Card>

        {live?.cameraUrl ? (
          <div className="v1-camera-card rc2-camera-card"><img src={live.cameraUrl} alt={`Livebild ${tent.name}`} className="ready" /><div className="v1-camera-label"><strong>{tent.name}</strong><span>Kamera</span></div></div>
        ) : (
          <V1Card className="v1-camera-empty is-compact"><span className="v1-card-kicker">Kamera</span><h2>Nicht eingerichtet</h2><p>{tent.cameraEntityId ?? 'Kamera-Entity fehlt im HA-Mapping.'}</p><V1LinkButton to="/home-assistant">HA-Mapping</V1LinkButton></V1Card>
        )}
      </section>

      <div className="v1-live-metrics-pair">
        <V1Section title="Zeltwerte"><div className="v1-metric-grid compact">{mapMetrics(live?.metrics ?? [], tentMetricDefinitions).map((metric) => <MetricCard key={metric.key} metric={metric} />)}</div></V1Section>
        <V1Section title="Reservoir"><div className="v1-metric-grid compact">{mapMetrics(live?.metrics ?? [], hydroMetricDefinitions).map((metric) => <MetricCard key={metric.key} metric={metric} />)}</div></V1Section>
      </div>

      <V1Section title="Hydro-Systeme" action={<V1LinkButton to="/hydro/new">Hydro anlegen</V1LinkButton>}>
        {activeHydroSetups.length === 0 ? <V1Empty title="Kein aktives Hydro-Setup" text="DWC/RDWC-Systeme werden separat angelegt und dann dem Zelt zugeordnet." /> : <div className="v1-card-grid v1-card-grid-compact">{activeHydroSetups.map((setup) => <HydroSetupCard key={setup.id} setup={setup} />)}</div>}
      </V1Section>

      <V1Section title="Aktive Grows" action={<V1LinkButton to="/grows/new">Grow starten</V1LinkButton>}>
        {grows.length === 0 ? <V1Empty title="Kein Grow in diesem Zelt" /> : <div className="v1-list">{grows.map((grow) => <Link key={grow.id} to={`/grows/${grow.id}`} className="v1-list-row"><strong>{grow.name}</strong><span>{grow.strain ?? 'Sorte offen'}{grow.breeder ? ` · ${grow.breeder}` : ''}</span><em>{grow.latestStage ?? grow.status}</em></Link>)}</div>}
      </V1Section>

      <V1Section title="Setups & Pflanzen">
        {setups.length === 0 ? <V1Empty title="Keine aktiven Plant-Setups" /> : <div className="v1-card-grid">{setups.map((setup) => <SetupCard key={setup.id} setup={setup} plants={plantsBySetupId[setup.id] ?? []} />)}</div>}
      </V1Section>
    </V1Page>
  )
}

function HydroSetupCard({ setup }: { setup: HydroSetupDto }) {
  return <V1Card><div className="v1-card-title-row"><div><span className="v1-card-kicker">{setup.hydroStyle}</span><h2>{setup.name}</h2></div><V1Badge tone="accent">{setup.layoutType}</V1Badge></div><div className="v1-info-grid compact"><Info label="Sites" value={String(setup.potCount ?? '–')} /><Info label="Topf" value={formatLiters(setup.potSizeLiters)} /><Info label="Tank" value={formatLiters(setup.reservoirLiters)} /><Info label="Gesamt" value={formatLiters(setup.totalVolumeLiters)} /><Info label="Chiller" value={setup.hasChiller ? 'ja' : 'nein'} /><Info label="Luft" value={setup.hasAirPump ? `${setup.airStoneCount ?? '–'} Steine` : 'offen'} /></div></V1Card>
}

function SetupCard({ setup, plants }: { setup: SetupDto; plants: PlantInstanceDto[] }) {
  return <V1Card><div className="v1-card-title-row"><div><span className="v1-card-kicker">{setup.setupType}</span><h2>{setup.name}</h2></div><V1Badge tone={setup.status === 'Active' ? 'ok' : 'neutral'}>{setup.status}</V1Badge></div><div className="v1-info-grid compact">{formatSetupDetails(setup).map((detail) => <Info key={detail.label} label={detail.label} value={detail.value} />)}</div>{plants.length === 0 ? <V1Empty title="Keine Pflanzen in diesem Setup" /> : <div className="v1-list">{plants.map((plant) => <div key={plant.id} className="v1-list-row"><strong>{plant.label}</strong><span>{formatPlantLine(plant)}</span><em>{plant.plantStatus}</em></div>)}</div>}</V1Card>
}

function MetricCard({ metric }: { metric: MetricPayload }) { return <V1Stat label={metric.label} value={metric.value} unit={metric.unit} hint={metric.hint ?? undefined} tone={metricTone(metric)} /> }
function Info({ label, value }: { label: string; value: string }) { return <div className="v1-info"><span>{label}</span><strong>{value}</strong></div> }
function mapMetrics(items: MetricPayload[], definitions: readonly (readonly [string, string, string | null])[]): MetricPayload[] { return definitions.map(([key, label, unit]) => { const found = items.find((item) => item.key === key); return found ? { ...found, label, unit: found.unit ?? unit } : { key, label, value: '–', unit, tone: 'muted', hint: null } }) }
function formatMetricValue(metric: MetricPayload) { return metric.unit && metric.value !== '–' ? `${metric.value} ${metric.unit}` : metric.value }
function buildScore(metrics: MetricPayload[], tent: TentDto | null) { const usable = metrics.filter((metric) => metric.value && metric.value !== '–').length; if (!tent || usable === 0) return { value: 0, label: 'Einrichten', tone: 'neutral' as const }; const warnings = metrics.filter((metric) => metric.tone === 'warning' || metric.tone === 'danger').length; const value = Math.max(0, Math.min(100, 100 - warnings * 18 - Math.max(0, 6 - usable) * 8)); return value < 55 ? { value, label: 'Kritisch', tone: 'critical' as const } : value < 82 ? { value, label: 'Beobachten', tone: 'warn' as const } : { value, label: 'Stabil', tone: 'ok' as const } }
function metricTone(metric: MetricPayload) { return metric.tone === 'danger' ? 'critical' : metric.tone === 'warning' ? 'warn' : metric.tone === 'success' ? 'ok' : 'neutral' }
function formatSetupDetails(setup: SetupDto): Array<{ label: string; value: string }> { const base = [{ label: 'Status', value: setup.status }]; if (setup.setupType === 'Mother') return [...base, { label: 'Clones', value: setup.cloneCounterTotal !== null ? String(setup.cloneCounterTotal) : '–' }, { label: 'Schnitt', value: setup.lastCloneCutAt ? formatDate(setup.lastCloneCutAt) : '–' }, { label: 'Health', value: setup.motherHealthStatus ?? '–' }]; if (setup.setupType === 'Quarantine') return [...base, { label: 'Start', value: setup.quarantineStartedAt ? formatDate(setup.quarantineStartedAt) : '–' }, { label: 'Ende', value: setup.quarantinePlannedEndAt ? formatDate(setup.quarantinePlannedEndAt) : '–' }, { label: 'Ergebnis', value: setup.quarantineResult ?? '–' }]; return [...base, { label: 'Notiz', value: setup.notes ?? '–' }] }
function formatDate(value: string): string { return value.slice(0, 10) }
function formatPlantLine(plant: PlantInstanceDto): string { const strain = plant.strainName ?? (plant.strainId ? `Strain #${plant.strainId}` : 'Ohne Strain'); const pheno = plant.phenoLabel ? ` · ${plant.phenoLabel}` : ''; return `${plant.plantRole} · ${strain}${pheno}` }
function isActiveSetup(setup: SetupDto): boolean { return setup.status === 'Planning' || setup.status === 'Active' }
async function fetchPlantsForSetups(setups: SetupDto[], signal?: AbortSignal): Promise<Array<readonly [number, PlantInstanceDto[]]>> { return Promise.all(setups.map(async (setup) => { const plants = await apiFetch<PlantInstanceDto[]>(`/api/plants?setupId=${setup.id}`, { signal }); return [setup.id, plants] as const })) }
function formatTentType(value: string) { return value === 'Production' ? 'Blüte / Run' : value === 'Mother' ? 'Mutter' : value === 'Propagation' ? 'Anzucht' : value === 'Quarantine' ? 'Quarantäne' : value === 'MultiPurpose' ? 'Mehrzweck' : value }
function formatSize(tent: TentDto) { return !tent.widthCm && !tent.depthCm && !tent.tentHeightCm ? 'Größe offen' : `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm` }
function formatLiters(value: number | null | undefined) { return value == null ? '–' : `${value.toLocaleString('de-DE', { maximumFractionDigits: 1 })} L` }

export default TentDetailPage
