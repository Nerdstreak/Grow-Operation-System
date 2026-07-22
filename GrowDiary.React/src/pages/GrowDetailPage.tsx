import { useEffect, useMemo, useState } from 'react'
import '../features/grow-detail/growdetail-instrument.css'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { formatDate, formatDateTime } from '../utils'
import { GrowDetailOverviewHero } from '../features/grow-detail/GrowDetailOverviewHero'
import { useGrowDetailBundle } from '../features/grow-detail/useGrowDetailBundle'
import { useGrowDetailMutations } from '../features/grow-detail/useGrowDetailMutations'
import {
  formatGrowHydroMedium,
  formatGrowRuntime,
  formatGrowStatus,
} from '../features/grow-detail/grow-detail-model'

const noop = async () => {}

// The grow's own page does exactly one thing: show this grow's overview. The former
// tabs (measurements, diagnosis, journal, SOPs, automation) are now their own
// top-level pages with a grow switcher — reached from the nav or the quick links
// below, pre-selected to this grow. No drilling into a grow to find features.
function GrowDetailPage() {
  const { growId } = useParams()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState<string | null>(null)
  const { bundle, loading, loadBundle } = useGrowDetailBundle({ growId, setError })
  const openTasks = useMemo(() => bundle.tasks.filter((task) => task.status === 'Open'), [bundle.tasks])
  const {
    archiveGrow,
    deleteGrow,
    handleGrowAction,
  } = useGrowDetailMutations({
    growId,
    grow: bundle.grow,
    saving,
    selectedMeasurement: null,
    sopStepNotesById: {},
    navigate,
    loadBundle,
    loadDeviations: noop,
    loadPhotos: noop,
    loadSopInstances: noop,
    loadTreatmentRecommendations: noop,
    setError,
    setNotice,
    setSaving,
  })

  useEffect(() => {
    const controller = new AbortController()
    const handle = window.setTimeout(() => {
      void loadBundle(controller.signal)
    }, 0)
    return () => {
      window.clearTimeout(handle)
      controller.abort()
    }
  }, [loadBundle])

  if (loading) {
    return (
      <>
        <div className="topbar"><span className="topbar-title">Grow-Detail</span></div>
        <div className="page-scroll"><div className="empty-hint">Lade Daten...</div></div>
      </>
    )
  }

  if (!bundle.grow) {
    return (
      <>
        <div className="topbar"><Link className="btn" to="/grows">Zurück</Link></div>
        <div className="page-scroll">
          <div className="empty-hint" style={{ color: 'var(--red)' }}>{error ?? 'Grow nicht gefunden.'}</div>
        </div>
      </>
    )
  }

  const grow = bundle.grow
  const latest = grow.latestMeasurement
  const scope = `?growId=${grow.id}`
  const canConfirmGermination = grow.startMaterial === 'Seed' && !grow.germinatedAt
  const canConfirmRooting = grow.startMaterial === 'Clone' && !grow.rootedAt
  const canFlipToFlower = grow.seedType !== 'Autoflower' && !grow.flipDate
  const canArchiveGrow = grow.status === 'Planning' || grow.status === 'Running'

  return (
    <div className="ix-growdetail">
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to="/grows">Zurück</Link>
          <span className="topbar-title">{grow.name}</span>
        </div>
        <div className="topbar-right">
          <span className={`badge ${grow.status === 'Running' ? 'badge-ok' : grow.status === 'Planning' ? 'badge-warn' : 'badge-neutral'}`}>{grow.status}</span>
          <div className="grow-management-actions" data-audit="grow-management-actions">
            <Link className="btn btn-primary" to={`/grows/${grow.id}/setup`}>Bearbeiten</Link>
            <button type="button" className="btn" disabled={Boolean(saving) || !canArchiveGrow} onClick={() => void archiveGrow()}>
              {saving === 'grow-archive' ? 'Beendet...' : canArchiveGrow ? 'Beenden' : 'Beendet'}
            </button>
            <button type="button" className="btn" disabled={Boolean(saving)} onClick={() => void deleteGrow()}>
              {saving === 'grow-delete' ? 'Löscht...' : 'Löschen'}
            </button>
          </div>
        </div>
      </div>

      <div className="page-scroll grow-detail-page" data-audit="grow-detail">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)' }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}
        {notice && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)', background: 'var(--green-bg)', borderColor: 'var(--green)' }}>
            <div className="alert-dot" style={{ background: 'var(--green)' }} />
            <strong style={{ color: 'var(--green)' }}>Info</strong>
            <span>{notice}</span>
          </div>
        )}

        <section className="grow-detail-mobile-summary" data-audit="grow-detail-summary">
          <div className="grow-detail-mobile-head">
            <div>
              <span className="section-label">Grow</span>
              <h1>{grow.name}</h1>
              <p>{grow.strain ?? 'Sorte offen'} · {grow.breeder ?? 'Breeder offen'}</p>
            </div>
            <span className={`badge ${grow.status === 'Running' ? 'badge-ok' : grow.status === 'Planning' ? 'badge-warn' : 'badge-neutral'}`}>{formatGrowStatus(grow.status)}</span>
          </div>
          <dl className="grow-detail-mobile-facts">
            <div><dt>Phase</dt><dd>{grow.latestMeasurement?.stage ?? grow.entryPoint ?? '–'}</dd></div>
            <div><dt>Zelt</dt><dd>{grow.tentName ?? 'ohne Zelt'}</dd></div>
            <div><dt>Hydro / Medium</dt><dd>{formatGrowHydroMedium(grow)}</dd></div>
            <div><dt>Start</dt><dd>{formatDate(grow.startDate)} · {formatGrowRuntime(grow.startDate)}</dd></div>
            <div><dt>Letzte Messung</dt><dd>{grow.latestMeasurement ? formatDateTime(grow.latestMeasurement.takenAt) : '–'}</dd></div>
            <div><dt>Messungen</dt><dd>{bundle.measurements.length}</dd></div>
          </dl>
          <div className="grow-detail-mobile-links">
            <Link className="btn" to={`/messungen${scope}`}>Messungen</Link>
            <Link className="btn" to={`/diagnose${scope}`}>Diagnose</Link>
            <Link className="btn" to={`/journal${scope}`}>Journal &amp; Fotos</Link>
            <Link className="btn" to={`/sops${scope}`}>SOPs</Link>
            <Link className="btn" to={`/automatik${scope}`}>Automatik</Link>
            <Link className="btn" to={`/grows/${grow.id}/addback`}>Addback</Link>
          </div>
        </section>

        <GrowDetailOverviewHero
          grow={grow}
          latest={latest}
          measurementCount={bundle.measurements.length}
          openTaskCount={openTasks.length}
          saving={saving}
          canConfirmGermination={canConfirmGermination}
          canConfirmRooting={canConfirmRooting}
          canFlipToFlower={canFlipToFlower}
          onGrowAction={(action) => void handleGrowAction(action)}
        />
      </div>
    </div>
  )
}

export default GrowDetailPage
