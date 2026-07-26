import type { FormEvent } from 'react'
import type { GrowDetail, GrowTaskDto, MeasurementDto, PhotoAssetDto, PhotoTag } from '../../types'
import { formatDate, formatDateTime, formatSeverityLabel } from '../../utils'
import { formatGrowHydroMedium, photoTags, type JournalFormState, type PhotoFormState, type TaskFormState } from './grow-detail-model'
import { V1Badge, V1Button, V1Empty, V1Field, V1Section } from '../../components/v1'
import './grow-side.css'

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
    <div className="grow-side">
      <V1Section title="Offene Aufgaben" action={<V1Badge tone={openTasks.length > 0 ? 'warn' : 'ok'}>{openTasks.length}</V1Badge>}>
        {openTasks.length === 0 ? (
          <V1Empty title="Nichts offen" />
        ) : (
          <ul className="grow-tasks">
            {openTasks.map((task) => (
              <li key={task.id}>
                <button
                  type="button"
                  className="grow-task-check"
                  aria-label={`${task.title} erledigt`}
                  disabled={saving === `task-status-${task.id}`}
                  onClick={() => onTaskStatusChange(task.id, 'Done')}
                />
                <div>
                  <strong>{task.title}</strong>
                  {task.dueAtUtc && <span>fällig {formatDate(task.dueAtUtc)}</span>}
                </div>
              </li>
            ))}
          </ul>
        )}

        {closedTasks.length > 0 && (
          <ul className="grow-tasks done">
            {closedTasks.slice(0, 5).map((task) => (
              <li key={task.id}>
                <button
                  type="button"
                  className="grow-task-check done"
                  aria-label={`${task.title} wieder öffnen`}
                  disabled={saving === `task-status-${task.id}`}
                  onClick={() => onTaskStatusChange(task.id, 'Open')}
                />
                <div><strong>{task.title}</strong></div>
              </li>
            ))}
          </ul>
        )}
      </V1Section>

      <V1Section title="Grow-Info">
        <div className="v1-list">
          {([
            ['Start', formatDate(grow.startDate)],
            ['Medium', grow.mediumType],
            ['Hydro-Setup', formatGrowHydroMedium(grow)],
            ['Wasser', grow.waterSource],
            ['Licht', grow.light ?? '—'],
            ['Reservoir', grow.reservoirSize ?? '—'],
            ['Nährstoffe', grow.nutrients ?? '—'],
          ] as Array<[string, string]>).map(([label, value]) => (
            <div key={label} className="v1-list-row">
              <span>{label}</span>
              <strong>{value}</strong>
            </div>
          ))}
        </div>
      </V1Section>

      <V1Section title="Journal-Eintrag">
        <form className="grow-side-form" onSubmit={onJournalSubmit}>
          <V1Field label="Titel">
            <input value={journalForm.title} onChange={(event) => onJournalFormChange({ title: event.target.value })} placeholder="Heute deutlich mehr Durst" />
          </V1Field>
          <V1Field label="Typ">
            <select value={journalForm.entryType} onChange={(event) => onJournalFormChange({ entryType: event.target.value })}>
              <option>Observation</option><option>Action</option><option>Problem</option><option>Solution</option><option>Training</option><option>Feeding</option><option>ReservoirChange</option>
            </select>
          </V1Field>
          <V1Field label="Eintrag">
            <textarea value={journalForm.body} onChange={(event) => onJournalFormChange({ body: event.target.value })} rows={3} placeholder="Was ist passiert?" />
          </V1Field>
          <V1Button type="submit" variant="primary" disabled={saving === 'journal'}>{saving === 'journal' ? 'Speichert...' : 'Journal speichern'}</V1Button>
        </form>
      </V1Section>

      <V1Section title="Aufgabe anlegen">
        <form className="grow-side-form" onSubmit={onTaskSubmit}>
          <V1Field label="Titel">
            <input value={taskForm.title} onChange={(event) => onTaskFormChange({ title: event.target.value })} placeholder="z. B. EC nach Addback prüfen" />
          </V1Field>
          <V1Field label="Priorität">
            <select value={taskForm.priority} onChange={(event) => onTaskFormChange({ priority: event.target.value })}>
              <option value="Low">{formatSeverityLabel('Low')}</option><option value="Normal">{formatSeverityLabel('Normal')}</option><option value="High">{formatSeverityLabel('High')}</option><option value="Critical">{formatSeverityLabel('Critical')}</option>
            </select>
          </V1Field>
          <V1Field label="Fällig">
            <input type="datetime-local" value={taskForm.dueAtLocal} onChange={(event) => onTaskFormChange({ dueAtLocal: event.target.value })} />
          </V1Field>
          <V1Button type="submit" variant="primary" disabled={saving === 'task'}>{saving === 'task' ? 'Speichert...' : 'Aufgabe speichern'}</V1Button>
        </form>
      </V1Section>

      <V1Section title="Fotos" action={<V1Badge tone="neutral">{photoLoading ? '…' : photos.length}</V1Badge>}>
        <form className="grow-side-form" onSubmit={onPhotoSubmit}>
          <V1Field label="Messung" hint={measurements.length === 0 ? 'Fotos hängen an einer Messung — leg zuerst eine an.' : undefined}>
            <select value={selectedMeasurementId ?? ''} onChange={(event) => onMeasurementSelection(event.target.value ? parseInt(event.target.value, 10) : null)} disabled={measurements.length === 0}>
              {measurements.length === 0 ? <option value="">Keine Messungen</option> : null}
              {measurements.map((measurement) => (
                <option key={measurement.id} value={measurement.id}>#{measurement.id} · {measurement.stage} · {formatDateTime(measurement.takenAt)}</option>
              ))}
            </select>
          </V1Field>
          <V1Field label="Art">
            <select value={photoForm.photoTag} onChange={(event) => onPhotoFormChange({ photoTag: event.target.value as PhotoTag })}>
              {photoTags.map((tag) => <option key={tag} value={tag}>{tag}</option>)}
            </select>
          </V1Field>
          <V1Field label="Bildunterschrift">
            <input value={photoForm.photoCaption} onChange={(event) => onPhotoFormChange({ photoCaption: event.target.value })} />
          </V1Field>
          <V1Field label="Dateien">
            <input type="file" accept="image/png,image/jpeg,image/webp" multiple onChange={(event) => onPhotoFormChange({ files: Array.from(event.target.files ?? []) })} />
          </V1Field>
          <V1Button type="submit" variant="primary" disabled={saving === 'photo' || measurements.length === 0}>{saving === 'photo' ? 'Lädt hoch...' : 'Fotos hochladen'}</V1Button>
        </form>

        {photos.length > 0 && (
          <div className="grow-photos">
            {photos.map((photo) => (
              <img key={photo.id} src={photo.relativePath} alt={photo.caption ?? `Foto ${photo.id}`} loading="lazy" />
            ))}
          </div>
        )}
      </V1Section>
    </div>
  )
}
