import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch } from '../../api'
import type { JournalEntryDto, MeasurementDto, PhotoAssetDto, PhotoTag } from '../../types'
import type { JournalFormState, PhotoFormState, TaskFormState } from './grow-detail-model'
import { buildJournalStream, streamTimeLabel } from './journal-stream'
import { V1Button, V1Field } from '../../components/v1'
import './journal-stream.css'

const photoTags: PhotoTag[] = ['Overview', 'Canopy', 'Leaf', 'Root', 'Training', 'Flower', 'Problem', 'Comparison', 'Other']

const taskPriorities: Array<{ value: string; label: string }> = [
  { value: 'Low', label: 'Niedrig' },
  { value: 'Normal', label: 'Normal' },
  { value: 'High', label: 'Hoch' },
  { value: 'Critical', label: 'Kritisch' },
]

const entryTypes: Array<{ value: string; label: string }> = [
  { value: 'Observation', label: 'Beobachtung' },
  { value: 'Note', label: 'Notiz' },
  { value: 'Action', label: 'Aktion' },
  { value: 'Problem', label: 'Problem' },
  { value: 'Solution', label: 'Lösung' },
  { value: 'Training', label: 'Training' },
  { value: 'Transplant', label: 'Umtopfen' },
  { value: 'Feeding', label: 'Fütterung' },
  { value: 'ReservoirChange', label: 'Wasserwechsel' },
]

/**
 * Journal & Fotos als ein Strom, wie im Entwurf: Zeitspalte links, getaggter
 * Eintrag rechts, Messfotos direkt beim Eintrag. „Nur Fotos" filtert den
 * Strom, „+ Eintrag" öffnet das Formular unter dem Panelkopf.
 */
export function JournalStreamSection({ growId, entries, measurements, journalForm, photoForm, taskForm, saving, selectedMeasurementId, onMeasurementSelection, onJournalFormChange, onPhotoFormChange, onTaskFormChange, onJournalSubmit, onPhotoSubmit, onTaskSubmit }: {
  growId: string
  entries: JournalEntryDto[]
  measurements: MeasurementDto[]
  journalForm: JournalFormState
  photoForm: PhotoFormState
  taskForm: TaskFormState
  saving: string | null
  selectedMeasurementId: number | null
  onMeasurementSelection: (measurementId: number | null) => void
  onJournalFormChange: (patch: Partial<JournalFormState>) => void
  onPhotoFormChange: (patch: Partial<PhotoFormState>) => void
  onTaskFormChange: (patch: Partial<TaskFormState>) => void
  onJournalSubmit: (event: FormEvent<HTMLFormElement>) => void
  onPhotoSubmit: (event: FormEvent<HTMLFormElement>) => void
  onTaskSubmit: (event: FormEvent<HTMLFormElement>) => void
}) {
  const [photos, setPhotos] = useState<PhotoAssetDto[]>([])
  const [photosOnly, setPhotosOnly] = useState(false)
  const [composerOpen, setComposerOpen] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const list = await apiFetch<PhotoAssetDto[]>(`/api/grows/${growId}/photos`, { signal: controller.signal })
        if (!controller.signal.aborted) setPhotos(list)
      } catch {
        if (!controller.signal.aborted) setPhotos([])
      }
    }
    void load()
    return () => controller.abort()
    // Auf die Identität des Arrays hören, nicht auf die Länge: nach einem
    // Foto-Upload lädt der Bundle neu (neues Array), die ANZAHL der Einträge
    // bleibt aber gleich — mit entries.length blieb der Strom stehen und das
    // frisch hochgeladene Foto erschien erst nach einem Seitenwechsel.
  }, [growId, entries])

  const stream = useMemo(() => buildJournalStream(entries, photos), [entries, photos])
  const visible = photosOnly ? stream.filter((item) => item.photos.length > 0) : stream

  return (
    <section className="ls-panel" data-audit="journal-stream">
      <div className="ls-panel-head">
        <span className="ls-label">Journal &amp; Fotos</span>
        <span className="ls-panel-meta">{stream.length} Einträge</span>
        <div className="co-row-end">
          <button type="button" className={`ls-btn is-small${photosOnly ? ' is-primary' : ''}`} onClick={() => setPhotosOnly((current) => !current)}>
            Nur Fotos
          </button>
          <button type="button" className="ls-btn is-small is-primary" data-audit="journal-add-entry" onClick={() => setComposerOpen((current) => !current)}>
            {composerOpen ? 'Schließen' : '+ Eintrag'}
          </button>
        </div>
      </div>

      {composerOpen && (
        <div className="js-composer">
          <form onSubmit={(event) => { onJournalSubmit(event); setComposerOpen(false) }} className="js-form" data-audit="journal-entry-form">
            <V1Field label="Art">
              <select value={journalForm.entryType} onChange={(event) => onJournalFormChange({ entryType: event.target.value })}>
                {entryTypes.map((type) => <option key={type.value} value={type.value}>{type.label}</option>)}
              </select>
            </V1Field>
            <V1Field label="Titel"><input value={journalForm.title} onChange={(event) => onJournalFormChange({ title: event.target.value })} placeholder="Was ist passiert?" /></V1Field>
            <V1Field label="Zeitpunkt"><input type="datetime-local" value={journalForm.occurredAtLocal} onChange={(event) => onJournalFormChange({ occurredAtLocal: event.target.value })} /></V1Field>
            <V1Field label="Text" wide><textarea rows={2} value={journalForm.body} onChange={(event) => onJournalFormChange({ body: event.target.value })} /></V1Field>
            <V1Button type="submit" variant="primary" disabled={saving === 'journal'}>{saving === 'journal' ? 'Speichert…' : 'Eintrag speichern'}</V1Button>
          </form>

          <form onSubmit={onPhotoSubmit} className="js-form" data-audit="journal-photo-form">
            <V1Field label="Foto zu Messung" hint={measurements.length === 0 ? 'Fotos hängen an Messungen — erst eine Messung erfassen.' : null}>
              <select value={selectedMeasurementId ?? ''} onChange={(event) => onMeasurementSelection(event.target.value ? parseInt(event.target.value, 10) : null)} disabled={measurements.length === 0}>
                {measurements.length === 0 ? <option value="">Keine Messungen</option> : null}
                {measurements.map((measurement) => (
                  <option key={measurement.id} value={measurement.id}>#{measurement.id} · {streamTimeLabel(measurement.takenAt).day} {streamTimeLabel(measurement.takenAt).clock}</option>
                ))}
              </select>
            </V1Field>
            <V1Field label="Art">
              <select value={photoForm.photoTag} onChange={(event) => onPhotoFormChange({ photoTag: event.target.value as PhotoTag })}>
                {photoTags.map((tag) => <option key={tag} value={tag}>{tag}</option>)}
              </select>
            </V1Field>
            <V1Field label="Bildunterschrift"><input value={photoForm.photoCaption} onChange={(event) => onPhotoFormChange({ photoCaption: event.target.value })} /></V1Field>
            <V1Field label="Dateien">
              <input type="file" accept="image/png,image/jpeg,image/webp" multiple onChange={(event) => onPhotoFormChange({ files: Array.from(event.target.files ?? []) })} />
            </V1Field>
            <V1Button type="submit" disabled={saving === 'photo' || measurements.length === 0}>{saving === 'photo' ? 'Lädt hoch…' : 'Fotos hochladen'}</V1Button>
          </form>

          <form onSubmit={onTaskSubmit} className="js-form" data-audit="journal-task-form">
            <V1Field label="Aufgabe" hint={'Erscheint unter „Aufgaben“ bei den Terminen.'}>
              <input value={taskForm.title} onChange={(event) => onTaskFormChange({ title: event.target.value })} placeholder="z. B. pH-Sonde kalibrieren" />
            </V1Field>
            <V1Field label="Fällig am"><input type="datetime-local" value={taskForm.dueAtLocal} onChange={(event) => onTaskFormChange({ dueAtLocal: event.target.value })} /></V1Field>
            {/* Die Priorität entscheidet, wie weit oben die Aufgabe unter
                „Aufgaben" steht — ohne sie landet alles auf „Normal". */}
            <V1Field label="Priorität">
              <select value={taskForm.priority} onChange={(event) => onTaskFormChange({ priority: event.target.value })}>
                {taskPriorities.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
              </select>
            </V1Field>
            <V1Button type="submit" disabled={saving === 'task'}>{saving === 'task' ? 'Speichert…' : 'Aufgabe anlegen'}</V1Button>
          </form>
        </div>
      )}

      {visible.length === 0 ? (
        <div className="ls-panel-body"><p>{photosOnly ? 'Noch keine Fotos in diesem Grow.' : 'Noch keine Journal-Einträge.'}</p></div>
      ) : (
        <div className="js-stream">
          {visible.map((item) => {
            const time = streamTimeLabel(item.at)
            return (
              <div key={item.key} className="js-row">
                <div className="js-when">{time.day}<br />{time.clock}</div>
                <div className="js-content">
                  <div className="js-headline">
                    <span className={`js-tag is-${item.tone}`}>{item.tag}</span>
                    <strong>{item.title}</strong>
                  </div>
                  {item.body && <p>{item.body}</p>}
                  {item.photos.length > 0 && (
                    <div className="js-photos">
                      {item.photos.map((photo) => (
                        <img key={photo.id} src={photo.relativePath} alt={photo.caption ?? `Foto ${photo.id}`} loading="lazy" />
                      ))}
                    </div>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </section>
  )
}
