import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type {
  CalibrationEventDto,
  GrowSummary,
  GrowTaskDto,
  HardwareItemDto,
  MaintenanceEventDto,
  RiskEventDto,
  SopInstanceDto,
  TentDto,
} from '../types'
import { classNames, formatDate, formatDateTime } from '../utils'

type LoadIssue = {
  area: string
  message: string
}

type MobileActionData = {
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

type ActionCardItem = {
  id: string
  title: string
  context: string
  primaryLabel: string
  primaryTo: string
  secondaryLabel?: string
  secondaryTo?: string
  tone?: 'critical' | 'warning' | 'normal'
}

const emptyData: MobileActionData = {
  grows: [],
  tasks: [],
  tents: [],
  hardwareItems: [],
  riskEvents: [],
  maintenanceEvents: [],
  calibrationEvents: [],
  sopInstances: [],
  issues: [],
}

const riskRank: Record<string, number> = {
  Critical: 0,
  Warning: 1,
  Info: 2,
}

function MobileActionPage() {
  const [data, setData] = useState<MobileActionData>(emptyData)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      const issues: LoadIssue[] = []
      const dueBeforeUtc = new Date(Date.now() + 3 * 24 * 60 * 60 * 1000).toISOString()

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
        const [grows, settings, hardwareItems, riskEvents, maintenanceEvents, calibrationEvents] = await Promise.all([
          fetchOptional<GrowSummary[]>('Grows', '/api/grows?archived=false', []),
          fetchOptional<{ tents: TentDto[] }>('Einstellungen', '/api/settings', { tents: [] }),
          fetchOptional<HardwareItemDto[]>('Hardware', '/api/hardware-items', []),
          fetchOptional<RiskEventDto[]>('RiskEvents', '/api/risk-events?status=Open', []),
          fetchOptional<MaintenanceEventDto[]>('MaintenanceEvents', `/api/maintenance-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
          fetchOptional<CalibrationEventDto[]>('CalibrationEvents', `/api/calibration-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
        ])

        const activeGrows = grows.filter((grow) => grow.status === 'Running')
        const [taskResults, sopResults] = await Promise.all([
          Promise.all(activeGrows.map((grow) => fetchOptional<GrowTaskDto[]>('Tasks', `/api/grows/${grow.id}/tasks`, []))),
          Promise.all(activeGrows.map((grow) => fetchOptional<SopInstanceDto[]>('SOPs', `/api/sop-instances?growId=${grow.id}`, []))),
        ])

        if (controller.signal.aborted) return

        setData({
          grows,
          tasks: taskResults.flat().filter((task) => task.status === 'Open'),
          tents: settings.tents,
          hardwareItems,
          riskEvents: riskEvents.filter((event) => event.status === 'Open'),
          maintenanceEvents: maintenanceEvents.filter((event) => event.status === 'Planned'),
          calibrationEvents: calibrationEvents.filter((event) => event.status === 'Planned'),
          sopInstances: sopResults.flat().filter((instance) => instance.status === 'Active'),
          issues,
        })
      } catch (caught) {
        if (!controller.signal.aborted) {
          setData((current) => ({
            ...current,
            issues: [...issues, { area: 'Action', message: formatApiError(caught, 'Action-Daten konnten nicht geladen werden.') }],
          }))
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  const activeGrows = useMemo(() => data.grows.filter((grow) => grow.status === 'Running'), [data.grows])
  const sortedRisks = useMemo(
    () => [...data.riskEvents].sort((a, b) => (riskRank[a.severity] ?? 99) - (riskRank[b.severity] ?? 99) || a.startedAtUtc.localeCompare(b.startedAtUtc)),
    [data.riskEvents],
  )
  const dueMaintenance = useMemo(
    () => [...data.maintenanceEvents].sort((a, b) => compareNullableDate(a.dueAtUtc, b.dueAtUtc)),
    [data.maintenanceEvents],
  )
  const dueCalibrations = useMemo(
    () => [...data.calibrationEvents].sort((a, b) => compareNullableDate(a.dueAtUtc, b.dueAtUtc)),
    [data.calibrationEvents],
  )
  const activeSops = useMemo(
    () => [...data.sopInstances].sort((a, b) => compareNullableDate(a.nextStepDueAtUtc ?? a.dueAtUtc, b.nextStepDueAtUtc ?? b.dueAtUtc)),
    [data.sopInstances],
  )
  const openTasks = useMemo(
    () => [...data.tasks].sort((a, b) => compareNullableDate(a.dueAtUtc, b.dueAtUtc)),
    [data.tasks],
  )

  const criticalRisks = sortedRisks.filter((event) => event.severity === 'Critical')
  const warningRisks = sortedRisks.filter((event) => event.severity === 'Warning')
  const statusTone = criticalRisks.length > 0
    ? 'critical'
    : warningRisks.length > 0 || dueMaintenance.length > 0 || dueCalibrations.length > 0
      ? 'warning'
      : 'normal'
  const statusLabel = statusTone === 'critical' ? 'Kritisch' : statusTone === 'warning' ? 'Beobachten' : 'Stabil'
  const statusText = loading
    ? 'Lade Action-Daten...'
    : statusTone === 'critical'
      ? 'Kritische Risiken zuerst prüfen.'
      : statusTone === 'warning'
        ? 'Offene Warnungen oder fällige Arbeiten prüfen.'
        : 'Keine dringenden Aktionen im aktuellen Fenster.'

  const actionCards = buildActionCards({
    criticalRisks,
    activeSops,
    dueMaintenance,
    dueCalibrations,
    openTasks,
    activeGrows,
    grows: data.grows,
    hardwareItems: data.hardwareItems,
  })

  const primaryGrowTarget = activeGrows.length === 1 ? `/grows/${activeGrows[0].id}` : '#active-grows'
  const primaryAddbackTarget = activeGrows.length === 1 ? `/grows/${activeGrows[0].id}/addback` : '#active-grows'

  return (
    <>
      <div className="topbar">
        <span className="topbar-title">Aktion</span>
        <div className="topbar-right">
          <Link className="btn" to="/">Operations</Link>
          <Link className="btn btn-primary" to="/hardware">Hardware</Link>
        </div>
      </div>

      <div className="page-scroll">
        <div className="action-page">
          {data.issues.length > 0 && (
            <div className="alert-bar">
              <div className="alert-dot" />
              <strong>Teilweise geladen</strong>
              <span>{data.issues.map((issue) => `${issue.area}: ${issue.message}`).join(' | ')}</span>
            </div>
          )}

          <section className={classNames('action-hero', statusTone === 'critical' && 'is-critical', statusTone === 'warning' && 'is-warning')}>
            <div>
              <div className="section-label">Mobile Action</div>
              <h1>{statusLabel}</h1>
              <p>{statusText}</p>
            </div>
            <div className="action-status-grid">
              <StatusChip label="Risiken" value={sortedRisks.length} tone={criticalRisks.length > 0 ? 'critical' : warningRisks.length > 0 ? 'warning' : 'normal'} />
              <StatusChip label="Wartung" value={dueMaintenance.length} tone={dueMaintenance.length > 0 ? 'warning' : 'normal'} />
              <StatusChip label="Kalibrierung" value={dueCalibrations.length} tone={dueCalibrations.length > 0 ? 'warning' : 'normal'} />
              <StatusChip label="Grows" value={activeGrows.length} tone="normal" />
            </div>
          </section>

          <section>
            <div className="section-label">Jetzt erledigen</div>
            {loading ? (
              <div className="empty-hint">Lade Aktionen...</div>
            ) : actionCards.length === 0 ? (
              <div className="action-card">
                <div className="action-card-title">Stabil</div>
                <div className="action-card-context">Keine kritischen Risiken, fälligen Wartungen oder offenen Action-Items gefunden.</div>
                <div className="action-actions">
                  <Link className="btn btn-primary action-primary" to="/">Operations öffnen</Link>
                </div>
              </div>
            ) : (
              <div className="action-grid">
                {actionCards.map((item) => (
                  <ActionCard key={item.id} item={item} />
                ))}
              </div>
            )}
          </section>

          <section>
            <div className="section-label">Quick Actions</div>
            <div className="action-grid">
              {activeGrows.length > 0 && (
                <>
                  <QuickAction label="Messung eintragen" to={primaryGrowTarget} primary />
                  <QuickAction label="Foto hinzufügen" to={primaryGrowTarget} />
                  <QuickAction label="SOPs fortsetzen" to={primaryGrowTarget} />
                  <QuickAction label="Addback berechnen" to={primaryAddbackTarget} />
                </>
              )}
              <QuickAction label="Risiken prüfen" to="/hardware" primary={activeGrows.length === 0} />
              <QuickAction label="Hardware öffnen" to="/hardware" />
            </div>
          </section>

          <section id="active-grows">
            <div className="section-label">Aktive Grows</div>
            {loading ? (
              <div className="empty-hint">Lade Grows...</div>
            ) : activeGrows.length === 0 ? (
              <div className="empty-hint">Keine aktiven Grows gefunden.</div>
            ) : (
              <div className="action-grid">
                {activeGrows.map((grow) => (
                  <article key={grow.id} className="action-card">
                    <div>
                      <div className="action-card-title">{grow.name}</div>
                      <div className="action-card-context">
                        {grow.latestStage ?? 'Phase offen'} · {grow.tentName ?? getTentName(data.tents, grow.tentId)} · letzte Messung {formatDate(grow.latestMeasurementAt)}
                      </div>
                    </div>
                    <div className="action-actions">
                      <Link className="btn btn-primary action-primary" to={`/grows/${grow.id}`}>Öffnen</Link>
                      <Link className="btn action-primary" to={`/grows/${grow.id}`}>Messung</Link>
                      <Link className="btn action-primary" to={`/grows/${grow.id}/addback`}>Addback</Link>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </section>

          <section>
            <div className="section-label">Risk-Actions</div>
            {loading ? (
              <div className="empty-hint">Lade Risiken...</div>
            ) : sortedRisks.length === 0 ? (
              <div className="empty-hint">Keine offenen RiskEvents.</div>
            ) : (
              <div className="action-grid">
                {sortedRisks.slice(0, 6).map((event) => (
                  <article key={event.id} className={classNames('action-card', event.severity === 'Critical' && 'is-critical', event.severity === 'Warning' && 'is-warning')}>
                    <div>
                      <div className="action-card-kicker">{event.severity} · {event.eventType}</div>
                      <div className="action-card-title">{event.title}</div>
                      <div className="action-card-context">
                        {getHardwareName(data.hardwareItems, event.hardwareItemId)} · {getTentName(data.tents, event.tentId)} · {getGrowName(data.grows, event.growId)}
                      </div>
                      <div className="action-card-context">Seit {formatDateTime(event.startedAtUtc)}</div>
                    </div>
                    <div className="action-actions">
                      <Link className="btn btn-primary action-primary" to="/hardware">Hardware öffnen</Link>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </section>
        </div>
      </div>
    </>
  )
}

function buildActionCards({
  criticalRisks,
  activeSops,
  dueMaintenance,
  dueCalibrations,
  openTasks,
  activeGrows,
  grows,
  hardwareItems,
}: {
  criticalRisks: RiskEventDto[]
  activeSops: SopInstanceDto[]
  dueMaintenance: MaintenanceEventDto[]
  dueCalibrations: CalibrationEventDto[]
  openTasks: GrowTaskDto[]
  activeGrows: GrowSummary[]
  grows: GrowSummary[]
  hardwareItems: HardwareItemDto[]
}): ActionCardItem[] {
  const cards: ActionCardItem[] = []

  criticalRisks.slice(0, 3).forEach((event) => {
    cards.push({
      id: `risk-${event.id}`,
      title: event.title,
      context: `${event.eventType} · ${getHardwareName(hardwareItems, event.hardwareItemId)} · ${getGrowName(grows, event.growId)}`,
      primaryLabel: 'Risiko prüfen',
      primaryTo: '/hardware',
      tone: 'critical',
    })
  })

  activeSops.slice(0, 3).forEach((instance) => {
    cards.push({
      id: `sop-${instance.id}`,
      title: `SOP fortsetzen: ${instance.sopName}`,
      context: `${getGrowName(grows, instance.growId)} · nächster Schritt ${formatDateTime(instance.nextStepDueAtUtc ?? instance.dueAtUtc)}`,
      primaryLabel: 'Grow öffnen',
      primaryTo: `/grows/${instance.growId}`,
      tone: 'normal',
    })
  })

  dueMaintenance.slice(0, 3).forEach((event) => {
    cards.push({
      id: `maintenance-${event.id}`,
      title: event.title,
      context: `${getHardwareName(hardwareItems, event.hardwareItemId)} · fällig ${formatDateTime(event.dueAtUtc)}`,
      primaryLabel: 'Wartung öffnen',
      primaryTo: '/hardware',
      tone: 'warning',
    })
  })

  dueCalibrations.slice(0, 3).forEach((event) => {
    cards.push({
      id: `calibration-${event.id}`,
      title: event.title,
      context: `${getHardwareName(hardwareItems, event.hardwareItemId)} · fällig ${formatDateTime(event.dueAtUtc)}`,
      primaryLabel: 'Kalibrierung öffnen',
      primaryTo: '/hardware',
      tone: 'warning',
    })
  })

  openTasks.slice(0, 4).forEach((task) => {
    cards.push({
      id: `task-${task.id}`,
      title: task.title,
      context: `${task.growName ?? getGrowName(grows, task.growId)}${task.dueAtUtc ? ` · fällig ${formatDateTime(task.dueAtUtc)}` : ''}`,
      primaryLabel: 'Grow öffnen',
      primaryTo: `/grows/${task.growId}`,
      tone: task.priority === 'Critical' || task.priority === 'High' ? 'warning' : 'normal',
    })
  })

  if (cards.length === 0) {
    activeGrows.slice(0, 3).forEach((grow) => {
      cards.push({
        id: `grow-${grow.id}`,
        title: `Grow prüfen: ${grow.name}`,
        context: `${grow.latestStage ?? 'Phase offen'} · ${grow.tentName ?? 'Ohne Zelt'} · letzte Messung ${formatDate(grow.latestMeasurementAt)}`,
        primaryLabel: 'Grow öffnen',
        primaryTo: `/grows/${grow.id}`,
        secondaryLabel: 'Addback',
        secondaryTo: `/grows/${grow.id}/addback`,
        tone: 'normal',
      })
    })
  }

  return cards
}

function ActionCard({ item }: { item: ActionCardItem }) {
  return (
    <article className={classNames('action-card', item.tone === 'critical' && 'is-critical', item.tone === 'warning' && 'is-warning')}>
      <div>
        <div className="action-card-title">{item.title}</div>
        <div className="action-card-context">{item.context}</div>
      </div>
      <div className="action-actions">
        <Link className="btn btn-primary action-primary" to={item.primaryTo}>{item.primaryLabel}</Link>
        {item.secondaryLabel && item.secondaryTo && (
          <Link className="btn action-primary" to={item.secondaryTo}>{item.secondaryLabel}</Link>
        )}
      </div>
    </article>
  )
}

function QuickAction({ label, to, primary = false }: { label: string; to: string; primary?: boolean }) {
  const className = classNames('btn', 'action-primary', primary && 'btn-primary')
  if (to.startsWith('#')) {
    return <a className={className} href={to}>{label}</a>
  }

  return <Link className={className} to={to}>{label}</Link>
}

function StatusChip({ label, value, tone }: { label: string; value: number; tone: 'critical' | 'warning' | 'normal' }) {
  return (
    <div className={classNames('action-status-chip', tone === 'critical' && 'is-critical', tone === 'warning' && 'is-warning')}>
      <strong>{value}</strong>
      <span>{label}</span>
    </div>
  )
}

function compareNullableDate(left: string | null | undefined, right: string | null | undefined): number {
  if (!left && !right) return 0
  if (!left) return 1
  if (!right) return -1
  return new Date(left).getTime() - new Date(right).getTime()
}

function getHardwareName(items: HardwareItemDto[], hardwareItemId: number | null): string {
  if (!hardwareItemId) return 'Ohne Hardware'
  return items.find((item) => item.id === hardwareItemId)?.name ?? `Hardware #${hardwareItemId}`
}

function getTentName(tents: TentDto[], tentId: number | null): string {
  if (!tentId) return 'Ohne Zelt'
  return tents.find((tent) => tent.id === tentId)?.name ?? `Zelt #${tentId}`
}

function getGrowName(grows: GrowSummary[], growId: number | null): string {
  if (!growId) return 'Ohne Grow'
  return grows.find((grow) => grow.id === growId)?.name ?? `Grow #${growId}`
}

function formatApiError(caught: unknown, fallback: string): string {
  if (caught instanceof ApiRequestError) {
    return caught.message
  }

  return caught instanceof Error ? caught.message : fallback
}

export default MobileActionPage
