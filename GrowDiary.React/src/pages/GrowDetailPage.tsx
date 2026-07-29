import { useEffect, useMemo, useState } from 'react'
import '../features/grow-detail/growdetail-instrument.css'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { formatNumber } from '../utils'
import { useGrowDetailBundle } from '../features/grow-detail/useGrowDetailBundle'
import { useGrowDetailMutations } from '../features/grow-detail/useGrowDetailMutations'
import { formatGrowStatus } from '../features/grow-detail/grow-detail-model'
import { V1Alert, V1Badge, V1Button, V1Empty, V1LinkButton, V1Page, V1Section, V1Stat } from '../components/v1'
import { buildPhaseTimeline, flipLabel } from '../features/grows/phase-timeline'
import type { GrowDeviationDto } from '../types'
import { apiFetch } from '../api'

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
  // Fuer die Diagnose-Kurzliste des Entwurfs: die zwei wichtigsten Abweichungen
  // direkt auf dem Ueberblick, der Rest hinter dem Link.
  const [deviations, setDeviations] = useState<GrowDeviationDto[]>([])
  useEffect(() => {
    if (!growId) return
    const controller = new AbortController()
    apiFetch<GrowDeviationDto[]>(`/api/grows/${growId}/deviations`, { signal: controller.signal })
      .then(setDeviations)
      .catch(() => { /* Kurzliste ist Beigabe — der Ueberblick steht auch ohne sie. */ })
    return () => controller.abort()
  }, [growId])
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
      <V1Page eyebrow="Grow" title="Lade Daten...">
        <V1Empty title="Einen Moment" />
      </V1Page>
    )
  }

  if (!bundle.grow) {
    return (
      <V1Page eyebrow="Grow" title="Nicht gefunden" action={<V1LinkButton to="/grows">Zu den Grows</V1LinkButton>}>
        <V1Alert title="Fehler" message={error ?? 'Diesen Grow gibt es nicht (mehr).'} tone="warn" />
      </V1Page>
    )
  }

  const grow = bundle.grow
  const latest = grow.latestMeasurement
  const scope = `?growId=${grow.id}`
  const canArchiveGrow = grow.status === 'Planning' || grow.status === 'Running'
  const statusTone = grow.status === 'Running' ? 'ok' : grow.status === 'Planning' ? 'warn' : 'neutral'
  const canFlip = grow.status === 'Running' && !grow.flipDate
  // Der Übergang zur Veg hängt am Aussehen, nicht am Kalender — echte gezackte
  // Blätter statt der zwei runden Keimblätter. Also ein Knopf, solange noch
  // nichts eingetragen ist und noch nicht geflippt wurde.
  // Beide Knöpfe hängen an der Phase, die der Server ausrechnet — dieselbe
  // Quelle wie die Zielwerte. „Sämling ist durch" gibt es nur im Sämling
  // (Klone haben nie einen), „Finish beginnt" nur in der Blüte — auch bei
  // Autoflowern, die keinen Flip kennen.
  const canConfirmVeg = grow.currentStage === 'Seedling' && !grow.vegStartedAt && grow.status === 'Running'
  const canConfirmFinish = ['Transition', 'Flower'].includes(grow.currentStage)
    && !grow.finishStartedAt && grow.status === 'Running'
  const canHarvest = ['Flower', 'Finish', 'Dry'].includes(latest?.stage ?? grow.entryPoint ?? '')
  const timeline = buildPhaseTimeline(grow)
  const lastMeasurements = [...bundle.measurements]
    .sort((a, b) => b.takenAt.localeCompare(a.takenAt))
    .slice(0, 4)

  return (
    <div className="ix-growdetail">
      <V1Page
        eyebrow={`Grow / ${grow.name}`}
        title={grow.name}
        action={(
          <div className="v1-action-row" data-audit="grow-management-actions">
            <V1Badge tone={statusTone}>{formatGrowStatus(grow.status)}</V1Badge>
            <V1LinkButton to={`/grows/${grow.id}/addback`}>Addback</V1LinkButton>
            {canConfirmVeg && (
              <V1Button disabled={Boolean(saving)} onClick={() => void handleGrowAction('veg')}>
                {saving === 'action-veg' ? 'Trägt ein…' : 'Sämling ist durch'}
              </V1Button>
            )}
            {canFlip && (
              <V1Button disabled={Boolean(saving)} onClick={() => void handleGrowAction('flip')}>
                {saving === 'flip' ? 'Trägt ein…' : 'Flip 12/12'}
              </V1Button>
            )}
            {canConfirmFinish && (
              <V1Button disabled={Boolean(saving)} onClick={() => void handleGrowAction('finish')}>
                {saving === 'action-finish' ? 'Trägt ein…' : 'Finish beginnt'}
              </V1Button>
            )}
            {canHarvest && <V1LinkButton to={`/grows/${grow.id}/harvest`} variant="primary">Ernte</V1LinkButton>}
            <a className="v1-button" href={`/grows/${grow.id}/export`}>Export</a>
          </div>
        )}
        className="grow-detail-page"
      >
        {error && <V1Alert title="Fehler" message={error} tone="warn" />}
        {notice && <V1Alert message={notice} tone="ok" />}

        {/* Die Tabs des Entwurfs führen zu den Top-Seiten — der Grow ist kein
            Behälter mehr, aber der Weg von hier zu seinen Daten bleibt einer. */}
        <nav className="gd-tabs" aria-label="Bereiche dieses Grows">
          <span className="gd-tab is-active">Überblick</span>
          <Link className="gd-tab" to={`/diagnose${scope}`}>Diagnose{deviations.length > 0 ? ` · ${deviations.length}` : ''}</Link>
          <Link className="gd-tab" to={`/messungen${scope}`}>Messungen · {bundle.measurements.length}</Link>
          <Link className="gd-tab" to={`/sops${scope}`}>SOPs</Link>
          <Link className="gd-tab" to={`/journal${scope}`}>Journal & Fotos</Link>
          <Link className="gd-tab" to={`/regeln${scope}&tab=automatik`}>Automatik</Link>
          <Link className="gd-tab" to={`/berater${scope}`}>KI-Berater</Link>
        </nav>

        {/* Phasen-Timeline — dieselbe Rechnung wie auf der Live-Seite. */}
        <section className="ls-panel" data-audit="grow-detail-timeline">
          <div className="ls-panel-body">
            <div className="ls-timeline">
              {timeline.phases.map((phase) => (
                <div key={phase.label} className={`ls-phase is-${phase.state}${phase.days === 0 ? ' is-unknown' : ''}`} style={{ flexGrow: Math.max(1, phase.days) }}>
                  {phase.progress != null && (
                    <i className="ls-phase-fill" style={{ width: `${Math.round(phase.progress * 100)}%` }} aria-hidden="true" />
                  )}
                  <span>{phase.label}</span>
                </div>
              ))}
              {timeline.phases.length === 0 && <div className="ls-phase is-planned"><span>Kein Startdatum</span></div>}
            </div>
            <div className="ls-timeline-dates">
              <span>Start {timeline.dates.start}</span>
              <span className={timeline.daysToFlip != null && timeline.daysToFlip < 0 ? 'is-due' : undefined}>
                {flipLabel(timeline.flipIsPlanned, timeline.daysToFlip, timeline.dates.flip)}
              </span>
              <span>{timeline.dates.harvest === '—' ? 'Ernte offen' : `Ernte ~${timeline.dates.harvest}`}</span>
            </div>
            {/* Ohne Plan bleibt der Strahl offen — dann steht hier, wo man ihn
                setzt, statt dass drei Striche ohne Erklaerung dastehen. */}
            {timeline.dates.flip === '—' && (
              <div className="gd-plan-hint">
                <p className="gc-facts">Keine Veg-Dauer geplant — ohne sie kann der Strahl keinen Flip- und Erntetermin zeigen.</p>
                <Link className="ls-btn is-small" to={`/grows/${grow.id}/setup`}>Veg-Dauer eintragen</Link>
              </div>
            )}
          </div>
        </section>

        {/* Fakten-Leiste wie im Entwurf: die sechs Zahlen, nach denen man sucht. */}
        <section className="v1-kpi-grid" data-audit="grow-detail-summary">
          <V1Stat label="Sorte" value={grow.strain ?? '—'} hint={[grow.breeder, grow.seedType].filter(Boolean).join(' · ') || undefined} />
          <V1Stat label="Pflanzen" value={grow.plantCount ?? '—'} />
          <V1Stat label="pH / EC" value={`${formatNumber(latest?.reservoirPh, 2)} · ${formatNumber(latest?.reservoirEc, 2)}`} />
          <V1Stat label="Klima" value={latest ? `${formatNumber(latest.airTemperatureC, 1)}° · ${formatNumber(latest.humidityPercent, 0)}%` : '—'} />
          <V1Stat label="Messungen" value={bundle.measurements.length} />
          <V1Stat label="Offene Tasks" value={openTasks.length} tone={openTasks.length > 0 ? 'warn' : 'neutral'} />
        </section>

        <div className="gd-lower">
          <section className="ls-panel gd-diagnose" data-audit="grow-detail-diagnose">
            <div className="ls-panel-head">
              <span className="ls-label">Diagnose</span>
              <span className="ls-panel-meta">Abweichung → Symptom → Behandlung</span>
              {deviations.length > 2 && <Link className="ls-btn is-small" to={`/diagnose${scope}`}>Alle {deviations.length}</Link>}
            </div>
            {deviations.length === 0 ? (
              <div className="ls-panel-body"><p>Keine offenen Abweichungen — alle Werte im Rahmen.</p></div>
            ) : (
              <div className="gd-devs">
                {deviations.slice(0, 2).map((deviation) => (
                  <article key={deviation.stableKey} className={`gd-dev is-${deviation.severity.toLowerCase()}`}>
                    <strong>{deviation.message}</strong>
                    {/* Die Empfehlung nur, wenn sie etwas hinzufuegt — bei
                        manchen Abweichungen ist sie woertlich die Meldung. */}
                    {deviation.recommendation && deviation.recommendation !== deviation.message && <p>{deviation.recommendation}</p>}
                    <div className="ls-panel-actions">
                      <Link className="ls-btn is-small" to={`/diagnose${scope}`}>Verlauf</Link>
                    </div>
                  </article>
                ))}
              </div>
            )}
          </section>

          <section className="ls-panel gd-meas" data-audit="grow-detail-measurements">
            <div className="ls-panel-head">
              <span className="ls-label">Letzte Messungen</span>
              <Link className="ls-btn is-small" to="/messung">Neue Messung</Link>
            </div>
            {lastMeasurements.length === 0 ? (
              <div className="ls-panel-body"><p>Noch keine Messung — die erste dauert zwei Minuten.</p></div>
            ) : (
              <div className="gd-meas-wrap">
                <table className="gd-meas-table">
                  <thead>
                    <tr><th scope="col">Zeit</th><th scope="col">pH</th><th scope="col">EC</th><th scope="col">DO</th><th scope="col">Temp</th></tr>
                  </thead>
                  <tbody>
                    {lastMeasurements.map((measurement) => (
                      <tr key={measurement.id}>
                        <td>{formatShortTime(measurement.takenAt)}</td>
                        <td>{formatNumber(measurement.reservoirPh, 2)}</td>
                        <td>{formatNumber(measurement.reservoirEc, 2)}</td>
                        <td>{formatNumber(measurement.dissolvedOxygenMgL, 1)}</td>
                        <td>{formatNumber(measurement.reservoirWaterTempC, 1)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>

        {/* Verwaltung unten — Beenden und Löschen gehören nicht neben die
            täglichen Handlungen in der Kopfzeile. */}
        <V1Section title="Verwaltung">
          <div className="v1-action-row">
            <V1LinkButton to={`/grows/${grow.id}/setup`}>Bearbeiten</V1LinkButton>
            <V1Button disabled={Boolean(saving) || !canArchiveGrow} onClick={() => void archiveGrow()}>
              {saving === 'grow-archive' ? 'Beendet...' : canArchiveGrow ? 'Beenden' : 'Beendet'}
            </V1Button>
            <V1Button variant="danger" disabled={Boolean(saving)} onClick={() => void deleteGrow()}>
              {saving === 'grow-delete' ? 'Löscht...' : 'Löschen'}
            </V1Button>
          </div>
        </V1Section>
      </V1Page>
    </div>
  )
}

/** „26.07. 09:30" — Datum und Uhrzeit, so kurz wie die Tabelle schmal ist. */
function formatShortTime(iso: string): string {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }).format(date)
}

export default GrowDetailPage
