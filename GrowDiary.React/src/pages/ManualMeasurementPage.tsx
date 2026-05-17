import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowStage, GrowSummary, MeasurementDto, MeasurementUpsertPayload, PhotoTag, ValueOrigin } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section, V1Switch } from '../components/v1'
import { toLocalInputValue } from '../utils'

type NumericKey = Exclude<keyof MeasurementDraft, 'takenAtLocal' | 'stage' | 'source' | 'notes' | 'solutionChange'>

type MeasurementDraft = {
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

type FieldDefinition = { key: NumericKey; label: string; unit: string; hint?: string }

type PhotoDraft = {
  files: File[]
  caption: string
  tag: PhotoTag
}

const stages: GrowStage[] = ['Seedling', 'Clone', 'Veg', 'Transition', 'Flower', 'Finish', 'Dry', 'Cure']
const photoTags: PhotoTag[] = ['Overview', 'Canopy', 'Leaf', 'Root', 'Training', 'Flower', 'Problem', 'Comparison', 'Other']

const climateFields: FieldDefinition[] = [
  { key: 'airTemperatureC', label: 'Lufttemperatur', unit: '°C' },
  { key: 'humidityPercent', label: 'Luftfeuchte', unit: '%' },
  { key: 'ppfdMol', label: 'PPFD', unit: 'µmol/m²/s' },
  { key: 'co2Ppm', label: 'CO₂', unit: 'ppm' },
  { key: 'heightCm', label: 'Pflanzenhöhe', unit: 'cm' },
]

const reservoirFields: FieldDefinition[] = [
  { key: 'reservoirPh', label: 'pH', unit: 'pH' },
  { key: 'reservoirEc', label: 'EC', unit: 'mS/cm' },
  { key: 'reservoirWaterTempC', label: 'Wassertemperatur', unit: '°C' },
  { key: 'reservoirLevelCm', label: 'Wasserstand', unit: 'cm' },
  { key: 'reservoirLevelLiters', label: 'Wasserstand', unit: 'L' },
  { key: 'dissolvedOxygenMgL', label: 'DO', unit: 'mg/L' },
  { key: 'orpMv', label: 'ORP', unit: 'mV' },
]

const irrigationFields: FieldDefinition[] = [
  { key: 'waterAmountMl', label: 'Gießmenge', unit: 'ml' },
  { key: 'runoffAmountMl', label: 'Runoff', unit: 'ml' },
  { key: 'irrigationPh', label: 'Input pH', unit: 'pH' },
  { key: 'irrigationEc', label: 'Input EC', unit: 'mS/cm' },
  { key: 'drainPh', label: 'Drain pH', unit: 'pH' },
  { key: 'drainEc', label: 'Drain EC', unit: 'mS/cm' },
  { key: 'topOffLiters', label: 'Top-Off', unit: 'L' },
  { key: 'addbackEc', label: 'Addback EC', unit: 'mS/cm' },
]

function ManualMeasurementPage() {
  const navigate = useNavigate()
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [selectedGrowId, setSelectedGrowId] = useState<number | null>(null)
  const [draft, setDraft] = useState<MeasurementDraft>(() => createDraft())
  const [photoDraft, setPhotoDraft] = useState<PhotoDraft>({ files: [], caption: '', tag: 'Overview' })
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const data = await apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal })
        if (controller.signal.aborted) return
        const active = data.filter((grow) => grow.status === 'Running' || grow.status === 'Planning')
        setGrows(active)
        setSelectedGrowId((current) => current ?? active[0]?.id ?? null)
        const stage = active[0]?.latestStage ?? 'Veg'
        setDraft((current) => ({ ...current, stage }))
      } catch (caught) {
        if (!controller.signal.aborted) setError(formatApiError(caught, 'Grows konnten nicht geladen werden.'))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  const selectedGrow = useMemo(() => grows.find((grow) => grow.id === selectedGrowId) ?? null, [grows, selectedGrowId])
  const filledCount = useMemo(() => countFilled(draft), [draft])

  function patch(patchValue: Partial<MeasurementDraft>) {
    setDraft((current) => ({ ...current, ...patchValue }))
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedGrowId) {
      setError('Bitte Grow auswählen.')
      return
    }

    setSaving(true)
    setError(null)
    setMessage(null)

    try {
      const payload = toPayload(draft)
      const measurement = await createMeasurement(selectedGrowId, payload)

      if (photoDraft.files.length > 0) {
        await uploadPhotos(measurement.id, photoDraft)
      }

      setMessage('Messung gespeichert.')
      navigate(`/grows/${selectedGrowId}`)
    } catch (caught) {
      setError(formatApiError(caught, 'Messung konnte nicht gespeichert werden.'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <V1Page
      eyebrow="Manuell"
      title="Messung erfassen"
      subtitle="Unabhängig von Addback und Home Assistant. Für echte Nutzung ohne vollständiges Sensor-Setup."
      action={<Link className="v1-button is-ghost" to="/">Zurück</Link>}
      className="rc2-measurement-page"
    >
      {error && <V1Alert title="Fehler" message={error} tone="warn" />}
      {message && <V1Alert message={message} tone="ok" />}

      {loading ? <V1Empty title="Lade Grows..." /> : grows.length === 0 ? (
        <V1Empty title="Kein aktiver Grow" text="Lege zuerst einen Grow an, bevor du manuelle Messungen erfassen kannst." action={<Link to="/grows/new" className="v1-button is-primary">Grow starten</Link>} />
      ) : (
        <form className="rc2-measurement-layout" onSubmit={(event) => void submit(event)}>
          <aside className="rc2-measurement-side">
            <V1Card className="rc2-sticky-card">
              <span className="v1-card-kicker">Kontext</span>
              <h2>{selectedGrow?.name ?? 'Grow wählen'}</h2>
              <p>{selectedGrow?.strain ?? 'Sorte offen'} · {selectedGrow?.tentName ?? 'ohne Zelt'}</p>
              <V1Field label="Grow">
                <select value={selectedGrowId ?? ''} onChange={(event) => setSelectedGrowId(Number(event.target.value))}>
                  {grows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
                </select>
              </V1Field>
              <V1Field label="Zeitpunkt">
                <input type="datetime-local" value={draft.takenAtLocal} onChange={(event) => patch({ takenAtLocal: event.target.value })} />
              </V1Field>
              <V1Field label="Phase">
                <select value={draft.stage} onChange={(event) => patch({ stage: event.target.value as GrowStage })}>
                  {stages.map((stage) => <option key={stage} value={stage}>{stage}</option>)}
                </select>
              </V1Field>
              <V1Badge tone={filledCount > 0 ? 'ok' : 'neutral'}>{filledCount} Werte</V1Badge>
            </V1Card>
          </aside>

          <div className="rc2-measurement-main">
            <V1Section title="RDWC/DWC Reservoir">
              <FieldGrid fields={reservoirFields} draft={draft} patch={patch} />
            </V1Section>

            <V1Section title="Zelt / Klima">
              <FieldGrid fields={climateFields} draft={draft} patch={patch} />
            </V1Section>

            <V1Section title="Gießen / Drain / Addback">
              <FieldGrid fields={irrigationFields} draft={draft} patch={patch} />
            </V1Section>

            <V1Section title="Notiz & Fotos">
              <div className="rc2-measurement-extra">
                <V1Field label="Notiz">
                  <textarea rows={4} value={draft.notes} onChange={(event) => patch({ notes: event.target.value })} placeholder="Beobachtung, Korrektur, Geruch, Wurzeln, Blattbild..." />
                </V1Field>
                <V1Switch label="Lösungswechsel" checked={draft.solutionChange} onChange={(checked) => patch({ solutionChange: checked })} hint="Reservoir oder Nährlösung vollständig gewechselt." />
                <V1Field label="Foto-Tag">
                  <select value={photoDraft.tag} onChange={(event) => setPhotoDraft((current) => ({ ...current, tag: event.target.value as PhotoTag }))}>
                    {photoTags.map((tag) => <option key={tag} value={tag}>{tag}</option>)}
                  </select>
                </V1Field>
                <V1Field label="Foto-Beschriftung">
                  <input value={photoDraft.caption} onChange={(event) => setPhotoDraft((current) => ({ ...current, caption: event.target.value }))} />
                </V1Field>
                <V1Field label="Fotos" wide>
                  <input type="file" accept="image/png,image/jpeg,image/webp" multiple onChange={(event) => setPhotoDraft((current) => ({ ...current, files: Array.from(event.target.files ?? []) }))} />
                </V1Field>
              </div>
            </V1Section>

            <div className="v1-form-actions sticky-actions">
              <Link className="v1-button is-ghost" to="/">Abbrechen</Link>
              <V1Button type="submit" variant="primary" disabled={saving}>{saving ? 'Speichert...' : 'Messung speichern'}</V1Button>
            </div>
          </div>
        </form>
      )}
    </V1Page>
  )
}

function FieldGrid({ fields, draft, patch }: { fields: FieldDefinition[]; draft: MeasurementDraft; patch: (patchValue: Partial<MeasurementDraft>) => void }) {
  return (
    <div className="rc2-measurement-grid">
      {fields.map((field) => (
        <V1Field key={field.key} label={`${field.label} ${field.unit ? `(${field.unit})` : ''}`} hint={field.hint}>
          <input
            inputMode="decimal"
            value={draft[field.key]}
            onChange={(event) => patch({ [field.key]: event.target.value } as Partial<MeasurementDraft>)}
            placeholder="–"
          />
        </V1Field>
      ))}
    </div>
  )
}

async function createMeasurement(growId: number, payload: MeasurementUpsertPayload) {
  try {
    return await apiFetch<MeasurementDto>(`/api/grows/${growId}/measurements`, {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  } catch (caught) {
    if (caught instanceof ApiRequestError && caught.status === 404) {
      return await apiFetch<MeasurementDto>(`/api/measurements?growId=${growId}`, {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    }
    throw caught
  }
}

async function uploadPhotos(measurementId: number, draft: PhotoDraft) {
  const form = new FormData()
  form.append('photoCaption', draft.caption)
  form.append('photoTag', draft.tag)
  form.append('useAsReferenceShot', 'false')
  form.append('source', 'Manual')
  for (const file of draft.files) form.append('photos', file)
  await apiFetch(`/api/measurements/${measurementId}/photos`, { method: 'POST', body: form })
}

function createDraft(): MeasurementDraft {
  return {
    takenAtLocal: toLocalInputValue(),
    stage: 'Veg',
    source: 'Manual',
    notes: '',
    solutionChange: false,
    airTemperatureC: '',
    humidityPercent: '',
    heightCm: '',
    waterAmountMl: '',
    runoffAmountMl: '',
    irrigationPh: '',
    irrigationEc: '',
    drainPh: '',
    drainEc: '',
    reservoirPh: '',
    reservoirEc: '',
    reservoirWaterTempC: '',
    reservoirLevelCm: '',
    reservoirLevelLiters: '',
    dissolvedOxygenMgL: '',
    orpMv: '',
    topOffLiters: '',
    addbackEc: '',
    ppfdMol: '',
    co2Ppm: '',
  }
}

function toPayload(draft: MeasurementDraft): MeasurementUpsertPayload {
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

function countFilled(draft: MeasurementDraft) {
  const ignored = new Set(['takenAtLocal', 'stage', 'source', 'notes', 'solutionChange'])
  return Object.entries(draft).filter(([key, value]) => !ignored.has(key) && String(value).trim().length > 0).length
}

function parseNullableNumber(value: string) {
  const trimmed = value.trim().replace(',', '.')
  if (!trimmed) return null
  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

function trimToNull(value: string) {
  const trimmed = value.trim()
  return trimmed ? trimmed : null
}

function formatApiError(caught: unknown, fallback: string) {
  return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback
}

export default ManualMeasurementPage
