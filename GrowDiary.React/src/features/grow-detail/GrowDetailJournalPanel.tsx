import type { FormEvent } from 'react'
import type { GrowDetail, GrowTaskDto, MeasurementDto, PhotoAssetDto, PhotoTag } from '../../types'
import { formatDate, formatDateTime, formatSeverityLabel } from '../../utils'
import { formatGrowHydroMedium, photoTags, type JournalFormState, type PhotoFormState, type TaskFormState } from './grow-detail-model'

type GrowDetailJournalPanelProps = {
  grow: GrowDetail
  openTasks: GrowTaskDto[]
  closedTasks: GrowTaskDto[]
  measurements: MeasurementDto[]
  photos: PhotoAssetDto[]
  selectedMeasurementId: number | null
  journalForm: JournalFormState
  taskForm: TaskFormState
  photoForm: PhotoFormState
  photoLoading: boolean
  saving: string | null
  onTaskStatusChange: (taskId: number, status: 'Open' | 'Done') => void
  onJournalFormChange: (patch: Partial<JournalFormState>) => void
  onTaskFormChange: (patch: Partial<TaskFormState>) => void
  onPhotoFormChange: (patch: Partial<PhotoFormState>) => void
  onMeasurementSelection: (measurementId: number | null) => void
  onJournalSubmit: (event: FormEvent<HTMLFormElement>) => void
  onTaskSubmit: (event: FormEvent<HTMLFormElement>) => void
  onPhotoSubmit: (event: FormEvent<HTMLFormElement>) => void
}

export function GrowDetailJournalPanel({
  grow,
  openTasks,
  closedTasks,
  measurements,
  photos,
  selectedMeasurementId,
  journalForm,
  taskForm,
  photoForm,
  photoLoading,
  saving,
  onTaskStatusChange,
  onJournalFormChange,
  onTaskFormChange,
  onPhotoFormChange,
  onMeasurementSelection,
  onJournalSubmit,
  onTaskSubmit,
  onPhotoSubmit,
}: GrowDetailJournalPanelProps) {
  return (
    <div className="side-panel">
      <div className="panel-card">
        <div className="panel-card-header">
          <span className="panel-card-title">Offene Tasks</span>
          <span className="panel-card-count">{openTasks.length}</span>
        </div>
        {openTasks.length === 0 ? (
          <div style={{ padding: '14px', fontSize: '12px', color: 'var(--faint)' }}>Keine offenen Tasks.</div>
        ) : (
          openTasks.map((task) => (
            <div key={task.id} className="task-item">
              <button
                type="button"
                className="task-check"
                disabled={saving === `task-status-${task.id}`}
                onClick={() => onTaskStatusChange(task.id, 'Done')}
              />
              <div style={{ flex: 1, minWidth: 0 }}>
                <div className="task-title">{task.title}</div>
                {task.dueAtUtc && <div className="task-sub">fällig {formatDate(task.dueAtUtc)}</div>}
              </div>
            </div>
          ))
        )}
        {closedTasks.length > 0 && (
          <div style={{ borderTop: '1px solid var(--border)', padding: '8px 14px' }}>
            {closedTasks.slice(0, 5).map((task) => (
              <div key={task.id} className="task-item" style={{ opacity: 0.5 }}>
                <button type="button" className="task-check done" disabled={saving === `task-status-${task.id}`} onClick={() => onTaskStatusChange(task.id, 'Open')} />
                <div className="task-title" style={{ textDecoration: 'line-through' }}>{task.title}</div>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="panel-card">
        <div className="panel-card-header"><span className="panel-card-title">Grow-Info</span></div>
        <div style={{ padding: '12px 14px', fontSize: 13, display: 'grid', gap: 8 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Start</span><span>{formatDate(grow.startDate)}</span></div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Medium</span><span>{grow.mediumType}</span></div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Hydro-Setup</span><span>{formatGrowHydroMedium(grow)}</span></div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Wasser</span><span>{grow.waterSource}</span></div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Licht</span><span>{grow.light ?? '—'}</span></div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Reservoir</span><span>{grow.reservoirSize ?? '—'}</span></div>
          <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Nährstoffe</span><span>{grow.nutrients ?? '—'}</span></div>
        </div>
      </div>

      <div className="panel-card">
        <div className="panel-card-header"><span className="panel-card-title">Journal-Eintrag</span></div>
        <form onSubmit={onJournalSubmit} style={{ padding: '12px 14px', display: 'grid', gap: 10 }}>
          <div className="field">
            <label>Titel</label>
            <input value={journalForm.title} onChange={(event) => onJournalFormChange({ title: event.target.value })} placeholder="Heute deutlich mehr Durst" />
          </div>
          <div className="field">
            <label>Typ</label>
            <select value={journalForm.entryType} onChange={(event) => onJournalFormChange({ entryType: event.target.value })}>
              <option>Observation</option><option>Action</option><option>Problem</option><option>Solution</option><option>Training</option><option>Feeding</option><option>ReservoirChange</option>
            </select>
          </div>
          <div className="field">
            <label>Eintrag</label>
            <textarea value={journalForm.body} onChange={(event) => onJournalFormChange({ body: event.target.value })} rows={3} placeholder="Was ist passiert?" />
          </div>
          <button className="btn btn-primary" disabled={saving === 'journal'}>{saving === 'journal' ? 'Speichert...' : 'Journal speichern'}</button>
        </form>
      </div>

      <div className="panel-card">
        <div className="panel-card-header"><span className="panel-card-title">Task anlegen</span></div>
        <form onSubmit={onTaskSubmit} style={{ padding: '12px 14px', display: 'grid', gap: 10 }}>
          <div className="field">
            <label>Titel</label>
            <input value={taskForm.title} onChange={(event) => onTaskFormChange({ title: event.target.value })} placeholder="z. B. EC nach Addback prüfen" />
          </div>
          <div className="field">
            <label>Prioritaet</label>
            <select value={taskForm.priority} onChange={(event) => onTaskFormChange({ priority: event.target.value })}>
              <option value="Low">{formatSeverityLabel('Low')}</option><option value="Normal">{formatSeverityLabel('Normal')}</option><option value="High">{formatSeverityLabel('High')}</option><option value="Critical">{formatSeverityLabel('Critical')}</option>
            </select>
          </div>
          <div className="field">
            <label>Faellig</label>
            <input type="datetime-local" value={taskForm.dueAtLocal} onChange={(event) => onTaskFormChange({ dueAtLocal: event.target.value })} />
          </div>
          <button className="btn btn-primary" disabled={saving === 'task'}>{saving === 'task' ? 'Speichert...' : 'Task speichern'}</button>
        </form>
      </div>

      <div className="panel-card">
        <div className="panel-card-header">
          <span className="panel-card-title">Fotos</span>
          <span className="panel-card-count">{photoLoading ? '...' : photos.length}</span>
        </div>
        <form onSubmit={onPhotoSubmit} style={{ padding: '12px 14px', display: 'grid', gap: 10 }}>
          <div className="field">
            <label>Messung</label>
            <select value={selectedMeasurementId ?? ''} onChange={(event) => onMeasurementSelection(event.target.value ? parseInt(event.target.value, 10) : null)} disabled={measurements.length === 0}>
              {measurements.length === 0 ? <option value="">Keine Messungen</option> : null}
              {measurements.map((measurement) => (
                <option key={measurement.id} value={measurement.id}>#{measurement.id} · {measurement.stage} · {formatDateTime(measurement.takenAt)}</option>
              ))}
            </select>
          </div>
          <div className="field">
            <label>Tag</label>
            <select value={photoForm.photoTag} onChange={(event) => onPhotoFormChange({ photoTag: event.target.value as PhotoTag })}>
              {photoTags.map((tag) => <option key={tag} value={tag}>{tag}</option>)}
            </select>
          </div>
          <div className="field">
            <label>Caption</label>
            <input value={photoForm.photoCaption} onChange={(event) => onPhotoFormChange({ photoCaption: event.target.value })} />
          </div>
          <div className="field">
            <label>Dateien</label>
            <input type="file" accept="image/png,image/jpeg,image/webp" multiple onChange={(event) => onPhotoFormChange({ files: Array.from(event.target.files ?? []) })} />
          </div>
          <button className="btn btn-primary" disabled={saving === 'photo' || measurements.length === 0}>{saving === 'photo' ? 'Lädt hoch...' : 'Fotos hochladen'}</button>
        </form>
        {photos.length > 0 && (
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, padding: '0 14px 14px' }}>
            {photos.map((photo) => (
              <div key={photo.id} style={{ borderRadius: 8, overflow: 'hidden', border: '1px solid var(--border)' }}>
                <img src={photo.relativePath} alt={photo.caption ?? `Foto ${photo.id}`} loading="lazy" style={{ width: '100%', aspectRatio: '4/3', objectFit: 'cover', display: 'block' }} />
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
