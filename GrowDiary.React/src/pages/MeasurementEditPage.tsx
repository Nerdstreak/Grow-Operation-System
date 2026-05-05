import type { FormEvent } from 'react'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowDetail, GrowStage, MeasurementDto, MeasurementUpsertPayload, PhotoAssetDto, PhotoTag, ValueOrigin } from '../types'
import { formatDateTime, toLocalInputValue } from '../utils'

interface MeasurementEditState {
  takenAtLocal: string
  stage: GrowStage
  source: ValueOrigin
  notes: string
  solutionChange: boolean
  airTemperatureC: string
  humidityPercent: string
  heightCm: string
  waterAmountMl: string
  runoffAmountMl: string
  irrigationPh: string
  irrigationEc: string
  drainPh: string
  drainEc: string
  reservoirPh: string
  reservoirEc: string
  reservoirWaterTempC: string
  reservoirLevelCm: string
  reservoirLevelLiters: string
  dissolvedOxygenMgL: string
  orpMv: string
  topOffLiters: string
  addbackEc: string
  ppfdMol: string
  co2Ppm: string
}

type MeasurementNumericFieldKey = Exclude<keyof MeasurementEditState, 'takenAtLocal' | 'stage' | 'source' | 'notes' | 'solutionChange'>

interface PhotoFormState {
  photoCaption: string
  photoTag: PhotoTag
  useAsReferenceShot: boolean
  source: ValueOrigin
  files: File[]
}

const photoTags: PhotoTag[] = ['Overview', 'Canopy', 'Leaf', 'Root', 'Training', 'Flower', 'Problem', 'Comparison', 'Other']
const stageOptions: GrowStage[] = ['Seedling', 'Clone', 'Veg', 'Transition', 'Flower', 'Finish', 'Dry', 'Cure']
const sourceOptions: ValueOrigin[] = ['Manual', 'HomeAssistant', 'Imported', 'Derived']

const fieldSections: Array<{ title: string, fields: Array<{ key: MeasurementNumericFieldKey, label: string, unit: string | null }> }> = [
  {
    title: 'Klima',
    fields: [
      { key: 'airTemperatureC', label: 'Lufttemp', unit: 'Â°C' },
      { key: 'humidityPercent', label: 'Luftfeuchte', unit: '%' },
      { key: 'co2Ppm', label: 'CO2', unit: 'ppm' },
      { key: 'ppfdMol', label: 'PPFD', unit: 'umol/m2/s' },
      { key: 'heightCm', label: 'Hoehe', unit: 'cm' },
    ],
  },
  {
    title: 'Irrigation',
    fields: [
      { key: 'waterAmountMl', label: 'Giessmenge', unit: 'ml' },
      { key: 'runoffAmountMl', label: 'Runoff', unit: 'ml' },
      { key: 'irrigationPh', label: 'Giess-pH', unit: 'pH' },
      { key: 'irrigationEc', label: 'Giess-EC', unit: 'mS/cm' },
      { key: 'drainPh', label: 'Drain-pH', unit: 'pH' },
      { key: 'drainEc', label: 'Drain-EC', unit: 'mS/cm' },
    ],
  },
  {
    title: 'Reservoir',
    fields: [
      { key: 'reservoirPh', label: 'Reservoir-pH', unit: 'pH' },
      { key: 'reservoirEc', label: 'Reservoir-EC', unit: 'mS/cm' },
      { key: 'reservoirWaterTempC', label: 'Wassertemp', unit: 'Â°C' },
      { key: 'reservoirLevelCm', label: 'Level', unit: 'cm' },
      { key: 'reservoirLevelLiters', label: 'Level', unit: 'L' },
      { key: 'topOffLiters', label: 'Top-Off', unit: 'L' },
      { key: 'addbackEc', label: 'Addback-EC', unit: 'mS/cm' },
      { key: 'dissolvedOxygenMgL', label: 'DO', unit: 'mg/L' },
      { key: 'orpMv', label: 'ORP', unit: 'mV' },
    ],
  },
]

function MeasurementEditPage() {
  const { measurementId } = useParams()
  const navigate = useNavigate()
  const [measurement, setMeasurement] = useState<MeasurementDto | null>(null)
  const [grow, setGrow] = useState<GrowDetail | null>(null)
  const [photos, setPhotos] = useState<PhotoAssetDto[]>([])
  const [draft, setDraft] = useState<MeasurementEditState | null>(null)
  const [photoForm, setPhotoForm] = useState<PhotoFormState>({ photoCaption: '', photoTag: 'Overview', useAsReferenceShot: false, source: 'Manual', files: [] })
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!measurementId) return
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      try {
        const nextMeasurement = await apiFetch<MeasurementDto>(`/api/measurements/${measurementId}`, { signal: controller.signal })
        const [nextGrow, nextPhotos] = await Promise.all([
          apiFetch<GrowDetail>(`/api/grows/${nextMeasurement.growId}`, { signal: controller.signal }),
          apiFetch<PhotoAssetDto[]>(`/api/measurements/${measurementId}/photos`, { signal: controller.signal }),
        ])

        setMeasurement(nextMeasurement)
        setGrow(nextGrow)
        setPhotos(nextPhotos)
        setDraft(createDraft(nextMeasurement))
        setError(null)
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Messung konnte nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [measurementId])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!measurementId || !draft) return

    setSaving(true)
    try {
      await apiFetch<MeasurementDto>(`/api/measurements/${measurementId}`, {
        method: 'PUT',
        body: JSON.stringify(toPayload(draft)),
      })
      navigate(grow ? `/grows/${grow.id}` : '/')
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Messung konnte nicht gespeichert werden.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete() {
    if (!measurementId || !window.confirm('Messung wirklich loeschen?')) return

    setDeleting(true)
    try {
      await apiFetch(`/api/measurements/${measurementId}`, { method: 'DELETE' })
      navigate(grow ? `/grows/${grow.id}` : '/')
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Messung konnte nicht geloescht werden.')
    } finally {
      setDeleting(false)
    }
  }

  async function handlePhotoSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!measurementId || photoForm.files.length === 0) {
      setError('Bitte mindestens ein Foto auswaehlen.')
      return
    }

    setUploading(true)
    try {
      const formData = new FormData()
      formData.append('photoCaption', photoForm.photoCaption)
      formData.append('photoTag', photoForm.photoTag)
      formData.append('useAsReferenceShot', String(photoForm.useAsReferenceShot))
      formData.append('source', photoForm.source)
      for (const file of photoForm.files) {
        formData.append('photos', file)
      }

      await apiFetch(`/api/measurements/${measurementId}/photos`, { method: 'POST', body: formData })
      setPhotos(await apiFetch<PhotoAssetDto[]>(`/api/measurements/${measurementId}/photos`))
      setPhotoForm({ photoCaption: '', photoTag: 'Overview', useAsReferenceShot: false, source: 'Manual', files: [] })
      setError(null)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Fotos konnten nicht gespeichert werden.')
    } finally {
      setUploading(false)
    }
  }

  return (
    <>
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to={grow ? `/grows/${grow.id}` : '/'}>â† Zurueck</Link>
          <span className="topbar-title">{grow?.name ?? 'Messung bearbeiten'}</span>
        </div>
        <div className="topbar-right">
          <button type="button" className="btn btn-danger" disabled={deleting} onClick={() => void handleDelete()}>{deleting ? 'Loeschtâ€¦' : 'Messung loeschen'}</button>
        </div>
      </div>

      <div className="page-scroll">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)' }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}

        {loading || !draft || !measurement ? (
          <div className="empty-hint">Lade Messung...</div>
        ) : (
          <div className="detail-layout">
            <div>
              <form onSubmit={handleSubmit} style={{ display: 'grid', gap: 14 }}>
                <div className="card">
                  <div className="card-header">
                    <span className="card-title">Basisdaten</span>
                    <span className="text-muted" style={{ fontSize: 12 }}>#{measurement.id} Â· {formatDateTime(measurement.takenAt)}</span>
                  </div>
                  <div style={{ padding: '16px 18px', display: 'grid', gap: 14 }}>
                    <div className="meas-fields">
                      <label className="meas-field">
                        <span>Zeitpunkt</span>
                        <input className="meas-input" type="datetime-local" value={draft.takenAtLocal} onChange={(event) => setDraft((current) => current ? { ...current, takenAtLocal: event.target.value } : current)} />
                      </label>
                      <label className="meas-field">
                        <span>Phase</span>
                        <select className="meas-input" value={draft.stage} onChange={(event) => setDraft((current) => current ? { ...current, stage: event.target.value as GrowStage } : current)}>
                          {stageOptions.map((stage) => <option key={stage} value={stage}>{stage}</option>)}
                        </select>
                      </label>
                      <label className="meas-field">
                        <span>Quelle</span>
                        <select className="meas-input" value={draft.source} onChange={(event) => setDraft((current) => current ? { ...current, source: event.target.value as ValueOrigin } : current)}>
                          {sourceOptions.map((source) => <option key={source} value={source}>{source}</option>)}
                        </select>
                      </label>
                    </div>
                    <label style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 14 }}>
                      <input type="checkbox" checked={draft.solutionChange} onChange={(event) => setDraft((current) => current ? { ...current, solutionChange: event.target.checked } : current)} />
                      <span>Loesungswechsel dokumentiert</span>
                    </label>
                    <label className="field">
                      <span>Notiz</span>
                      <textarea rows={3} value={draft.notes} onChange={(event) => setDraft((current) => current ? { ...current, notes: event.target.value } : current)} />
                    </label>
                  </div>
                </div>

                {fieldSections.map((section) => (
                  <div key={section.title} className="card">
                    <div className="card-header">
                      <span className="card-title">{section.title}</span>
                    </div>
                    <div className="meas-fields" style={{ padding: '16px 18px' }}>
                      {section.fields.map((field) => (
                        <label key={field.key} className="meas-field">
                          <span>{field.label}</span>
                          <div className="meas-field-inner">
                            <input className="meas-input" value={draft[field.key]} onChange={(event) => setDraft((current) => current ? { ...current, [field.key]: event.target.value } : current)} />
                            {field.unit && <span className="meas-unit">{field.unit}</span>}
                          </div>
                        </label>
                      ))}
                    </div>
                  </div>
                ))}

                <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
                  <Link className="btn" to={grow ? `/grows/${grow.id}` : '/'}>Abbrechen</Link>
                  <button className="btn btn-primary" disabled={saving}>{saving ? 'Speichertâ€¦' : 'Aenderungen speichern'}</button>
                </div>
              </form>
            </div>

            <div className="side-panel">
              <div className="panel-card">
                <div className="panel-card-header">
                  <span className="panel-card-title">Vorhandene Fotos</span>
                  <span className="panel-card-count">{photos.length}</span>
                </div>
                {photos.length === 0 ? (
                  <div className="empty-hint" style={{ padding: 14 }}>Noch keine Fotos an dieser Messung.</div>
                ) : (
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, padding: 14 }}>
                    {photos.map((photo) => (
                      <div key={photo.id} style={{ border: '1px solid var(--border)', borderRadius: 8, overflow: 'hidden' }}>
                        <img src={photo.relativePath} alt={photo.caption ?? `Foto ${photo.id}`} loading="lazy" style={{ width: '100%', aspectRatio: '4 / 3', objectFit: 'cover' }} />
                      </div>
                    ))}
                  </div>
                )}
              </div>

              <div className="panel-card">
                <div className="panel-card-header">
                  <span className="panel-card-title">Fotos ergaenzen</span>
                </div>
                <form onSubmit={handlePhotoSubmit} style={{ padding: '12px 14px', display: 'grid', gap: 10 }}>
                  <label className="field">
                    <span>Tag</span>
                    <select value={photoForm.photoTag} onChange={(event) => setPhotoForm((current) => ({ ...current, photoTag: event.target.value as PhotoTag }))}>
                      {photoTags.map((tag) => <option key={tag} value={tag}>{tag}</option>)}
                    </select>
                  </label>
                  <label className="field">
                    <span>Caption</span>
                    <input value={photoForm.photoCaption} onChange={(event) => setPhotoForm((current) => ({ ...current, photoCaption: event.target.value }))} />
                  </label>
                  <label className="field">
                    <span>Dateien</span>
                    <input type="file" accept="image/png,image/jpeg,image/webp" multiple onChange={(event) => setPhotoForm((current) => ({ ...current, files: Array.from(event.target.files ?? []) }))} />
                  </label>
                  <label style={{ display: 'flex', alignItems: 'center', gap: 10, fontSize: 14 }}>
                    <input type="checkbox" checked={photoForm.useAsReferenceShot} onChange={(event) => setPhotoForm((current) => ({ ...current, useAsReferenceShot: event.target.checked }))} />
                    <span>Als Referenzshot markieren</span>
                  </label>
                  <button className="btn btn-primary" disabled={uploading}>{uploading ? 'Laedt hochâ€¦' : 'Fotos hochladen'}</button>
                </form>
              </div>
            </div>
          </div>
        )}
      </div>
    </>
  )
}

function createDraft(measurement: MeasurementDto): MeasurementEditState {
  return {
    takenAtLocal: toLocalInputValue(new Date(measurement.takenAt)),
    stage: measurement.stage,
    source: measurement.source,
    notes: measurement.notes ?? '',
    solutionChange: measurement.solutionChange,
    airTemperatureC: formatDraftNumber(measurement.airTemperatureC),
    humidityPercent: formatDraftNumber(measurement.humidityPercent),
    heightCm: formatDraftNumber(measurement.heightCm),
    waterAmountMl: formatDraftNumber(measurement.waterAmountMl),
    runoffAmountMl: formatDraftNumber(measurement.runoffAmountMl),
    irrigationPh: formatDraftNumber(measurement.irrigationPh),
    irrigationEc: formatDraftNumber(measurement.irrigationEc),
    drainPh: formatDraftNumber(measurement.drainPh),
    drainEc: formatDraftNumber(measurement.drainEc),
    reservoirPh: formatDraftNumber(measurement.reservoirPh),
    reservoirEc: formatDraftNumber(measurement.reservoirEc),
    reservoirWaterTempC: formatDraftNumber(measurement.reservoirWaterTempC),
    reservoirLevelCm: formatDraftNumber(measurement.reservoirLevelCm),
    reservoirLevelLiters: formatDraftNumber(measurement.reservoirLevelLiters),
    dissolvedOxygenMgL: formatDraftNumber(measurement.dissolvedOxygenMgL),
    orpMv: formatDraftNumber(measurement.orpMv),
    topOffLiters: formatDraftNumber(measurement.topOffLiters),
    addbackEc: formatDraftNumber(measurement.addbackEc),
    ppfdMol: formatDraftNumber(measurement.ppfdMol),
    co2Ppm: formatDraftNumber(measurement.co2Ppm),
  }
}

function toPayload(draft: MeasurementEditState): MeasurementUpsertPayload {
  return {
    takenAtLocal: draft.takenAtLocal,
    stage: draft.stage,
    source: draft.source,
    notes: trimToNull(draft.notes),
    airTemperatureC: parseNullableNumber(draft.airTemperatureC),
    humidityPercent: parseNullableNumber(draft.humidityPercent),
    heightCm: parseNullableNumber(draft.heightCm),
    waterAmountMl: parseNullableNumber(draft.waterAmountMl),
    runoffAmountMl: parseNullableNumber(draft.runoffAmountMl),
    irrigationPh: parseNullableNumber(draft.irrigationPh),
    irrigationEc: parseNullableNumber(draft.irrigationEc),
    drainPh: parseNullableNumber(draft.drainPh),
    drainEc: parseNullableNumber(draft.drainEc),
    reservoirPh: parseNullableNumber(draft.reservoirPh),
    reservoirEc: parseNullableNumber(draft.reservoirEc),
    reservoirWaterTempC: parseNullableNumber(draft.reservoirWaterTempC),
    reservoirLevelCm: parseNullableNumber(draft.reservoirLevelCm),
    reservoirLevelLiters: parseNullableNumber(draft.reservoirLevelLiters),
    dissolvedOxygenMgL: parseNullableNumber(draft.dissolvedOxygenMgL),
    orpMv: parseNullableNumber(draft.orpMv),
    topOffLiters: parseNullableNumber(draft.topOffLiters),
    addbackEc: parseNullableNumber(draft.addbackEc),
    solutionChange: draft.solutionChange,
    ppfdMol: parseNullableNumber(draft.ppfdMol),
    co2Ppm: parseNullableNumber(draft.co2Ppm),
  }
}

function formatDraftNumber(value: number | null | undefined) {
  if (value == null || Number.isNaN(value)) return ''
  return String(value)
}

function parseNullableNumber(value: string) {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number(trimmed.replace(',', '.'))
  return Number.isNaN(parsed) ? null : parsed
}

function trimToNull(value: string) {
  const trimmed = value.trim()
  return trimmed ? trimmed : null
}

export default MeasurementEditPage
