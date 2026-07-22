import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, HarvestDto } from '../types'
import { formatDate, formatNumber } from '../utils'
import { V1Page, V1Alert, V1Empty, V1Stat, V1Badge } from '../components/v1'

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
    <V1Page eyebrow="Grows" title="Archiv" subtitle="Abgeschlossene und abgebrochene Grows — mit Ertrag.">
      {error && <V1Alert message={error} tone="warn" />}

      <section className="v1-kpi-grid">
        <V1Stat label="Archivierte Runs" value={grows.length} />
        <V1Stat label="Abgeschlossen" value={grows.filter((grow) => grow.status === 'Completed').length} />
        <V1Stat label="Abgebrochen" value={grows.filter((grow) => grow.status === 'Aborted').length} />
        {totalDryWeight > 0 && <V1Stat label="Gesamt-Ertrag" value={formatNumber(totalDryWeight, 0)} unit="g trocken" />}
      </section>

      {loading ? (
        <V1Empty title="Lade Archiv…" />
      ) : grows.length === 0 ? (
        <V1Empty title="Noch keine archivierten Grows" text="Abgeschlossene Grows erscheinen hier — samt Ertrag." />
      ) : (
        <div className="v1-list">
          {grows.map((grow) => (
            <Link key={grow.id} to={`/grows/${grow.id}`} className="v1-list-row">
              <div>
                <strong>{grow.name}</strong>
                <span>{[grow.strain ?? '–', grow.breeder, yieldLine(harvestByGrow.get(grow.id))].filter(Boolean).join(' · ')}</span>
              </div>
              <em>{grow.tentName ?? 'ohne Zelt'}{grow.endDate ? ` · ${formatDate(grow.endDate)}` : ''}</em>
              <V1Badge tone={grow.status === 'Completed' ? 'ok' : 'neutral'}>{grow.status === 'Completed' ? 'Abgeschlossen' : 'Abgebrochen'}</V1Badge>
            </Link>
          ))}
        </div>
      )}
    </V1Page>
  )
}

export default ArchivePage
