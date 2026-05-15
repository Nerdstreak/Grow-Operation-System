import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GerminationMethod, GrowDetail, GrowEntryPoint, GrowEnvironment, GrowStatus, GrowUpsertPayload, HydroSetupDto, HydroStyle, KnowledgeOverviewDto, NutrientProgramDto, PropagationMedium, SeedType, SelectableHydroStyle, StartMaterial, TentDto, WaterSource } from '../types'
import { V1Alert, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section, V1Wizard, formatLiters, toNullableInt, toNullableString } from '../components/v1'

const steps = ['Run', 'Zelt', 'Hydro', 'Zeit', 'Programm', 'Prüfen']
const seedTypes: SeedType[] = ['Feminized', 'Autoflower', 'Regular']
const startMaterials: StartMaterial[] = ['Seed', 'Clone']
const germinationMethods: GerminationMethod[] = ['PaperTowel', 'Rockwool', 'RapidRooter', 'DirectInSystem']
const entryPoints: GrowEntryPoint[] = ['Germination', 'Seedling', 'Veg', 'Flower', 'Flush']
const statuses: GrowStatus[] = ['Planning', 'Running', 'Completed', 'Aborted']
const waterSources: WaterSource[] = ['Tap', 'RO', 'Mixed']
const propagationMedia: PropagationMedium[] = ['Rockwool', 'Hydroton', 'RapidRooter', 'Neoprene']

function emptyForm(): GrowUpsertPayload {
  return { templateId: null, name: '', tentId: null, systemId: null, setupId: null, strain: null, breeder: null, seedType: 'Feminized', startMaterial: 'Seed', germinationMethod: 'PaperTowel', cloneSource: null, cloneIsRooted: false, phenoNumber: null, breederFlowerWeeksMin: null, breederFlowerWeeksMax: null, hydroStyle: 'RDWC', plantCount: null, reservoirSize: null, containerSize: null, propagationMedium: 'Rockwool', light: null, hasChiller: false, waterSource: 'RO', nutrients: null, startDate: new Date().toISOString().slice(0, 10), entryPoint: 'Germination', daysAlreadyInPhase: null, autoflowerDaysSinceGermination: null, flipDate: null, notes: null, status: 'Planning', environment: 'Indoor' as GrowEnvironment }
}

function GrowSetupPage() {
  const { growId } = useParams()
  const navigate = useNavigate()
  const isEditing = Boolean(growId)
  const [tents, setTents] = useState<TentDto[]>([])
  const [hydroSetups, setHydroSetups] = useState<HydroSetupDto[]>([])
  const [programs, setPrograms] = useState<NutrientProgramDto[]>([])
  const [form, setForm] = useState<GrowUpsertPayload>(() => emptyForm())
  const [customProgram, setCustomProgram] = useState('')
  const [step, setStep] = useState(1)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const [tentData, hydroData, knowledge, grow] = await Promise.all([
          apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal }),
          apiFetch<HydroSetupDto[]>('/api/hydro-setups', { signal: controller.signal }),
          apiFetch<KnowledgeOverviewDto>('/api/knowledge', { signal: controller.signal }),
          isEditing && growId ? apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal: controller.signal }) : Promise.resolve(null),
        ])
        setTents(tentData)
        setHydroSetups(hydroData.filter((setup) => setup.status === 'Active'))
        setPrograms(knowledge.programs ?? [])
        if (grow) {
          setForm(mapGrowToPayload(grow))
          if (grow.nutrients && !knowledge.programs.some((program) => program.name === grow.nutrients || program.key === grow.nutrients)) setCustomProgram(grow.nutrients)
        }
      } catch (caught) {
        if (!controller.signal.aborted) setError(formatApiError(caught, 'Grow-Wizard konnte nicht geladen werden.'))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [growId, isEditing])

  const selectedTent = tents.find((tent) => tent.id === form.tentId) ?? null
  const availableHydroSetups = useMemo(() => hydroSetups.filter((setup) => !form.tentId || setup.tentId === form.tentId), [form.tentId, hydroSetups])
  const selectedHydro = hydroSetups.find((setup) => setup.id === form.systemId) ?? null
  const isAutoflower = form.seedType === 'Autoflower'
  const selectedProgram = programs.find((program) => program.name === form.nutrients || program.key === form.nutrients) ?? null

  function patch(patch: Partial<GrowUpsertPayload>) { setForm((current) => ({ ...current, ...patch })) }
  function selectTent(id: number) { patch({ tentId: id, systemId: hydroSetups.some((setup) => setup.id === form.systemId && setup.tentId === id) ? form.systemId : null }) }
  function selectHydro(setup: HydroSetupDto) { patch({ systemId: setup.id, hydroStyle: toSelectableHydroStyle(setup.hydroStyle), reservoirSize: formatLiters(setup.totalVolumeLiters ?? setup.reservoirLiters), containerSize: formatLiters(setup.potSizeLiters), hasChiller: setup.hasChiller }) }

  function goTo(next: number) {
    if (next > step) {
      const message = validateStep(step, form, selectedHydro)
      if (message) { setError(message); return }
    }
    setError(null)
    setStep(next)
  }

  async function saveGrow() {
    for (let current = 1; current < steps.length; current += 1) {
      const message = validateStep(current, form, selectedHydro)
      if (message) { setStep(current); setError(message); return }
    }
    if (step !== steps.length) { setStep(steps.length); setError('Gespeichert wird erst im Schritt Prüfen.'); return }
    setSaving(true)
    setError(null)
    try {
      const payload = normalizePayload(form, selectedHydro, programs, customProgram)
      const saved = await apiFetch<GrowDetail>(isEditing && growId ? `/api/grows/${growId}` : '/api/grows', { method: isEditing ? 'PUT' : 'POST', body: JSON.stringify(payload) })
      navigate(`/grows/${saved.id}`)
    } catch (caught) {
      setError(formatApiError(caught, 'Grow konnte nicht gespeichert werden.'))
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <V1Page eyebrow="Grow" title={isEditing ? 'Grow bearbeiten' : 'Grow starten'}><V1Empty title="Lade Wizard..." /></V1Page>

  return (
    <V1Page eyebrow="Grow" title={isEditing ? 'Grow bearbeiten' : 'Grow starten'} action={<Link className="v1-button is-ghost" to={isEditing && growId ? `/grows/${growId}` : '/'}>Zurück</Link>}>
      {error && <V1Alert message={error} tone="warn" />}
      <V1Wizard steps={steps} currentStep={step} onStep={goTo} />
      <V1Section title={steps[step - 1]}>
        {step === 1 && <RunStep form={form} patch={patch} />}
        {step === 2 && <TentStep tents={tents} selectedId={form.tentId} onSelect={selectTent} />}
        {step === 3 && <HydroStep setups={availableHydroSetups} selectedId={form.systemId} onSelect={selectHydro} />}
        {step === 4 && <TimeStep form={form} patch={patch} isAutoflower={isAutoflower} />}
        {step === 5 && <ProgramStep programs={programs} selected={form.nutrients ?? ''} custom={customProgram} setCustom={setCustomProgram} patch={patch} />}
        {step === 6 && <ReviewStep form={form} tent={selectedTent} hydro={selectedHydro} program={selectedProgram} customProgram={customProgram} />}
      </V1Section>
      <div className="v1-form-actions sticky-actions"><V1Button variant="ghost" onClick={() => step === 1 ? navigate('/') : setStep((current) => Math.max(1, current - 1))}>{step === 1 ? 'Abbrechen' : 'Zurück'}</V1Button>{step < steps.length ? <V1Button variant="primary" onClick={() => goTo(step + 1)}>Weiter</V1Button> : <V1Button variant="primary" disabled={saving} onClick={() => void saveGrow()}>{saving ? 'Speichert...' : isEditing ? 'Speichern' : 'Grow starten'}</V1Button>}</div>
    </V1Page>
  )
}

function RunStep({ form, patch }: { form: GrowUpsertPayload; patch: (patch: Partial<GrowUpsertPayload>) => void }) {
  return <div className="v1-form-grid"><V1Field label="Grow-Name"><input value={form.name} onChange={(event) => patch({ name: event.target.value })} placeholder="Purple Lemonade RDWC" /></V1Field><V1Field label="Sorte"><input value={form.strain ?? ''} onChange={(event) => patch({ strain: event.target.value })} placeholder="Purple Lemonade" /></V1Field><V1Field label="Breeder"><input value={form.breeder ?? ''} onChange={(event) => patch({ breeder: event.target.value })} /></V1Field><V1Field label="Pflanzen"><input type="number" min="1" value={form.plantCount ?? ''} onChange={(event) => patch({ plantCount: toNullableInt(event.target.value) })} /></V1Field><V1Field label="Seed Type"><select value={form.seedType} onChange={(event) => patch({ seedType: event.target.value as SeedType })}>{seedTypes.map((value) => <option key={value}>{value}</option>)}</select></V1Field><V1Field label="Startmaterial"><select value={form.startMaterial} onChange={(event) => patch({ startMaterial: event.target.value as StartMaterial })}>{startMaterials.map((value) => <option key={value} value={value}>{value === 'Seed' ? 'Samen' : 'Steckling'}</option>)}</select></V1Field>{form.startMaterial === 'Seed' ? <V1Field label="Keimmethode"><select value={form.germinationMethod ?? 'PaperTowel'} onChange={(event) => patch({ germinationMethod: event.target.value as GerminationMethod })}>{germinationMethods.map((value) => <option key={value} value={value}>{formatGermination(value)}</option>)}</select></V1Field> : <V1Field label="Stecklingsquelle"><input value={form.cloneSource ?? ''} onChange={(event) => patch({ cloneSource: event.target.value })} /></V1Field>}</div>
}

function TentStep({ tents, selectedId, onSelect }: { tents: TentDto[]; selectedId: number | null; onSelect: (id: number) => void }) {
  if (tents.length === 0) return <V1Empty title="Kein Zelt angelegt" action={<Link className="v1-button is-primary" to="/zelte">Zelt anlegen</Link>} />
  return <div className="v1-choice-grid">{tents.map((tent) => <button type="button" key={tent.id} className={selectedId === tent.id ? 'v1-choice active' : 'v1-choice'} onClick={() => onSelect(tent.id)}><strong>{tent.name}</strong><span>{formatTentType(tent.tentType)} · {tent.widthCm ?? '?'}×{tent.depthCm ?? '?'}×{tent.tentHeightCm ?? '?'} cm</span></button>)}</div>
}

function HydroStep({ setups, selectedId, onSelect }: { setups: HydroSetupDto[]; selectedId: number | null; onSelect: (setup: HydroSetupDto) => void }) {
  if (setups.length === 0) return <V1Empty title="Kein passendes Hydro-Setup" action={<Link className="v1-button is-primary" to="/hydro">Hydro anlegen</Link>} />
  return <div className="v1-choice-grid">{setups.map((setup) => <button type="button" key={setup.id} className={selectedId === setup.id ? 'v1-choice active' : 'v1-choice'} onClick={() => onSelect(setup)}><strong>{setup.name}</strong><span>{setup.hydroStyle} · {formatLiters(setup.totalVolumeLiters)} · {setup.tentName ?? 'ohne Zelt'}</span></button>)}</div>
}

function TimeStep({ form, patch, isAutoflower }: { form: GrowUpsertPayload; patch: (patch: Partial<GrowUpsertPayload>) => void; isAutoflower: boolean }) {
  return <div className="v1-form-grid"><V1Field label="Startdatum"><input type="date" value={form.startDate} onChange={(event) => patch({ startDate: event.target.value })} /></V1Field><V1Field label="Startpunkt"><select value={form.entryPoint} onChange={(event) => patch({ entryPoint: event.target.value as GrowEntryPoint })}>{entryPoints.map((value) => <option key={value} value={value}>{formatEntryPoint(value)}</option>)}</select></V1Field><V1Field label="Tage in Phase"><input type="number" min="0" value={form.daysAlreadyInPhase ?? ''} onChange={(event) => patch({ daysAlreadyInPhase: toNullableInt(event.target.value) })} /></V1Field>{isAutoflower ? <V1Field label="Tage seit Keimung"><input type="number" min="0" value={form.autoflowerDaysSinceGermination ?? ''} onChange={(event) => patch({ autoflowerDaysSinceGermination: toNullableInt(event.target.value) })} /></V1Field> : <V1Field label="Flipdatum / geplant"><input type="date" value={form.flipDate ?? ''} onChange={(event) => patch({ flipDate: event.target.value || null })} /></V1Field>}<V1Field label="Status"><select value={form.status} onChange={(event) => patch({ status: event.target.value as GrowStatus })}>{statuses.map((value) => <option key={value} value={value}>{formatStatus(value)}</option>)}</select></V1Field><V1Field label="Wasser"><select value={form.waterSource} onChange={(event) => patch({ waterSource: event.target.value as WaterSource })}>{waterSources.map((value) => <option key={value} value={value}>{value}</option>)}</select></V1Field><V1Field label="Propagation"><select value={form.propagationMedium ?? ''} onChange={(event) => patch({ propagationMedium: event.target.value as PropagationMedium })}><option value="">offen</option>{propagationMedia.map((value) => <option key={value} value={value}>{value}</option>)}</select></V1Field></div>
}

function ProgramStep({ programs, selected, custom, setCustom, patch }: { programs: NutrientProgramDto[]; selected: string; custom: string; setCustom: (value: string) => void; patch: (patch: Partial<GrowUpsertPayload>) => void }) {
  return <div className="v1-choice-stack"><div className="v1-choice-grid">{programs.map((program) => <button key={program.key} type="button" className={(selected === program.name || selected === program.key) ? 'v1-choice active' : 'v1-choice'} onClick={() => patch({ nutrients: program.name })}><strong>{program.name}</strong><span>{program.manufacturer} · {program.category}</span></button>)}<button type="button" className={selected === custom && custom ? 'v1-choice active' : 'v1-choice'} onClick={() => patch({ nutrients: custom || 'Eigenes / unbekannt' })}><strong>Eigenes / unbekannt</strong><span>Ohne automatische Programmlogik</span></button></div><V1Field label="Eigenes Programm"><input value={custom} onChange={(event) => { setCustom(event.target.value); patch({ nutrients: event.target.value || null }) }} placeholder="z. B. eigene Mischung" /></V1Field></div>
}

function ReviewStep({ form, tent, hydro, program, customProgram }: { form: GrowUpsertPayload; tent: TentDto | null; hydro: HydroSetupDto | null; program: NutrientProgramDto | null; customProgram: string }) {
  return <div className="v1-review-layout"><V1Card><div className="v1-info-grid"><Info label="Grow" value={form.name || '–'} /><Info label="Sorte" value={form.strain ?? '–'} /><Info label="Zelt" value={tent?.name ?? '–'} /><Info label="Hydro" value={hydro?.name ?? '–'} /><Info label="Start" value={form.startDate} /><Info label="Flip" value={form.flipDate ?? 'offen'} /><Info label="Programm" value={program?.name ?? customProgram ?? form.nutrients ?? '–'} /><Info label="Status" value={formatStatus(form.status)} /></div></V1Card>{program && <V1Card><span className="v1-card-kicker">Programm</span><h2>{program.name}</h2><p>{program.summary}</p><div className="v1-chip-row"><span>{program.phGuidance}</span><span>{program.ecGuidance}</span></div></V1Card>}</div>
}

function Info({ label, value }: { label: string; value: string }) { return <div className="v1-info"><span>{label}</span><strong>{value}</strong></div> }
function validateStep(step: number, form: GrowUpsertPayload, hydro: HydroSetupDto | null) { if (step === 1 && !form.name.trim()) return 'Bitte Grow-Namen eintragen.'; if (step === 2 && !form.tentId) return 'Bitte Zelt wählen.'; if (step === 3 && !hydro) return 'Bitte Hydro-Setup wählen.'; if (step === 4 && !form.startDate) return 'Bitte Startdatum setzen.'; return null }
function normalizePayload(form: GrowUpsertPayload, hydro: HydroSetupDto | null, programs: NutrientProgramDto[], customProgram: string): GrowUpsertPayload { const selectedProgram = programs.find((program) => program.name === form.nutrients || program.key === form.nutrients); const nutrients = selectedProgram?.name ?? toNullableString(customProgram) ?? toNullableString(form.nutrients); return { ...form, name: form.name.trim(), strain: toNullableString(form.strain), breeder: toNullableString(form.breeder), cloneSource: form.startMaterial === 'Clone' ? toNullableString(form.cloneSource) : null, germinationMethod: form.startMaterial === 'Seed' ? form.germinationMethod : null, systemId: hydro?.id ?? form.systemId, hydroStyle: hydro ? toSelectableHydroStyle(hydro.hydroStyle) : form.hydroStyle, reservoirSize: hydro ? formatLiters(hydro.totalVolumeLiters ?? hydro.reservoirLiters) : form.reservoirSize, containerSize: hydro ? formatLiters(hydro.potSizeLiters) : form.containerSize, hasChiller: hydro?.hasChiller ?? form.hasChiller, nutrients, notes: toNullableString(form.notes), flipDate: toNullableString(form.flipDate) } }
function mapGrowToPayload(grow: GrowDetail): GrowUpsertPayload { return { templateId: null, name: grow.name, tentId: grow.tentId, systemId: grow.systemId, setupId: grow.setupId, strain: grow.strain, breeder: grow.breeder, seedType: grow.seedType, startMaterial: grow.startMaterial, germinationMethod: grow.germinationMethod, cloneSource: grow.cloneSource, cloneIsRooted: grow.cloneIsRooted, phenoNumber: grow.phenoNumber, breederFlowerWeeksMin: grow.breederFlowerWeeksMin, breederFlowerWeeksMax: grow.breederFlowerWeeksMax, hydroStyle: grow.hydroStyle, plantCount: grow.plantCount, reservoirSize: grow.reservoirSize, containerSize: grow.containerSize, propagationMedium: grow.propagationMedium, light: grow.light, hasChiller: grow.hasChiller, waterSource: grow.waterSource, nutrients: grow.nutrients, startDate: grow.startDate.slice(0, 10), entryPoint: grow.entryPoint, daysAlreadyInPhase: grow.daysAlreadyInPhase, autoflowerDaysSinceGermination: grow.autoflowerDaysSinceGermination, flipDate: grow.flipDate ? grow.flipDate.slice(0, 10) : null, notes: grow.notes, status: grow.status, environment: grow.environment } }
function toSelectableHydroStyle(value: HydroStyle): SelectableHydroStyle { return value === 'DWC' ? 'DWC' : 'RDWC' }
function formatGermination(value: GerminationMethod) { return value === 'PaperTowel' ? 'Küchenpapier' : value === 'Rockwool' ? 'Steinwolle' : value === 'RapidRooter' ? 'Rapid Rooter' : 'Direkt im System' }
function formatEntryPoint(value: GrowEntryPoint) { return value === 'Germination' ? 'Keimung' : value === 'Seedling' ? 'Seedling' : value === 'Veg' ? 'Vegetation' : value === 'Flower' ? 'Blüte' : 'Flush' }
function formatStatus(value: GrowStatus) { return value === 'Planning' ? 'Planung' : value === 'Running' ? 'Läuft' : value === 'Completed' ? 'Abgeschlossen' : 'Abgebrochen' }
function formatTentType(value: string) { return value === 'Production' ? 'Blüte / Run' : value === 'Mother' ? 'Mutter' : value === 'Propagation' ? 'Anzucht' : value === 'Quarantine' ? 'Quarantäne' : 'Mehrzweck' }
function formatApiError(caught: unknown, fallback: string) { return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback }

export default GrowSetupPage
