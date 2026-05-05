import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, GrowTaskDto, TentDto } from '../types'
import { formatDate, formatDateTime, formatNumber } from '../utils'

function DashboardPage() {
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [tasks, setTasks] = useState<GrowTaskDto[]>([])
  const [tents, setTents] = useState<TentDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const [growData, settingsData] = await Promise.all([
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }),
          apiFetch<{ tents: TentDto[] }>('/api/settings', { signal: controller.signal }),
        ])
        setGrows(growData)
        setTents(settingsData.tents)

        const openTasks: GrowTaskDto[] = []
        await Promise.all(
          growData
            .filter((g) => g.status === 'Running')
            .map(async (g) => {
              try {
                const t = await apiFetch<GrowTaskDto[]>(`/api/grows/${g.id}/tasks`, {
                  signal: controller.signal,
                })
                openTasks.push(...t.filter((x) => x.status === 'Open'))
              } catch {
                // ignore per-grow errors
              }
            }),
        )
        setTasks(openTasks)
      } catch (caught) {
        if (controller.signal.aborted) return
        const message = caught instanceof ApiRequestError ? caught.message : 'Dashboard konnte nicht geladen werden.'
        setError(message)
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  const stats = useMemo(() => {
    const running = grows.filter((g) => g.status === 'Running').length
    const activeTents = tents.filter((t) => t.activeGrowCount > 0).length
    const openCount = tasks.filter((t) => t.status === 'Open').length
    return { running, activeTents, openCount }
  }, [grows, tasks, tents])

  const activeGrows = useMemo(() => grows.filter((g) => g.status === 'Running'), [grows])

  function stageBadgeClass(stage: string | null): string {
    if (!stage) return 'badge-neutral'
    if (stage === 'Flower' || stage === 'Finish') return 'badge-warn'
    if (stage === 'Seedling' || stage === 'Clone') return 'badge-info'
    return 'badge-ok'
  }

  return (
    <>
      <div className="topbar">
        <span className="topbar-title">Operations</span>
        <div className="topbar-right">
          <Link className="btn btn-primary" to="/grows/new">+ Neuer Grow</Link>
        </div>
      </div>

      <div className="page-scroll">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14 }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}

        <div className="stats-row">
          <div className="stat-chip"><strong>{stats.running}</strong>Aktive Runs</div>
          <div className="stat-chip"><strong>{stats.activeTents}</strong>Aktive Zelte</div>
          <div className="stat-chip"><strong>{stats.openCount}</strong>Offen heute</div>
        </div>

        <div className="ops-layout">
          <div>
            {loading ? (
              <div className="empty-hint">Lade Daten…</div>
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
                      <span className={`badge ${stageBadgeClass(grow.latestStage)}`}>{grow.latestStage ?? '—'}</span>
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
                        <div className="tc-metric-label">Start</div>
                        <div className="tc-metric-value" style={{ fontSize: 15 }}>{formatDate(grow.startDate)}</div>
                        <div className="tc-metric-unit">Datum</div>
                      </div>
                    </div>

                    <div className="tc-footer">
                      <span className="tc-meta">{grow.latestMeasurementAt ? `Letzte Messung ${formatDateTime(grow.latestMeasurementAt)}` : 'Noch keine Messung'}</span>
                      <button className="tc-open-btn" type="button">Öffnen →</button>
                    </div>
                  </Link>
                ))}
              </div>
            )}
          </div>

          <div className="side-panel">
            <div className="panel-card">
              <div className="panel-card-header">
                <span className="panel-card-title">Heute zu tun</span>
                <span className="panel-card-count">{tasks.filter((t) => t.status === 'Open').length}</span>
              </div>
              {tasks.length === 0 ? (
                <div style={{ padding: '14px', fontSize: '12px', color: 'var(--faint)' }}>
                  {loading ? 'Lade…' : 'Keine offenen Aufgaben.'}
                </div>
              ) : (
                tasks
                  .filter((t) => t.status === 'Open')
                  .slice(0, 10)
                  .map((task) => (
                    <div key={task.id} className="task-item">
                      <div className={`prio-dot ${task.priority === 'Critical' || task.priority === 'High' ? 'prio-high' : task.priority === 'Normal' ? 'prio-med' : 'prio-low'}`} />
                      <div>
                        <div className="task-title">{task.title}</div>
                        <div className="task-sub">{task.growName}{task.dueAtUtc ? ` · fällig ${formatDate(task.dueAtUtc)}` : ''}</div>
                      </div>
                    </div>
                  ))
              )}
            </div>

            <div className="panel-card">
              <div className="panel-card-header">
                <span className="panel-card-title">Alle Grows</span>
                <span className="panel-card-count">{grows.length}</span>
              </div>
              {grows.length === 0 ? (
                <div style={{ padding: '14px', fontSize: '12px', color: 'var(--faint)' }}>
                  {loading ? 'Lade…' : 'Keine Grows.'}
                </div>
              ) : (
                grows.slice(0, 8).map((grow) => (
                  <Link key={grow.id} to={`/grows/${grow.id}`} className="addback-item" style={{ display: 'block', textDecoration: 'none' }}>
                    <div className="addback-name">{grow.name}</div>
                    <div className="addback-detail">{grow.strain ?? '—'} · {grow.status}</div>
                  </Link>
                ))
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  )
}

export default DashboardPage
