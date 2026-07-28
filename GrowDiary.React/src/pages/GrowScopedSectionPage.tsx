import { Link } from 'react-router-dom'
import '../features/grow-detail/growdetail-instrument.css'
import { V1Page, V1Card, V1Alert, V1Empty } from '../components/v1'
import { GrowScopePicker } from '../features/grow-scope/GrowScopePicker'
import { GrowWorkspace } from '../features/grow-scope/GrowWorkspace'
import { useSelectedGrow } from '../features/grow-scope/useSelectedGrow'
import type { GrowDetailSection } from '../features/grow-detail/grow-detail-model'

// A top-level, single-purpose page for one grow section (measurements, diagnosis,
// journal, SOPs). V1 chrome with the grow switcher in the header action — works on
// desktop and mobile; no drilling into a grow first.
export function GrowScopedSectionPage({ title, section, eyebrow = 'Grow', intro }: { title: string; section: GrowDetailSection; eyebrow?: string; intro?: string }) {
  const { grows, growId, setGrowId, loading, error } = useSelectedGrow()
  const grow = grows.find((item) => String(item.id) === String(growId)) ?? null

  return (
    <V1Page
      eyebrow={eyebrow}
      title={title}
      subtitle={intro}
      action={<GrowScopePicker grows={grows} growId={growId} onChange={setGrowId} />}
    >
      {/* Diese Seiten waren Einbahnstraßen: aus dem Grow-Detail führen Tabs
          hierher, zurück führte nichts. Den Namen trägt der Umschalter eine
          Zeile darüber — ihn hier zu wiederholen hiess, denselben Grow zweimal
          untereinander zu schreiben. */}
      {grow && (
        <Link className="ls-btn is-small gs-back" to={`/grows/${grow.id}`}>← Zur Grow-Übersicht</Link>
      )}
      {error && <V1Alert message={error} tone="critical" />}
      {loading ? (
        <V1Card>Lädt…</V1Card>
      ) : grows.length === 0 ? (
        <V1Empty title="Kein aktiver Grow" text="Lege zuerst einen Grow an, dann erscheint er hier." />
      ) : growId ? (
        <GrowWorkspace growId={growId} section={section} />
      ) : null}
    </V1Page>
  )
}
