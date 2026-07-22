import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'

// The knowledge base's SOP definitions, so a user can start a routine proactively —
// not only when a risk happens to recommend one.
type SopCatalogEntry = {
  id: string
  name?: string
  type?: string
  intervalDays?: number | null
  estimatedDurationMinutes?: number | null
  steps?: unknown[]
}

function meta(entry: SopCatalogEntry): string {
  const parts: string[] = []
  const stepCount = Array.isArray(entry.steps) ? entry.steps.length : 0
  if (entry.type) parts.push(entry.type)
  if (stepCount > 0) parts.push(`${stepCount} Schritte`)
  if (entry.estimatedDurationMinutes) parts.push(`~${entry.estimatedDurationMinutes} Min`)
  if (entry.intervalDays) parts.push(`alle ${entry.intervalDays} Tage`)
  return parts.join(' · ')
}

export function SopCatalog({
  growId,
  activeSopIds,
  onStarted,
}: {
  growId: string
  activeSopIds: Set<string>
  onStarted: (notice: string) => void
}) {
  const [catalog, setCatalog] = useState<SopCatalogEntry[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const list = await apiFetch<SopCatalogEntry[]>('/api/knowledge/sops', { signal: controller.signal })
        if (!controller.signal.aborted) setCatalog(list)
      } catch (caught) {
        if (!controller.signal.aborted) setError(caught instanceof Error ? caught.message : 'SOP-Katalog konnte nicht geladen werden.')
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  async function start(sopId: string) {
    setBusy(sopId)
    setError(null)
    try {
      await apiFetch('/api/sop-instances/start', {
        method: 'POST',
        body: JSON.stringify({ growId: Number(growId), sopId, source: 'Manual' }),
      })
      onStarted('Routine gestartet.')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Routine konnte nicht gestartet werden.')
    } finally {
      setBusy(null)
    }
  }

  return (
    <>
      <div className="section-label">Routine starten</div>
      <div className="card" style={{ marginBottom: 14 }}>
        <div className="card-header">
          <span className="card-title">SOP-Katalog</span>
          <span className="text-muted" style={{ fontSize: 13 }}>{catalog.length}</span>
        </div>
        {error && <div className="empty-hint" style={{ color: 'var(--red)' }}>{error}</div>}
        {catalog.length === 0 && !error ? (
          <div className="empty-hint">Keine Routinen im Katalog.</div>
        ) : (
          catalog.map((entry) => {
            const active = activeSopIds.has(entry.id)
            return (
              <div key={entry.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, padding: '12px 16px', borderTop: '1px solid var(--border)', flexWrap: 'wrap' }}>
                <div style={{ minWidth: 0 }}>
                  <div className="tl-title">{entry.name || entry.id}</div>
                  <div className="tl-sub">{meta(entry) || '—'}</div>
                </div>
                <button type="button" className="btn btn-primary" disabled={active || busy === entry.id} onClick={() => void start(entry.id)}>
                  {active ? 'Läuft' : busy === entry.id ? 'Startet…' : 'Starten'}
                </button>
              </div>
            )
          })
        )}
      </div>
    </>
  )
}
