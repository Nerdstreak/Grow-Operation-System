import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, HydroSetupDto } from '../types'
import '../features/grows/grows-instrument.css'

function GrowsPage() {
  const [activeGrows, setActiveGrows] = useState<GrowSummary[]>([])
  const [archivedGrows, setArchivedGrows] = useState<GrowSummary[]>([])
  const [hydroSetups, setHydroSetups] = useState<HydroSetupDto[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  useEffect(() => { void loadGrows() }, [])

  const visibleArchived = useMemo(() => archivedGrows.slice(0, 6), [archivedGrows])
  const hydroNames = useMemo(() => new Map(hydroSetups.map((setup) => [setup.id, setup.name])), [hydroSetups])

  async function loadGrows() {
    setLoading(true)
    setError(null)
    try {
      const [active, archived, setups] = await Promise.all([
        apiFetch<GrowSummary[]>('/api/grows?archived=false'),
        apiFetch<GrowSummary[]>('/api/grows?archived=true'),
        apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true').catch(() => []),
      ])
      setActiveGrows(sortGrows(active))
      setArchivedGrows(sortGrows(archived))
      setHydroSetups(setups)
    } catch (caught) {
      setError(formatApiError(caught, 'Grows konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }

  async function archiveGrow(grow: GrowSummary) {
    if (saving) return
    if (!window.confirm(`${grow.name} beenden und archivieren?`)) return
    setSaving(`archive-${grow.id}`)
    setError(null)
    setNotice(null)
    try {
      await apiFetch(`/api/grows/${grow.id}/archive`, { method: 'POST' })
      setNotice('Grow beendet und archiviert.')
      await loadGrows()
    } catch (caught) {
      if (isNotFound(caught)) { setNotice('Eintrag existiert bereits nicht mehr.'); await loadGrows(); return }
      setError(formatApiError(caught, 'Grow konnte nicht beendet werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function deleteGrow(grow: GrowSummary) {
    if (saving) return
    if (!window.confirm(`${grow.name} endgültig löschen?`)) return
    setSaving(`delete-${grow.id}`)
    setError(null)
    setNotice(null)
    try {
      await apiFetch(`/api/grows/${grow.id}`, { method: 'DELETE' })
      setNotice('Grow gelöscht.')
      await loadGrows()
    } catch (caught) {
      if (isNotFound(caught)) { setNotice('Eintrag existiert bereits nicht mehr.'); await loadGrows(); return }
      setError(formatApiError(caught, 'Grow konnte nicht gelöscht werden.'))
    } finally {
      setSaving(null)
    }
  }

  return (
    <div className="ix-grows" data-audit="grows-page">
      <div className="ix-top">
        <div className="ix-brand"><span className="dot" /><b>GROWS</b></div>
        <Link className="ix-btn pri" to="/grows/new" style={{ marginLeft: 'auto' }}>Neuen Grow anlegen</Link>
      </div>

      {error && <div className="ix-empty-line" style={{ color: 'var(--ix-red)' }}>{error}</div>}
      {notice && <div className="ix-empty-line" style={{ color: 'var(--ix-phos)' }}>{notice}</div>}

      <section className="ix-grows-kpis ix-rise ix-d1">
        <div className="ix-grows-kpi"><span>Aktiv</span><strong>{activeGrows.filter((grow) => grow.status === 'Running').length}</strong></div>
        <div className="ix-grows-kpi"><span>Geplant</span><strong>{activeGrows.filter((grow) => grow.status === 'Planning').length}</strong></div>
        <div className="ix-grows-kpi"><span>Archiviert</span><strong>{archivedGrows.length}</strong></div>
        <div className="ix-grows-kpi"><span>Messungen</span><strong>{activeGrows.reduce((sum, grow) => sum + grow.measurementCount, 0)}</strong></div>
      </section>

      {loading ? (
        <div className="ix-panel ix-grows-empty"><h2>Lade Grows …</h2></div>
      ) : (
        <>
          <section className="ix-grows-section ix-rise ix-d2">
            <h2>Aktive Grows</h2>
            {activeGrows.length === 0 ? (
              <div className="ix-panel ix-grows-empty" data-audit="grows-empty-state">
                <h2>Noch kein Grow</h2>
                <div className="ix-grows-empty-actions">
                  <Link className="ix-btn pri" to="/grows/new">Neuen Grow anlegen</Link>
                  <Link className="ix-btn" to="/zelte/new">Zelt anlegen</Link>
                </div>
              </div>
            ) : (
              <div className="ix-grows-grid" data-audit="grows-overview">
                {activeGrows.map((grow) => <GrowCard key={grow.id} grow={grow} saving={saving} hydroName={getHydroName(grow, hydroNames)} onArchive={archiveGrow} onDelete={deleteGrow} />)}
              </div>
            )}
          </section>

          {visibleArchived.length > 0 && (
            <section className="ix-grows-section ix-rise ix-d3">
              <h2>Zuletzt archiviert</h2>
              <div className="ix-grows-grid compact" data-audit="grows-archive">
                {visibleArchived.map((grow) => <GrowCard key={grow.id} grow={grow} saving={saving} archived hydroName={getHydroName(grow, hydroNames)} onArchive={archiveGrow} onDelete={deleteGrow} />)}
              </div>
            </section>
          )}
        </>
      )}
    </div>
  )
}

function GrowCard({ grow, saving, hydroName, archived = false, onArchive, onDelete }: { grow: GrowSummary; saving: string | null; hydroName: string; archived?: boolean; onArchive: (grow: GrowSummary) => void; onDelete: (grow: GrowSummary) => void }) {
  const canArchive = grow.status === 'Planning' || grow.status === 'Running'
  const actionDisabled = Boolean(saving)
  const tone = grow.status === 'Running' ? 'ix-b-ok' : grow.status === 'Planning' ? 'ix-b-warn' : 'ix-b-neutral'
  return (
    <article className={`ix-panel ix-grow-card${archived ? ' archived' : ''}`}>
      <div className="ix-grow-card-head">
        <div><span className="ix-kick">{grow.hydroStyle}</span><h2>{grow.name}</h2></div>
        <span className={`ix-badge ${tone}`}>{formatStatus(grow.status)}</span>
      </div>
      <dl className="ix-grow-metrics">
        <Metric label="Zelt" value={grow.tentName ?? '–'} />
        <Metric label="Hydro" value={formatHydroMedium(grow, hydroName)} />
        <Metric label="Start" value={`${formatDate(grow.startDate)} · ${formatRuntime(grow.startDate)}`} />
        <Metric label="Phase" value={grow.latestStage ?? '–'} />
        <Metric label="Letzte Messung" value={formatDateTime(grow.latestMeasurementAt)} />
        <Metric label="Messungen" value={String(grow.measurementCount)} />
      </dl>
      <div className="ix-grow-card-actions" data-audit="grow-list-actions">
        <Link className="ix-btn pri" to={`/grows/${grow.id}`}>Öffnen</Link>
        <Link className="ix-btn" to={`/grows/${grow.id}/setup`}>Bearbeiten</Link>
        <button type="button" className="ix-btn" disabled={actionDisabled || !canArchive} onClick={() => void onArchive(grow)}>{saving === `archive-${grow.id}` ? 'Beendet…' : canArchive ? 'Beenden' : 'Beendet'}</button>
        <button type="button" className="ix-btn danger" disabled={actionDisabled} onClick={() => void onDelete(grow)}>{saving === `delete-${grow.id}` ? 'Löscht…' : 'Löschen'}</button>
      </div>
    </article>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>
}

function sortGrows(items: GrowSummary[]) {
  return [...items].sort((a, b) => statusRank(a.status) - statusRank(b.status) || a.name.localeCompare(b.name))
}

function getHydroName(grow: GrowSummary, hydroNames: Map<number, string>) {
  if (grow.hydroSetupName) return grow.hydroSetupName
  return grow.systemId ? hydroNames.get(grow.systemId) ?? `Hydro #${grow.systemId}` : grow.setupId ? `Setup #${grow.setupId}` : '–'
}

function formatStatus(status: GrowSummary['status']) {
  return status === 'Running' ? 'aktiv' : status === 'Planning' ? 'geplant' : status === 'Completed' ? 'beendet' : status === 'Aborted' ? 'abgebrochen' : status
}

function formatDate(value: string | null) {
  if (!value) return '–'
  return new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit', year: '2-digit' }).format(new Date(value))
}

function formatDateTime(value: string | null) {
  if (!value) return '–'
  return new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function formatHydroMedium(grow: GrowSummary, hydroName: string) {
  if (hydroName !== '–') return hydroName
  return grow.hydroStyle === 'None' ? 'Medium offen' : grow.hydroStyle
}

function formatRuntime(startDate: string | null) {
  if (!startDate) return '–'
  const start = new Date(startDate)
  if (Number.isNaN(start.getTime())) return '–'
  const days = Math.max(0, Math.floor((Date.now() - start.getTime()) / 86_400_000))
  return `${days} d`
}

function statusRank(status: GrowSummary['status']) {
  return status === 'Running' ? 0 : status === 'Planning' ? 1 : status === 'Completed' ? 2 : status === 'Aborted' ? 3 : 9
}

function formatApiError(caught: unknown, fallback: string) {
  if (caught instanceof ApiRequestError && caught.status === 409) return caught.payload?.message ?? 'Grow kann wegen verknüpfter Daten nicht gelöscht werden.'
  if (caught instanceof ApiRequestError && caught.payload?.message) return caught.payload.message
  return caught instanceof Error ? caught.message : fallback
}

function isNotFound(caught: unknown) {
  return caught instanceof ApiRequestError && caught.status === 404
}

export default GrowsPage
