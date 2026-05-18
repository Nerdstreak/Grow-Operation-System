import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../api'
import type { CalibrationEventDto, GrowSummary, GrowTaskDto, HardwareItemDto, MaintenanceEventDto, RiskEventDto, SopInstanceDto } from '../types'
import { V1Alert, V1Card, V1Empty, V1LinkButton, V1Page, V1Section, V1Stat } from '../components/v1'
import { classNames, formatDateTime } from '../utils'

type ActionState = { grows: GrowSummary[]; risks: RiskEventDto[]; tasks: GrowTaskDto[]; maintenance: MaintenanceEventDto[]; calibration: CalibrationEventDto[]; sops: SopInstanceDto[]; hardware: HardwareItemDto[]; issues: string[] }
const initial: ActionState = { grows: [], risks: [], tasks: [], maintenance: [], calibration: [], sops: [], hardware: [], issues: [] }
const riskRank: Record<string, number> = { Critical: 0, Warning: 1, Info: 2 }

function MobileActionPage() {
  const [state, setState] = useState<ActionState>(initial)
  const [loading, setLoading] = useState(true)

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
        safe<RiskEventDto[]>('Risiken', '/api/risk-events?status=Open', []),
        safe<MaintenanceEventDto[]>('Wartung', `/api/maintenance-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
        safe<CalibrationEventDto[]>('Kalibrierung', `/api/calibration-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
        safe<HardwareItemDto[]>('Hardware', '/api/hardware-items', []),
      ])
      const activeGrows = grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning')
      const taskLists = await Promise.all(activeGrows.map((grow) => safe<GrowTaskDto[]>(`Tasks ${grow.id}`, `/api/grows/${grow.id}/tasks`, [])))
      const sopLists = await Promise.all(activeGrows.map((grow) => safe<SopInstanceDto[]>(`SOP ${grow.id}`, `/api/sop-instances?growId=${grow.id}`, [])))
      if (controller.signal.aborted) return
      setState({ grows, risks: risks.filter((risk) => risk.status === 'Open'), maintenance: maintenance.filter((item) => item.status === 'Planned'), calibration: calibration.filter((item) => item.status === 'Planned'), tasks: taskLists.flat().filter((task) => task.status === 'Open'), sops: sopLists.flat().filter((sop) => sop.status === 'Active'), hardware, issues })
      setLoading(false)
    }
    void load()
    return () => controller.abort()
  }, [])

  const activeGrows = useMemo(() => state.grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning'), [state.grows])
  const risks = useMemo(() => [...state.risks].sort((a, b) => (riskRank[a.severity] ?? 9) - (riskRank[b.severity] ?? 9)), [state.risks])
  const rows = buildRows(state, risks)
  const status = loading ? 'Lädt' : risks.some((risk) => risk.severity === 'Critical') ? 'Kritisch' : rows.length > 0 ? 'Offen' : 'Bereit'
  const primaryGrow = activeGrows[0]
  const actionCards = buildActionCards(state, primaryGrow)

  return (
    <V1Page eyebrow="Aktion" title={status}>
      {state.issues.length > 0 && <V1Alert title="Teilweise offline" message={state.issues.join(' · ')} tone="warn" />}
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
      <V1Section title="Jetzt">
        {loading ? <V1Empty title="Lade Aktionen..." /> : rows.length === 0 ? <V1Empty title="Keine offenen Aktionen" text="Es gibt aktuell keine kritischen Risiken, fälligen Wartungen oder aktiven SOP-Schritte." /> : <div className="v1-list">{rows.map((row) => <Link key={row.id} to={row.to} className={classNames('v1-list-row', row.tone)}><strong>{row.title}</strong><span>{row.meta}</span></Link>)}</div>}
      </V1Section>
    </V1Page>
  )
}

function buildRows(state: ActionState, risks: RiskEventDto[]) {
  return [
    ...risks.map((risk) => ({ id: `risk-${risk.id}`, title: risk.title, meta: `${risk.severity} · ${risk.eventType}`, to: '/hardware', tone: risk.severity === 'Critical' ? 'critical' : 'warning' })),
    ...state.sops.map((sop) => ({ id: `sop-${sop.id}`, title: sop.sopName, meta: `${getGrowName(state.grows, sop.growId)} · ${formatDateTime(sop.nextStepDueAtUtc ?? sop.dueAtUtc)}`, to: `/grows/${sop.growId}`, tone: 'normal' })),
    ...state.maintenance.map((event) => ({ id: `maintenance-${event.id}`, title: event.title, meta: `${getHardwareName(state.hardware, event.hardwareItemId)} · ${formatDateTime(event.dueAtUtc)}`, to: '/hardware', tone: 'warning' })),
    ...state.calibration.map((event) => ({ id: `calibration-${event.id}`, title: event.title, meta: `${getHardwareName(state.hardware, event.hardwareItemId)} · ${formatDateTime(event.dueAtUtc)}`, to: '/hardware', tone: 'warning' })),
    ...state.tasks.map((task) => ({ id: `task-${task.id}`, title: task.title, meta: `${task.growName ?? getGrowName(state.grows, task.growId)} · ${formatDateTime(task.dueAtUtc)}`, to: `/grows/${task.growId}`, tone: 'normal' })),
  ].slice(0, 12)
}

function buildActionCards(state: ActionState, primaryGrow: GrowSummary | undefined) {
  const activeSensors = state.hardware.filter((item) => isSensorLike(item) && item.status === 'Active').length
  const mappedHardware = state.hardware.filter((item) => item.haEntityId).length
  const dueSensorWork = state.maintenance.length + state.calibration.length

  return [
    {
      key: 'addback',
      kicker: 'Addback',
      title: 'Nährlösung prüfen und Addback berechnen',
      description: 'Reservoirwerte, Wasserstand und Ziel-EC zusammen kontrollieren.',
      status: primaryGrow ? `Kontext: ${primaryGrow.name}` : 'Kein aktiver Grow ausgewählt.',
      to: primaryGrow ? `/grows/${primaryGrow.id}/addback` : '/grows/new',
      cta: primaryGrow ? 'Addback starten' : 'Grow starten',
      primary: true,
      tone: state.risks.some((risk) => risk.severity === 'Critical') ? 'warn' as const : 'neutral' as const,
    },
    {
      key: 'measurement',
      kicker: 'Messung',
      title: 'Aktuelle Werte dokumentieren',
      description: 'pH, EC, Klima, Reservoir und Beobachtungen als Verlauf speichern.',
      status: primaryGrow?.latestMeasurementAt ? `Letzte Messung: ${formatDateTime(primaryGrow.latestMeasurementAt)}` : 'Noch keine aktuelle Messung erkannt.',
      to: '/messung',
      cta: 'Messung erfassen',
      primary: false,
      tone: 'neutral' as const,
    },
    {
      key: 'sensors',
      kicker: 'Sensoren',
      title: 'Sensorstatus, Wartung und Kalibrierung prüfen',
      description: 'Offline-Sensoren und fällige Pflege entscheiden, wie belastbar Live-Werte sind.',
      status: activeSensors === 0 ? 'Sensorvertrauen nicht bewertet.' : `${activeSensors} aktive Sensoren, ${dueSensorWork} fällige Aufgaben.`,
      to: '/hardware',
      cta: 'Sensoren prüfen',
      primary: false,
      tone: activeSensors === 0 || dueSensorWork > 0 ? 'warn' as const : 'ok' as const,
    },
    {
      key: 'ha',
      kicker: 'Home Assistant',
      title: 'Verbindung und Mapping prüfen',
      description: 'Sensor-Inventar bleibt getrennt vom Entity-Mapping in Home Assistant.',
      status: mappedHardware > 0 ? `${mappedHardware} Hardware-Entities verknüpft.` : 'Mapping noch offen oder unvollständig.',
      to: '/home-assistant',
      cta: 'HA einrichten',
      primary: false,
      tone: mappedHardware > 0 ? 'neutral' as const : 'warn' as const,
    },
  ]
}

function getGrowName(grows: GrowSummary[], id: number | null) { return id == null ? 'Grow offen' : grows.find((grow) => grow.id === id)?.name ?? `Grow #${id}` }
function getHardwareName(items: HardwareItemDto[], id: number | null) { return id == null ? 'Hardware offen' : items.find((item) => item.id === id)?.name ?? `Hardware #${id}` }
function isSensorLike(item: HardwareItemDto) {
  const text = `${item.name} ${item.category}`.toLowerCase()
  return ['sensor', 'sonde', 'probe', 'ph', 'ec', 'orp', 'do', 'temperatur', 'level'].some((term) => text.includes(term))
}
export default MobileActionPage
