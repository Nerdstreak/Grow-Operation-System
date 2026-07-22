import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, HarvestDto } from '../types'
import { formatDate, formatNumber } from '../utils'

// One-line yield summary for an archived grow, so the harvest a user carefully filled
// in is actually visible again instead of vanishing after saving.
function yieldLine(harvest: HarvestDto | undefined): string | null {
  if (!harvest) return null
  const parts: string[] = []
  if (harvest.dryWeightG != null) parts.push(`${formatNumber(harvest.dryWeightG, 0)} g trocken`)
  else if (harvest.wetWeightG != null) parts.push(`${formatNumber(harvest.wetWeightG, 0)} g frisch`)
  if (harvest.rating != null) parts.push(`★ ${formatNumber(harvest.rating, 0)}/10`)
  return parts.length > 0 ? parts.join(' · ') : null
}

function ArchivePage() {
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [harvestByGrow, setHarvestByGrow] = useState<Map<number, HarvestDto>>(new Map())
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const data = await apiFetch<GrowSummary[]>('/api/grows?archived=true', { signal: controller.signal })
        if (controller.signal.aborted) return
        setGrows(data)
        const harvests = await Promise.all(data.map((grow) =>
          apiFetch<HarvestDto>(`/api/grows/${grow.id}/harvest`, { signal: controller.signal }).catch(() => null),
        ))
        if (controller.signal.aborted) return
        const map = new Map<number, HarvestDto>()
        data.forEach((grow, index) => { const harvest = harvests[index]; if (harvest) map.set(grow.id, harvest) })
        setHarvestByGrow(map)
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

  const totalDryWeight = useMemo(
    () => [...harvestByGrow.values()].reduce((sum, harvest) => sum + (harvest.dryWeightG ?? 0), 0),
    [harvestByGrow],
  )

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
          {totalDryWeight > 0 && <div className="stat-chip"><strong>{formatNumber(totalDryWeight, 0)} g</strong>Gesamt-Ertrag (trocken)</div>}
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
                  {yieldLine(harvestByGrow.get(grow.id)) && (
                    <div className="row-sub" style={{ color: 'var(--green)' }}>{yieldLine(harvestByGrow.get(grow.id))}</div>
                  )}
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
