import '../features/grow-detail/growdetail-instrument.css'
import { GrowScopeHeader } from '../features/grow-scope/GrowScopeHeader'
import { GrowWorkspace } from '../features/grow-scope/GrowWorkspace'
import { useSelectedGrow } from '../features/grow-scope/useSelectedGrow'
import type { GrowDetailSection } from '../features/grow-detail/grow-detail-model'

// A top-level, single-purpose page for one grow section (measurements, diagnosis,
// journal, SOPs, automation). Picks the grow up top; no drilling into a grow first.
export function GrowScopedSectionPage({ title, section, intro }: { title: string; section: GrowDetailSection; intro?: string }) {
  const { grows, growId, setGrowId, loading, error } = useSelectedGrow()

  return (
    <>
      <GrowScopeHeader title={title} grows={grows} growId={growId} onChange={setGrowId} />
      <div className="page-scroll">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)' }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}
        {intro && <p className="text-muted" style={{ margin: '0 0 14px', fontSize: 13 }}>{intro}</p>}
        {loading ? (
          <div className="empty-hint">Lade Grows…</div>
        ) : grows.length === 0 ? (
          <div className="empty-hint">Kein aktiver Grow. Lege zuerst einen Grow an, dann erscheint er hier.</div>
        ) : growId ? (
          <GrowWorkspace growId={growId} section={section} />
        ) : null}
      </div>
    </>
  )
}
