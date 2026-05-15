import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { CalibrationEventDto, GrowSummary, GrowTaskDto, HardwareItemDto, MaintenanceEventDto, RiskEventDto, SopInstanceDto, TentDto } from '../types'
import { classNames, formatDateTime } from '../utils'

type LoadIssue = { area: string; message: string }
type ActionData = {
  grows: GrowSummary[]
  tasks: GrowTaskDto[]
  tents: TentDto[]
  hardwareItems: HardwareItemDto[]
  riskEvents: RiskEventDto[]
  maintenanceEvents: MaintenanceEventDto[]
  calibrationEvents: CalibrationEventDto[]
  sopInstances: SopInstanceDto[]
  issues: LoadIssue[]
}

const emptyData: ActionData = { grows: [], tasks: [], tents: [], hardwareItems: [], riskEvents: [], maintenanceEvents: [], calibrationEvents: [], sopInstances: [], issues: [] }
const riskRank: Record<string, number> = { Critical: 0, Warning: 1, Info: 2 }

function MobileActionPage() {
  const [data, setData] = useState<ActionData>(emptyData)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      const issues: LoadIssue[] = []
      const dueBeforeUtc = new Date(Date.now() + 3 * 24 * 60 * 60 * 1000).toISOString()
      const fetchOptional = async <T,>(area: string, path: string, fallback: T): Promise<T> => {
        try { return await apiFetch<T>(path, { signal: controller.signal }) } catch (caught) {
          if (!controller.signal.aborted) issues.push({ area, message: caught instanceof ApiRequestError ? caught.message : `${area} fehlgeschlagen.` })
          return fallback
        }
      }

      const [grows, settings, hardwareItems, riskEvents, maintenanceEvents, calibrationEvents] = await Promise.all([
        fetchOptional<GrowSummary[]>('Grows', '/api/grows?archived=false', []),
        fetchOptional<{ tents: TentDto[] }>('Settings', '/api/settings', { tents: [] }),
        fetchOptional<HardwareItemDto[]>('Hardware', '/api/hardware-items', []),
        fetchOptional<RiskEventDto[]>('Risiken', '/api/risk-events?status=Open', []),
        fetchOptional<MaintenanceEventDto[]>('Wartung', `/api/maintenance-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
        fetchOptional<CalibrationEventDto[]>('Kalibrierung', `/api/calibration-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
      ])
      const activeGrows = grows.filter((grow) => grow.status === 'Running')
      const [tasks, sops] = await Promise.all([
        Promise.all(activeGrows.map((grow) => fetchOptional<GrowTaskDto[]>('Tasks', `/api/grows/${grow.id}/tasks`, []))),
        Promise.all(activeGrows.map((grow) => fetchOptional<SopInstanceDto[]>('SOPs', `/api/sop-instances?growId=${grow.id}`, []))),
      ])
      if (controller.signal.aborted) return
      setData({ grows, tasks: tasks.flat().filter((task) => task.status === 'Open'), tents: settings.tents, hardwareItems, riskEvents: riskEvents.filter((event) => event.status === 'Open'), maintenanceEvents: maintenanceEvents.filter((event) => event.status === 'Planned'), calibrationEvents: calibrationEvents.filter((event) => event.status === 'Planned'), sopInstances: sops.flat().filter((instance) => instance.status === 'Active'), issues })
      setLoading(false)
    }
    void load()
    return () => controller.abort()
  }, [])

  const activeGrows = useMemo(() => data.grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning'), [data.grows])
  const sortedRisks = useMemo(() => [...data.riskEvents].sort((a, b) => (riskRank[a.severity] ?? 99) - (riskRank[b.severity] ?? 99)), [data.riskEvents])
  const criticalRisks = sortedRisks.filter((event) => event.severity === 'Critical')
  const dueMaintenance = data.maintenanceEvents
  const dueCalibrations = data.calibrationEvents
  const status = loading ? 'Lädt' : criticalRisks.length > 0 ? 'Kritisch' : sortedRisks.length > 0 || dueMaintenance.length > 0 || dueCalibrations.length > 0 ? 'Offen' : 'Stabil'
  const primaryGrow = activeGrows[0]

  return (
    <main className="page-scroll app-page action-rebuild-page">
      <section className={classNames('control-header action-status-header', criticalRisks.length > 0 && 'is-critical')}>
        <div>
          <span className="control-kicker">Aktion</span>
          <h1>{status}</h1>
        </div>
        <div className="action-count-grid">
          <Counter label="Risiken" value={sortedRisks.length} />
          <Counter label="Wartung" value={dueMaintenance.length} />
          <Counter label="Kalibrierung" value={dueCalibrations.length} />
        </div>
      </section>

      {data.issues.length > 0 && <div className="inline-issues"><strong>Teilweise offline</strong><span>{data.issues.map((issue) => issue.area).join(' · ')}</span></div>}

      <section className="quick-action-grid">
        {primaryGrow ? <Link className="quick-action primary" to={`/grows/${primaryGrow.id}/addback`}>Addback</Link> : <Link className="quick-action primary" to="/grows/new">Grow starten</Link>}
        {primaryGrow && <Link className="quick-action" to={`/grows/${primaryGrow.id}`}>Messung</Link>}
        <Link className="quick-action" to="/hardware">Hardware</Link>
        <Link className="quick-action" to="/home-assistant">HA</Link>
      </section>

      <section className="compact-section">
        <div className="section-headline"><h2>Jetzt</h2></div>
        {loading ? <div className="empty-hint tight">Lädt...</div> : <ActionList risks={sortedRisks} maintenance={dueMaintenance} calibrations={dueCalibrations} tasks={data.tasks} sops={data.sopInstances} grows={data.grows} hardware={data.hardwareItems} />}
      </section>

      <section className="compact-section">
        <div className="section-headline"><h2>Grows</h2><Link className="btn" to="/grows/new">Starten</Link></div>
        {activeGrows.length === 0 ? <div className="empty-hint tight">Keine aktiven Grows.</div> : (
          <div className="compact-list">
            {activeGrows.map((grow) => <Link key={grow.id} className="compact-row" to={`/grows/${grow.id}`}><strong>{grow.name}</strong><span>{grow.tentName ?? getTentName(data.tents, grow.tentId)}</span><span>{grow.latestStage ?? 'Phase offen'}</span></Link>)}
          </div>
        )}
      </section>
    </main>
  )
}

function ActionList({ risks, maintenance, calibrations, tasks, sops, grows, hardware }: { risks: RiskEventDto[]; maintenance: MaintenanceEventDto[]; calibrations: CalibrationEventDto[]; tasks: GrowTaskDto[]; sops: SopInstanceDto[]; grows: GrowSummary[]; hardware: HardwareItemDto[] }) {
  const rows = [
    ...risks.slice(0, 4).map((risk) => ({ id: `risk-${risk.id}`, title: risk.title, meta: `${risk.severity} · ${risk.eventType}`, to: '/hardware', tone: risk.severity === 'Critical' ? 'critical' : 'warning' })),
    ...sops.slice(0, 3).map((sop) => ({ id: `sop-${sop.id}`, title: sop.sopName, meta: `${getGrowName(grows, sop.growId)} · ${formatDateTime(sop.nextStepDueAtUtc ?? sop.dueAtUtc)}`, to: `/grows/${sop.growId}`, tone: 'normal' })),
    ...maintenance.slice(0, 3).map((event) => ({ id: `maintenance-${event.id}`, title: event.title, meta: `${getHardwareName(hardware, event.hardwareItemId)} · ${formatDateTime(event.dueAtUtc)}`, to: '/hardware', tone: 'warning' })),
    ...calibrations.slice(0, 3).map((event) => ({ id: `calibration-${event.id}`, title: event.title, meta: `${getHardwareName(hardware, event.hardwareItemId)} · ${formatDateTime(event.dueAtUtc)}`, to: '/hardware', tone: 'warning' })),
    ...tasks.slice(0, 4).map((task) => ({ id: `task-${task.id}`, title: task.title, meta: `${task.growName ?? getGrowName(grows, task.growId)} · ${formatDateTime(task.dueAtUtc)}`, to: `/grows/${task.growId}`, tone: 'normal' })),
  ]

  if (rows.length === 0) return <div className="empty-hint tight">Keine offenen Aktionen.</div>
  return <div className="compact-list">{rows.map((row) => <Link key={row.id} to={row.to} className={classNames('compact-row action-row', row.tone === 'critical' && 'is-critical', row.tone === 'warning' && 'is-warning')}><strong>{row.title}</strong><span>{row.meta}</span></Link>)}</div>
}

function Counter({ label, value }: { label: string; value: number }) { return <div><strong>{value}</strong><span>{label}</span></div> }
function getGrowName(grows: GrowSummary[], id: number | null) { return id == null ? 'Grow offen' : grows.find((grow) => grow.id === id)?.name ?? `Grow #${id}` }
function getTentName(tents: TentDto[], id: number | null) { return id == null ? 'Ohne Zelt' : tents.find((tent) => tent.id === id)?.name ?? `Zelt #${id}` }
function getHardwareName(items: HardwareItemDto[], id: number | null) { return id == null ? 'Hardware offen' : items.find((item) => item.id === id)?.name ?? `Hardware #${id}` }

export default MobileActionPage
