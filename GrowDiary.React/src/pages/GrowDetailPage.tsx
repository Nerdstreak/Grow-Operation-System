import { useEffect, useMemo, useState } from 'react'
import '../features/grow-detail/growdetail-instrument.css'
import { useNavigate, useParams } from 'react-router-dom'
import { formatDate, formatDateTime } from '../utils'
import { GrowDetailOverviewHero } from '../features/grow-detail/GrowDetailOverviewHero'
import { useGrowDetailBundle } from '../features/grow-detail/useGrowDetailBundle'
import { useGrowDetailMutations } from '../features/grow-detail/useGrowDetailMutations'
import {
  formatGrowHydroMedium,
  formatGrowRuntime,
  formatGrowStatus,
} from '../features/grow-detail/grow-detail-model'
import { V1Alert, V1Badge, V1Button, V1Empty, V1LinkButton, V1Page, V1Section } from '../components/v1'

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

  return (
    <div className="ix-growdetail">
      <V1Page
        eyebrow={`${grow.strain ?? 'Sorte offen'} · ${grow.breeder ?? 'Breeder offen'}`}
        title={grow.name}
        action={(
          <div className="v1-action-row" data-audit="grow-management-actions">
            <V1Badge tone={statusTone}>{formatGrowStatus(grow.status)}</V1Badge>
            <V1LinkButton to={`/grows/${grow.id}/setup`} variant="primary">Bearbeiten</V1LinkButton>
            <V1Button disabled={Boolean(saving) || !canArchiveGrow} onClick={() => void archiveGrow()}>
              {saving === 'grow-archive' ? 'Beendet...' : canArchiveGrow ? 'Beenden' : 'Beendet'}
            </V1Button>
            <V1Button variant="danger" disabled={Boolean(saving)} onClick={() => void deleteGrow()}>
              {saving === 'grow-delete' ? 'Löscht...' : 'Löschen'}
            </V1Button>
          </div>
        )}
        className="grow-detail-page"
      >
        {error && <V1Alert title="Fehler" message={error} tone="warn" />}
        {notice && <V1Alert message={notice} tone="ok" />}

        {/* Name, Sorte und Status standen bis hier dreimal auf der Seite: in der
            Kopfzeile, in der Mobil-Zusammenfassung und noch einmal im Hero. Sie
            stehen jetzt einmal oben; was bleibt, sind die Fakten, die sie nicht
            wiederholen. */}
        <V1Section title="Auf einen Blick">
          <div className="v1-list" data-audit="grow-detail-summary">
            {([
              ['Phase', grow.latestMeasurement?.stage ?? grow.entryPoint ?? '–'],
              ['Zelt', grow.tentName ?? 'ohne Zelt'],
              ['Hydro / Medium', formatGrowHydroMedium(grow)],
              ['Start', `${formatDate(grow.startDate)} · ${formatGrowRuntime(grow.startDate)}`],
              ['Letzte Messung', grow.latestMeasurement ? formatDateTime(grow.latestMeasurement.takenAt) : '–'],
              ['Messungen', String(bundle.measurements.length)],
            ] as Array<[string, string]>).map(([label, value]) => (
              <div key={label} className="v1-list-row">
                <span>{label}</span>
                <strong>{value}</strong>
              </div>
            ))}
          </div>
        </V1Section>

        <GrowDetailOverviewHero
          grow={grow}
          latest={latest}
          measurementCount={bundle.measurements.length}
          openTaskCount={openTasks.length}
        />

        <V1Section title="Zu diesem Grow">
          <div className="v1-action-row">
            <V1LinkButton to={`/messungen${scope}`}>Messungen</V1LinkButton>
            <V1LinkButton to={`/diagnose${scope}`}>Diagnose</V1LinkButton>
            <V1LinkButton to={`/journal${scope}`}>Journal &amp; Fotos</V1LinkButton>
            <V1LinkButton to={`/sops${scope}`}>SOPs</V1LinkButton>
            <V1LinkButton to={`/regeln${scope}&tab=automatik`}>Automatik</V1LinkButton>
          </div>
        </V1Section>
      </V1Page>
    </div>
  )
}

export default GrowDetailPage
