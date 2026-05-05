import { useEffect, useState } from 'react'
import type { Dispatch, FormEvent, SetStateAction } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type {
  GerminationMethod,
  GrowDetail,
  GrowEntryPoint,
  GrowEnvironment,
  GrowStatus,
  GrowUpsertPayload,
  HydroStyle,
  PropagationMedium,
  SeedType,
  StartMaterial,
  TentDto,
  WaterSource,
} from '../types'

const seedTypes: SeedType[] = ['Feminized', 'Autoflower', 'Regular']
const startMaterials: StartMaterial[] = ['Seed', 'Clone']
const germinationMethods: GerminationMethod[] = ['PaperTowel', 'Rockwool', 'RapidRooter', 'DirectInSystem']
const hydroStyles: HydroStyle[] = ['DWC', 'RDWC', 'NFT', 'Aeroponic', 'Other']
const waterSources: WaterSource[] = ['Tap', 'RO', 'Mixed']
const entryPoints: GrowEntryPoint[] = ['Germination', 'Seedling', 'Veg', 'Flower', 'Flush']
const statuses: GrowStatus[] = ['Planning', 'Running', 'Completed', 'Aborted']
const environments: GrowEnvironment[] = ['Indoor', 'Outdoor', 'Greenhouse']
const propagationMedia: PropagationMedium[] = ['Rockwool', 'Hydroton', 'RapidRooter', 'Neoprene']

const emptyForm = (): GrowUpsertPayload => ({
  templateId: null,
  name: '',
  tentId: null,
  systemId: null,
  strain: null,
  breeder: null,
  seedType: 'Feminized',
  startMaterial: 'Seed',
  germinationMethod: 'PaperTowel',
  cloneSource: null,
  cloneIsRooted: false,
  phenoNumber: null,
  breederFlowerWeeksMin: null,
  breederFlowerWeeksMax: null,
  hydroStyle: 'RDWC',
  plantCount: null,
  reservoirSize: null,
  containerSize: null,
  propagationMedium: null,
  light: null,
  hasChiller: false,
  waterSource: 'RO',
  nutrients: null,
  startDate: new Date().toISOString().slice(0, 10),
  entryPoint: 'Germination',
  daysAlreadyInPhase: null,
  autoflowerDaysSinceGermination: null,
  flipDate: null,
  notes: null,
  status: 'Planning',
  environment: 'Indoor',
})

function GrowSetupPage() {
  const { growId } = useParams()
  const navigate = useNavigate()
  const isEditing = Boolean(growId)
  const [tents, setTents] = useState<TentDto[]>([])
  const [form, setForm] = useState<GrowUpsertPayload>(() => emptyForm())
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)

      try {
        const tentsPromise = apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal })
        const growPromise = isEditing && growId
          ? apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal: controller.signal })
          : Promise.resolve(null)

        const [loadedTents, grow] = await Promise.all([tentsPromise, growPromise])
        setTents(loadedTents)
        setForm(grow ? mapGrowToPayload(grow) : emptyForm())
      } catch (caught) {
        if (controller.signal.aborted) {
          return
        }

        const message = caught instanceof ApiRequestError ? caught.message : 'Grow-Setup konnte nicht geladen werden.'
        setError(message)
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void load()
    return () => controller.abort()
  }, [growId, isEditing])

  const isAutoflower = form.seedType === 'Autoflower'
  const needsDaysInPhase = form.entryPoint !== 'Germination' && !isAutoflower
  const needsFlipDate = form.entryPoint === 'Flower' && !isAutoflower
  const pageTitle = isEditing ? 'Grow-Setup bearbeiten' : 'Neuen Grow anlegen'

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    setError(null)

    const payload = normalizePayload(form)

    try {
      const saved = await apiFetch<GrowDetail>(isEditing && growId ? `/api/grows/${growId}` : '/api/grows', {
        method: isEditing ? 'PUT' : 'POST',
        body: JSON.stringify(payload),
      })

      navigate(`/grows/${saved.id}`)
    } catch (caught) {
      const message = caught instanceof ApiRequestError ? caught.message : 'Grow konnte nicht gespeichert werden.'
      setError(message)
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return (
      <>
        <div className="topbar"><span className="topbar-title">{isEditing ? 'Grow bearbeiten' : 'Neuer Grow'}</span></div>
        <div className="page-scroll"><div className="empty-hint">Lade…</div></div>
      </>
    )
  }

  return (
    <>
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to={isEditing && growId ? `/grows/${growId}` : '/'}>
            {isEditing ? '← Zum Grow' : '← Dashboard'}
          </Link>
          <span className="topbar-title">{pageTitle}</span>
        </div>
      </div>

      <div className="page-scroll">
      {error ? (
        <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)' }}>
          <div className="alert-dot" />
          <strong>Fehler</strong>
          <span>{error}</span>
        </div>
      ) : null}

      <form onSubmit={handleSubmit}>
        <div className="tents-grid" style={{ marginBottom: 14 }}>
          <div className="card">
            <div className="card-header"><span className="card-title">Genetik &amp; Identität</span></div>
            <div style={{ padding: '14px 16px', display: 'grid', gap: 12 }}>
              <label className="field"><span>Name</span><input required value={form.name} onChange={(event) => patchForm(setForm, { name: event.target.value })} placeholder="Blue Dream RDWC Run" /></label>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <label className="field"><span>Strain</span><input value={form.strain ?? ''} onChange={(event) => patchForm(setForm, { strain: toNullableString(event.target.value) })} /></label>
                <label className="field"><span>Breeder</span><input value={form.breeder ?? ''} onChange={(event) => patchForm(setForm, { breeder: toNullableString(event.target.value) })} /></label>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <label className="field"><span>Seed Type</span><select value={form.seedType} onChange={(event) => patchForm(setForm, { seedType: event.target.value as SeedType })}>{seedTypes.map((value) => <option key={value} value={value}>{value}</option>)}</select></label>
                <label className="field"><span>Startmaterial</span><select value={form.startMaterial} onChange={(event) => patchForm(setForm, { startMaterial: event.target.value as StartMaterial })}>{startMaterials.map((value) => <option key={value} value={value}>{value}</option>)}</select></label>
              </div>
              {form.startMaterial === 'Seed' ? (
                <label className="field"><span>Keimmethode</span><select value={form.germinationMethod ?? 'PaperTowel'} onChange={(event) => patchForm(setForm, { germinationMethod: event.target.value as GerminationMethod })}>{germinationMethods.map((value) => <option key={value} value={value}>{value}</option>)}</select></label>
              ) : (
                <>
                  <label className="field"><span>Clone Source</span><input value={form.cloneSource ?? ''} onChange={(event) => patchForm(setForm, { cloneSource: toNullableString(event.target.value) })} placeholder="Mutterpflanze / Cut Nr. 3" /></label>
                  <label style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, fontSize: 14, cursor: 'pointer' }}><span>Steckling ist bereits bewurzelt</span><input type="checkbox" checked={form.cloneIsRooted} onChange={(event) => patchForm(setForm, { cloneIsRooted: event.target.checked })} /></label>
                </>
              )}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <label className="field"><span>Pheno</span><input value={form.phenoNumber ?? ''} onChange={(event) => patchForm(setForm, { phenoNumber: toNullableInteger(event.target.value) })} /></label>
                <label className="field"><span>Pflanzen</span><input value={form.plantCount ?? ''} onChange={(event) => patchForm(setForm, { plantCount: toNullableInteger(event.target.value) })} /></label>
              </div>
              {!isAutoflower ? (
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                  <label className="field"><span>Flower Weeks Min</span><input value={form.breederFlowerWeeksMin ?? ''} onChange={(event) => patchForm(setForm, { breederFlowerWeeksMin: toNullableInteger(event.target.value) })} /></label>
                  <label className="field"><span>Flower Weeks Max</span><input value={form.breederFlowerWeeksMax ?? ''} onChange={(event) => patchForm(setForm, { breederFlowerWeeksMax: toNullableInteger(event.target.value) })} /></label>
                </div>
              ) : null}
            </div>
          </div>

          <div className="card">
            <div className="card-header"><span className="card-title">System &amp; Hardware</span></div>
            <div style={{ padding: '14px 16px', display: 'grid', gap: 12 }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <label className="field"><span>Zelt</span><select value={form.tentId ?? ''} onChange={(event) => patchForm(setForm, { tentId: toNullableInteger(event.target.value) })}><option value="">Ohne Zelt</option>{tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}</select></label>
                <label className="field"><span>Hydro Style</span><select value={form.hydroStyle} onChange={(event) => patchForm(setForm, { hydroStyle: event.target.value as HydroStyle })}>{hydroStyles.map((value) => <option key={value} value={value}>{value}</option>)}</select></label>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <label className="field"><span>Reservoir</span><input value={form.reservoirSize ?? ''} onChange={(event) => patchForm(setForm, { reservoirSize: toNullableString(event.target.value) })} placeholder="70 L" /></label>
                <label className="field"><span>Container</span><input value={form.containerSize ?? ''} onChange={(event) => patchForm(setForm, { containerSize: toNullableString(event.target.value) })} placeholder="20 L" /></label>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <label className="field"><span>Licht</span><input value={form.light ?? ''} onChange={(event) => patchForm(setForm, { light: toNullableString(event.target.value) })} placeholder="LED Bar 480W" /></label>
                <label className="field"><span>Propagation</span><select value={form.propagationMedium ?? ''} onChange={(event) => patchForm(setForm, { propagationMedium: toNullableString(event.target.value) as PropagationMedium | null })}><option value="">Nicht gesetzt</option>{propagationMedia.map((value) => <option key={value} value={value}>{value}</option>)}</select></label>
              </div>
              <label style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, fontSize: 14, cursor: 'pointer' }}><span>Chiller vorhanden</span><input type="checkbox" checked={form.hasChiller} onChange={(event) => patchForm(setForm, { hasChiller: event.target.checked })} /></label>
            </div>
          </div>

          <div className="card">
            <div className="card-header"><span className="card-title">Start &amp; Status</span></div>
            <div style={{ padding: '14px 16px', display: 'grid', gap: 12 }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <label className="field"><span>Startdatum</span><input required type="date" value={form.startDate} onChange={(event) => patchForm(setForm, { startDate: event.target.value })} /></label>
                <label className="field"><span>Entry Point</span><select value={form.entryPoint} onChange={(event) => patchForm(setForm, { entryPoint: event.target.value as GrowEntryPoint })}>{entryPoints.map((value) => <option key={value} value={value}>{value}</option>)}</select></label>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <label className="field"><span>Status</span><select value={form.status} onChange={(event) => patchForm(setForm, { status: event.target.value as GrowStatus })}>{statuses.map((value) => <option key={value} value={value}>{value}</option>)}</select></label>
                <label className="field"><span>Environment</span><select value={form.environment} onChange={(event) => patchForm(setForm, { environment: event.target.value as GrowEnvironment })}>{environments.map((value) => <option key={value} value={value}>{value}</option>)}</select></label>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                <label className="field"><span>Wasserquelle</span><select value={form.waterSource} onChange={(event) => patchForm(setForm, { waterSource: event.target.value as WaterSource })}>{waterSources.map((value) => <option key={value} value={value}>{value}</option>)}</select></label>
                {isAutoflower ? (
                  <label className="field"><span>Tage seit Keimung</span><input value={form.autoflowerDaysSinceGermination ?? ''} onChange={(event) => patchForm(setForm, { autoflowerDaysSinceGermination: toNullableInteger(event.target.value) })} /></label>
                ) : (
                  <label className="field"><span>Tage bereits in Phase</span><input value={form.daysAlreadyInPhase ?? ''} onChange={(event) => patchForm(setForm, { daysAlreadyInPhase: toNullableInteger(event.target.value) })} disabled={!needsDaysInPhase} /></label>
                )}
              </div>
              <label className="field"><span>Flip-Datum</span><input type="date" value={form.flipDate ?? ''} onChange={(event) => patchForm(setForm, { flipDate: toNullableString(event.target.value) })} disabled={!needsFlipDate} /></label>
            </div>
          </div>
        </div>

        <div className="tents-grid" style={{ marginBottom: 14 }}>
          <div className="card">
            <div className="card-header"><span className="card-title">Nährstoffe &amp; Notizen</span></div>
            <div style={{ padding: '14px 16px', display: 'grid', gap: 12 }}>
              <label className="field"><span>Naehrstoffe</span><input value={form.nutrients ?? ''} onChange={(event) => patchForm(setForm, { nutrients: toNullableString(event.target.value) })} placeholder="Athena Pro, Canna Aqua, ..." /></label>
              <label className="field"><span>Notizen</span><textarea rows={5} value={form.notes ?? ''} onChange={(event) => patchForm(setForm, { notes: toNullableString(event.target.value) })} placeholder="Besonderheiten, Ziele, bekannte Risiken..." /></label>
            </div>
          </div>

          <div className="card">
            <div className="card-header"><span className="card-title">Vorschau</span></div>
            <div style={{ padding: '14px 16px' }}>
              <div style={{ fontSize: 15, fontWeight: 600, marginBottom: 8 }}>{form.name || 'Unbenannter Grow'}</div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 6, fontSize: 13, color: 'var(--muted)' }}>
                  <span>{form.startMaterial}</span>
                  <span>{form.hydroStyle}</span>
                  <span>{tents.find((tent) => tent.id === form.tentId)?.name ?? 'ohne Zelt'}</span>
                  <span>{form.startDate}</span>
              </div>
              <p className="hint-text">Das Formular sendet nur die Felder, die auch im C#-Contract existieren. Irrelevante Werte werden vor dem Submit auf `null` gesetzt.</p>
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end', marginBottom: 24 }}>
          <Link className="btn" to={isEditing && growId ? `/grows/${growId}` : '/'}>Abbrechen</Link>
          <button className="btn btn-primary" disabled={saving}>{saving ? 'Speichert…' : isEditing ? 'Grow aktualisieren' : 'Grow anlegen'}</button>
        </div>
      </form>
      </div>
    </>
  )
}

function patchForm(
  setForm: Dispatch<SetStateAction<GrowUpsertPayload>>,
  patch: Partial<GrowUpsertPayload>,
) {
  setForm((current) => ({ ...current, ...patch }))
}

function mapGrowToPayload(grow: GrowDetail): GrowUpsertPayload {
  return {
    templateId: null,
    name: grow.name,
    tentId: grow.tentId,
    systemId: grow.systemId,
    strain: grow.strain,
    breeder: grow.breeder,
    seedType: grow.seedType,
    startMaterial: grow.startMaterial,
    germinationMethod: grow.germinationMethod,
    cloneSource: grow.cloneSource,
    cloneIsRooted: grow.cloneIsRooted,
    phenoNumber: grow.phenoNumber,
    breederFlowerWeeksMin: grow.breederFlowerWeeksMin,
    breederFlowerWeeksMax: grow.breederFlowerWeeksMax,
    hydroStyle: grow.hydroStyle,
    plantCount: grow.plantCount,
    reservoirSize: grow.reservoirSize,
    containerSize: grow.containerSize,
    propagationMedium: grow.propagationMedium,
    light: grow.light,
    hasChiller: grow.hasChiller,
    waterSource: grow.waterSource,
    nutrients: grow.nutrients,
    startDate: grow.startDate.slice(0, 10),
    entryPoint: grow.entryPoint,
    daysAlreadyInPhase: grow.daysAlreadyInPhase,
    autoflowerDaysSinceGermination: grow.autoflowerDaysSinceGermination,
    flipDate: grow.flipDate ? grow.flipDate.slice(0, 10) : null,
    notes: grow.notes,
    status: grow.status,
    environment: grow.environment,
  }
}

function normalizePayload(form: GrowUpsertPayload): GrowUpsertPayload {
  const isAutoflower = form.seedType === 'Autoflower'
  const seedSetup = form.startMaterial === 'Seed'
  const needsDaysInPhase = form.entryPoint !== 'Germination' && !isAutoflower
  const needsFlipDate = form.entryPoint === 'Flower' && !isAutoflower

  return {
    ...form,
    name: form.name.trim(),
    strain: toNullableString(form.strain),
    breeder: toNullableString(form.breeder),
    germinationMethod: seedSetup ? form.germinationMethod : null,
    cloneSource: seedSetup ? null : toNullableString(form.cloneSource),
    cloneIsRooted: seedSetup ? false : form.cloneIsRooted,
    breederFlowerWeeksMin: isAutoflower ? null : form.breederFlowerWeeksMin,
    breederFlowerWeeksMax: isAutoflower ? null : form.breederFlowerWeeksMax,
    reservoirSize: toNullableString(form.reservoirSize),
    containerSize: toNullableString(form.containerSize),
    propagationMedium: form.propagationMedium,
    light: toNullableString(form.light),
    nutrients: toNullableString(form.nutrients),
    daysAlreadyInPhase: needsDaysInPhase ? form.daysAlreadyInPhase : null,
    autoflowerDaysSinceGermination: isAutoflower ? form.autoflowerDaysSinceGermination : null,
    flipDate: needsFlipDate ? toNullableString(form.flipDate) : null,
    notes: toNullableString(form.notes),
  }
}

function toNullableString(value: string | null | undefined): string | null {
  const trimmed = value?.trim() ?? ''
  return trimmed ? trimmed : null
}

function toNullableInteger(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) {
    return null
  }

  const parsed = Number.parseInt(trimmed, 10)
  return Number.isNaN(parsed) ? null : parsed
}

export default GrowSetupPage
