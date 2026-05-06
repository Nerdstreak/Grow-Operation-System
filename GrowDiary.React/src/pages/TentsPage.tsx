import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { TentDto, TentLivePayload } from '../types'

function TentsPage() {
  const [tents, setTents] = useState<TentDto[]>([])
  const [liveByTentId, setLiveByTentId] = useState<Record<number, TentLivePayload>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const allTents = await apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal })
        const activeTents = allTents.filter((tent) => tent.activeGrowCount > 0 || tent.activeSetupCount > 0)
        setTents(activeTents)

        const liveEntries = await Promise.all(
          activeTents.map(async (tent) => {
            try {
              const payload = await apiFetch<TentLivePayload>(`/api/live/tents/${tent.id}`, { signal: controller.signal })
              return [tent.id, payload] as const
            } catch {
              return [tent.id, null] as const
            }
          }),
        )

        if (!controller.signal.aborted) {
          setLiveByTentId(Object.fromEntries(liveEntries.filter((entry): entry is readonly [number, TentLivePayload] => entry[1] !== null)))
        }
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Zelte konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  const activeGrowCount = useMemo(() => tents.reduce((sum, tent) => sum + tent.activeGrowCount, 0), [tents])
  const activeSetupCount = useMemo(() => tents.reduce((sum, tent) => sum + tent.activeSetupCount, 0), [tents])

  return (
    <>
      <div className="page-scroll">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14 }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}

        <div className="stats-row">
          <div className="stat-chip"><strong>{tents.length}</strong>Aktive Zelte</div>
          <div className="stat-chip"><strong>{activeGrowCount}</strong>Laufende/geplante Grows</div>
          <div className="stat-chip"><strong>{activeSetupCount}</strong>Aktive Setups</div>
        </div>

        {loading ? (
          <div className="empty-hint">Lade Zelte...</div>
        ) : tents.length === 0 ? (
          <div className="empty-hint">Keine aktiven Zelte.</div>
        ) : (
          <div className="tents-grid">
            {tents.map((tent) => {
              const live = liveByTentId[tent.id]
              const metrics = live?.metrics.slice(0, 6) ?? []
              const footerParts = formatTentActivity(tent)

              return (
                <Link key={tent.id} to={`/zelte/${tent.id}`} className="tent-card" style={{ textDecoration: 'none', display: 'block' }}>
                  <div className="tc-header">
                    <div>
                      <div className="tc-name">{tent.name}</div>
                      <div className="tc-meta">{tent.kind} · {tent.tentType}</div>
                    </div>
                    <span className={`badge ${live?.stateTone === 'critical' ? 'badge-crit' : live?.stateTone === 'attention' ? 'badge-warn' : 'badge-ok'}`}>
                      {live?.stateLabel ?? 'offline'}
                    </span>
                  </div>

                  <div className="tc-section-label">Live</div>
                  <div className="tc-metrics-row">
                    {metrics.length === 0 ? (
                      <div className="empty-hint" style={{ padding: 18, gridColumn: '1 / -1' }}>Keine Live-Metriken verfuegbar.</div>
                    ) : (
                      metrics.map((metric) => (
                        <div key={metric.key} className="tc-metric">
                          <div className="tc-metric-label">{metric.label}</div>
                          <div className={`tc-metric-value ${metric.tone === 'danger' ? 'crit' : metric.tone === 'warning' ? 'warn' : metric.tone === 'success' ? 'ok' : ''}`}>{metric.value}</div>
                          <div className="tc-metric-unit">{metric.unit ?? ' '}</div>
                        </div>
                      ))
                    )}
                  </div>

                  <div className="tc-footer">
                    <span className="tc-meta">{footerParts.join(' · ')}</span>
                  </div>
                </Link>
              )
            })}
          </div>
        )}
      </div>
    </>
  )
}

function formatTentActivity(tent: TentDto): string[] {
  const parts: string[] = []
  if (tent.activeGrowCount > 0) {
    parts.push(`${tent.activeGrowCount} ${tent.activeGrowCount === 1 ? 'aktiver Grow' : 'aktive Grows'}`)
  }
  if (tent.activeSetupCount > 0) {
    parts.push(`${tent.activeSetupCount} ${tent.activeSetupCount === 1 ? 'aktives Setup' : 'aktive Setups'}`)
  }
  return parts.length > 0 ? parts : ['Keine aktive Nutzung']
}

export default TentsPage
