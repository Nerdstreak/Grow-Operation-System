import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary } from '../types'
import { buildPhaseTimeline } from '../features/grows/phase-timeline'
import { V1Alert, V1Page, V1Skeleton } from '../components/v1'
import '../features/grows/grows.css'

/**
 * Die Grow-Übersicht nach dem Entwurf: eine Karte je laufendem oder geplantem
 * Grow, dazu die gestrichelte Geisterkarte zum Anlegen. Beendete Grows stehen
 * nicht hier, sondern unter Ernte & Archiv — zwei Listen auf einer Seite haben
 * die Übersicht nur verwässert. Beenden/Löschen wohnt in der Verwaltung des
 * Grow-Details.
 */
function GrowsPage() {
  const navigate = useNavigate()
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const active = await apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal })
        if (!controller.signal.aborted) setGrows(sortGrows(active))
      } catch (caught) {
        if (!controller.signal.aborted) setError(caught instanceof ApiRequestError ? caught.message : 'Grows konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  return (
    <V1Page
      eyebrow="Grow / Alle"
      title="Grows"
      action={<Link className="ls-btn is-primary" to="/grows/new">+ Grow anlegen</Link>}
    >
      {error && <V1Alert message={error} tone="critical" />}

      {loading ? (
        <V1Skeleton tiles={3} label="Lade Grows" />
      ) : (
        <div className="co-grid" data-audit="grows-overview">
          {grows.map((grow) => <GrowCard key={grow.id} grow={grow} />)}
          <button type="button" className="co-ghost" data-audit="grows-empty-state" onClick={() => navigate('/grows/new')}>
            + Grow anlegen
            <small>Sorte, Zelt &amp; System wählen — 1 Seite</small>
          </button>
        </div>
      )}
    </V1Page>
  )
}

function GrowCard({ grow }: { grow: GrowSummary }) {
  const running = grow.status === 'Running'
  const timeline = buildPhaseTimeline(grow)
  return (
    <article className={`gc-card${running ? ' is-running' : ''}`}>
      <div className="gc-head">
        <strong>{grow.name}</strong>
        <span className={`ls-pill${running ? '' : ' is-plan'}`}>{statusLabel(grow.status)}</span>
        <span className="gc-day">{dayLabel(grow)}</span>
      </div>
      <div className="gc-body">
        {running && timeline.phases.length > 0 && (
          <div className="gc-phasebar" aria-hidden="true">
            {/* Keimphase als fester kleiner Anteil wie im Entwurf — sie ist
                vorbei, sobald der Grow läuft. */}
            <i className="is-done" style={{ flex: 12 }} />
            {timeline.phases.map((phase) => (
              <i key={phase.label} className={`is-${phase.state}`} style={{ flex: Math.max(phase.days, 4) }} />
            ))}
            {/* Ohne Flip-Datum gibt es keine geplante Blüte in der Timeline —
                der Balken zeigt trotzdem, dass noch etwas kommt (8-Wochen-
                Richtwert, wie in der Phasen-Timeline). */}
            {!timeline.phases.some((phase) => phase.state === 'planned') && <i className="is-planned" style={{ flex: 56 }} />}
          </div>
        )}
        <div className="gc-facts">{factsLine(grow)}</div>
        <div className="co-actions" data-audit="grow-list-actions">
          <Link className="ls-btn is-small is-primary" to={`/grows/${grow.id}`}>Öffnen</Link>
          {running && <Link className="ls-btn is-small" to={`/grows/${grow.id}/addback`}>Addback</Link>}
          <Link className="ls-btn is-small" to={`/grows/${grow.id}/setup`}>Bearbeiten</Link>
        </div>
      </div>
    </article>
  )
}

/** „Fast Buds · 6 Pflanzen · RDWC Test Setup · AC Infinity" — nur, was belegt ist. */
function factsLine(grow: GrowSummary): string {
  return [
    grow.breeder ?? grow.strain,
    grow.plantCount != null ? `${grow.plantCount} Pflanzen` : null,
    grow.hydroSetupName,
    grow.tentName,
  ].filter(Boolean).join(' · ') || 'Noch keine Angaben'
}

/** „Veg Tag 26" bzw. „Blüte Tag 12" — geplante Grows haben noch keinen Tag. */
function dayLabel(grow: GrowSummary): string {
  if (grow.status !== 'Running' || !grow.startDate) return grow.startDate ? `Start ${shortDate(grow.startDate)}` : ''
  const now = Date.now()
  const flip = grow.flipDate ? new Date(grow.flipDate).getTime() : null
  if (flip != null && now >= flip) return `Blüte Tag ${daysSince(flip)}`
  return `Veg Tag ${daysSince(new Date(grow.startDate).getTime())}`
}

function daysSince(timestamp: number): number {
  return Math.max(1, Math.floor((Date.now() - timestamp) / 86_400_000) + 1)
}

function shortDate(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit' }).format(date)
}

function statusLabel(status: GrowSummary['status']) {
  return status === 'Running' ? 'Läuft' : status === 'Planning' ? 'Geplant' : status === 'Completed' ? 'Beendet' : 'Abgebrochen'
}

function sortGrows(items: GrowSummary[]) {
  const rank = (status: GrowSummary['status']) => (status === 'Running' ? 0 : status === 'Planning' ? 1 : 2)
  return [...items].sort((a, b) => rank(a.status) - rank(b.status) || a.name.localeCompare(b.name))
}

export default GrowsPage
