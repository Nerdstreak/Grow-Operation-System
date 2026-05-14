import { useEffect, useMemo, useState } from 'react'
import type { Dispatch, SetStateAction } from 'react'
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
  PropagationMedium,
  SeedType,
  SelectableHydroStyle,
  SetupDto,
  StartMaterial,
  TentDto,
  TentType,
  WaterSource,
} from '../types'

const seedTypes: SeedType[] = ['Feminized', 'Autoflower', 'Regular']
const startMaterials: StartMaterial[] = ['Seed', 'Clone']
const germinationMethods: GerminationMethod[] = ['PaperTowel', 'Rockwool', 'RapidRooter', 'DirectInSystem']
const waterSources: WaterSource[] = ['Tap', 'RO', 'Mixed']
const entryPoints: GrowEntryPoint[] = ['Germination', 'Seedling', 'Veg', 'Flower', 'Flush']
const statuses: GrowStatus[] = ['Planning', 'Running', 'Completed', 'Aborted']
const environments: GrowEnvironment[] = ['Indoor', 'Outdoor', 'Greenhouse']
const propagationMedia: PropagationMedium[] = ['Rockwool', 'Hydroton', 'RapidRooter', 'Neoprene']
const growWizardSteps = ['Grow', 'Zelt', 'Hydro-Setup', 'Start', 'Prüfen'] as const

const emptyForm = (): GrowUpsertPayload => ({
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
  const [setups, setSetups] = useState<SetupDto[]>([])
  const [hydroSetups, setHydroSetups] = useState<HydroSetupDto[]>([])
  const [form, setForm] = useState<GrowUpsertPayload>(() => emptyForm())
  const [wizardStep, setWizardStep] = useState(1)
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
        const setupsPromise = apiFetch<SetupDto[]>('/api/setups', { signal: controller.signal })
        const hydroSetupsPromise = apiFetch<HydroSetupDto[]>('/api/hydro-setups', { signal: controller.signal })
        const growPromise = isEditing && growId
          ? apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal: controller.signal })
          : Promise.resolve(null)

        const [loadedTents, loadedSetups, loadedHydroSetups, grow] = await Promise.all([tentsPromise, setupsPromise, hydroSetupsPromise, growPromise])
        setTents([...loadedTents].sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name)))
        setSetups(loadedSetups)
        setHydroSetups([...loadedHydroSetups].sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name)))
        setForm(grow ? mapGrowToPayload(grow) : emptyForm())
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Grow-Wizard konnte nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [growId, isEditing])

  const selectedTent = useMemo(() => tents.find((tent) => tent.id === form.tentId) ?? null, [form.tentId, tents])
  const selectedHydroSetup = useMemo(() => hydroSetups.find((setup) => setup.id === form.systemId) ?? null, [form.systemId, hydroSetups])
  const activeHydroSetupsForTent = useMemo(
    () => hydroSetups.filter((setup) => setup.status === 'Active' && setup.tentId === form.tentId),
    [form.tentId, hydroSetups],
  )
  const legacySelectedHydroSetup = selectedHydroSetup && selectedHydroSetup.tentId !== form.tentId ? selectedHydroSetup : null
  const isAutoflower = form.seedType === 'Autoflower'
  const needsDaysInPhase = form.entryPoint !== 'Germination' && !isAutoflower
  const needsFlipDate = form.entryPoint === 'Flower' && !isAutoflower
  const pageTitle = isEditing ? 'Grow bearbeiten' : 'Neuen Grow starten'
  const productionSetupsForTent = getSelectableProductionSetupsForTent(setups, form.tentId)
  const archivedSelectedSetup = getArchivedSelectedSetup(setups, form.setupId ?? null, form.tentId)

  async function handleSaveGrow() {
    const invalid = getFirstInvalidWizardStep(form, selectedHydroSetup)
    if (invalid) {
      setWizardStep(invalid.step)
      setError(invalid.message)
      return
    }

    if (wizardStep !== growWizardSteps.length) {
      setWizardStep(growWizardSteps.length)
      setError('Prüfe die Zusammenfassung. Gespeichert wird erst mit „Grow starten“.')
      return
    }

    setSaving(true)
    setError(null)

    const payload = normalizePayload(form, selectedHydroSetup)

    try {
      const saved = await apiFetch<GrowDetail>(isEditing && growId ? `/api/grows/${growId}` : '/api/grows', {
        method: isEditing ? 'PUT' : 'POST',
        body: JSON.stringify(payload),
      })

      navigate(`/grows/${saved.id}`)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Grow konnte nicht gespeichert werden.')
    } finally {
      setSaving(false)
    }
  }

  function selectTent(tentId: number | null) {
    const setupId = isSetupValidForTent(setups, form.setupId ?? null, tentId) ? form.setupId ?? null : null
    const systemId = hydroSetups.some((setup) => setup.id === form.systemId && setup.tentId === tentId && setup.status === 'Active') ? form.systemId : null
    patchForm(setForm, { tentId, setupId, systemId })
  }

  function selectHydroSetup(setup: HydroSetupDto) {
    patchForm(setForm, {
      systemId: setup.id,
      hydroStyle: toSelectableHydroStyle(setup.hydroStyle),
      reservoirSize: formatLiters(setup.totalVolumeLiters ?? setup.reservoirLiters),
      containerSize: formatLiters(setup.potSizeLiters),
      hasChiller: setup.hasChiller,
    })
  }

  function goToStep(nextStep: number) {
    setError(null)
    if (nextStep > wizardStep) {
      const message = validateWizardStep(form, selectedHydroSetup, wizardStep)
      if (message) {
        setError(message)
        return
      }
    }
    setWizardStep(nextStep)
  }

  if (loading) {
    return (
      <>
        <div className="topbar"><span className="topbar-title">{isEditing ? 'Grow bearbeiten' : 'Neuer Grow'}</span></div>
        <div className="page-scroll"><div className="empty-hint">Lade...</div></div>
      </>
    )
  }

  return (
    <>
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to={isEditing && growId ? `/grows/${growId}` : '/'}>
            {isEditing ? 'Zurück zum Grow' : 'Zurück'}
          </Link>
          <span className="topbar-title">{pageTitle}</span>
        </div>
      </div>

      <div className="page-scroll">
        <div className="grow-wizard-page">
          {error ? (
            <div className="alert-bar">
              <div className="alert-dot" />
              <strong>Fehler</strong>
              <span>{error}</span>
            </div>
          ) : null}

          <header className="systems-header">
            <div>
              <div className="live-kicker">Grow / Run</div>
              <h1>{pageTitle}</h1>
            </div>
          </header>

          <div className="hydro-builder grow-wizard" role="group" aria-label="Grow-Wizard">
            <div className="hydro-stepper" aria-label="Grow-Wizard Schritte">
              {growWizardSteps.map((label, index) => {
                const step = index + 1
                return (
                  <button
                    key={label}
                    type="button"
                    className={`hydro-step ${wizardStep === step ? 'is-active' : ''} ${wizardStep > step ? 'is-complete' : ''}`}
                    onClick={() => goToStep(step)}
                  >
                    <span>{step}</span>
                    <strong>{label}</strong>
                  </button>
                )
              })}
            </div>

            {wizardStep === 1 && (
              <div className="hydro-builder-step">
                <div className="hydro-step-copy">
                  <strong>Grow</strong>
                </div>
                <label className="field">
                  <span>Grow-Name</span>
                  <input required value={form.name} onChange={(event) => patchForm(setForm, { name: event.target.value })} placeholder="Blue Dream RDWC Run" />
                </label>
                <label className="field">
                  <span>Sorte / Strain</span>
                  <input value={form.strain ?? ''} onChange={(event) => patchForm(setForm, { strain: event.target.value })} placeholder="Blue Dream" />
                </label>
                <label className="field">
                  <span>Breeder</span>
                  <input value={form.breeder ?? ''} onChange={(event) => patchForm(setForm, { breeder: event.target.value })} placeholder="optional" />
                </label>
                <label className="field">
                  <span>Seed Type</span>
                  <select value={form.seedType} onChange={(event) => patchForm(setForm, { seedType: event.target.value as SeedType })}>
                    {seedTypes.map((value) => <option key={value} value={value}>{value}</option>)}
                  </select>
                </label>
                <label className="field">
                  <span>Startmaterial</span>
                  <select value={form.startMaterial} onChange={(event) => patchForm(setForm, { startMaterial: event.target.value as StartMaterial })}>
                    {startMaterials.map((value) => <option key={value} value={value}>{formatStartMaterial(value)}</option>)}
                  </select>
                </label>
                <label className="field">
                  <span>Pflanzenanzahl</span>
                  <input inputMode="numeric" value={form.plantCount ?? ''} onChange={(event) => patchForm(setForm, { plantCount: toNullableInteger(event.target.value) })} placeholder="optional" />
                </label>
                {form.startMaterial === 'Seed' ? (
                  <label className="field">
                    <span>Keimmethode</span>
                    <select value={form.germinationMethod ?? 'PaperTowel'} onChange={(event) => patchForm(setForm, { germinationMethod: event.target.value as GerminationMethod })}>
                      {germinationMethods.map((value) => <option key={value} value={value}>{formatGerminationMethod(value)}</option>)}
                    </select>
                  </label>
                ) : (
                  <>
                    <label className="field">
                      <span>Clone Source</span>
                      <input value={form.cloneSource ?? ''} onChange={(event) => patchForm(setForm, { cloneSource: event.target.value })} placeholder="Mutterpflanze / Cut Nr. 3" />
                    </label>
                    <label className="systems-check">
                      <span>Steckling ist bewurzelt</span>
                      <input type="checkbox" checked={form.cloneIsRooted} onChange={(event) => patchForm(setForm, { cloneIsRooted: event.target.checked })} />
                    </label>
                  </>
                )}
                <label className="field systems-form-wide">
                  <span>Notizen</span>
                  <textarea rows={3} value={form.notes ?? ''} onChange={(event) => patchForm(setForm, { notes: event.target.value })} placeholder="Ziele, Besonderheiten, Risiken..." />
                </label>
              </div>
            )}

            {wizardStep === 2 && (
              <div className="hydro-builder-step">
                <div className="hydro-step-copy">
                  <strong>Grow-Zelt</strong>
                </div>
                {tents.length === 0 ? (
                  <div className="systems-empty systems-form-wide">
                    <strong>Noch kein Zelt eingerichtet.</strong>
                    <Link className="btn btn-primary" to="/zelte">Zelt anlegen</Link>
                  </div>
                ) : (
                  <div className="grow-choice-grid systems-form-wide">
                    {tents.map((tent) => (
                      <TentChoiceCard
                        key={tent.id}
                        tent={tent}
                        selected={form.tentId === tent.id}
                        hydroSetupCount={hydroSetups.filter((setup) => setup.tentId === tent.id && setup.status === 'Active').length}
                        onSelect={() => selectTent(tent.id)}
                      />
                    ))}
                  </div>
                )}
              </div>
            )}

            {wizardStep === 3 && (
              <div className="hydro-builder-step">
                <div className="hydro-step-copy">
                  <strong>Hydro-Setup</strong>
                </div>
                {!form.tentId ? (
                  <div className="systems-empty systems-form-wide">Wähle zuerst ein Grow-Zelt.</div>
                ) : activeHydroSetupsForTent.length === 0 && !legacySelectedHydroSetup ? (
                  <div className="systems-empty systems-form-wide">
                    <strong>Noch kein DWC/RDWC-System für dieses Zelt.</strong>
                    <Link className="btn btn-primary" to="/zelte">Hydro-Setup anlegen</Link>
                  </div>
                ) : (
                  <div className="grow-choice-grid systems-form-wide">
                    {legacySelectedHydroSetup && (
                      <HydroSetupChoiceCard setup={legacySelectedHydroSetup} selected onSelect={() => selectHydroSetup(legacySelectedHydroSetup)} legacy />
                    )}
                    {activeHydroSetupsForTent.map((setup) => (
                      <HydroSetupChoiceCard key={setup.id} setup={setup} selected={form.systemId === setup.id} onSelect={() => selectHydroSetup(setup)} />
                    ))}
                  </div>
                )}
              </div>
            )}

            {wizardStep === 4 && (
              <div className="hydro-builder-step">
                <div className="hydro-step-copy">
                  <strong>Start &amp; Methode</strong>
                </div>
                <label className="field">
                  <span>Startdatum</span>
                  <input required type="date" value={form.startDate} onChange={(event) => patchForm(setForm, { startDate: event.target.value })} />
                </label>
                <label className="field">
                  <span>Phase</span>
                  <select value={form.entryPoint} onChange={(event) => patchForm(setForm, { entryPoint: event.target.value as GrowEntryPoint })}>
                    {entryPoints.map((value) => <option key={value} value={value}>{formatEntryPoint(value)}</option>)}
                  </select>
                </label>
                <label className="field">
                  <span>Status</span>
                  <select value={form.status} onChange={(event) => patchForm(setForm, { status: event.target.value as GrowStatus })}>
                    {statuses.map((value) => <option key={value} value={value}>{formatGrowStatus(value)}</option>)}
                  </select>
                </label>
                <label className="field">
                  <span>Umgebung</span>
                  <select value={form.environment} onChange={(event) => patchForm(setForm, { environment: event.target.value as GrowEnvironment })}>
                    {environments.map((value) => <option key={value} value={value}>{value}</option>)}
                  </select>
                </label>
                <label className="field">
                  <span>Wasserquelle</span>
                  <select value={form.waterSource} onChange={(event) => patchForm(setForm, { waterSource: event.target.value as WaterSource })}>
                    {waterSources.map((value) => <option key={value} value={value}>{value}</option>)}
                  </select>
                </label>
                <label className="field">
                  <span>Nährstoffschema</span>
                  <input value={form.nutrients ?? ''} onChange={(event) => patchForm(setForm, { nutrients: event.target.value })} placeholder="Athena Pro, Canna Aqua, ..." />
                </label>
                <label className="field">
                  <span>Licht</span>
                  <input value={form.light ?? ''} onChange={(event) => patchForm(setForm, { light: event.target.value })} placeholder="optional" />
                </label>
                {isAutoflower ? (
                  <label className="field">
                    <span>Tage seit Keimung</span>
                    <input inputMode="numeric" value={form.autoflowerDaysSinceGermination ?? ''} onChange={(event) => patchForm(setForm, { autoflowerDaysSinceGermination: toNullableInteger(event.target.value) })} />
                  </label>
                ) : (
                  <label className="field">
                    <span>Tage bereits in Phase</span>
                    <input inputMode="numeric" value={form.daysAlreadyInPhase ?? ''} onChange={(event) => patchForm(setForm, { daysAlreadyInPhase: toNullableInteger(event.target.value) })} disabled={!needsDaysInPhase} />
                  </label>
                )}
                <label className="field">
                  <span>Flip-Datum</span>
                  <input type="date" value={form.flipDate ?? ''} onChange={(event) => patchForm(setForm, { flipDate: toNullableString(event.target.value) })} disabled={!needsFlipDate} />
                </label>
                <details className="grow-legacy-details systems-form-wide">
                  <summary>Erweiterte Legacy-Details für bestehende Grows</summary>
                  <div className="grow-legacy-grid">
                    <label className="field">
                      <span>Production-Setup (Plant-Kontext)</span>
                      <select
                        value={form.setupId ?? ''}
                        onChange={(event) => patchForm(setForm, { setupId: toNullableInteger(event.target.value) })}
                        disabled={!form.tentId}
                      >
                        <option value="">Kein Plant-Setup</option>
                        {archivedSelectedSetup && <option value={archivedSelectedSetup.id} disabled>{archivedSelectedSetup.name} (archiviert)</option>}
                        {productionSetupsForTent.map((setup) => <option key={setup.id} value={setup.id}>{setup.name}</option>)}
                      </select>
                    </label>
                    <label className="field">
                      <span>Propagation</span>
                      <select value={form.propagationMedium ?? ''} onChange={(event) => patchForm(setForm, { propagationMedium: toNullableString(event.target.value) as PropagationMedium | null })}>
                        <option value="">Nicht gesetzt</option>
                        {propagationMedia.map((value) => <option key={value} value={value}>{value}</option>)}
                      </select>
                    </label>
                    <label className="field">
                      <span>ReservoirSize</span>
                      <input value={form.reservoirSize ?? ''} onChange={(event) => patchForm(setForm, { reservoirSize: event.target.value })} />
                    </label>
                    <label className="field">
                      <span>ContainerSize</span>
                      <input value={form.containerSize ?? ''} onChange={(event) => patchForm(setForm, { containerSize: event.target.value })} />
                    </label>
                  </div>
                </details>
              </div>
            )}

            {wizardStep === 5 && (
              <div className="hydro-builder-step">
                <div className="hydro-step-copy">
                  <strong>Prüfen &amp; starten</strong>
                </div>
                <div className="hydro-review-grid systems-form-wide">
                  <div className="hydro-review-card grow-final-summary">
                    <Fact label="Grow" value={form.name.trim() || '–'} />
                    <Fact label="Sorte" value={form.strain?.trim() || '–'} />
                    <Fact label="Grow-Zelt" value={selectedTent?.name ?? '–'} />
                    <Fact label="Hydro-Setup" value={selectedHydroSetup?.name ?? '–'} />
                    <Fact label="DWC/RDWC" value={selectedHydroSetup?.hydroStyle ?? form.hydroStyle} />
                    <Fact label="Gesamtvolumen" value={formatLiters(selectedHydroSetup?.totalVolumeLiters ?? null)} />
                    <Fact label="Startdatum" value={form.startDate} />
                    <Fact label="Phase" value={formatEntryPoint(form.entryPoint)} />
                    <Fact label="Pflanzen" value={form.plantCount === null ? '–' : String(form.plantCount)} />
                  </div>
                  <div className="hydro-review-side">
                    {selectedHydroSetup ? <HydroSetupMiniSummary setup={selectedHydroSetup} /> : <div className="systems-empty systems-empty-compact">Legacy-Grow ohne Hydro-Setup.</div>}
                  </div>
                </div>
              </div>
            )}

            <div className="hydro-builder-actions grow-wizard-actions">
              <Link className="btn" to={isEditing && growId ? `/grows/${growId}` : '/'}>Abbrechen</Link>
              <button type="button" className="btn" onClick={() => goToStep(Math.max(1, wizardStep - 1))} disabled={wizardStep === 1}>Zurück</button>
              {wizardStep < growWizardSteps.length ? (
                <button type="button" className="btn btn-primary" onClick={() => goToStep(wizardStep + 1)}>
                  {wizardStep === growWizardSteps.length - 1 ? 'Prüfen' : 'Weiter'}
                </button>
              ) : (
                <button type="button" className="btn btn-primary" disabled={saving} onClick={() => void handleSaveGrow()}>
                  {saving ? 'Speichert...' : isEditing ? 'Grow aktualisieren' : 'Grow starten'}
                </button>
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  )
}

function TentChoiceCard(props: { tent: TentDto; selected: boolean; hydroSetupCount: number; onSelect: () => void }) {
  const { tent, selected, hydroSetupCount, onSelect } = props

  return (
    <button type="button" className={`grow-pick-card ${selected ? 'is-selected' : ''}`} onClick={onSelect}>
      <div className="grow-pick-main">
        <span className="grow-pick-kicker">Grow-Zelt</span>
        <strong>{tent.name}</strong>
        <span>{formatTentType(tent.tentType)} · {formatTentDimensions(tent)}</span>
      </div>
      <div className="grow-pick-meta">
        <span>{tent.kind}</span>
        <span>{hydroSetupCount} Hydro-Setup{hydroSetupCount === 1 ? '' : 's'}</span>
      </div>
      {selected && <div className="grow-pick-selected">Ausgewählt</div>}
    </button>
  )
}

function HydroSetupChoiceCard({ setup, selected, onSelect, legacy = false }: { setup: HydroSetupDto; selected: boolean; onSelect: () => void; legacy?: boolean }) {
  return (
    <button type="button" className={`grow-pick-card grow-pick-card-hydro ${selected ? 'is-selected' : ''}`} onClick={onSelect}>
      <div className="grow-pick-main">
        <span className="grow-pick-kicker">{legacy ? 'Legacy-Zuordnung' : 'Hydro-Setup'}</span>
        <strong>{setup.name}</strong>
        <span>{legacy ? 'außerhalb des gewählten Zelts' : `${setup.hydroStyle} DWC/RDWC-System`}</span>
      </div>
      <HydroSetupMiniSummary setup={setup} compact />
      {selected && <div className="grow-pick-selected">Ausgewählt</div>}
    </button>
  )
}

function HydroSetupMiniSummary({ setup, compact = false }: { setup: HydroSetupDto; compact?: boolean }) {
  const chips = [
    setup.hasCirculationPump && 'Umwälzpumpe',
    setup.hasAirPump && 'Luftpumpe',
    typeof setup.airStoneCount === 'number' && `${setup.airStoneCount} Luftsteine`,
    setup.hasChiller && 'Chiller',
    setup.hasUvSterilizer && 'UV-C',
  ].filter(Boolean) as string[]

  return (
    <div className={compact ? 'grow-system-brief' : 'grow-hydro-summary'}>
      <div className="grow-system-volume">
        <span>Gesamtvolumen</span>
        <strong>{formatLiters(setup.totalVolumeLiters)}</strong>
      </div>
      <div className="grow-system-lines">
        <span>{formatNullableNumber(setup.potCount)} Sites/Töpfe</span>
        <span>{formatLiters(setup.potSizeLiters)} pro Site</span>
        <span>{formatLiters(setup.reservoirLiters)} Tank</span>
        <span>{formatLayout(setup.layoutType)} · Tank {formatReservoirPosition(setup.reservoirPosition)}</span>
      </div>
      <div className="system-chip-row">
        {(chips.length > 0 ? chips : ['Keine Technik markiert']).map((chip) => <span key={chip}>{chip}</span>)}
      </div>
    </div>
  )
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function patchForm(setForm: Dispatch<SetStateAction<GrowUpsertPayload>>, patch: Partial<GrowUpsertPayload>) {
  setForm((current) => ({ ...current, ...patch }))
}

function getSelectableProductionSetupsForTent(setups: SetupDto[], tentId: number | null): SetupDto[] {
  if (!tentId) return []
  return setups.filter((setup) => setup.setupType === 'Production' && setup.status !== 'Archived' && setup.tentId === tentId)
}

function getArchivedSelectedSetup(setups: SetupDto[], setupId: number | null, tentId: number | null): SetupDto | null {
  if (!setupId || !tentId) return null
  return setups.find((setup) => setup.id === setupId && setup.tentId === tentId && setup.setupType === 'Production' && setup.status === 'Archived') ?? null
}

function isSetupValidForTent(setups: SetupDto[], setupId: number | null, tentId: number | null): boolean {
  if (!setupId) return true
  return setups.some((setup) => setup.id === setupId && setup.setupType === 'Production' && setup.tentId === tentId)
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

function normalizePayload(form: GrowUpsertPayload, hydroSetup: HydroSetupDto | null): GrowUpsertPayload {
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
    systemId: hydroSetup?.id ?? form.systemId,
    hydroStyle: hydroSetup ? toSelectableHydroStyle(hydroSetup.hydroStyle) : form.hydroStyle,
    reservoirSize: hydroSetup ? formatLiters(hydroSetup.totalVolumeLiters ?? hydroSetup.reservoirLiters) : toNullableString(form.reservoirSize),
    containerSize: hydroSetup ? formatLiters(hydroSetup.potSizeLiters) : toNullableString(form.containerSize),
    hasChiller: hydroSetup?.hasChiller ?? form.hasChiller,
    propagationMedium: form.propagationMedium,
    setupId: form.setupId ?? null,
    light: toNullableString(form.light),
    nutrients: toNullableString(form.nutrients),
    daysAlreadyInPhase: needsDaysInPhase ? form.daysAlreadyInPhase : null,
    autoflowerDaysSinceGermination: isAutoflower ? form.autoflowerDaysSinceGermination : null,
    flipDate: needsFlipDate ? toNullableString(form.flipDate) : null,
    notes: toNullableString(form.notes),
  }
}

function validateWizardStep(form: GrowUpsertPayload, hydroSetup: HydroSetupDto | null, step: number): string | null {
  if (step === 1 && form.name.trim().length === 0) return 'Bitte gib einen Grow-Namen ein.'
  if (step === 2 && !form.tentId) return 'Bitte wähle ein Grow-Zelt aus.'
  if (step === 3 && !hydroSetup) return 'Bitte wähle ein Hydro-Setup aus.'
  if (step === 4 && !form.startDate) return 'Bitte wähle ein Startdatum.'
  return null
}

function getFirstInvalidWizardStep(form: GrowUpsertPayload, hydroSetup: HydroSetupDto | null): { step: number; message: string } | null {
  for (let step = 1; step <= growWizardSteps.length - 1; step += 1) {
    const message = validateWizardStep(form, hydroSetup, step)
    if (message) return { step, message }
  }
  return null
}

function toSelectableHydroStyle(value: HydroStyle): SelectableHydroStyle {
  return value === 'DWC' ? 'DWC' : 'RDWC'
}

function toNullableString(value: string | null | undefined): string | null {
  const trimmed = value?.trim() ?? ''
  return trimmed ? trimmed : null
}

function toNullableInteger(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number.parseInt(trimmed, 10)
  return Number.isNaN(parsed) ? null : parsed
}

function formatTentType(value: TentType): string {
  switch (value) {
    case 'Production': return 'Blüte / Run'
    case 'Mother': return 'Mutter'
    case 'Propagation': return 'Anzucht'
    case 'Quarantine': return 'Quarantäne'
    case 'MultiPurpose': return 'Mehrzweck'
  }
}

function formatTentDimensions(tent: TentDto): string {
  if (!tent.widthCm && !tent.depthCm && !tent.tentHeightCm) return '–'
  return `${tent.widthCm ?? '?'} × ${tent.depthCm ?? '?'} × ${tent.tentHeightCm ?? '?'} cm`
}

function formatNullableNumber(value: number | null): string {
  return value === null ? '–' : String(value)
}

function formatLiters(value: number | null): string {
  return value === null ? '–' : `${value.toLocaleString('de-DE', { maximumFractionDigits: 1 })} L`
}

function formatLayout(value: string): string {
  switch (value) {
    case 'SingleBucket': return 'Einzeleimer'
    case 'Row': return 'Reihe'
    case 'Grid2x2': return '2x2'
    case 'Grid2x3': return '2x3'
    case 'Grid2x4': return '2x4'
    case 'Custom': return 'Custom'
    default: return value
  }
}

function formatReservoirPosition(value: string): string {
  switch (value) {
    case 'None': return 'keiner'
    case 'Left': return 'links'
    case 'Right': return 'rechts'
    case 'Top': return 'oben'
    case 'Bottom': return 'unten'
    case 'External': return 'extern'
    default: return value
  }
}

function formatStartMaterial(value: StartMaterial): string {
  return value === 'Seed' ? 'Samen' : 'Clone'
}

function formatGerminationMethod(value: GerminationMethod): string {
  switch (value) {
    case 'PaperTowel': return 'Küchenpapier'
    case 'Rockwool': return 'Steinwolle'
    case 'RapidRooter': return 'Rapid Rooter'
    case 'DirectInSystem': return 'Direkt im System'
  }
}

function formatEntryPoint(value: GrowEntryPoint): string {
  switch (value) {
    case 'Germination': return 'Keimung'
    case 'Seedling': return 'Seedling'
    case 'Veg': return 'Vegetation'
    case 'Flower': return 'Blüte'
    case 'Flush': return 'Flush'
  }
}

function formatGrowStatus(value: GrowStatus): string {
  switch (value) {
    case 'Planning': return 'Planung'
    case 'Running': return 'Läuft'
    case 'Completed': return 'Abgeschlossen'
    case 'Aborted': return 'Abgebrochen'
  }
}

export default GrowSetupPage
