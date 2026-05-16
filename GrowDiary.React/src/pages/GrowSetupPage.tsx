import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type {
  GerminationMethod,
  GrowDetail,
  GrowEntryPoint,
  GrowEnvironment,
  GrowStatus,
  GrowUpsertPayload,
  HydroSetupDto,
  HydroStyle,
  KnowledgeOverviewDto,
  NutrientProgramDto,
  NutrientProgramStageDto,
  PropagationMedium,
  SeedType,
  SelectableHydroStyle,
  StartMaterial,
  TentDto,
  WaterSource,
} from '../types'
import {
  V1Alert,
  V1Badge,
  V1Button,
  V1Card,
  V1Empty,
  V1Field,
  V1LinkButton,
  V1Page,
  V1Section,
  V1Stat,
  V1Wizard,
  formatDateShort,
  formatLiters,
  toNullableInt,
  toNullableString,
} from '../components/v1'
import { classNames } from '../utils'

const steps = ['Run', 'Zelt', 'Hydro', 'Zeit', 'Programm', 'Prüfen']

const seedTypes: SeedType[] = ['Feminized', 'Autoflower', 'Regular']
const startMaterials: StartMaterial[] = ['Seed', 'Clone']
const germinationMethods: GerminationMethod[] = ['PaperTowel', 'Rockwool', 'RapidRooter', 'DirectInSystem']
const entryPoints: GrowEntryPoint[] = ['Germination', 'Seedling', 'Veg', 'Flower', 'Flush']
const statuses: GrowStatus[] = ['Planning', 'Running', 'Completed', 'Aborted']
const waterSources: WaterSource[] = ['Tap', 'RO', 'Mixed']
const propagationMedia: PropagationMedium[] = ['Rockwool', 'Hydroton', 'RapidRooter', 'Neoprene']

type GrowPatch = (patch: Partial<GrowUpsertPayload>) => void

function emptyForm(): GrowUpsertPayload {
  return {
    templateId: null,
    name: '',
    tentId: null,
    systemId: null,
    setupId: null,
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
    propagationMedium: 'Rockwool',
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
    environment: 'Indoor' as GrowEnvironment,
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
          apiFetch<HydroSetupDto[]>('/api/hydro-setups', { signal: controller.signal }),
          apiFetch<KnowledgeOverviewDto>('/api/knowledge', { signal: controller.signal }),
          isEditing && growId ? apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal: controller.signal }) : Promise.resolve(null),
        ])

        const activeHydroSetups = hydroData.filter((setup) => setup.status === 'Active')
        setTents(tentData)
        setHydroSetups(activeHydroSetups)
        setPrograms(knowledge.programs ?? [])

        if (grow) {
          const payload = mapGrowToPayload(grow)
          setForm(payload)

          const knownProgram = (knowledge.programs ?? []).some((program) => program.name === grow.nutrients || program.key === grow.nutrients)
          if (grow.nutrients && !knownProgram) {
            setCustomProgram(grow.nutrients)
          }
        }
      } catch (caught) {
        if (!controller.signal.aborted) {
          setError(formatApiError(caught, 'Grow-Wizard konnte nicht geladen werden.'))
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void load()
    return () => controller.abort()
  }, [growId, isEditing])

  const selectedTent = tents.find((tent) => tent.id === form.tentId) ?? null
  const availableHydroSetups = useMemo(
    () => hydroSetups.filter((setup) => !form.tentId || setup.tentId === form.tentId),
    [form.tentId, hydroSetups],
  )
  const selectedHydro = hydroSetups.find((setup) => setup.id === form.systemId) ?? null
  const selectedProgram = programs.find((program) => program.name === form.nutrients || program.key === form.nutrients) ?? null
  const selectedStage = useMemo(() => findProgramStage(selectedProgram, form.entryPoint), [form.entryPoint, selectedProgram])
  const isAutoflower = form.seedType === 'Autoflower'

  function patch(patchValue: Partial<GrowUpsertPayload>) {
    setForm((current) => ({ ...current, ...patchValue }))
  }

  function selectTent(id: number) {
    setForm((current) => {
      const currentHydroStillFits = hydroSetups.some((setup) => setup.id === current.systemId && setup.tentId === id)
      return {
        ...current,
        tentId: id,
        systemId: currentHydroStillFits ? current.systemId : null,
      }
    })
  }

  function selectHydro(setup: HydroSetupDto) {
    patch({
      systemId: setup.id,
      hydroStyle: toSelectableHydroStyle(setup.hydroStyle),
      reservoirSize: formatLiters(setup.totalVolumeLiters ?? setup.reservoirLiters),
      containerSize: formatLiters(setup.potSizeLiters),
      hasChiller: setup.hasChiller,
    })
  }

  function goTo(next: number) {
    if (next > step) {
      const message = validateStep(step, form, selectedHydro)
      if (message) {
        setError(message)
        return
      }
    }

    setError(null)
    setStep(next)
  }

  async function saveGrow() {
    for (let current = 1; current < steps.length; current += 1) {
      const message = validateStep(current, form, selectedHydro)
      if (message) {
        setStep(current)
        setError(message)
        return
      }
    }

    if (step !== steps.length) {
      setStep(steps.length)
      setError('Gespeichert wird erst im Schritt Prüfen.')
      return
    }

    setSaving(true)
    setError(null)

    try {
      const payload = normalizePayload(form, selectedHydro, programs, customProgram)
      const saved = await apiFetch<GrowDetail>(isEditing && growId ? `/api/grows/${growId}` : '/api/grows', {
        method: isEditing ? 'PUT' : 'POST',
        body: JSON.stringify(payload),
      })
      navigate(`/grows/${saved.id}`)
    } catch (caught) {
      setError(formatApiError(caught, 'Grow konnte nicht gespeichert werden.'))
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return (
      <V1Page eyebrow="Grow" title={isEditing ? 'Grow bearbeiten' : 'Grow starten'}>
        <V1Empty title="Lade Wizard..." />
      </V1Page>
    )
  }

  return (
    <V1Page
      eyebrow="Grow"
      title={isEditing ? 'Grow bearbeiten' : 'Grow starten'}
      className="grow-wizard-page"
      action={<Link className="v1-button is-ghost" to={isEditing && growId ? `/grows/${growId}` : '/'}>Zurück</Link>}
    >
      {error && <V1Alert message={error} tone="warn" />}

      <V1Wizard steps={steps} currentStep={step} onStep={goTo} />

      <div className="grow-wizard-shell">
        <aside className="grow-wizard-context" aria-label="Grow-Zusammenfassung">
          <SummaryPanel form={form} tent={selectedTent} hydro={selectedHydro} program={selectedProgram} customProgram={customProgram} stage={selectedStage} />
        </aside>

        <div className="grow-wizard-main">
          {step === 1 && <RunStep form={form} patch={patch} />}
          {step === 2 && <TentStep tents={tents} selectedId={form.tentId} onSelect={selectTent} />}
          {step === 3 && <HydroStep setups={availableHydroSetups} selectedId={form.systemId} onSelect={selectHydro} tent={selectedTent} />}
          {step === 4 && <TimeStep form={form} patch={patch} isAutoflower={isAutoflower} />}
          {step === 5 && <ProgramStep programs={programs} selected={form.nutrients ?? ''} custom={customProgram} setCustom={setCustomProgram} patch={patch} entryPoint={form.entryPoint} />}
          {step === 6 && <ReviewStep form={form} tent={selectedTent} hydro={selectedHydro} program={selectedProgram} customProgram={customProgram} stage={selectedStage} />}
        </div>
      </div>

      <div className="v1-form-actions sticky-actions">
        <V1Button variant="ghost" onClick={() => (step === 1 ? navigate('/') : setStep((current) => Math.max(1, current - 1)))}>
          {step === 1 ? 'Abbrechen' : 'Zurück'}
        </V1Button>
        {step < steps.length ? (
          <V1Button variant="primary" onClick={() => goTo(step + 1)}>Weiter</V1Button>
        ) : (
          <V1Button variant="primary" disabled={saving} onClick={() => void saveGrow()}>
            {saving ? 'Speichert...' : isEditing ? 'Speichern' : 'Grow starten'}
          </V1Button>
        )}
      </div>
    </V1Page>
  )
}

function RunStep({ form, patch }: { form: GrowUpsertPayload; patch: GrowPatch }) {
  return (
    <V1Section title="Run">
      <div className="grow-step-lead">
        <h2>Was läuft?</h2>
        <p>Name, Genetik und Pflanzenzahl.</p>
      </div>

      <div className="v1-form-grid grow-form-grid">
        <V1Field label="Grow-Name" wide>
          <input value={form.name} onChange={(event) => patch({ name: event.target.value })} placeholder="Purple Lemonade RDWC" />
        </V1Field>

        <V1Field label="Sorte">
          <input value={form.strain ?? ''} onChange={(event) => patch({ strain: event.target.value })} placeholder="Purple Lemonade" />
        </V1Field>

        <V1Field label="Breeder">
          <input value={form.breeder ?? ''} onChange={(event) => patch({ breeder: event.target.value })} placeholder="Fast Buds, Ethos, ..." />
        </V1Field>

        <V1Field label="Pflanzen">
          <input type="number" min="1" value={form.plantCount ?? ''} onChange={(event) => patch({ plantCount: toNullableInt(event.target.value) })} />
        </V1Field>

        <V1Field label="Seed Type">
          <select value={form.seedType} onChange={(event) => patch({ seedType: event.target.value as SeedType, flipDate: event.target.value === 'Autoflower' ? null : form.flipDate })}>
            {seedTypes.map((value) => <option key={value} value={value}>{formatSeedType(value)}</option>)}
          </select>
        </V1Field>

        <V1Field label="Startmaterial">
          <select value={form.startMaterial} onChange={(event) => patch({ startMaterial: event.target.value as StartMaterial })}>
            {startMaterials.map((value) => <option key={value} value={value}>{value === 'Seed' ? 'Samen' : 'Steckling'}</option>)}
          </select>
        </V1Field>

        {form.startMaterial === 'Seed' ? (
          <V1Field label="Keimmethode">
            <select value={form.germinationMethod ?? 'PaperTowel'} onChange={(event) => patch({ germinationMethod: event.target.value as GerminationMethod })}>
              {germinationMethods.map((value) => <option key={value} value={value}>{formatGermination(value)}</option>)}
            </select>
          </V1Field>
        ) : (
          <>
            <V1Field label="Stecklingsquelle">
              <input value={form.cloneSource ?? ''} onChange={(event) => patch({ cloneSource: event.target.value })} placeholder="Mutterpflanze / Batch" />
            </V1Field>
            <label className="v1-switch grow-inline-switch">
              <input type="checkbox" checked={form.cloneIsRooted} onChange={(event) => patch({ cloneIsRooted: event.target.checked })} />
              <span><strong>bewurzelt</strong><small>Clone ist bereits angewurzelt</small></span>
            </label>
          </>
        )}

        <V1Field label="Pheno #">
          <input type="number" min="1" value={form.phenoNumber ?? ''} onChange={(event) => patch({ phenoNumber: toNullableInt(event.target.value) })} />
        </V1Field>

        <V1Field label="Blüte laut Breeder">
          <div className="grow-inline-inputs">
            <input type="number" min="1" value={form.breederFlowerWeeksMin ?? ''} onChange={(event) => patch({ breederFlowerWeeksMin: toNullableInt(event.target.value) })} placeholder="min." />
            <input type="number" min="1" value={form.breederFlowerWeeksMax ?? ''} onChange={(event) => patch({ breederFlowerWeeksMax: toNullableInt(event.target.value) })} placeholder="max." />
          </div>
        </V1Field>
      </div>
    </V1Section>
  )
}

function TentStep({ tents, selectedId, onSelect }: { tents: TentDto[]; selectedId: number | null; onSelect: (id: number) => void }) {
  if (tents.length === 0) {
    return <V1Empty title="Kein Zelt angelegt" text="Lege zuerst den physischen Raum an." action={<V1LinkButton to="/zelte" variant="primary">Zelt anlegen</V1LinkButton>} />
  }

  return (
    <V1Section title="Zelt">
      <div className="grow-step-lead">
        <h2>Wo steht der Grow?</h2>
        <p>Das Zelt liefert Klima, Licht, Kamera und Sensor-Kontext.</p>
      </div>

      <div className="grow-select-grid">
        {tents.map((tent) => (
          <button type="button" key={tent.id} className={classNames('grow-select-card', selectedId === tent.id && 'active')} onClick={() => onSelect(tent.id)}>
            <span className="grow-card-topline">
              <strong>{tent.name}</strong>
              <V1Badge tone={tent.status === 'Active' ? 'ok' : 'neutral'}>{formatTentStatus(tent.status)}</V1Badge>
            </span>
            <span className="grow-card-meta">{formatTentType(tent.tentType)} · {formatTentSize(tent)}</span>
            <span className="grow-card-facts">
              <b>{tent.lightWatt ? `${tent.lightWatt} W` : 'Licht offen'}</b>
              <b>{tent.cameraEntityId ? 'Kamera' : 'keine Kamera'}</b>
              <b>{tent.activeSetupCount} Hydro</b>
            </span>
          </button>
        ))}
      </div>
    </V1Section>
  )
}

function HydroStep({ setups, selectedId, onSelect, tent }: { setups: HydroSetupDto[]; selectedId: number | null; onSelect: (setup: HydroSetupDto) => void; tent: TentDto | null }) {
  if (setups.length === 0) {
    return (
      <V1Empty
        title={tent ? 'Kein Hydro-Setup für dieses Zelt' : 'Kein Hydro-Setup gewählt'}
        text={tent ? 'Lege für dieses Zelt ein DWC/RDWC-System an.' : 'Wähle erst ein Zelt oder lege ein Hydro-Setup an.'}
        action={<V1LinkButton to="/hydro" variant="primary">Hydro anlegen</V1LinkButton>}
      />
    )
  }

  return (
    <V1Section title="Hydro">
      <div className="grow-step-lead">
        <h2>Welches System?</h2>
        <p>Reservoir, Volumen und Technik kommen aus dem Hydro-Setup.</p>
      </div>

      <div className="grow-select-grid">
        {setups.map((setup) => (
          <button type="button" key={setup.id} className={classNames('grow-select-card', selectedId === setup.id && 'active')} onClick={() => onSelect(setup)}>
            <span className="grow-card-topline">
              <strong>{setup.name}</strong>
              <V1Badge tone="accent">{setup.hydroStyle}</V1Badge>
            </span>
            <span className="grow-card-meta">{setup.tentName ?? 'ohne Zelt'} · {formatHydroLayout(setup.layoutType)}</span>
            <span className="grow-card-facts">
              <b>{setup.potCount ?? 1} Sites</b>
              <b>{formatLiters(setup.totalVolumeLiters)}</b>
              <b>{setup.hasChiller ? 'Chiller' : 'ohne Chiller'}</b>
            </span>
          </button>
        ))}
      </div>
    </V1Section>
  )
}

function TimeStep({ form, patch, isAutoflower }: { form: GrowUpsertPayload; patch: GrowPatch; isAutoflower: boolean }) {
  const phaseLabel = form.entryPoint === 'Flower' ? 'Blüte läuft' : form.entryPoint === 'Veg' ? 'Vegetation' : formatEntryPoint(form.entryPoint)

  return (
    <V1Section title="Zeit">
      <div className="grow-step-lead">
        <h2>{phaseLabel}</h2>
        <p>Startpunkt, Phase und Flip bestimmen spätere Empfehlungen.</p>
      </div>

      <div className="v1-form-grid grow-form-grid">
        <V1Field label="Startdatum">
          <input type="date" value={form.startDate} onChange={(event) => patch({ startDate: event.target.value })} />
        </V1Field>

        <V1Field label="Startpunkt">
          <select value={form.entryPoint} onChange={(event) => patch({ entryPoint: event.target.value as GrowEntryPoint })}>
            {entryPoints.map((value) => <option key={value} value={value}>{formatEntryPoint(value)}</option>)}
          </select>
        </V1Field>

        <V1Field label="Tage in Phase">
          <input type="number" min="0" value={form.daysAlreadyInPhase ?? ''} onChange={(event) => patch({ daysAlreadyInPhase: toNullableInt(event.target.value) })} />
        </V1Field>

        {isAutoflower ? (
          <V1Field label="Tage seit Keimung">
            <input type="number" min="0" value={form.autoflowerDaysSinceGermination ?? ''} onChange={(event) => patch({ autoflowerDaysSinceGermination: toNullableInt(event.target.value), flipDate: null })} />
          </V1Field>
        ) : (
          <V1Field label="Flipdatum / geplant" hint="Kann auch vor dem Flip als Planung gesetzt werden.">
            <input type="date" value={form.flipDate ?? ''} onChange={(event) => patch({ flipDate: event.target.value || null })} />
          </V1Field>
        )}

        <V1Field label="Status">
          <select value={form.status} onChange={(event) => patch({ status: event.target.value as GrowStatus })}>
            {statuses.map((value) => <option key={value} value={value}>{formatStatus(value)}</option>)}
          </select>
        </V1Field>

        <V1Field label="Wasser">
          <select value={form.waterSource} onChange={(event) => patch({ waterSource: event.target.value as WaterSource })}>
            {waterSources.map((value) => <option key={value} value={value}>{formatWaterSource(value)}</option>)}
          </select>
        </V1Field>

        <V1Field label="Propagation">
          <select value={form.propagationMedium ?? ''} onChange={(event) => patch({ propagationMedium: (event.target.value || null) as PropagationMedium | null })}>
            <option value="">offen</option>
            {propagationMedia.map((value) => <option key={value} value={value}>{formatPropagation(value)}</option>)}
          </select>
        </V1Field>
      </div>
    </V1Section>
  )
}

function ProgramStep({
  programs,
  selected,
  custom,
  setCustom,
  patch,
  entryPoint,
}: {
  programs: NutrientProgramDto[]
  selected: string
  custom: string
  setCustom: (value: string) => void
  patch: GrowPatch
  entryPoint: GrowEntryPoint
}) {
  const selectedProgram = programs.find((program) => program.name === selected || program.key === selected) ?? null

  return (
    <V1Section title="Programm">
      <div className="grow-step-lead">
        <h2>Nährstoffprogramm</h2>
        <p>Die Auswahl wird zur Grundlage für Addback, SOPs und Empfehlungen.</p>
      </div>

      <div className="program-grid">
        {programs.map((program) => {
          const active = selected === program.name || selected === program.key
          const stage = findProgramStage(program, entryPoint)

          return (
            <button key={program.key} type="button" className={classNames('program-card', active && 'active')} onClick={() => { setCustom(''); patch({ nutrients: program.name }) }}>
              <span className="grow-card-topline">
                <strong>{program.name}</strong>
                <V1Badge tone="accent">{program.manufacturer}</V1Badge>
              </span>
              <span className="program-summary">{program.summary}</span>
              <span className="program-targets">
                <b>{program.phGuidance}</b>
                <b>{program.ecGuidance}</b>
              </span>
              {stage && <span className="program-stage">{stage.stage}: {stage.target}</span>}
            </button>
          )
        })}

        <button type="button" className={classNames('program-card', !selectedProgram && Boolean(selected) && 'active')} onClick={() => patch({ nutrients: custom || 'Eigenes / unbekannt' })}>
          <span className="grow-card-topline"><strong>Eigenes / unbekannt</strong><V1Badge tone="neutral">manuell</V1Badge></span>
          <span className="program-summary">Ohne automatische Programmlogik.</span>
          <span className="program-targets"><b>pH manuell</b><b>EC manuell</b></span>
        </button>
      </div>

      {!selectedProgram && (
        <div className="grow-custom-program">
          <V1Field label="Eigenes Programm">
            <input value={custom} onChange={(event) => { setCustom(event.target.value); patch({ nutrients: event.target.value || 'Eigenes / unbekannt' }) }} placeholder="z. B. eigene Mischung" />
          </V1Field>
        </div>
      )}
    </V1Section>
  )
}

function ReviewStep({
  form,
  tent,
  hydro,
  program,
  customProgram,
  stage,
}: {
  form: GrowUpsertPayload
  tent: TentDto | null
  hydro: HydroSetupDto | null
  program: NutrientProgramDto | null
  customProgram: string
  stage: NutrientProgramStageDto | null
}) {
  return (
    <V1Section title="Prüfen">
      <div className="grow-review-hero">
        <div>
          <span className="v1-card-kicker">bereit</span>
          <h2>{form.name || 'Neuer Grow'}</h2>
          <p>{form.strain || 'Sorte offen'} · {tent?.name ?? 'kein Zelt'} · {hydro?.name ?? 'kein Hydro'}</p>
        </div>
        <V1Badge tone={form.status === 'Running' ? 'ok' : 'neutral'}>{formatStatus(form.status)}</V1Badge>
      </div>

      <div className="grow-review-grid">
        <Info label="Grow" value={form.name || '–'} />
        <Info label="Sorte" value={form.strain ?? '–'} />
        <Info label="Zelt" value={tent?.name ?? '–'} />
        <Info label="Hydro" value={hydro?.name ?? '–'} />
        <Info label="Systemvolumen" value={hydro ? formatLiters(hydro.totalVolumeLiters) : form.reservoirSize ?? '–'} />
        <Info label="Start" value={formatDateShort(form.startDate)} />
        <Info label="Startpunkt" value={formatEntryPoint(form.entryPoint)} />
        <Info label="Flip" value={form.seedType === 'Autoflower' ? 'Autoflower' : form.flipDate ? formatDateShort(form.flipDate) : 'offen'} />
        <Info label="Programm" value={program?.name ?? customProgram ?? form.nutrients ?? '–'} />
        <Info label="Wasser" value={formatWaterSource(form.waterSource)} />
      </div>

      {program && (
        <V1Card className="program-review-card">
          <span className="v1-card-kicker">Programm</span>
          <h2>{program.name}</h2>
          <p>{program.summary}</p>
          <div className="v1-chip-row">
            <span>{program.phGuidance}</span>
            <span>{program.ecGuidance}</span>
            {stage && <span>{stage.stage}: {stage.dose}</span>}
          </div>
        </V1Card>
      )}
    </V1Section>
  )
}

function SummaryPanel({
  form,
  tent,
  hydro,
  program,
  customProgram,
  stage,
}: {
  form: GrowUpsertPayload
  tent: TentDto | null
  hydro: HydroSetupDto | null
  program: NutrientProgramDto | null
  customProgram: string
  stage: NutrientProgramStageDto | null
}) {
  return (
    <V1Card className="grow-summary-card">
      <span className="v1-card-kicker">Grow-Basis</span>
      <h2>{form.name || 'Neuer Grow'}</h2>
      <div className="grow-summary-list">
        <span><b>Sorte</b>{form.strain || 'offen'}</span>
        <span><b>Zelt</b>{tent?.name ?? 'offen'}</span>
        <span><b>Hydro</b>{hydro?.name ?? 'offen'}</span>
        <span><b>Start</b>{form.startDate ? formatDateShort(form.startDate) : 'offen'}</span>
        <span><b>Flip</b>{form.seedType === 'Autoflower' ? 'Autoflower' : form.flipDate ? formatDateShort(form.flipDate) : 'offen'}</span>
        <span><b>Programm</b>{program?.name ?? customProgram ?? form.nutrients ?? 'offen'}</span>
      </div>

      <div className="grow-summary-stats">
        <V1Stat label="Volumen" value={hydro ? formatLiters(hydro.totalVolumeLiters) : '–'} />
        <V1Stat label="Sites" value={hydro?.potCount ?? form.plantCount ?? '–'} />
      </div>

      {program && (
        <div className="grow-program-mini">
          <strong>{program.manufacturer}</strong>
          <span>{program.phGuidance}</span>
          <span>{program.ecGuidance}</span>
          {stage && <span>{stage.stage}: {stage.target}</span>}
        </div>
      )}
    </V1Card>
  )
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="v1-info">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function validateStep(step: number, form: GrowUpsertPayload, hydro: HydroSetupDto | null) {
  if (step === 1 && !form.name.trim()) return 'Bitte Grow-Namen eintragen.'
  if (step === 2 && !form.tentId) return 'Bitte Zelt wählen.'
  if (step === 3 && !hydro) return 'Bitte Hydro-Setup wählen.'
  if (step === 4 && !form.startDate) return 'Bitte Startdatum setzen.'
  if (step === 5 && !toNullableString(form.nutrients)) return 'Bitte Nährstoffprogramm wählen oder eigenes Programm eintragen.'
  return null
}

function normalizePayload(form: GrowUpsertPayload, hydro: HydroSetupDto | null, programs: NutrientProgramDto[], customProgram: string): GrowUpsertPayload {
  const selectedProgram = programs.find((program) => program.name === form.nutrients || program.key === form.nutrients)
  const nutrients = selectedProgram?.name ?? toNullableString(customProgram) ?? toNullableString(form.nutrients)

  return {
    ...form,
    name: form.name.trim(),
    strain: toNullableString(form.strain),
    breeder: toNullableString(form.breeder),
    cloneSource: form.startMaterial === 'Clone' ? toNullableString(form.cloneSource) : null,
    germinationMethod: form.startMaterial === 'Seed' ? form.germinationMethod : null,
    systemId: hydro?.id ?? form.systemId,
    hydroStyle: hydro ? toSelectableHydroStyle(hydro.hydroStyle) : form.hydroStyle,
    reservoirSize: hydro ? formatLiters(hydro.totalVolumeLiters ?? hydro.reservoirLiters) : form.reservoirSize,
    containerSize: hydro ? formatLiters(hydro.potSizeLiters) : form.containerSize,
    hasChiller: hydro?.hasChiller ?? form.hasChiller,
    nutrients,
    notes: toNullableString(form.notes),
    flipDate: form.seedType === 'Autoflower' ? null : toNullableString(form.flipDate),
  }
}

function mapGrowToPayload(grow: GrowDetail): GrowUpsertPayload {
  return {
    templateId: null,
    name: grow.name,
    tentId: grow.tentId,
    systemId: grow.systemId,
    setupId: grow.setupId,
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

function findProgramStage(program: NutrientProgramDto | null, entryPoint: GrowEntryPoint): NutrientProgramStageDto | null {
  if (!program) return null

  const wanted = entryPoint === 'Germination' || entryPoint === 'Seedling' ? ['seedling', 'clone', 'veg', 'vegetation'] :
    entryPoint === 'Veg' ? ['veg', 'vegetation'] :
      entryPoint === 'Flower' ? ['flower', 'bloom', 'early flower'] :
        entryPoint === 'Flush' ? ['flush', 'finish'] : []

  return program.stages.find((stage) => wanted.some((needle) => stage.stage.toLowerCase().includes(needle))) ?? program.stages[0] ?? null
}

function toSelectableHydroStyle(value: HydroStyle): SelectableHydroStyle {
  return value === 'DWC' ? 'DWC' : 'RDWC'
}

function formatGermination(value: GerminationMethod) {
  return value === 'PaperTowel' ? 'Küchenpapier' : value === 'Rockwool' ? 'Steinwolle' : value === 'RapidRooter' ? 'Rapid Rooter' : 'Direkt im System'
}

function formatEntryPoint(value: GrowEntryPoint) {
  return value === 'Germination' ? 'Keimung' : value === 'Seedling' ? 'Seedling' : value === 'Veg' ? 'Vegetation' : value === 'Flower' ? 'Blüte' : 'Flush'
}

function formatSeedType(value: SeedType) {
  return value === 'Feminized' ? 'Feminisiert' : value === 'Autoflower' ? 'Autoflower' : 'Regular'
}

function formatStatus(value: GrowStatus) {
  return value === 'Planning' ? 'Planung' : value === 'Running' ? 'Läuft' : value === 'Completed' ? 'Abgeschlossen' : 'Abgebrochen'
}

function formatTentStatus(value: string) {
  return value === 'Active' ? 'aktiv' : value === 'Archived' ? 'archiviert' : value
}

function formatTentType(value: string) {
  return value === 'Production' ? 'Blüte / Run' : value === 'Mother' ? 'Mutter' : value === 'Propagation' ? 'Anzucht' : value === 'Quarantine' ? 'Quarantäne' : 'Mehrzweck'
}

function formatTentSize(tent: TentDto) {
  if (!tent.widthCm && !tent.depthCm && !tent.tentHeightCm) return 'Größe offen'
  return `${tent.widthCm ?? '?'}×${tent.depthCm ?? '?'}×${tent.tentHeightCm ?? '?'} cm`
}

function formatHydroLayout(value: string) {
  return value === 'SingleBucket' ? 'Einzeleimer' : value === 'Grid2x2' ? '2×2' : value === 'Grid2x3' ? '2×3' : value === 'Grid2x4' ? '2×4' : value === 'Inline' ? 'Reihe' : 'Custom'
}

function formatWaterSource(value: WaterSource) {
  return value === 'RO' ? 'RO/VE' : value === 'Tap' ? 'Leitungswasser' : 'Gemischt'
}

function formatPropagation(value: PropagationMedium) {
  return value === 'Rockwool' ? 'Steinwolle' : value === 'Hydroton' ? 'Blähton' : value === 'RapidRooter' ? 'Rapid Rooter' : 'Neopren'
}

function formatApiError(caught: unknown, fallback: string) {
  return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback
}

export default GrowSetupPage
