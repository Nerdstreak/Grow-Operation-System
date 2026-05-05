import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, TentDto, TentLivePayload } from '../types'

function TentDetailPage() {
  const { tentId } = useParams()
  const [tent, setTent] = useState<TentDto | null>(null)
  const [live, setLive] = useState<TentLivePayload | null>(null)
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      if (!tentId) return

      setLoading(true)
      setError(null)

      try {
        const [tents, livePayload, activeGrows] = await Promise.all([
          apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal }),
          apiFetch<TentLivePayload>(`/api/live/tents/${tentId}`, { signal: controller.signal }),
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }),
        ])

        const selectedTent = tents.find((item) => item.id === Number(tentId)) ?? null
        setTent(selectedTent)
        setLive(livePayload)
        setGrows(activeGrows.filter((grow) => grow.tentId === Number(tentId)))
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

  const hasCritical = useMemo(() => live?.stateTone === 'critical', [live])

  return (
    <>
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to="/zelte">← Zelte</Link>
          <span className="topbar-title">{tent?.name ?? 'Zelt-Detail'}</span>
        </div>
        <div className="topbar-right">
          <Link className="btn btn-primary" to="/settings">Konfiguration</Link>
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

        {loading ? (
          <div className="empty-hint">Lade Zelt-Daten...</div>
        ) : !tent ? (
          <div className="empty-hint">Zelt nicht gefunden.</div>
        ) : (
          <div className="detail-layout">
            <div>
              <div className="metric-row">
                {(live?.metrics ?? []).map((metric) => (
                  <div key={metric.key} className="metric-block">
                    <div className="metric-block-label">{metric.label}</div>
                    <div className={`metric-block-val ${metric.tone === 'danger' ? 'crit' : metric.tone === 'warning' ? 'warn' : metric.tone === 'success' ? 'ok' : 'neutral'}`}>{metric.value}</div>
                    <div className="metric-block-unit">{metric.unit ?? ' '}</div>
                  </div>
                ))}
              </div>

              <div className="section-label">Aktive Grows</div>
              {grows.length === 0 ? (
                <div className="empty-hint" style={{ padding: '30px 0' }}>Keine aktiven Grows in diesem Zelt.</div>
              ) : (
                <div className="data-table">
                  {grows.map((grow) => (
                    <Link key={grow.id} to={`/grows/${grow.id}`} className="data-row" style={{ gridTemplateColumns: '2fr 1fr 1fr 60px', textDecoration: 'none' }}>
                      <div>
                        <div className="row-name">{grow.name}</div>
                        <div className="row-sub">{grow.strain ?? '–'}{grow.breeder ? ` · ${grow.breeder}` : ''}</div>
                      </div>
                      <div><span className="badge badge-neutral">{grow.latestStage ?? '–'}</span></div>
                      <div><span className={`badge ${hasCritical ? 'badge-crit' : 'badge-ok'}`}>{live?.stateLabel ?? 'stabil'}</span></div>
                      <div className="row-muted">→</div>
                    </Link>
                  ))}
                </div>
              )}
            </div>

            <div className="side-panel">
              <div className="panel-card">
                <div className="panel-card-header">
                  <span className="panel-card-title">Info</span>
                </div>
                <div style={{ padding: '12px 14px', display: 'grid', gap: 8, fontSize: 13 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Typ</span><span>{tent.kind}</span></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Tent-Typ</span><span>{tent.tentType}</span></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Aktive Runs</span><span>{tent.activeGrowCount}</span></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Archivierte Runs</span><span>{tent.archivedGrowCount}</span></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Sensoren</span><span>{tent.sensors.filter((sensor) => sensor.isActive).length}</span></div>
                </div>
              </div>

              {live?.cameraUrl && (
                <div className="panel-card">
                  <div className="panel-card-header">
                    <span className="panel-card-title">Kamera</span>
                  </div>
                  <div style={{ padding: 12 }}>
                    <img src={live.cameraUrl} alt={`Livebild ${tent.name}`} style={{ width: '100%', borderRadius: 8, display: 'block' }} />
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </>
  )
}

export default TentDetailPage
