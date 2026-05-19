import { useEffect, useMemo, useState } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1LinkButton, V1Page, V1Section, V1Stat } from '../components/v1'
import type { GrowSummary, HydroSetupDto } from '../types'
import { classNames } from '../utils'

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
    const confirmed = window.confirm(`${grow.name} beenden und archivieren?`)
    if (!confirmed) return

    setSaving(`archive-${grow.id}`)
    setError(null)
    setNotice(null)
    try {
      await apiFetch(`/api/grows/${grow.id}/archive`, { method: 'POST' })
      setActiveGrows((current) => current.filter((item) => item.id !== grow.id))
      setNotice('Grow beendet und archiviert.')
      await loadGrows()
    } catch (caught) {
      if (isNotFound(caught)) {
        setActiveGrows((current) => current.filter((item) => item.id !== grow.id))
        setArchivedGrows((current) => current.filter((item) => item.id !== grow.id))
        setNotice('Eintrag existiert bereits nicht mehr.')
        await loadGrows()
        return
      }
      setError(formatApiError(caught, 'Grow konnte nicht beendet werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function deleteGrow(grow: GrowSummary) {
    if (saving) return
    const confirmed = window.confirm(`${grow.name} endgültig löschen?`)
    if (!confirmed) return

    setSaving(`delete-${grow.id}`)
    setError(null)
    setNotice(null)
    try {
      await apiFetch(`/api/grows/${grow.id}`, { method: 'DELETE' })
      setActiveGrows((current) => current.filter((item) => item.id !== grow.id))
      setArchivedGrows((current) => current.filter((item) => item.id !== grow.id))
      setNotice('Grow gelöscht.')
      await loadGrows()
    } catch (caught) {
      if (isNotFound(caught)) {
        setActiveGrows((current) => current.filter((item) => item.id !== grow.id))
        setArchivedGrows((current) => current.filter((item) => item.id !== grow.id))
        setNotice('Eintrag existiert bereits nicht mehr.')
        await loadGrows()
        return
      }
      setError(formatApiError(caught, 'Grow konnte nicht gelöscht werden.'))
    } finally {
      setSaving(null)
    }
  }

  return (
    <V1Page
      eyebrow="Grow-Verwaltung"
      title="Grows"
      subtitle="Aktive und geplante Grows verwalten. Neue Grows werden über den bestehenden Start-Workflow angelegt."
      action={<V1LinkButton to="/grows/new" variant="primary">Neuen Grow anlegen</V1LinkButton>}
    >
      {error && <V1Alert title="Fehler" message={error} tone="warn" />}
      {notice && <V1Alert message={notice} tone="ok" />}

      <section className="v1-kpi-grid">
        <V1Stat label="Aktiv" value={activeGrows.filter((grow) => grow.status === 'Running').length} />
        <V1Stat label="Geplant" value={activeGrows.filter((grow) => grow.status === 'Planning').length} />
        <V1Stat label="Archiviert" value={archivedGrows.length} />
        <V1Stat label="Messungen" value={activeGrows.reduce((sum, grow) => sum + grow.measurementCount, 0)} />
      </section>

      {loading ? <V1Empty title="Lade Grows..." /> : (
        <>
          <V1Section title="Aktive Grows">
            {activeGrows.length === 0 ? (
              <V1Empty title="Keine aktiven Grows" action={<V1LinkButton to="/grows/new" variant="primary">Neuen Grow anlegen</V1LinkButton>} />
            ) : (
              <div className="grows-overview-grid" data-audit="grows-overview">
                {activeGrows.map((grow) => (
                  <GrowCard
                    key={grow.id}
                    grow={grow}
                    saving={saving}
                    hydroName={getHydroName(grow, hydroNames)}
                    onArchive={archiveGrow}
                    onDelete={deleteGrow}
                  />
                ))}
              </div>
            )}
          </V1Section>

          {visibleArchived.length > 0 && (
            <V1Section title="Zuletzt archiviert">
              <div className="grows-overview-grid compact" data-audit="grows-archive">
                {visibleArchived.map((grow) => (
                  <GrowCard
                    key={grow.id}
                    grow={grow}
                    saving={saving}
                    archived
                    hydroName={getHydroName(grow, hydroNames)}
                    onArchive={archiveGrow}
                    onDelete={deleteGrow}
                  />
                ))}
              </div>
            </V1Section>
          )}
        </>
      )}
    </V1Page>
  )
}

function GrowCard({ grow, saving, hydroName, archived = false, onArchive, onDelete }: { grow: GrowSummary; saving: string | null; hydroName: string; archived?: boolean; onArchive: (grow: GrowSummary) => void; onDelete: (grow: GrowSummary) => void }) {
  const canArchive = grow.status === 'Planning' || grow.status === 'Running'
  const actionDisabled = Boolean(saving)
  return (
    <V1Card className={classNames('grow-overview-card', archived && 'archived')} tone={grow.status === 'Running' ? 'ok' : grow.status === 'Planning' ? 'warn' : 'neutral'}>
      <div className="grow-overview-card__header">
        <div>
          <span className="v1-card-kicker">{grow.hydroStyle}</span>
          <h2>{grow.name}</h2>
        </div>
        <V1Badge tone={grow.status === 'Running' ? 'ok' : grow.status === 'Planning' ? 'warn' : 'neutral'}>{formatStatus(grow.status)}</V1Badge>
      </div>

      <dl className="grow-overview-card__metrics">
        <Metric label="Zelt" value={grow.tentName ?? '–'} />
        <Metric label="Hydro" value={hydroName} />
        <Metric label="Start" value={formatDate(grow.startDate)} />
        <Metric label="Phase" value={grow.latestStage ?? '–'} />
        <Metric label="Letzte Messung" value={formatDateTime(grow.latestMeasurementAt)} />
        <Metric label="Messungen" value={String(grow.measurementCount)} />
      </dl>

      <div className="grow-overview-card__actions" data-audit="grow-list-actions">
        <V1LinkButton to={`/grows/${grow.id}`} variant="primary">Öffnen</V1LinkButton>
        <V1LinkButton to={`/grows/${grow.id}/setup`}>Bearbeiten</V1LinkButton>
        {canArchive && <V1Button disabled={actionDisabled} onClick={() => void onArchive(grow)}>{saving === `archive-${grow.id}` ? 'Beendet...' : 'Beenden'}</V1Button>}
        <V1Button variant="danger" disabled={actionDisabled} onClick={() => void onDelete(grow)}>{saving === `delete-${grow.id}` ? 'Löscht...' : 'Löschen'}</V1Button>
      </div>
    </V1Card>
  )
}

function Metric({ label, value }: { label: string; value: string }) {
  return <div><dt>{label}</dt><dd>{value}</dd></div>
}

function sortGrows(items: GrowSummary[]) {
  return [...items].sort((a, b) => a.status.localeCompare(b.status) || a.name.localeCompare(b.name))
}

function getHydroName(grow: GrowSummary, hydroNames: Map<number, string>) {
  return grow.systemId ? hydroNames.get(grow.systemId) ?? `Hydro #${grow.systemId}`
    : grow.setupId ? `Setup #${grow.setupId}`
      : '–'
}

function formatStatus(status: GrowSummary['status']) {
  return status === 'Running' ? 'aktiv'
    : status === 'Planning' ? 'geplant'
      : status === 'Completed' ? 'beendet'
        : status === 'Aborted' ? 'abgebrochen'
          : status
}

function formatDate(value: string | null) {
  if (!value) return '–'
  return new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit', year: '2-digit' }).format(new Date(value))
}

function formatDateTime(value: string | null) {
  if (!value) return '–'
  return new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function formatApiError(caught: unknown, fallback: string) {
  if (caught instanceof ApiRequestError && caught.payload?.message) return caught.payload.message
  return caught instanceof Error ? caught.message : fallback
}

function isNotFound(caught: unknown) {
  return caught instanceof ApiRequestError && caught.status === 404
}

export default GrowsPage
