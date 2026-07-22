import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import { resolveUrl } from '../base'
import type { GrowStage, GrowSummary, HydroStyle, MeasurementDto, MeasurementUpsertPayload, PhotoTag, TentDto, TentLivePayload, ValueOrigin } from '../types'
import FileInput from '../components/FileInput'
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
  { key: 'airTemperatureC', label: 'Temperatur', unit: '°C' },
  { key: 'humidityPercent', label: 'Luftfeuchte', unit: '%' },
  { key: 'ppfdMol', label: 'PPFD', unit: 'µmol/m²/s' },
  { key: 'co2Ppm', label: 'CO₂', unit: 'ppm' },
]

const reservoirFields: FieldDefinition[] = [
  { key: 'reservoirPh', label: 'pH', unit: 'pH' },
  { key: 'reservoirEc', label: 'EC', unit: 'mS/cm' },
  { key: 'reservoirWaterTempC', label: 'Wassertemp.', unit: '°C' },
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

const soilSolutionFields: FieldDefinition[] = irrigationFields.filter((field) => field.key !== 'topOffLiters' && field.key !== 'addbackEc')

const observationFields: FieldDefinition[] = [
  { key: 'heightCm', label: 'Höhe', unit: 'cm' },
]

// Live Home Assistant metric keys → measurement draft fields, so a new measurement
// starts pre-filled from the sensors that are already mapped.
const LIVE_TO_DRAFT: Partial<Record<string, NumericKey>> = {
  'reservoir-ph': 'reservoirPh',
  'reservoir-ec': 'reservoirEc',
  'reservoir-temp': 'reservoirWaterTempC',
  'reservoir-level': 'reservoirLevelLiters',
  'reservoir-level-cm': 'reservoirLevelCm',
  'orp': 'orpMv',
  'dissolved-oxygen': 'dissolvedOxygenMgL',
  'temperature': 'airTemperatureC',
  'humidity': 'humidityPercent',
  'co2': 'co2Ppm',
  'ppfd': 'ppfdMol',
}

function normalizeLiveValue(value: string): string | null {
  const cleaned = value.trim().replace(',', '.')
  if (cleaned === '' || cleaned === '–' || cleaned === '-') return null
  return Number.isFinite(Number(cleaned)) ? cleaned : null
}

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
  const [prefilled, setPrefilled] = useState(false)
  const [livePulling, setLivePulling] = useState(false)
  const [cameras, setCameras] = useState<string[]>([])
  const [snapshotCam, setSnapshotCam] = useState('')
  const [snapshotting, setSnapshotting] = useState(false)
  const [growActionSaving, setGrowActionSaving] = useState<string | null>(null)

  // Fetches the tent's current live values and writes the mappable ones into the draft.
  // Returns whether any live value was available at all.
  const pullLive = useCallback(async (tentId: number, overwrite: boolean, signal?: AbortSignal): Promise<boolean> => {
    const live = await apiFetch<TentLivePayload>(`/api/live/tents/${tentId}`, signal ? { signal } : undefined)
    const mappable = live.metrics.some((metric) => LIVE_TO_DRAFT[metric.key] && normalizeLiveValue(metric.value) != null)
    setDraft((current) => {
      const next = { ...current }
      for (const metric of live.metrics) {
        const field = LIVE_TO_DRAFT[metric.key]
        if (!field) continue
        const value = normalizeLiveValue(metric.value)
        if (value == null) continue
        if (!overwrite && next[field].trim() !== '') continue
        next[field] = value
      }
      return next
    })
    return mappable
  }, [])

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
  const vpd = useMemo(() => calculateVpd(draft.airTemperatureC, draft.humidityPercent), [draft.airTemperatureC, draft.humidityPercent])
  const isHydroGrow = isHydroStyle(selectedGrow?.hydroStyle)
  const solutionFields = isHydroGrow ? reservoirFields : soilSolutionFields
  const tentId = selectedGrow?.tentId ?? null

  // Lifecycle confirmations belong here, at measurement time — confirming germination,
  // rooting, or the flip to 12/12 is an observation you make when you check the plant.
  const canConfirmGermination = selectedGrow?.startMaterial === 'Seed' && !selectedGrow?.germinatedAt
  const canConfirmRooting = selectedGrow?.startMaterial === 'Clone' && !selectedGrow?.rootedAt
  const canFlipToFlower = selectedGrow != null && selectedGrow.seedType !== 'Autoflower' && !selectedGrow.flipDate

  // Pre-fill the mappable fields from Home Assistant when the tent context appears or
  // changes. Best-effort: silently skipped if HA is unreachable.
  useEffect(() => {
    if (tentId == null) return
    const controller = new AbortController()
    void (async () => {
      try {
        const any = await pullLive(tentId, true, controller.signal)
        if (!controller.signal.aborted && any) setPrefilled(true)
      } catch { /* HA offline or no live values — leave fields empty */ }
    })()
    return () => controller.abort()
  }, [tentId, pullLive])

  // Load the tent's cameras so a snapshot can be attached to the measurement.
  useEffect(() => {
    const controller = new AbortController()
    void (async () => {
      if (tentId == null) {
        setCameras([])
        setSnapshotCam('')
        return
      }
      try {
        const tent = await apiFetch<TentDto>(`/api/settings/tents/${tentId}`, { signal: controller.signal })
        if (controller.signal.aborted) return
        const list = tent.cameras ?? []
        setCameras(list)
        setSnapshotCam(list[0] ?? '')
      } catch { /* ignore */ }
    })()
    return () => controller.abort()
  }, [tentId])

  async function captureSnapshot() {
    if (tentId == null || snapshotCam === '') return
    setSnapshotting(true)
    setError(null)
    setMessage(null)
    try {
      const response = await fetch(resolveUrl(`/api/live/tents/${tentId}/camera?entity=${encodeURIComponent(snapshotCam)}&t=${Date.now()}`))
      if (!response.ok) throw new Error('Kamera nicht erreichbar')
      const blob = await response.blob()
      const file = new File([blob], `snapshot-${Date.now()}.jpg`, { type: blob.type || 'image/jpeg' })
      setPhotoDraft((current) => ({ ...current, files: [...current.files, file] }))
      setMessage('Kamera-Snapshot hinzugefügt.')
    } catch {
      setError('Snapshot konnte nicht aufgenommen werden — ist die Kamera in Home Assistant erreichbar?')
    } finally {
      setSnapshotting(false)
    }
  }

  async function confirmGrowAction(action: 'germination' | 'rooting' | 'flip') {
    if (!selectedGrowId) return
    const route = action === 'germination' ? 'confirm-germination' : action === 'rooting' ? 'confirm-rooting' : 'flip-to-flower'
    setGrowActionSaving(action)
    setError(null)
    setMessage(null)
    try {
      const result = await apiFetch<{ message: string }>(`/api/grows/${selectedGrowId}/actions/${route}`, { method: 'POST' })
      setMessage(result.message)
      // Re-pull grows so the just-confirmed step drops off (germinatedAt/rootedAt/flipDate set).
      const data = await apiFetch<GrowSummary[]>('/api/grows?archived=false')
      setGrows(data.filter((grow) => grow.status === 'Running' || grow.status === 'Planning'))
    } catch (caught) {
      setError(formatApiError(caught, 'Aktion konnte nicht ausgeführt werden.'))
    } finally {
      setGrowActionSaving(null)
    }
  }

  async function refreshFromLive() {
    if (tentId == null) return
    setLivePulling(true)
    setError(null)
    try {
      const any = await pullLive(tentId, true)
      setPrefilled(any)
      if (!any) setMessage('Keine Live-Werte in Home Assistant gefunden.')
    } catch {
      setError('Live-Werte konnten nicht geladen werden.')
    } finally {
      setLivePulling(false)
    }
  }

  function patch(patchValue: Partial<MeasurementDraft>) {
    setDraft((current) => ({ ...current, ...patchValue }))
  }

  function selectGrow(growId: number) {
    const grow = grows.find((item) => item.id === growId)
    setSelectedGrowId(growId)
    if (grow?.latestStage) patch({ stage: grow.latestStage })
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
      subtitle="Werte, Foto, speichern."
      action={<Link className="v1-button is-ghost" to="/">Zurück</Link>}
      className="rc2-measurement-page"
    >
      {error && <V1Alert title="Fehler" message={error} tone="warn" />}
      {message && <V1Alert message={message} tone="ok" />}

      {loading ? <V1Empty title="Lade Grows..." /> : grows.length === 0 ? (
        <div data-audit="measurement-empty-state">
          <V1Empty
            title="Noch kein Grow für Messungen"
            action={(
              <div className="measurement-empty-actions">
                <Link to="/grows/new" className="v1-button is-primary">Grow anlegen</Link>
                <Link to="/zelte/new" className="v1-button is-secondary">Zelt anlegen</Link>
              </div>
            )}
          />
        </div>
      ) : (
        <form className="rc2-measurement-layout" data-audit="measurement-form" onSubmit={(event) => void submit(event)}>
          <aside className="rc2-measurement-side" data-audit="measurement-section-context">
            <V1Card className="rc2-sticky-card rc2-measurement-context">
              <span className="v1-card-kicker">Kontext</span>
              <h2>{selectedGrow?.name ?? 'Grow wählen'}</h2>
              <p>{selectedGrow?.strain ?? 'Sorte offen'} · {selectedGrow?.tentName ?? 'ohne Zelt'}</p>
              {selectedGrow && <p className="rc2-measurement-note">Hydro: {formatGrowHydroMedium(selectedGrow)}</p>}
              <V1Field label="Grow">
                <select value={selectedGrowId ?? ''} onChange={(event) => selectGrow(Number(event.target.value))}>
                  {grows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
                </select>
              </V1Field>
              {grows.length === 1 && <small className="rc2-measurement-note">Eindeutig vorausgewählt, Wechsel bleibt möglich.</small>}
              <V1Field label="Zeitpunkt">
                <input type="datetime-local" value={draft.takenAtLocal} onChange={(event) => patch({ takenAtLocal: event.target.value })} />
              </V1Field>
              <V1Field label="Phase">
                <select value={draft.stage} onChange={(event) => patch({ stage: event.target.value as GrowStage })}>
                  {stages.map((stage) => <option key={stage} value={stage}>{stage}</option>)}
                </select>
              </V1Field>
              <V1Badge tone={filledCount > 0 ? 'ok' : 'neutral'}>{filledCount} Werte</V1Badge>
              {(canConfirmGermination || canConfirmRooting || canFlipToFlower) && (
                <div className="rc2-measurement-live" style={{ marginTop: 12, display: 'grid', gap: 8 }}>
                  <span className="v1-card-kicker">Phase bestätigen</span>
                  {canConfirmGermination && (
                    <V1Button variant="secondary" onClick={() => void confirmGrowAction('germination')} disabled={growActionSaving !== null}>
                      {growActionSaving === 'germination' ? 'Bestätigt…' : 'Keimung bestätigen'}
                    </V1Button>
                  )}
                  {canConfirmRooting && (
                    <V1Button variant="secondary" onClick={() => void confirmGrowAction('rooting')} disabled={growActionSaving !== null}>
                      {growActionSaving === 'rooting' ? 'Bestätigt…' : 'Bewurzelung bestätigen'}
                    </V1Button>
                  )}
                  {canFlipToFlower && (
                    <V1Button variant="secondary" onClick={() => void confirmGrowAction('flip')} disabled={growActionSaving !== null}>
                      {growActionSaving === 'flip' ? 'Trägt ein…' : 'Flip zu 12/12'}
                    </V1Button>
                  )}
                </div>
              )}
              {tentId != null && (
                <div className="rc2-measurement-live" style={{ marginTop: 12, display: 'grid', gap: 8 }}>
                  {prefilled && <p className="rc2-measurement-note">Aus Home Assistant vorbefüllt — anpassbar.</p>}
                  <V1Button variant="secondary" onClick={() => void refreshFromLive()} disabled={livePulling}>
                    {livePulling ? 'Lädt…' : 'Aus Home Assistant übernehmen'}
                  </V1Button>
                </div>
              )}
            </V1Card>
          </aside>

          <div className="rc2-measurement-main">
            <div data-audit="measurement-section-climate">
              <V1Section title="Klima">
                <FieldGrid fields={climateFields} draft={draft} patch={patch}>
                  <div className="rc2-measurement-derived" data-audit="measurement-vpd">
                    <span>VPD</span>
                    <strong>{vpd ?? '–'}<em>kPa</em></strong>
                  </div>
                </FieldGrid>
              </V1Section>
            </div>

            <div data-audit="measurement-section-hydro">
              <V1Section title={isHydroGrow ? 'Hydro / Nährlösung' : 'Gießen / Drain'}>
                <FieldGrid fields={solutionFields} draft={draft} patch={patch} />
              </V1Section>
            </div>

            {isHydroGrow && (
              <div data-audit="measurement-section-addback">
                <V1Section title="Addback">
                  <FieldGrid fields={irrigationFields} draft={draft} patch={patch} />
                </V1Section>
              </div>
            )}

            <div data-audit="measurement-section-observation">
              <V1Section title="Beobachtung">
                <div className="rc2-measurement-extra">
                  <FieldGrid fields={observationFields} draft={draft} patch={patch} />
                  <V1Switch label="Lösungswechsel" checked={draft.solutionChange} onChange={(checked) => patch({ solutionChange: checked })} hint="Reservoir oder Nährlösung vollständig gewechselt." />
                  <V1Field label="Notiz" wide>
                    <textarea rows={4} value={draft.notes} onChange={(event) => patch({ notes: event.target.value })} placeholder="Blattbild, Wurzeln, Geruch, Korrektur..." />
                  </V1Field>
                </div>
              </V1Section>
            </div>

            <div data-audit="measurement-section-photo">
              <V1Section title="Foto">
              <div className="rc2-measurement-extra rc2-measurement-photo">
                {cameras.length > 0 && (
                  <V1Field label="Kamera-Snapshot" wide hint="Nimmt ein Foto vom aktuellen Kamerabild und hängt es an.">
                    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                      {cameras.length > 1 && (
                        <select value={snapshotCam} onChange={(event) => setSnapshotCam(event.target.value)}>
                          {cameras.map((camera, index) => <option key={camera} value={camera}>{`Kamera ${index + 1}`}</option>)}
                        </select>
                      )}
                      <V1Button variant="secondary" onClick={() => void captureSnapshot()} disabled={snapshotting}>
                        {snapshotting ? 'Nimmt auf…' : 'Snapshot aufnehmen'}
                      </V1Button>
                    </div>
                  </V1Field>
                )}
                <V1Field label="Foto-Tag">
                  <select value={photoDraft.tag} onChange={(event) => setPhotoDraft((current) => ({ ...current, tag: event.target.value as PhotoTag }))}>
                    {photoTags.map((tag) => <option key={tag} value={tag}>{tag}</option>)}
                  </select>
                </V1Field>
                <V1Field label="Beschriftung">
                  <input value={photoDraft.caption} onChange={(event) => setPhotoDraft((current) => ({ ...current, caption: event.target.value }))} />
                </V1Field>
                <V1Field label="Fotos" wide>
                  <FileInput accept="image/png,image/jpeg,image/webp" label="Foto auswählen" multiple fileNames={photoDraft.files.map((file) => file.name)} onFiles={(files) => setPhotoDraft((current) => ({ ...current, files }))} />
                  <small>Optional, ein oder mehrere Bilder.</small>
                </V1Field>
              </div>
              </V1Section>
            </div>

            <div className="v1-form-actions measurement-form-actions" data-audit="measurement-form-actions">
              <Link className="v1-button is-ghost" to="/">Abbrechen</Link>
              <V1Button type="submit" variant="primary" disabled={saving}>{saving ? 'Speichert...' : 'Messung speichern'}</V1Button>
            </div>
          </div>
        </form>
      )}
    </V1Page>
  )
}

function FieldGrid({ children, fields, draft, patch }: { children?: ReactNode; fields: FieldDefinition[]; draft: MeasurementDraft; patch: (patchValue: Partial<MeasurementDraft>) => void }) {
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
      {children}
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

function isHydroStyle(style: HydroStyle | null | undefined) {
  return style != null && style !== 'None'
}

function formatGrowHydroMedium(grow: GrowSummary) {
  return grow.hydroSetupName ?? (grow.hydroStyle === 'None' ? 'kein Hydro-Setup' : grow.hydroStyle)
}

function calculateVpd(temperatureValue: string, humidityValue: string) {
  const temperature = parseNullableNumber(temperatureValue)
  const humidity = parseNullableNumber(humidityValue)
  if (temperature == null || humidity == null || humidity < 0 || humidity > 100) return null
  const saturationKpa = 0.6108 * Math.exp((17.27 * temperature) / (temperature + 237.3))
  return (saturationKpa * (1 - humidity / 100)).toFixed(2)
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
