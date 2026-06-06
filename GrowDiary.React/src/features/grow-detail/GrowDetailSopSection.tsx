import type { SopInstanceDto, SopStepInstanceDto, SopStepInstanceStatus } from '../../types'
import { formatDateTime } from '../../utils'
import type { GrowDetailSection } from './grow-detail-model'

type GrowDetailSopSectionProps = {
  activeSection: GrowDetailSection
  sopInstances: SopInstanceDto[]
  sopStepsByInstanceId: Record<number, SopStepInstanceDto[]>
  sopStepNotesById: Record<number, string>
  sopInstanceError: string | null
  saving: string | null
  onNoteChange: (stepId: number, notes: string) => void
  onUpdateStep: (step: SopStepInstanceDto, status: SopStepInstanceStatus) => void
}

export function GrowDetailSopSection({
  activeSection,
  sopInstances,
  sopStepsByInstanceId,
  sopStepNotesById,
  sopInstanceError,
  saving,
  onNoteChange,
  onUpdateStep,
}: GrowDetailSopSectionProps) {
  const isVisible = activeSection === 'sops'

  return (
    <>
      <div className="section-label" style={{ display: isVisible ? undefined : 'none' }}>SOPs</div>
      <div className="card" style={{ marginBottom: 14, display: isVisible ? undefined : 'none' }}>
        <div className="card-header">
          <span className="card-title">SOP-Instanzen</span>
          <span className="text-muted" style={{ fontSize: 13 }}>{sopInstances.length}</span>
        </div>
        {sopInstanceError ? (
          <div className="empty-hint" style={{ color: 'var(--red)' }}>{sopInstanceError}</div>
        ) : sopInstances.length === 0 ? (
          <div className="empty-hint">Keine SOP-Instanz.</div>
        ) : (
          <div style={{ display: 'grid' }}>
            {sopInstances.map((instance) => (
              <div key={instance.id} style={{ display: 'grid', gap: 10, padding: '12px 16px', borderTop: '1px solid var(--border)' }}>
                <div style={{ display: 'grid', gridTemplateColumns: 'minmax(180px, 1fr) 120px 120px 1fr', gap: 10, alignItems: 'center' }}>
                  <div>
                    <div className="tl-title">{instance.sopName}</div>
                    <div className="tl-sub">{instance.sopId}</div>
                  </div>
                  <span className="badge badge-neutral">{instance.sopType}</span>
                  <span className={`badge ${instance.status === 'Completed' ? 'badge-ok' : 'badge-neutral'}`}>{instance.status}</span>
                  <div className="tl-sub">
                    {instance.stepCount} Steps &ndash; Start {formatDateTime(instance.startedAtUtc)}
                    {instance.isRecurring && <span className="badge badge-neutral" style={{ marginLeft: 8 }}>Recurring</span>}
                    {instance.dueAtUtc && <span style={{ marginLeft: 8 }}>Fällig: {formatDateTime(instance.dueAtUtc)}</span>}
                    {instance.nextStepDueAtUtc && instance.status === 'Active' && (
                      <span style={{ marginLeft: 8 }}>Nächster Step: {formatDateTime(instance.nextStepDueAtUtc)}</span>
                    )}
                  </div>
                </div>
                <div style={{ display: 'grid', gap: 8 }}>
                  {(sopStepsByInstanceId[instance.id] ?? []).map((step) => (
                    <div key={step.id} style={{ display: 'grid', gridTemplateColumns: '48px minmax(180px, 1fr) 120px 120px minmax(180px, 1fr) 240px', gap: 8, alignItems: 'start' }}>
                      <span className="tl-sub">#{step.order}</span>
                      <div>
                        <div className="tl-title">{step.title}</div>
                        <div className="tl-sub">{step.stepType}</div>
                        {step.dueAtUtc && (
                          <div className="tl-sub">Fällig: {formatDateTime(step.dueAtUtc)}</div>
                        )}
                        {step.availableAtUtc && !step.dueAtUtc && (
                          <div className="tl-sub">Verfügbar ab: {formatDateTime(step.availableAtUtc)}</div>
                        )}
                        {step.reminderTaskId && (
                          <div className="tl-sub" style={{ opacity: 0.6 }}>Task #{step.reminderTaskId}</div>
                        )}
                      </div>
                      <span className="badge badge-neutral">{step.status}</span>
                      <span className="tl-sub">{step.subSopId ? `SubSOP: ${step.subSopId}` : ''}</span>
                      <input
                        value={sopStepNotesById[step.id] ?? ''}
                        onChange={(event) => onNoteChange(step.id, event.target.value)}
                        placeholder="Notiz"
                        disabled={instance.status !== 'Active'}
                      />
                      {instance.status === 'Active' ? (
                        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                          {step.availableAtUtc && new Date(step.availableAtUtc) > new Date() && (
                            <span className="tl-sub" style={{ alignSelf: 'center', width: '100%' }}>
                              Verfügbar ab {formatDateTime(step.availableAtUtc)}
                            </span>
                          )}
                          <button type="button" className="btn btn-secondary" disabled={saving === `sop-step-${step.id}-InProgress`} onClick={() => onUpdateStep(step, 'InProgress')}>
                            Starten
                          </button>
                          <button type="button" className="btn" disabled={saving === `sop-step-${step.id}-Done`} onClick={() => onUpdateStep(step, 'Done')}>
                            Erledigt
                          </button>
                          <button type="button" className="btn btn-secondary" disabled={saving === `sop-step-${step.id}-Skipped`} onClick={() => onUpdateStep(step, 'Skipped')}>
                            Überspringen
                          </button>
                        </div>
                      ) : (
                        <span className="tl-sub">Keine Aktionen</span>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </>
  )
}
