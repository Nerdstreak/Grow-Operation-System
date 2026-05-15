import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary } from '../types'
import { formatDate } from '../utils'

function AddbackHubPage() {
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      try {
        const items = await apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal })
        setGrows(items)
        setError(null)
      } catch (caught) {
        if (!controller.signal.aborted) setError(caught instanceof ApiRequestError ? caught.message : 'Grows konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  const activeGrows = useMemo(() => grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning'), [grows])

  return (
    <main className="page-scroll app-page addback-hub-page">
      <section className="control-header">
        <div>
          <span className="control-kicker">Reservoir</span>
          <h1>Addback</h1>
        </div>
      </section>

      {error && <div className="inline-error"><strong>Fehler</strong><span>{error}</span></div>}

      {loading ? (
        <div className="empty-hint tight">Lädt...</div>
      ) : activeGrows.length === 0 ? (
        <section className="start-grid">
          <Link to="/grows/new" className="start-card"><strong>Grow</strong><span>Starten</span></Link>
          <Link to="/zelte" className="start-card"><strong>RDWC/DWC</strong><span>Setup</span></Link>
        </section>
      ) : (
        <section className="addback-grow-grid">
          {activeGrows.map((grow) => (
            <Link key={grow.id} to={`/grows/${grow.id}/addback`} className="ops-card addback-grow-card">
              <header>
                <h2>{grow.name}</h2>
                <span>{grow.tentName ?? 'Ohne Zelt'}</span>
              </header>
              <div className="addback-live-row">
                <Metric label="pH" value={formatValue(grow.latestReservoirPh, 2)} />
                <Metric label="EC" value={formatValue(grow.latestReservoirEc, 2)} />
                <Metric label="Messung" value={formatDate(grow.latestMeasurementAt)} />
              </div>
              <div className="btn btn-primary">Addback öffnen</div>
            </Link>
          ))}
        </section>
      )}
    </main>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return <div className="addback-mini-metric"><span>{label}</span><strong>{value}</strong></div>
}

function formatValue(value: number | null | undefined, digits: number): string {
  if (value == null || Number.isNaN(value)) return '–'
  return value.toLocaleString('de-DE', { maximumFractionDigits: digits })
}

export default AddbackHubPage
