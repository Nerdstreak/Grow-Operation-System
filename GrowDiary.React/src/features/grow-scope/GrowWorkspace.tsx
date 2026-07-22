import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { formatDateTime } from '../../utils'
import { GrowDetailDiagnosisSection } from '../grow-detail/GrowDetailDiagnosisSection'
import { GrowDetailJournalPanel } from '../grow-detail/GrowDetailJournalPanel'
import { GrowDetailMeasurementsSection } from '../grow-detail/GrowDetailMeasurementsSection'
import { GrowDetailSopSection } from '../grow-detail/GrowDetailSopSection'
import { SopCatalog } from '../grow-detail/SopCatalog'
import { useGrowDetailBundle } from '../grow-detail/useGrowDetailBundle'
import { useGrowDetailMutations } from '../grow-detail/useGrowDetailMutations'
import { useGrowDetailResources } from '../grow-detail/useGrowDetailResources'
import type { GrowDetailSection } from '../grow-detail/grow-detail-model'

// The full grow-detail wiring (all the hooks that used to power the grow's tabs),
// rendering exactly ONE section. Each top-level grow-scoped page hosts one of these,
// so a section that used to be a tab inside a grow is now its own single-purpose page.
export function GrowWorkspace({ growId, section }: { growId: string; section: GrowDetailSection }) {
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState<string | null>(null)

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

  const openTasks = useMemo(() => bundle.tasks.filter((task) => task.status === 'Open'), [bundle.tasks])
  const activeSopIds = useMemo(() => new Set(sopInstances.filter((sop) => sop.status === 'Active').map((sop) => sop.sopId)), [sopInstances])
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
    if (!growId) return
    const controller = new AbortController()
    const handle = window.setTimeout(() => {
      void loadBundle(controller.signal)
      if (section === 'diagnosis') {
        void loadDeviations(controller.signal)
        void loadTreatmentRecommendations(controller.signal)
        void loadRiskEvents(controller.signal)
      }
      if (section === 'sops') void loadSopInstances(controller.signal)
    }, 0)
    return () => {
      window.clearTimeout(handle)
      controller.abort()
    }
  }, [growId, section, loadBundle, loadDeviations, loadTreatmentRecommendations, loadRiskEvents, loadSopInstances])

  if (loading) {
    return <div className="empty-hint">Lade Daten…</div>
  }

  if (!bundle.grow) {
    return <div className="empty-hint" style={{ color: 'var(--red)' }}>{error ?? 'Grow nicht gefunden.'}</div>
  }

  const grow = bundle.grow

  return (
    <>
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

      {section === 'measurements' && (
        <GrowDetailMeasurementsSection
          activeSection="measurements"
          measurements={bundle.measurements}
          selectedMeasurementId={selectedMeasurementId}
          measurementForm={measurementForm}
          saving={saving}
          onSelectMeasurement={(measurementId) => void handleMeasurementSelection(measurementId)}
          onMeasurementFormChange={(patch) => setMeasurementForm((current) => ({ ...current, ...patch }))}
          onSubmit={handleMeasurementSubmit}
        />
      )}

      {section === 'diagnosis' && (
        <GrowDetailDiagnosisSection
          activeSection="diagnosis"
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
      )}

      {section === 'sops' && (
        <SopCatalog growId={growId} activeSopIds={activeSopIds} onStarted={(message) => { setNotice(message); void loadSopInstances() }} />
      )}

      {section === 'sops' && (
        <GrowDetailSopSection
          activeSection="sops"
          sopInstances={sopInstances}
          sopStepsByInstanceId={sopStepsByInstanceId}
          sopStepNotesById={sopStepNotesById}
          sopInstanceError={sopInstanceError}
          saving={saving}
          onNoteChange={(stepId, notes) => setSopStepNotesById((current) => ({ ...current, [stepId]: notes }))}
          onUpdateStep={(step, status) => void updateSopStep(step, status)}
        />
      )}

      {section === 'journal' && (
        <div className="detail-layout">
          <div>
            <div className="section-label">Journal</div>
            <div className="card" style={{ marginBottom: 14 }}>
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
          </div>

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
        </div>
      )}
    </>
  )
}
