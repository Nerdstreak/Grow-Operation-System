import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowDetail, GrowEntryPoint, GrowStatus, GrowSummary, GrowUpsertPayload, HydroSetupDto, KnowledgeOverviewDto, NutrientProgramDto, SeedType, StartMaterial, TentDto } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1LinkButton, V1Page, V1Section, V1Skeleton } from '../components/v1'
import { formatLiters, toNullableInt } from '../components/v1-utils'
import { classNames } from '../utils'
import { GrowPlanPanel } from '../features/grows/GrowPlanPanel'
import { buildTimeline, canCreate, checkPlan } from '../features/grows/grow-plan-model'
import '../features/grows/grows.css'

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
  // Fuer die Belegungspruefung: welche anderen Grows sitzen schon im Zelt.
  const [otherGrows, setOtherGrows] = useState<GrowSummary[]>([])
  const [form, setForm] = useState<GrowUpsertPayload>(() => emptyForm())
  const [customProgram, setCustomProgram] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const [tentData, hydroData, knowledge, grow, growsData] = await Promise.all([
          apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal }),
          apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true', { signal: controller.signal }),
          apiFetch<KnowledgeOverviewDto>('/api/knowledge', { signal: controller.signal }),
          isEditing && growId ? apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal: controller.signal }) : Promise.resolve(null),
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }).catch(() => []),
        ])
        if (controller.signal.aborted) return
        setTents(tentData)
        setHydroSetups(hydroData.filter((setup) => setup.status === 'Active'))
        setPrograms(knowledge.programs ?? [])
        setOtherGrows(growsData)
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

  async function saveGrow() {
    // Der Validator lief frueher pro Wizard-Schritt. Auf einer Seite gilt er
    // einmal fuer alles — und meldet den ersten Einwand, statt zu einem Schritt
    // zu springen, den es nicht mehr gibt.
    for (let current = 1; current <= 5; current += 1) {
      const message = validateStep(current, form, selectedHydro)
      if (message) { setError(message); return }
    }
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

  if (loading) return <V1Page eyebrow="Grow" title={isEditing ? 'Grow bearbeiten' : 'Grow starten'}><V1Skeleton rows={5} label="Lade Formular" /></V1Page>

  // Prüfung und Timeline rechnen bei jeder Eingabe mit — das ist der Grund,
  // warum die sechs Schritte zu einer Seite werden konnten.
  const planInput = {
    plantCount: form.plantCount ?? null,
    startDate: form.startDate ?? null,
    flipDate: form.flipDate ?? null,
    vegDays: null,
    flowerDays: null,
    tent: selectedTent,
    hydro: selectedHydro,
    otherGrows: otherGrows.filter((grow) => grow.id !== Number(growId)),
    programName: selectedProgram?.name ?? (customProgram.trim() || null),
  }
  const timeline = buildTimeline(planInput)
  const findings = checkPlan(planInput)
  const allowed = canCreate(findings)

  return (
    <V1Page eyebrow="Grow" title={isEditing ? 'Grow bearbeiten' : 'Grow starten'} className="grow-wizard-page" action={<Link className="v1-button is-ghost" to={isEditing && growId ? `/grows/${growId}` : '/grows'}>Zurück</Link>}>
      <div className="grow-wizard-mobile-surface" data-audit="grow-wizard">
      {error && <V1Alert message={error} tone="warn" />}
      {/* Eine Seite statt sechs Schritte: links eintragen, rechts sofort sehen.
          Ob die Pflanzenzahl zu den Sites passt oder das Zelt am Starttag belegt
          ist, merkte man vorher erst am Ende — oder gar nicht. */}
      <div className="grow-wizard-shell">
        <div className="grow-wizard-main">
          <RunStep form={form} patch={patch} />
          <TentStep tents={tents} selectedId={form.tentId} onSelect={selectTent} />
          <HydroStep setups={availableHydro} exactCount={exactHydro.length} selectedId={form.systemId ?? null} onSelect={selectHydro} tent={selectedTent} />
          <TimeStep form={form} patch={patch} />
          <ProgramStep programs={programs} selected={form.nutrients ?? ''} custom={customProgram} setCustom={setCustomProgram} patch={patch} />
        </div>

        <aside className="grow-wizard-context">
          <GrowPlanPanel
            timeline={timeline}
            findings={findings}
            summary={<Summary form={form} tent={selectedTent} hydro={selectedHydro} program={selectedProgram} custom={customProgram} />}
          />
        </aside>
      </div>

      <div className="v1-form-actions sticky-actions" data-audit="grow-wizard-actions">
        <V1Button variant="ghost" onClick={() => navigate(isEditing && growId ? `/grows/${growId}` : '/grows')}>Abbrechen</V1Button>
        <V1Button
          variant="primary"
          disabled={saving || !allowed}
          onClick={() => void saveGrow()}
        >
          {saving ? 'Speichert...' : isEditing ? 'Speichern' : 'Grow starten'}
        </V1Button>
      </div>
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
  return <V1Section title="Zeit"><div className="v1-form-grid grow-form-grid"><V1Field label="Startdatum"><input type="date" value={form.startDate} onChange={(event) => patch({ startDate: event.target.value })} /></V1Field><V1Field label="Startpunkt"><select value={form.entryPoint} onChange={(event) => patch({ entryPoint: event.target.value as GrowEntryPoint })}>{entryPoints.map((value) => <option key={value} value={value}>{value}</option>)}</select></V1Field><V1Field label="Tage in Phase"><input type="number" min="0" value={form.daysAlreadyInPhase ?? ''} onChange={(event) => patch({ daysAlreadyInPhase: toNullableInt(event.target.value) })} /></V1Field>{form.seedType !== 'Autoflower' && <V1Field label="Flipdatum"><input type="date" value={form.flipDate ?? ''} onChange={(event) => patch({ flipDate: event.target.value || null })} /></V1Field>}<V1Field label="Status"><select value={form.status} onChange={(event) => patch({ status: event.target.value as GrowStatus })}>{statuses.map((value) => <option key={value} value={value}>{value}</option>)}</select></V1Field></div></V1Section>
}

function ProgramStep({ programs, selected, custom, setCustom, patch }: { programs: NutrientProgramDto[]; selected: string; custom: string; setCustom: (value: string) => void; patch: (value: Partial<GrowUpsertPayload>) => void }) {
  return <V1Section title="Programm"><div className="program-grid">{programs.map((program) => <button key={program.key} type="button" className={classNames('program-card', (selected === program.name || selected === program.key) && 'active')} onClick={() => { setCustom(''); patch({ nutrients: program.name }) }}><span className="grow-card-topline"><strong>{program.name}</strong><V1Badge tone="accent">{program.manufacturer}</V1Badge></span><span className="program-summary">{program.summary}</span></button>)}</div><div className="grow-custom-program"><V1Field label="Eigenes Programm"><input value={custom} onChange={(event) => { setCustom(event.target.value); patch({ nutrients: event.target.value || null }) }} placeholder="Eigene Mischung" /></V1Field></div></V1Section>
}

function Summary({ form, tent, hydro, program, custom }: { form: GrowUpsertPayload; tent: TentDto | null; hydro: HydroSetupDto | null; program: NutrientProgramDto | null; custom: string }) {
  return <V1Card className="grow-summary-card"><span className="v1-card-kicker">Grow-Basis</span><h2>{form.name || 'Neuer Grow'}</h2><div className="grow-summary-list"><span><b>Zelt</b>{tent?.name ?? 'offen'}</span><span><b>Hydro</b>{hydro?.name ?? 'offen'}</span><span><b>Programm</b>{program?.name ?? custom ?? form.nutrients ?? 'offen'}</span></div></V1Card>
}

function formatTentSize(tent: TentDto) { return !tent.widthCm && !tent.depthCm && !tent.tentHeightCm ? 'Größe offen' : `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm` }
function validateStep(step: number, form: GrowUpsertPayload, hydro: HydroSetupDto | null) { if (step === 1 && !form.name.trim()) return 'Bitte Grow-Namen eingeben.'; if (step === 2 && !form.tentId) return 'Bitte Zelt wählen.'; if (step === 3 && !hydro) return 'Bitte Hydro-Setup wählen.'; return null }
function formatApiError(caught: unknown, fallback: string) { return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback }

export default GrowSetupPage
