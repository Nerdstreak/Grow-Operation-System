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
import { classNames, formatDate, formatDateTime, formatNumber } from '../utils'

type LoadIssue = {
  area: string
  message: string
}

type OperationsData = {
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

const emptyData: OperationsData = {
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

function DashboardPage() {
  const [data, setData] = useState<OperationsData>(emptyData)
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

      const [grows, settings, hardwareItems, riskEvents, maintenanceEvents, calibrationEvents] = await Promise.all([
        fetchOptional<GrowSummary[]>('Grows', '/api/grows?archived=false', []),
        fetchOptional<{ tents: TentDto[] }>('Einstellungen', '/api/settings', { tents: [] }),
        fetchOptional<HardwareItemDto[]>('Hardware', '/api/hardware-items', []),
        fetchOptional<RiskEventDto[]>('RiskEvents', '/api/risk-events?status=Open', []),
        fetchOptional<MaintenanceEventDto[]>('MaintenanceEvents', `/api/maintenance-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
        fetchOptional<CalibrationEventDto[]>('CalibrationEvents', `/api/calibration-events?dueBeforeUtc=${encodeURIComponent(dueBeforeUtc)}`, []),
      ])

      const activeGrows = grows.filter((grow) => grow.status === 'Running')
      const taskResults = await Promise.all(activeGrows.map((grow) => fetchOptional<GrowTaskDto[]>('Tasks', `/api/grows/${grow.id}/tasks`, [])))
      const sopResults = await Promise.all(activeGrows.map((grow) => fetchOptional<SopInstanceDto[]>('SOPs', `/api/sop-instances?growId=${grow.id}`, [])))

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
      setLoading(false)
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

  const criticalRiskCount = sortedRisks.filter((event) => event.severity === 'Critical').length
  const warningRiskCount = sortedRisks.filter((event) => event.severity === 'Warning').length
  const statusTone = criticalRiskCount > 0
    ? 'kritisch'
    : warningRiskCount > 0 || dueMaintenance.length > 0 || dueCalibrations.length > 0
      ? 'beobachten'
      : 'stabil'
  const statusText = criticalRiskCount > 0
    ? 'Kritische Risiken brauchen jetzt Aufmerksamkeit.'
    : warningRiskCount > 0 || dueMaintenance.length > 0 || dueCalibrations.length > 0
      ? 'Es gibt offene Warnungen oder faellige Arbeiten.'
      : 'Keine offenen Risiken oder faelligen Wartungen im Operations-Fenster.'

  return (
    <>
      <div className="topbar">
        <span className="topbar-title">Operations</span>
        <div className="topbar-right">
          <Link className="btn btn-primary" to="/grows/new">+ Neuer Grow</Link>
        </div>
      </div>

      <div className="page-scroll">
        {data.issues.length > 0 && (
          <div className="alert-bar" style={{ marginBottom: 14 }}>
            <div className="alert-dot" />
            <strong>Teilweise geladen</strong>
            <span>{data.issues.map((issue) => `${issue.area}: ${issue.message}`).join(' | ')}</span>
          </div>
        )}

        <div className="section-label">Status Summary</div>
        <div className="stats-row" style={{ marginBottom: 18 }}>
          <div className="stat-chip"><strong>{criticalRiskCount}</strong>Kritische Risiken</div>
          <div className="stat-chip"><strong>{dueMaintenance.length}</strong>Maintenance faellig</div>
          <div className="stat-chip"><strong>{dueCalibrations.length}</strong>Calibration faellig</div>
          <div className="stat-chip"><strong>{activeGrows.length}</strong>Aktive Grows</div>
        </div>

        <div className="card" style={{ marginBottom: 24 }}>
          <div className="card-header">
            <span className="card-title">Tageslage</span>
            <span className={classNames('badge', statusTone === 'kritisch' ? 'badge-crit' : statusTone === 'beobachten' ? 'badge-warn' : 'badge-ok')}>
              {statusTone}
            </span>
          </div>
          <div style={{ padding: '14px 16px', color: 'var(--muted)', fontSize: 14 }}>
            {loading ? 'Lade Operations-Daten...' : statusText}
          </div>
        </div>

        <div className="ops-layout">
          <div style={{ display: 'grid', gap: 24 }}>
            <section>
              <div className="section-label">Critical Now</div>
              <div className="card">
                <div className="card-header">
                  <span className="card-title">Offene RiskEvents</span>
                  <Link className="btn" to="/hardware">Hardware oeffnen</Link>
                </div>
                <div style={{ padding: '14px 16px', display: 'grid', gap: 8 }}>
                  {loading ? (
                    <div style={emptyStyle}>Lade Risiken...</div>
                  ) : sortedRisks.length === 0 ? (
                    <div style={emptyStyle}>Keine offenen RiskEvents.</div>
                  ) : (
                    sortedRisks.slice(0, 8).map((event) => (
                      <div key={event.id} className="task-item" style={{ alignItems: 'start' }}>
                        <div className={`prio-dot ${event.severity === 'Critical' ? 'prio-high' : event.severity === 'Warning' ? 'prio-med' : 'prio-low'}`} />
                        <div>
                          <div className="task-title">{event.title}</div>
                          <div className="task-sub">
                            {event.severity} · {event.eventType} · {getHardwareName(data.hardwareItems, event.hardwareItemId)} · {getTentName(data.tents, event.tentId)} · {getGrowName(data.grows, event.growId)}
                          </div>
                          <div className="task-sub">Seit {formatDateTime(event.startedAtUtc)}</div>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </section>

            <section>
              <div className="section-label">Heute / bald faellig</div>
              <div className="card">
                <div className="card-header">
                  <span className="card-title">Wartung und Kalibrierung</span>
                  <Link className="btn" to="/hardware">Zur HardwarePage</Link>
                </div>
                <div style={{ padding: '14px 16px', display: 'grid', gap: 14 }}>
                  <DueList
                    title="Maintenance"
                    loading={loading}
                    items={dueMaintenance}
                    hardwareItems={data.hardwareItems}
                  />
                  <DueList
                    title="Calibration"
                    loading={loading}
                    items={dueCalibrations}
                    hardwareItems={data.hardwareItems}
                  />
                </div>
              </div>
            </section>

            <section>
              <div className="section-label">Aktive Grows</div>
              {loading ? (
                <div className="empty-hint">Lade Grows...</div>
              ) : activeGrows.length === 0 ? (
                <div className="empty-hint">Keine aktiven Grows gefunden.</div>
              ) : (
                <div className="tents-grid">
                  {activeGrows.map((grow) => (
                    <Link key={grow.id} to={`/grows/${grow.id}`} className="tent-card" style={{ textDecoration: 'none', display: 'block' }}>
                      <div className="tc-header">
                        <div>
                          <div className="tc-name">{grow.name}</div>
                          <div className="tc-meta">{grow.strain ?? 'Unbekannter Strain'} · {grow.tentName ?? 'Ohne Zelt'}</div>
                        </div>
                        <span className={`badge ${stageBadgeClass(grow.latestStage)}`}>{grow.latestStage ?? 'Phase offen'}</span>
                      </div>

                      <div className="tc-section-label">Reservoir</div>
                      <div className="tc-metrics-row">
                        <div className="tc-metric">
                          <div className="tc-metric-label">pH</div>
                          <div className="tc-metric-value">{formatNumber(grow.latestReservoirPh, 2)}</div>
                          <div className="tc-metric-unit">pH</div>
                        </div>
                        <div className="tc-metric">
                          <div className="tc-metric-label">EC</div>
                          <div className="tc-metric-value">{formatNumber(grow.latestReservoirEc, 2)}</div>
                          <div className="tc-metric-unit">mS/cm</div>
                        </div>
                        <div className="tc-metric">
                          <div className="tc-metric-label">Messungen</div>
                          <div className="tc-metric-value">{grow.measurementCount}</div>
                          <div className="tc-metric-unit">gesamt</div>
                        </div>
                        <div className="tc-metric">
                          <div className="tc-metric-label">Letzte</div>
                          <div className="tc-metric-value" style={{ fontSize: 15 }}>{formatDate(grow.latestMeasurementAt)}</div>
                          <div className="tc-metric-unit">Messung</div>
                        </div>
                      </div>
                    </Link>
                  ))}
                </div>
              )}
            </section>
          </div>

          <div className="side-panel">
            <div className="panel-card">
              <div className="panel-card-header">
                <span className="panel-card-title">Aktive SOPs</span>
                <span className="panel-card-count">{activeSops.length}</span>
              </div>
              {loading ? (
                <div style={sideEmptyStyle}>Lade...</div>
              ) : activeSops.length === 0 ? (
                <div style={sideEmptyStyle}>Keine aktiven SOPs.</div>
              ) : (
                activeSops.slice(0, 8).map((instance) => (
                  <Link key={instance.id} to={`/grows/${instance.growId}`} className="addback-item" style={{ display: 'block', textDecoration: 'none' }}>
                    <div className="addback-name">{instance.sopName}</div>
                    <div className="addback-detail">
                      {getGrowName(data.grows, instance.growId)} · {instance.status} · naechster Step {formatDateTime(instance.nextStepDueAtUtc)}
                    </div>
                  </Link>
                ))
              )}
            </div>

            <div className="panel-card">
              <div className="panel-card-header">
                <span className="panel-card-title">Offene Tasks</span>
                <span className="panel-card-count">{data.tasks.length}</span>
              </div>
              {loading ? (
                <div style={sideEmptyStyle}>Lade...</div>
              ) : data.tasks.length === 0 ? (
                <div style={sideEmptyStyle}>Keine offenen Aufgaben.</div>
              ) : (
                data.tasks.slice(0, 8).map((task) => (
                  <Link key={task.id} to={`/grows/${task.growId}`} className="task-item" style={{ textDecoration: 'none' }}>
                    <div className={`prio-dot ${task.priority === 'Critical' || task.priority === 'High' ? 'prio-high' : task.priority === 'Normal' ? 'prio-med' : 'prio-low'}`} />
                    <div>
                      <div className="task-title">{task.title}</div>
                      <div className="task-sub">{task.growName}{task.dueAtUtc ? ` · faellig ${formatDate(task.dueAtUtc)}` : ''}</div>
                    </div>
                  </Link>
                ))
              )}
            </div>

            <div className="panel-card">
              <div className="panel-card-header">
                <span className="panel-card-title">Schnelle Aktionen</span>
              </div>
              <div style={{ padding: 14, display: 'grid', gap: 8 }}>
                <Link className="btn btn-primary" to="/grows/new">Neuer Grow</Link>
                {activeGrows.slice(0, 3).map((grow) => (
                  <Link key={grow.id} className="btn" to={`/grows/${grow.id}`}>{grow.name} oeffnen</Link>
                ))}
                <Link className="btn" to="/hardware">Hardware oeffnen</Link>
                <Link className="btn" to="/wissen">Wissen oeffnen</Link>
              </div>
            </div>
          </div>
        </div>
      </div>
    </>
  )
}

function DueList({
  title,
  loading,
  items,
  hardwareItems,
}: {
  title: string
  loading: boolean
  items: Array<MaintenanceEventDto | CalibrationEventDto>
  hardwareItems: HardwareItemDto[]
}) {
  return (
    <div style={{ display: 'grid', gap: 8 }}>
      <div style={{ fontSize: 13, fontWeight: 700 }}>{title}</div>
      {loading ? (
        <div style={emptyStyle}>Lade...</div>
      ) : items.length === 0 ? (
        <div style={emptyStyle}>Nichts faellig.</div>
      ) : (
        items.slice(0, 8).map((item) => (
          <div key={`${title}-${item.id}`} className="task-item">
            <div className="prio-dot prio-med" />
            <div>
              <div className="task-title">{item.title}</div>
              <div className="task-sub">
                {getHardwareName(hardwareItems, item.hardwareItemId)} · {item.status} · faellig {formatDateTime(item.dueAtUtc)}
              </div>
            </div>
          </div>
        ))
      )}
    </div>
  )
}

function compareNullableDate(left: string | null | undefined, right: string | null | undefined): number {
  if (!left && !right) return 0
  if (!left) return 1
  if (!right) return -1
  return new Date(left).getTime() - new Date(right).getTime()
}

function stageBadgeClass(stage: string | null): string {
  if (!stage) return 'badge-neutral'
  if (stage === 'Flower' || stage === 'Finish') return 'badge-warn'
  if (stage === 'Seedling' || stage === 'Clone') return 'badge-info'
  return 'badge-ok'
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

const emptyStyle = { fontSize: 13, color: 'var(--faint)' }
const sideEmptyStyle = { padding: '14px', fontSize: '12px', color: 'var(--faint)' }

export default DashboardPage
