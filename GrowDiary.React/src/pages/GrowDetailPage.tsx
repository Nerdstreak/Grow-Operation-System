import { useEffect, useMemo, useState } from 'react'
import '../features/grow-detail/growdetail-instrument.css'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { formatDate, formatDateTime } from '../utils'
import { GrowDetailAutomationSection } from '../features/grow-detail/GrowDetailAutomationSection'
import { GrowDetailDiagnosisSection } from '../features/grow-detail/GrowDetailDiagnosisSection'
import { GrowDetailJournalPanel } from '../features/grow-detail/GrowDetailJournalPanel'
import { GrowDetailMeasurementsSection } from '../features/grow-detail/GrowDetailMeasurementsSection'
import { GrowDetailOverviewHero } from '../features/grow-detail/GrowDetailOverviewHero'
import { GrowDetailSopSection } from '../features/grow-detail/GrowDetailSopSection'
import { useGrowDetailAutomation } from '../features/grow-detail/useGrowDetailAutomation'
import { useGrowDetailBundle } from '../features/grow-detail/useGrowDetailBundle'
import { useGrowDetailMutations } from '../features/grow-detail/useGrowDetailMutations'
import { useGrowDetailResources } from '../features/grow-detail/useGrowDetailResources'
import {
  detailSections,
  formatGrowHydroMedium,
  formatGrowRuntime,
  formatGrowStatus,
  type GrowDetailSection,
} from '../features/grow-detail/grow-detail-model'

function GrowDetailPage() {
  const { growId } = useParams()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState<string | null>(null)
  const [searchParams] = useSearchParams()
  // Allow deep-linking to a section, e.g. /grows/42?section=diagnosis from the live page.
  const initialSection = detailSections.find((section) => section.key === searchParams.get('section'))?.key ?? 'overview'
  const [activeSection, setActiveSection] = useState<GrowDetailSection>(initialSection)
  const {
    bundle,
    loading,
    photoLoading,
    photos,
    selectedMeasurementId,
    handleMeasurementSelection,
    loadBundle,
    loadPhotos,
  } = useGrowDetailBundle({ growId, setError })
  const {
    deviationError,
    deviations,
    loadDeviations,
    loadRiskEvents,
    loadSopInstances,
    loadTreatmentRecommendations,
    riskEventError,
    riskEvents,
    setSopStepNotesById,
    sopInstanceError,
    sopInstances,
    sopStepNotesById,
    sopStepsByInstanceId,
    treatmentRecommendationError,
    treatmentRecommendations,
  } = useGrowDetailResources({ growId })
  const {
    autoConfigForm,
    autoConfigs,
    autoLoading,
    autoMappingsByConfigId,
    autoRunsByConfigId,
    autoStatusByConfigId,
    autoStatusError,
    mappingDraftsByConfigId,
    addMappingDraft,
    createLightPreset,
    handleAutoConfigSubmit,
    loadAutoMeasurements,
    removeMappingDraft,
    saveMappingDrafts,
    setAutoConfigForm,
    updateMappingDraft,
  } = useGrowDetailAutomation({
    growId,
    grow: bundle.grow,
    setError,
    setNotice,
    setSaving,
  })
  const openTasks = useMemo(() => bundle.tasks.filter((task) => task.status === 'Open'), [bundle.tasks])
  const closedTasks = useMemo(() => bundle.tasks.filter((task) => task.status !== 'Open'), [bundle.tasks])
  const selectedMeasurement = useMemo(
    () => bundle.measurements.find((measurement) => measurement.id === selectedMeasurementId) ?? null,
    [bundle.measurements, selectedMeasurementId],
  )
  const {
    journalForm,
    measurementForm,
    photoForm,
    taskForm,
    archiveGrow,
    deleteGrow,
    handleGrowAction,
    handleJournalSubmit,
    handleMeasurementSubmit,
    handlePhotoSubmit,
    handleTaskSubmit,
    setJournalForm,
    setMeasurementForm,
    setPhotoForm,
    setTaskForm,
    startRecommendedSop,
    updateSopStep,
    updateTaskStatus,
  } = useGrowDetailMutations({
    growId,
    grow: bundle.grow,
    saving,
    selectedMeasurement,
    sopStepNotesById,
    navigate,
    loadBundle,
    loadDeviations,
    loadPhotos,
    loadSopInstances,
    loadTreatmentRecommendations,
    setError,
    setNotice,
    setSaving,
  })

  useEffect(() => {
    const controller = new AbortController()
    const handle = window.setTimeout(() => {
      void loadBundle(controller.signal)
      void loadAutoMeasurements(controller.signal)
      void loadDeviations(controller.signal)
      void loadTreatmentRecommendations(controller.signal)
      void loadSopInstances(controller.signal)
      void loadRiskEvents(controller.signal)
    }, 0)
    return () => {
      window.clearTimeout(handle)
      controller.abort()
    }
  }, [loadAutoMeasurements, loadBundle, loadDeviations, loadTreatmentRecommendations, loadSopInstances, loadRiskEvents])

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
            <Link className="btn" to={`/grows/${grow.id}/addback`}>Addback</Link>
            <Link className="btn" to="/messung">Messung</Link>
            <Link className="btn" to={`/analyse?leftGrowId=${grow.id}`}>Vergleichen</Link>
          </div>
          <div className="grow-management-actions grow-detail-mobile-actions" data-audit="grow-detail-actions">
            <Link className="btn btn-primary" to={`/grows/${grow.id}/setup`}>Bearbeiten</Link>
            <button type="button" className="btn" disabled={Boolean(saving) || !canArchiveGrow} onClick={() => void archiveGrow()}>
              {saving === 'grow-archive' ? 'Beendet...' : canArchiveGrow ? 'Beenden' : 'Beendet'}
            </button>
            <button type="button" className="btn" disabled={Boolean(saving)} onClick={() => void deleteGrow()}>
              {saving === 'grow-delete' ? 'Löscht...' : 'Löschen'}
            </button>
          </div>
        </section>

        <div className="section-tabs detail-tabs" style={{ marginBottom: 18 }}>
          {detailSections.map((section) => (
            <button
              key={section.key}
              type="button"
              className={`btn ${activeSection === section.key ? 'btn-primary' : ''}`}
              onClick={() => setActiveSection(section.key)}
            >
              {section.label}
            </button>
          ))}
        </div>

        <div style={{ display: activeSection === 'overview' ? undefined : 'none' }}>
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

        <GrowDetailDiagnosisSection
          activeSection={activeSection}
          deviations={deviations}
          deviationError={deviationError}
          treatmentRecommendations={treatmentRecommendations}
          treatmentRecommendationError={treatmentRecommendationError}
          riskEvents={riskEvents}
          riskEventError={riskEventError}
          saving={saving}
          onStartRecommendedSop={(recommendation) => void startRecommendedSop(recommendation)}
          onRiskChanged={(message) => { setNotice(message); void loadRiskEvents() }}
        />

        <GrowDetailSopSection
          activeSection={activeSection}
          sopInstances={sopInstances}
          sopStepsByInstanceId={sopStepsByInstanceId}
          sopStepNotesById={sopStepNotesById}
          sopInstanceError={sopInstanceError}
          saving={saving}
          onNoteChange={(stepId, notes) => setSopStepNotesById((current) => ({ ...current, [stepId]: notes }))}
          onUpdateStep={(step, status) => void updateSopStep(step, status)}
        />

        <div className="detail-layout" style={{ display: activeSection === 'overview' || activeSection === 'diagnosis' || activeSection === 'sops' ? 'none' : undefined }}>
          <div>
            <GrowDetailMeasurementsSection
              activeSection={activeSection}
              measurements={bundle.measurements}
              selectedMeasurementId={selectedMeasurementId}
              measurementForm={measurementForm}
              saving={saving}
              onSelectMeasurement={(measurementId) => void handleMeasurementSelection(measurementId)}
              onMeasurementFormChange={(patch) => setMeasurementForm((current) => ({ ...current, ...patch }))}
              onSubmit={handleMeasurementSubmit}
            />

            <div className="section-label" style={{ display: activeSection === 'journal' ? undefined : 'none' }}>Journal</div>
            <div className="card" style={{ marginBottom: 14, display: activeSection === 'journal' ? undefined : 'none' }}>
              <div className="card-header">
                <span className="card-title">Einträge</span>
                <span className="text-muted" style={{ fontSize: 13 }}>{bundle.journal.length}</span>
              </div>
              {bundle.journal.length === 0 ? (
                <div className="empty-hint">Noch keine Journal-Einträge.</div>
              ) : (
                bundle.journal.map((entry) => (
                  <div key={entry.id} className="timeline-item" style={{ padding: '12px 16px' }}>
                    <div className="tl-dot-col">
                      <div className="tl-dot journal" />
                      <div className="tl-line" />
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="tl-title">{entry.title ?? entry.entryType}</div>
                      {entry.body && <div className="tl-sub">{entry.body}</div>}
                    </div>
                    <div className="tl-time">{formatDateTime(entry.occurredAtUtc)}</div>
                  </div>
                ))
              )}
            </div>

            <GrowDetailAutomationSection
              activeSection={activeSection}
              autoConfigs={autoConfigs}
              autoConfigForm={autoConfigForm}
              autoStatusByConfigId={autoStatusByConfigId}
              autoMappingsByConfigId={autoMappingsByConfigId}
              autoRunsByConfigId={autoRunsByConfigId}
              mappingDraftsByConfigId={mappingDraftsByConfigId}
              autoStatusError={autoStatusError}
              autoLoading={autoLoading}
              saving={saving}
              onAutoConfigFormChange={(patch) => setAutoConfigForm((current) => ({ ...current, ...patch }))}
              onAutoConfigSubmit={handleAutoConfigSubmit}
              onAddMappingDraft={addMappingDraft}
              onUpdateMappingDraft={updateMappingDraft}
              onRemoveMappingDraft={removeMappingDraft}
              onSaveMappingDrafts={(configId) => void saveMappingDrafts(configId)}
              onCreateLightPreset={() => void createLightPreset()}
            />
          </div>

          {activeSection === 'journal' && (
            <GrowDetailJournalPanel
              grow={grow}
              openTasks={openTasks}
              closedTasks={closedTasks}
              measurements={bundle.measurements}
              photos={photos}
              selectedMeasurementId={selectedMeasurementId}
              journalForm={journalForm}
              taskForm={taskForm}
              photoForm={photoForm}
              photoLoading={photoLoading}
              saving={saving}
              onTaskStatusChange={(taskId, status) => void updateTaskStatus(taskId, status)}
              onJournalFormChange={(patch) => setJournalForm((current) => ({ ...current, ...patch }))}
              onTaskFormChange={(patch) => setTaskForm((current) => ({ ...current, ...patch }))}
              onPhotoFormChange={(patch) => setPhotoForm((current) => ({ ...current, ...patch }))}
              onMeasurementSelection={(measurementId) => void handleMeasurementSelection(measurementId)}
              onJournalSubmit={handleJournalSubmit}
              onTaskSubmit={handleTaskSubmit}
              onPhotoSubmit={handlePhotoSubmit}
            />
          )}
        </div>
      </div>
    </div>
  )
}

export default GrowDetailPage
