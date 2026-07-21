import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../api'
import type { CalibrationEventDto, GrowSummary, GrowTaskDto, HardwareItemDto, MaintenanceEventDto, RiskEventDto, SopInstanceDto } from '../types'
import { V1Alert, V1Card, V1Empty, V1LinkButton, V1Page, V1Section, V1Stat } from '../components/v1'
import { classNames, formatDateTime, formatSeverityLabel } from '../utils'
import { RiskActionCard } from '../features/risks/RiskActionCard'

type ActionState = { grows: GrowSummary[]; risks: RiskEventDto[]; tasks: GrowTaskDto[]; maintenance: MaintenanceEventDto[]; calibration: CalibrationEventDto[]; sops: SopInstanceDto[]; hardware: HardwareItemDto[]; issues: string[] }
const initial: ActionState = { grows: [], risks: [], tasks: [], maintenance: [], calibration: [], sops: [], hardware: [], issues: [] }
const riskRank: Record<string, number> = { Critical: 0, Warning: 1, Info: 2 }
const taskRank: Record<string, number> = { Critical: 0, High: 1, Normal: 2, Low: 3 }

type ActionRow = {
  id: string
  title: string
  context: string
  priority: string
  action: string
  to: string
  tone: 'critical' | 'warning' | 'normal'
  rank: number
}

function MobileActionPage() {
  const [state, setState] = useState<ActionState>(initial)
  const [loading, setLoading] = useState(true)
  const [refresh, setRefresh] = useState(0)
  const [notice, setNotice] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      const issues: string[] = []
      const dueBeforeUtc = new Date(Date.now() + 3 * 24 * 60 * 60 * 1000).toISOString()
      const safe = async <T,>(label: string, path: string, fallback: T): Promise<T> => {
        try { return await apiFetch<T>(path, { signal: controller.signal }) } catch { if (!controller.signal.aborted) issues.push(label); return fallback }
      }
      const [grows, risks, maintenance, calibration, hardware] = await Promise.all([
        safe<GrowSummary[]>('Grows', '/api/grows?archived=false', []),
        safe<RiskEventDto[]>('Risiken', '/api/risk-events?openOnly=true', []),
        safe<MaintenanceEventDto[]>('Wartung', `/api/maintenance-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
        safe<CalibrationEventDto[]>('Kalibrierung', `/api/calibration-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
        safe<HardwareItemDto[]>('Hardware', '/api/hardware-items', []),
      ])
      const activeGrows = grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning')
      const taskLists = await Promise.all(activeGrows.map((grow) => safe<GrowTaskDto[]>(`Tasks ${grow.id}`, `/api/grows/${grow.id}/tasks`, [])))
      const sopLists = await Promise.all(activeGrows.map((grow) => safe<SopInstanceDto[]>(`SOP ${grow.id}`, `/api/sop-instances?growId=${grow.id}`, [])))
      if (controller.signal.aborted) return
      setState({ grows, risks: risks.filter((risk) => risk.status === 'Open' || risk.status === 'Acknowledged'), maintenance: maintenance.filter((item) => item.status === 'Planned'), calibration: calibration.filter((item) => item.status === 'Planned'), tasks: taskLists.flat().filter((task) => task.status === 'Open'), sops: sopLists.flat().filter((sop) => sop.status === 'Active'), hardware, issues })
      setLoading(false)
    }
    void load()
    return () => controller.abort()
  }, [refresh])

  const activeGrows = useMemo(() => state.grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning'), [state.grows])
  const risks = useMemo(() => [...state.risks].sort((a, b) => (riskRank[a.severity] ?? 9) - (riskRank[b.severity] ?? 9)), [state.risks])
  const rows = buildRows(state)
  const status = loading ? 'Lädt' : risks.some((risk) => risk.severity === 'Critical') ? 'Kritisch' : risks.length > 0 || rows.length > 0 ? 'Offen' : 'Bereit'
  const primaryGrow = activeGrows[0]
  const actionCards = buildActionCards(state, primaryGrow)
  const handleRiskChanged = (message: string) => {
    setNotice(message)
    setRefresh((current) => current + 1)
  }

  return (
    <V1Page eyebrow="Aktion" title={status}>
      {state.issues.length > 0 && <V1Alert title="Teilweise offline" message={state.issues.join(' · ')} tone="warn" />}
      {notice && <V1Alert title="Erledigt" message={notice} tone="ok" />}
      <section className="v1-kpi-grid"><V1Stat label="Risiken" value={risks.length} /><V1Stat label="Wartung" value={state.maintenance.length} /><V1Stat label="Kalibrierung" value={state.calibration.length} /><V1Stat label="Grows" value={activeGrows.length} /></section>
      <section className="rc-action-guide-grid">
        {actionCards.map((card) => (
          <V1Card key={card.key} className="rc-action-guide-card" tone={card.tone}>
            <span className="v1-card-kicker">{card.kicker}</span>
            <h2>{card.title}</h2>
            <p>{card.description}</p>
            <p>{card.status}</p>
            <V1LinkButton to={card.to} variant={card.primary ? 'primary' : 'secondary'}>{card.cta}</V1LinkButton>
          </V1Card>
        ))}
      </section>
      {risks.length > 0 && (
        <V1Section title="Risiken">
          <div className="rc-risk-action-grid" data-audit="risk-action-section">
            {risks.map((risk) => (
              <RiskActionCard
                key={risk.id}
                risk={risk}
                context={risk.growId ? getGrowName(state.grows, risk.growId) : risk.hardwareItemId ? getHardwareName(state.hardware, risk.hardwareItemId) : risk.tentId ? `Zelt #${risk.tentId}` : 'System'}
                onChanged={handleRiskChanged}
              />
            ))}
          </div>
        </V1Section>
      )}
      <V1Section title="Jetzt">
        {loading ? <V1Empty title="Lade Aktionen..." /> : rows.length === 0 ? <V1Empty title="Keine offenen Aufgaben" text="Es gibt aktuell keine kritischen Risiken, fälligen Wartungen oder aktiven SOP-Schritte." /> : <div className="v1-list rc-action-list" data-audit="open-action-list">{rows.map((row) => <ActionListRow key={row.id} row={row} />)}</div>}
      </V1Section>
    </V1Page>
  )
}

function ActionListRow({ row }: { row: ActionRow }) {
  return (
    <Link to={row.to} className={classNames('v1-list-row rc-action-row', row.tone)} data-audit="open-action-row">
      <div>
        <strong>{row.title}</strong>
        <span>{row.context}</span>
      </div>
      <em>{formatSeverityLabel(row.priority)}</em>
      <small>{row.action}</small>
    </Link>
  )
}

function buildRows(state: ActionState): ActionRow[] {
  return [
    ...state.tasks.map((task) => ({
      id: `task-${task.id}`,
      title: task.title,
      context: task.growName ?? getGrowName(state.grows, task.growId),
      priority: task.priority,
      action: task.dueAtUtc ? `Fällig ${formatDateTime(task.dueAtUtc)}` : 'Offene Aufgabe',
      to: `/grows/${task.growId}`,
      tone: task.priority === 'Critical' ? 'critical' as const : task.priority === 'High' ? 'warning' as const : 'normal' as const,
      rank: 20 + (taskRank[task.priority] ?? 9),
    })),
    ...state.sops.map((sop) => ({
      id: `sop-${sop.id}`,
      title: sop.sopName,
      context: getGrowName(state.grows, sop.growId),
      priority: 'SOP',
      action: formatDateTime(sop.nextStepDueAtUtc ?? sop.dueAtUtc),
      to: `/grows/${sop.growId}`,
      tone: 'normal' as const,
      rank: 35,
    })),
    ...state.maintenance.map((event) => ({
      id: `maintenance-${event.id}`,
      title: event.title,
      context: getHardwareName(state.hardware, event.hardwareItemId),
      priority: 'Wartung',
      action: formatDateTime(event.dueAtUtc),
      to: '/hardware',
      tone: 'warning' as const,
      rank: 40,
    })),
    ...state.calibration.map((event) => ({
      id: `calibration-${event.id}`,
      title: event.title,
      context: getHardwareName(state.hardware, event.hardwareItemId),
      priority: 'Kalibrierung',
      action: formatDateTime(event.dueAtUtc),
      to: '/hardware',
      tone: 'warning' as const,
      rank: 41,
    })),
    ...buildHardwareRows(state),
  ].sort((a, b) => a.rank - b.rank || a.title.localeCompare(b.title)).slice(0, 16)
}

// A "mapping missing" warning only makes sense for FIXED sensors that are supposed to
// deliver live values via Home Assistant. Handheld meters (e.g. a BlueLab pen) and
// equipment (pumps, chillers) are never mapped — nagging them was wrong.
function isMappingExpected(item: HardwareItemDto) {
  return item.deviceKind === 'FixedSensor'
}

function buildHardwareRows(state: ActionState): ActionRow[] {
  return state.hardware
    .filter((item) => item.status === 'Offline' || item.status === 'MaintenanceDue' || (isMappingExpected(item) && !item.haEntityId))
    .map((item) => ({
      id: `hardware-${item.id}`,
      title: item.name,
      context: item.growId ? getGrowName(state.grows, item.growId) : item.hydroSetupId ? `Hydro #${item.hydroSetupId}` : item.tentId ? `Zelt #${item.tentId}` : 'Hardware',
      priority: item.status === 'Offline' || item.criticality === 'Critical' ? 'Critical' : 'Warning',
      action: item.status === 'Offline' ? 'Offline prüfen' : isMappingExpected(item) && !item.haEntityId ? 'Mapping prüfen' : 'Wartung prüfen',
      to: '/hardware',
      tone: item.status === 'Offline' || item.criticality === 'Critical' ? 'critical' as const : 'warning' as const,
      rank: item.status === 'Offline' ? 10 : 45,
    }))
}

function buildActionCards(state: ActionState, primaryGrow: GrowSummary | undefined) {
  const activeSensors = state.hardware.filter((item) => isSensorLike(item) && item.status === 'Active').length
  const mappedHardware = state.hardware.filter((item) => item.haEntityId).length
  const unmappedFixedSensors = state.hardware.filter((item) => isMappingExpected(item) && !item.haEntityId).length
  const dueSensorWork = state.maintenance.length + state.calibration.length

  return [
    {
      key: 'addback',
      kicker: 'Addback',
      title: 'Addback berechnen',
      description: 'Reservoir, Wasserstand und Ziel-EC prüfen.',
      status: primaryGrow ? `Kontext: ${primaryGrow.name}` : 'Kein aktiver Grow ausgewählt.',
      to: primaryGrow ? `/grows/${primaryGrow.id}/addback` : '/grows/new',
      cta: primaryGrow ? 'Addback starten' : 'Grow starten',
      primary: true,
      tone: state.risks.some((risk) => risk.severity === 'Critical') ? 'warn' as const : 'neutral' as const,
    },
    {
      key: 'measurement',
      kicker: 'Messung',
      title: 'Werte dokumentieren',
      description: 'pH, EC, Klima und Beobachtungen speichern.',
      status: primaryGrow?.latestMeasurementAt ? `Letzte Messung: ${formatDateTime(primaryGrow.latestMeasurementAt)}` : 'Noch keine aktuelle Messung erkannt.',
      to: '/messung',
      cta: 'Messung erfassen',
      primary: false,
      tone: 'neutral' as const,
    },
    {
      key: 'sensors',
      kicker: 'Sensoren',
      title: 'Sensoren prüfen',
      description: 'Offline-Sensoren und fällige Pflege prüfen.',
      status: activeSensors === 0 ? 'Sensorvertrauen nicht bewertet.' : `${activeSensors} aktive Sensoren, ${dueSensorWork} fällige Aufgaben.`,
      to: '/hardware',
      cta: 'Sensoren prüfen',
      primary: false,
      tone: activeSensors === 0 || dueSensorWork > 0 ? 'warn' as const : 'ok' as const,
    },
    {
      key: 'ha',
      kicker: 'Home Assistant',
      title: 'HA-Mapping prüfen',
      description: 'Hardware-Entities in Home Assistant prüfen.',
      status: unmappedFixedSensors > 0
        ? `${unmappedFixedSensors} fester Sensor(en) ohne Entity.`
        : mappedHardware > 0
          ? `${mappedHardware} Hardware-Entities verknüpft.`
          : 'Keine festen Sensoren gemappt — optional.',
      to: '/home-assistant',
      cta: 'HA einrichten',
      primary: false,
      tone: unmappedFixedSensors > 0 ? 'warn' as const : 'neutral' as const,
    },
  ]
}

function getGrowName(grows: GrowSummary[], id: number | null) { return id == null ? 'Grow offen' : grows.find((grow) => grow.id === id)?.name ?? `Grow #${id}` }
function getHardwareName(items: HardwareItemDto[], id: number | null) { return id == null ? 'Hardware offen' : items.find((item) => item.id === id)?.name ?? `Hardware #${id}` }
function isSensorLike(item: HardwareItemDto) {
  if (item.deviceKind === 'FixedSensor' || item.deviceKind === 'HandheldMeter') return true
  if (item.deviceKind === 'Equipment') return false
  const text = `${item.name} ${item.category}`.toLowerCase()
  return ['sensor', 'sonde', 'probe', 'ph', 'ec', 'orp', 'do', 'temperatur', 'level'].some((term) => text.includes(term))
}
export default MobileActionPage
