import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../api'
import type { CalibrationEventDto, GrowSummary, GrowTaskDto, HardwareItemDto, MaintenanceEventDto, RiskEventDto, SopInstanceDto } from '../types'
import { V1Alert, V1Page, V1Skeleton } from '../components/v1'
import { classNames } from '../utils'
import { RiskActionCard } from '../features/risks/RiskActionCard'

type ActionState = { grows: GrowSummary[]; risks: RiskEventDto[]; tasks: GrowTaskDto[]; maintenance: MaintenanceEventDto[]; calibration: CalibrationEventDto[]; sops: SopInstanceDto[]; hardware: HardwareItemDto[]; issues: string[] }
const initial: ActionState = { grows: [], risks: [], tasks: [], maintenance: [], calibration: [], sops: [], hardware: [], issues: [] }
const riskRank: Record<string, number> = { Critical: 0, Warning: 1, Info: 2 }

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

  const risks = useMemo(() => [...state.risks].sort((a, b) => (riskRank[a.severity] ?? 9) - (riskRank[b.severity] ?? 9)), [state.risks])
  const termine = buildTermine(state)
  const wartung = buildWartung(state)
  const critCount = risks.filter((risk) => risk.severity === 'Critical').length
  const warnCount = risks.filter((risk) => risk.severity === 'Warning').length

  const handleRiskChanged = (message: string) => {
    setNotice(message)
    setRefresh((current) => current + 1)
  }

  return (
    <V1Page
      eyebrow="Jetzt / Aufgaben"
      title="Was jetzt zu tun ist"
      subtitle="Risiken zuerst, dann Termine. Jede Zeile hat genau eine Hauptaktion — nichts, das man erst suchen muss."
    >
      {state.issues.length > 0 && <V1Alert title="Teilweise offline" message={state.issues.join(' · ')} tone="warn" />}
      {notice && <V1Alert title="Erledigt" message={notice} tone="ok" />}

      {loading ? <V1Skeleton tiles={3} rows={4} label="Lade Aufgaben" /> : (
        <div className="af-cols" data-audit="open-action-list">
          {/* Risiken zuerst — mit ihrer einen Hauptaktion. Die Karten bringen
              Bestätigen/Erledigen bereits mit. */}
          <section className="ls-panel af-col" data-audit="risk-action-section">
            <div className="ls-panel-head">
              <span className="ls-label">Risiken</span>
              {critCount > 0 && <span className="af-count is-crit">{critCount} kritisch</span>}
              {warnCount > 0 && <span className="af-count is-warn">{warnCount} Warnung</span>}
              {risks.length === 0 && <span className="ls-panel-meta">nichts offen</span>}
            </div>
            {risks.length === 0 ? (
              <div className="ls-panel-body"><p>Keine offenen Risiken im Bestand.</p></div>
            ) : (
              <div className="af-risks">
                {risks.map((risk) => (
                  <RiskActionCard
                    key={risk.id}
                    risk={risk}
                    context={risk.growId ? getGrowName(state.grows, risk.growId) : risk.hardwareItemId ? getHardwareName(state.hardware, risk.hardwareItemId) : risk.tentId ? `Zelt #${risk.tentId}` : 'System'}
                    onChanged={handleRiskChanged}
                  />
                ))}
              </div>
            )}
          </section>

          <section className="ls-panel af-col" data-audit="af-termine">
            <div className="ls-panel-head">
              <span className="ls-label">Termine</span>
              <span className="ls-panel-meta">{termine.length} offen</span>
            </div>
            {termine.length === 0 ? (
              <div className="ls-panel-body"><p>Keine offenen Termine.</p></div>
            ) : (
              <ul className="af-rows">{termine.map((item) => <AfRow key={item.id} item={item} />)}</ul>
            )}
          </section>

          <section className="ls-panel af-col" data-audit="af-wartung">
            <div className="ls-panel-head">
              <span className="ls-label">Wartung</span>
              <span className="ls-panel-meta">{wartung.length} fällig</span>
            </div>
            {wartung.length === 0 ? (
              <div className="ls-panel-body"><p>Nichts fällig — Sensoren und Technik sind versorgt.</p></div>
            ) : (
              <ul className="af-rows">{wartung.map((item) => <AfRow key={item.id} item={item} />)}</ul>
            )}
          </section>
        </div>
      )}
    </V1Page>
  )
}

type AfItem = { id: string; when: string; due: boolean; title: string; to: string; action: string }

function AfRow({ item }: { item: AfItem }) {
  return (
    <li className="af-row">
      <span className={classNames('af-when', item.due && 'is-due')}>{item.when}</span>
      <span className="af-title">{item.title}</span>
      <Link className="ls-btn is-small" to={item.to}>{item.action}</Link>
    </li>
  )
}

/** Aufgaben und laufende SOPs — das, was einen Zeitpunkt hat. */
function buildTermine(state: ActionState): AfItem[] {
  return [
    ...state.tasks.map((task) => {
      const when = dueShort(task.dueAtUtc)
      return {
        id: `task-${task.id}`,
        when: when.label,
        due: when.due || task.priority === 'Critical',
        title: task.title,
        to: task.growId ? `/journal?growId=${task.growId}` : '/journal',
        action: 'Öffnen',
      }
    }),
    ...state.sops.map((sop) => {
      const when = dueShort(sop.nextStepDueAtUtc ?? sop.dueAtUtc)
      return {
        id: `sop-${sop.id}`,
        when: when.label,
        due: when.due,
        title: sop.sopName,
        to: sop.growId ? `/sops?growId=${sop.growId}` : '/sops',
        action: 'Weiter',
      }
    }),
  ].sort((a, b) => Number(b.due) - Number(a.due) || a.title.localeCompare(b.title)).slice(0, 8)
}

/** Wartung, Kalibrierung und Hardware, die Aufmerksamkeit braucht. */
function buildWartung(state: ActionState): AfItem[] {
  const rows: AfItem[] = [
    ...state.maintenance.map((event) => {
      const when = dueInDays(event.dueAtUtc)
      return { id: `maintenance-${event.id}`, when: when.label, due: when.due, title: event.title, to: '/sensoren', action: 'Öffnen' }
    }),
    ...state.calibration.map((event) => {
      const when = dueInDays(event.dueAtUtc)
      return { id: `calibration-${event.id}`, when: when.label, due: when.due, title: event.title, to: '/sensoren', action: 'Öffnen' }
    }),
    ...state.hardware
      .filter((item) => item.status === 'Offline' || item.status === 'MaintenanceDue' || (isMappingExpected(item) && !item.haEntityId))
      .map((item) => ({
        id: `hardware-${item.id}`,
        when: 'jetzt',
        due: item.status === 'Offline' || item.criticality === 'Critical',
        title: `${item.name} · ${item.status === 'Offline' ? 'offline' : isMappingExpected(item) && !item.haEntityId ? 'kein Mapping' : 'Wartung fällig'}`,
        to: '/sensoren',
        action: 'Öffnen',
      })),
  ]
  return rows.sort((a, b) => Number(b.due) - Number(a.due) || a.title.localeCompare(b.title)).slice(0, 8)
}

/** „heute", Wochentag oder Datum — kurz genug für die Spalte. */
function dueShort(iso: string | null | undefined): { label: string; due: boolean } {
  if (!iso) return { label: 'offen', due: false }
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return { label: 'offen', due: false }
  const now = new Date()
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const diffDays = Math.floor((date.getTime() - startOfToday.getTime()) / 86_400_000)
  if (diffDays <= 0) return { label: 'heute', due: true }
  if (diffDays < 7) return { label: new Intl.DateTimeFormat('de-DE', { weekday: 'short' }).format(date), due: false }
  return { label: new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit' }).format(date), due: false }
}

/** „-1 T" / „7 T" — Wartung denkt in Tagen, nicht in Uhrzeiten. */
function dueInDays(iso: string | null | undefined): { label: string; due: boolean } {
  if (!iso) return { label: '—', due: false }
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return { label: '—', due: false }
  const days = Math.floor((date.getTime() - Date.now()) / 86_400_000)
  return { label: `${days} T`, due: days <= 0 }
}

// A "mapping missing" warning only makes sense for FIXED sensors that are supposed to
// deliver live values via Home Assistant. Handheld meters (e.g. a BlueLab pen) and
// equipment (pumps, chillers) are never mapped — nagging them was wrong.
function isMappingExpected(item: HardwareItemDto) {
  return item.deviceKind === 'FixedSensor'
}

function getGrowName(grows: GrowSummary[], id: number | null) { return id == null ? 'Grow offen' : grows.find((grow) => grow.id === id)?.name ?? `Grow #${id}` }
function getHardwareName(items: HardwareItemDto[], id: number | null) { return id == null ? 'Hardware offen' : items.find((item) => item.id === id)?.name ?? `Hardware #${id}` }
export default MobileActionPage
