import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowDetail, GrowEntryPoint, GrowStatus, GrowUpsertPayload, HydroSetupDto, KnowledgeOverviewDto, NutrientProgramDto, SeedType, StartMaterial, TentDto } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1LinkButton, V1Page, V1Section, V1Wizard } from '../components/v1'
import { formatDateShort, formatLiters, toNullableInt } from '../components/v1-utils'
import { classNames } from '../utils'

const steps = ['Run', 'Zelt', 'Hydro', 'Zeit', 'Programm', 'Prüfen']
const entryPoints: GrowEntryPoint[] = ['Germination', 'Seedling', 'Veg', 'Flower', 'Flush']
const statuses: GrowStatus[] = ['Planning', 'Running', 'Completed', 'Aborted']
const seedTypes: SeedType[] = ['Feminized', 'Autoflower', 'Regular']
const startMaterials: StartMaterial[] = ['Seed', 'Clone']

function emptyForm(): GrowUpsertPayload {
  return {
    templateId: null, name: '', tentId: null, systemId: null, setupId: null, strain: null, breeder: null, seedType: 'Feminized', startMaterial: 'Seed', germinationMethod: 'PaperTowel',
    cloneSource: null, cloneIsRooted: false, phenoNumber: null, breederFlowerWeeksMin: null, breederFlowerWeeksMax: null, hydroStyle: 'RDWC', plantCount: null, reservoirSize: null,
    containerSize: null, propagationMedium: 'Rockwool', light: null, hasChiller: false, waterSource: 'RO', nutrients: null, startDate: new Date().toISOString().slice(0, 10),
    entryPoint: 'Germination', daysAlreadyInPhase: null, autoflowerDaysSinceGermination: null, flipDate: null, notes: null, status: 'Planning', environment: 'Indoor',
  }
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
          apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true', { signal: controller.signal }),
          apiFetch<KnowledgeOverviewDto>('/api/knowledge', { signal: controller.signal }),
          isEditing && growId ? apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal: controller.signal }) : Promise.resolve(null),
        ])
        if (controller.signal.aborted) return
        setTents(tentData)
        setHydroSetups(hydroData.filter((setup) => setup.status === 'Active'))
        setPrograms(knowledge.programs ?? [])
        if (grow) setForm({ ...emptyForm(), name: grow.name, tentId: grow.tentId, systemId: grow.systemId, setupId: grow.setupId, strain: grow.strain, breeder: grow.breeder, seedType: grow.seedType, startMaterial: grow.startMaterial, hydroStyle: grow.hydroStyle, plantCount: grow.plantCount, reservoirSize: grow.reservoirSize, containerSize: grow.containerSize, light: grow.light, hasChiller: grow.hasChiller, waterSource: grow.waterSource, nutrients: grow.nutrients, startDate: grow.startDate, entryPoint: grow.entryPoint, daysAlreadyInPhase: grow.daysAlreadyInPhase, autoflowerDaysSinceGermination: grow.autoflowerDaysSinceGermination, flipDate: grow.flipDate, notes: grow.notes, status: grow.status, environment: grow.environment, germinationMethod: grow.germinationMethod, propagationMedium: grow.propagationMedium, cloneSource: grow.cloneSource, cloneIsRooted: grow.cloneIsRooted, phenoNumber: grow.phenoNumber, breederFlowerWeeksMin: grow.breederFlowerWeeksMin, breederFlowerWeeksMax: grow.breederFlowerWeeksMax })
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
  const exactHydro = useMemo(() => hydroSetups.filter((setup) => form.tentId ? setup.tentId === form.tentId : true), [form.tentId, hydroSetups])
  const availableHydro = exactHydro.length > 0 ? exactHydro : hydroSetups
  const selectedHydro = hydroSetups.find((setup) => setup.id === form.systemId) ?? null
  const selectedProgram = programs.find((program) => program.name === form.nutrients || program.key === form.nutrients) ?? null

  function patch(value: Partial<GrowUpsertPayload>) { setForm((current) => ({ ...current, ...value })) }
  function selectTent(id: number) { setForm((current) => ({ ...current, tentId: id, systemId: hydroSetups.some((setup) => setup.id === current.systemId && setup.tentId === id) ? current.systemId : null, setupId: null })) }
  function selectHydro(setup: HydroSetupDto) { patch({ systemId: setup.id, setupId: null, hydroStyle: setup.hydroStyle, reservoirSize: formatLiters(setup.totalVolumeLiters ?? setup.reservoirLiters), containerSize: formatLiters(setup.potSizeLiters), hasChiller: setup.hasChiller }) }

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
      const payload = { ...form, nutrients: form.nutrients || customProgram || null, setupId: form.setupId ?? null }
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
    <V1Page eyebrow="Grow" title={isEditing ? 'Grow bearbeiten' : 'Grow starten'} className="grow-wizard-page" action={<Link className="v1-button is-ghost" to={isEditing && growId ? `/grows/${growId}` : '/'}>Zurück</Link>}>
      {error && <V1Alert message={error} tone="warn" />}
      <V1Wizard steps={steps} currentStep={step} onStep={goTo} />

      <div className="grow-wizard-shell">
        <aside className="grow-wizard-context"><Summary form={form} tent={selectedTent} hydro={selectedHydro} program={selectedProgram} custom={customProgram} /></aside>
        <div className="grow-wizard-main">
          {step === 1 && <RunStep form={form} patch={patch} />}
          {step === 2 && <TentStep tents={tents} selectedId={form.tentId} onSelect={selectTent} />}
          {step === 3 && <HydroStep setups={availableHydro} exactCount={exactHydro.length} selectedId={form.systemId ?? null} onSelect={selectHydro} tent={selectedTent} />}
          {step === 4 && <TimeStep form={form} patch={patch} />}
          {step === 5 && <ProgramStep programs={programs} selected={form.nutrients ?? ''} custom={customProgram} setCustom={setCustomProgram} patch={patch} />}
          {step === 6 && <ReviewStep form={form} tent={selectedTent} hydro={selectedHydro} program={selectedProgram} custom={customProgram} />}
        </div>
      </div>

      <div className="v1-form-actions sticky-actions">
        <V1Button variant="ghost" onClick={() => (step === 1 ? navigate('/') : setStep((current) => Math.max(1, current - 1)))}>{step === 1 ? 'Abbrechen' : 'Zurück'}</V1Button>
        {step < steps.length ? <V1Button variant="primary" onClick={() => goTo(step + 1)}>Weiter</V1Button> : <V1Button variant="primary" disabled={saving} onClick={() => void saveGrow()}>{saving ? 'Speichert...' : isEditing ? 'Speichern' : 'Grow starten'}</V1Button>}
      </div>
    </V1Page>
  )
}

function RunStep({ form, patch }: { form: GrowUpsertPayload; patch: (value: Partial<GrowUpsertPayload>) => void }) {
  return <V1Section title="Run"><div className="v1-form-grid grow-form-grid"><V1Field label="Grow-Name" wide><input value={form.name} onChange={(event) => patch({ name: event.target.value })} placeholder="Purple Lemonade RDWC" /></V1Field><V1Field label="Sorte"><input value={form.strain ?? ''} onChange={(event) => patch({ strain: event.target.value })} /></V1Field><V1Field label="Breeder"><input value={form.breeder ?? ''} onChange={(event) => patch({ breeder: event.target.value })} /></V1Field><V1Field label="Pflanzen"><input type="number" min="1" value={form.plantCount ?? ''} onChange={(event) => patch({ plantCount: toNullableInt(event.target.value) })} /></V1Field><V1Field label="Seed Type"><select value={form.seedType} onChange={(event) => patch({ seedType: event.target.value as SeedType })}>{seedTypes.map((value) => <option key={value} value={value}>{value}</option>)}</select></V1Field><V1Field label="Startmaterial"><select value={form.startMaterial} onChange={(event) => patch({ startMaterial: event.target.value as StartMaterial })}>{startMaterials.map((value) => <option key={value} value={value}>{value}</option>)}</select></V1Field></div></V1Section>
}

function TentStep({ tents, selectedId, onSelect }: { tents: TentDto[]; selectedId: number | null; onSelect: (id: number) => void }) {
  if (tents.length === 0) return <V1Empty title="Kein Zelt angelegt" action={<V1LinkButton to="/zelte/new" variant="primary">Zelt anlegen</V1LinkButton>} />
  return <V1Section title="Zelt"><div className="grow-select-grid">{tents.map((tent) => <button type="button" key={tent.id} className={classNames('grow-select-card', selectedId === tent.id && 'active')} onClick={() => onSelect(tent.id)}><span className="grow-card-topline"><strong>{tent.name}</strong><V1Badge tone={tent.status === 'Active' ? 'ok' : 'neutral'}>{tent.status}</V1Badge></span><span className="grow-card-meta">{tent.tentType} · {formatTentSize(tent)}</span><span className="grow-card-facts"><b>{tent.activeGrowCount} Grows</b><b>{tent.activeSetupCount} Setups</b></span></button>)}</div></V1Section>
}

function HydroStep({ setups, exactCount, selectedId, onSelect, tent }: { setups: HydroSetupDto[]; exactCount: number; selectedId: number | null; onSelect: (setup: HydroSetupDto) => void; tent: TentDto | null }) {
  if (setups.length === 0) return <V1Empty title="Kein Hydro-Setup vorhanden" text="Lege zuerst ein DWC/RDWC-System an." action={<V1LinkButton to="/hydro/new" variant="primary">Hydro anlegen</V1LinkButton>} />
  return <V1Section title="Hydro">{tent && exactCount === 0 && <V1Alert title="Kein Setup direkt am Zelt" message="Es gibt aktive Hydro-Setups, aber keines ist diesem Zelt zugeordnet. Du kannst eines wählen oder zuerst die Zeltzuordnung im Hydro-Setup korrigieren." tone="warn" />}<div className="grow-select-grid">{setups.map((setup) => <button type="button" key={setup.id} className={classNames('grow-select-card', selectedId === setup.id && 'active')} onClick={() => onSelect(setup)}><span className="grow-card-topline"><strong>{setup.name}</strong><V1Badge tone="accent">{setup.hydroStyle}</V1Badge></span><span className="grow-card-meta">{setup.tentName ?? 'ohne Zelt'} · {setup.layoutType}</span><span className="grow-card-facts"><b>{setup.potCount ?? 1} Sites</b><b>{formatLiters(setup.totalVolumeLiters)}</b><b>{setup.hasChiller ? 'Chiller' : 'ohne Chiller'}</b></span></button>)}</div></V1Section>
}

function TimeStep({ form, patch }: { form: GrowUpsertPayload; patch: (value: Partial<GrowUpsertPayload>) => void }) {
  return <V1Section title="Zeit"><div className="v1-form-grid grow-form-grid"><V1Field label="Startdatum"><input type="date" value={form.startDate} onChange={(event) => patch({ startDate: event.target.value })} /></V1Field><V1Field label="Startpunkt"><select value={form.entryPoint} onChange={(event) => patch({ entryPoint: event.target.value as GrowEntryPoint })}>{entryPoints.map((value) => <option key={value} value={value}>{value}</option>)}</select></V1Field><V1Field label="Tage in Phase"><input type="number" min="0" value={form.daysAlreadyInPhase ?? ''} onChange={(event) => patch({ daysAlreadyInPhase: toNullableInt(event.target.value) })} /></V1Field><V1Field label="Flipdatum"><input type="date" value={form.flipDate ?? ''} onChange={(event) => patch({ flipDate: event.target.value || null })} /></V1Field><V1Field label="Status"><select value={form.status} onChange={(event) => patch({ status: event.target.value as GrowStatus })}>{statuses.map((value) => <option key={value} value={value}>{value}</option>)}</select></V1Field></div></V1Section>
}

function ProgramStep({ programs, selected, custom, setCustom, patch }: { programs: NutrientProgramDto[]; selected: string; custom: string; setCustom: (value: string) => void; patch: (value: Partial<GrowUpsertPayload>) => void }) {
  return <V1Section title="Programm"><div className="program-grid">{programs.map((program) => <button key={program.key} type="button" className={classNames('program-card', (selected === program.name || selected === program.key) && 'active')} onClick={() => { setCustom(''); patch({ nutrients: program.name }) }}><span className="grow-card-topline"><strong>{program.name}</strong><V1Badge tone="accent">{program.manufacturer}</V1Badge></span><span className="program-summary">{program.summary}</span></button>)}</div><div className="grow-custom-program"><V1Field label="Eigenes Programm"><input value={custom} onChange={(event) => { setCustom(event.target.value); patch({ nutrients: event.target.value || null }) }} placeholder="Eigene Mischung" /></V1Field></div></V1Section>
}

function ReviewStep({ form, tent, hydro, program, custom }: { form: GrowUpsertPayload; tent: TentDto | null; hydro: HydroSetupDto | null; program: NutrientProgramDto | null; custom: string }) {
  return <V1Section title="Prüfen"><div className="grow-review-grid"><Info label="Grow" value={form.name || '–'} /><Info label="Zelt" value={tent?.name ?? '–'} /><Info label="Hydro" value={hydro?.name ?? '–'} /><Info label="Start" value={formatDateShort(form.startDate)} /><Info label="Programm" value={program?.name ?? custom ?? form.nutrients ?? '–'} /></div></V1Section>
}

function Summary({ form, tent, hydro, program, custom }: { form: GrowUpsertPayload; tent: TentDto | null; hydro: HydroSetupDto | null; program: NutrientProgramDto | null; custom: string }) {
  return <V1Card className="grow-summary-card"><span className="v1-card-kicker">Grow-Basis</span><h2>{form.name || 'Neuer Grow'}</h2><div className="grow-summary-list"><span><b>Zelt</b>{tent?.name ?? 'offen'}</span><span><b>Hydro</b>{hydro?.name ?? 'offen'}</span><span><b>Programm</b>{program?.name ?? custom ?? form.nutrients ?? 'offen'}</span></div></V1Card>
}

function Info({ label, value }: { label: string; value: string }) { return <div className="v1-info"><span>{label}</span><strong>{value}</strong></div> }
function formatTentSize(tent: TentDto) { return !tent.widthCm && !tent.depthCm && !tent.tentHeightCm ? 'Größe offen' : `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm` }
function validateStep(step: number, form: GrowUpsertPayload, hydro: HydroSetupDto | null) { if (step === 1 && !form.name.trim()) return 'Bitte Grow-Namen eingeben.'; if (step === 2 && !form.tentId) return 'Bitte Zelt wählen.'; if (step === 3 && !hydro) return 'Bitte Hydro-Setup wählen.'; return null }
function formatApiError(caught: unknown, fallback: string) { return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback }

export default GrowSetupPage
