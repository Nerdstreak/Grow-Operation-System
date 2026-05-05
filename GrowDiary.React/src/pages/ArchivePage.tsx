import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary } from '../types'
import { formatDate } from '../utils'

function ArchivePage() {
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const data = await apiFetch<GrowSummary[]>('/api/grows?archived=true', { signal: controller.signal })
        setGrows(data)
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Archiv konnte nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  return (
    <>
      <div className="topbar">
        <span className="topbar-title">Archiv</span>
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
          <div className="stat-chip"><strong>{grows.length}</strong>Archivierte Runs</div>
          <div className="stat-chip"><strong>{grows.filter((grow) => grow.status === 'Completed').length}</strong>Abgeschlossen</div>
          <div className="stat-chip"><strong>{grows.filter((grow) => grow.status === 'Aborted').length}</strong>Abgebrochen</div>
        </div>

        <div className="data-table">
          <div className="data-table-header grows-cols">
            <span>Name</span>
            <span>Zelt</span>
            <span>Status</span>
            <span>Zeitraum</span>
            <span></span>
          </div>

          {loading ? (
            <div className="empty-hint">Lade Archiv...</div>
          ) : grows.length === 0 ? (
            <div className="empty-hint">Noch keine archivierten Grows.</div>
          ) : (
            grows.map((grow) => (
              <Link key={grow.id} to={`/grows/${grow.id}`} className="data-row grows-cols" style={{ textDecoration: 'none' }}>
                <div>
                  <div className="row-name">{grow.name}</div>
                  <div className="row-sub">
                    {grow.strain ?? '–'}
                    {grow.breeder ? ` · ${grow.breeder}` : ''}
                  </div>
                </div>
                <div className="row-muted">{grow.tentName ?? '–'}</div>
                <div>
                  <span className={`badge ${grow.status === 'Completed' ? 'badge-ok' : 'badge-neutral'}`}>
                    {grow.status === 'Completed' ? 'Abgeschlossen' : 'Abgebrochen'}
                  </span>
                </div>
                <div className="row-muted">
                  {formatDate(grow.startDate)}
                  {grow.endDate ? ` – ${formatDate(grow.endDate)}` : ''}
                </div>
                <div className="row-muted">→</div>
              </Link>
            ))
          )}
        </div>
      </div>
    </>
  )
}

export default ArchivePage
