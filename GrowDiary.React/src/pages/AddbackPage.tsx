import { useEffect, useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type {
  AddbackDefaultsDto,
  AddbackLogDto,
  AddbackResultDto,
  CreateAddbackLogRequest,
  GrowDetail,
  KnowledgeOverviewDto,
  NutrientProgramDto,
  NutrientProgramStageDto,
} from '../types'
import {
  V1Alert,
  V1Button,
  V1Card,
  V1Empty,
  V1Field,
  V1LinkButton,
  V1Page,
  V1Section,
  V1Stat,
  V1Wizard,
} from '../components/v1'
import { classNames, formatNumber } from '../utils'

type AddbackStep = 1 | 2 | 3 | 4 | 5 | 6

interface AddbackFormState {
  reservoirLiters: string
  ecIst: string
  ecZiel: string
  ecStock: string
  phBefore: string
  phTarget: string
  ecAfter: string
  phAfter: string
  notes: string
}

interface ComponentDraft {
  id: string
  name: string
  amountMl: string
  done: boolean
}

const steps = ['System', 'Istwerte', 'Ziel', 'Dosierung', 'Nachmessung', 'Speichern']
const genericComponents = ['Silikat / Stabilisator', 'CalMag / Basis', 'Base A', 'Base B', 'PK / Additiv', 'pH-Korrektur']

function AddbackPage() {
  const { growId } = useParams()
  const [defaults, setDefaults] = useState<AddbackDefaultsDto | null>(null)
  const [grow, setGrow] = useState<GrowDetail | null>(null)
  const [knowledge, setKnowledge] = useState<KnowledgeOverviewDto | null>(null)
  const [logs, setLogs] = useState<AddbackLogDto[]>([])
  const [step, setStep] = useState<AddbackStep>(1)
  const [programKey, setProgramKey] = useState<string>('custom')
  const [form, setForm] = useState<AddbackFormState>({
    reservoirLiters: '',
    ecIst: '',
    ecZiel: '',
    ecStock: '3',
    phBefore: '',
    phTarget: '5.8',
    ecAfter: '',
    phAfter: '',
    notes: '',
  })
  const [components, setComponents] = useState<ComponentDraft[]>(createComponents(genericComponents))
  const [result, setResult] = useState<AddbackResultDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [calculating, setCalculating] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  useEffect(() => {
    if (!growId) return
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      setSuccess(null)
      try {
        const [nextDefaults, nextGrow, nextLogs, nextKnowledge] = await Promise.all([
          apiFetch<AddbackDefaultsDto>(`/api/grows/${growId}/addback`, { signal: controller.signal }),
          apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal: controller.signal }),
          apiFetch<AddbackLogDto[]>(`/api/grows/${growId}/addback/logs`, { signal: controller.signal }).catch(() => []),
          apiFetch<KnowledgeOverviewDto>('/api/knowledge', { signal: controller.signal }).catch(() => null),
        ])

        if (controller.signal.aborted) return

        setDefaults(nextDefaults)
        setGrow(nextGrow)
        setLogs(nextLogs)
        setKnowledge(nextKnowledge)

        const matchedProgram = matchProgram(nextKnowledge?.programs ?? [], nextGrow.nutrients)
        const initialProgramKey = matchedProgram?.key ?? 'custom'
        setProgramKey(initialProgramKey)

        setForm({
          reservoirLiters: draftNumber(nextDefaults.reservoirLiters),
          ecIst: draftNumber(nextDefaults.ecIst),
          ecZiel: draftNumber(nextDefaults.ecZiel),
          ecStock: draftNumber(nextDefaults.ecStock),
          phBefore: draftNumber(nextGrow.latestMeasurement?.reservoirPh),
          phTarget: derivePhTarget(matchedProgram) ?? '5.8',
          ecAfter: '',
          phAfter: '',
          notes: '',
        })

        setComponents(createComponents(componentNamesForProgram(matchedProgram)))
      } catch (caught) {
        if (!controller.signal.aborted) {
          setError(caught instanceof ApiRequestError ? caught.message : 'Addback-Daten konnten nicht geladen werden.')
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [growId])

  const programs = useMemo(() => knowledge?.programs ?? [], [knowledge?.programs])
  const selectedProgram = useMemo(
    () => programs.find((program) => program.key === programKey) ?? null,
    [programKey, programs],
  )

  const selectedStage = useMemo(
    () => findProgramStage(selectedProgram, grow?.latestMeasurement?.stage ?? null),
    [selectedProgram, grow?.latestMeasurement?.stage],
  )

  const lastLog = logs[0] ?? null
  const hasCalculatedResult = result !== null && !result.errorMessage

  function updateForm<K extends keyof AddbackFormState>(key: K, value: AddbackFormState[K]) {
    setForm((current) => ({ ...current, [key]: value }))
    if (['reservoirLiters', 'ecIst', 'ecZiel', 'ecStock'].includes(key)) {
      setResult(null)
    }
  }

  function updateComponent(id: string, update: Partial<ComponentDraft>) {
    setComponents((current) => current.map((component) => (component.id === id ? { ...component, ...update } : component)))
  }

  function handleProgramChange(nextKey: string) {
    setProgramKey(nextKey)
    const nextProgram = programs.find((program) => program.key === nextKey) ?? null
    setComponents(createComponents(componentNamesForProgram(nextProgram)))
    const nextPhTarget = derivePhTarget(nextProgram)
    if (nextPhTarget) updateForm('phTarget', nextPhTarget)
  }

  async function calculateAddback(): Promise<AddbackResultDto | null> {
    if (!growId) return null

    const validation = validateCalculation(form)
    if (validation) {
      setError(validation)
      return null
    }

    setCalculating(true)
    setError(null)
    setSuccess(null)
    try {
      const nextResult = await apiFetch<AddbackResultDto>(`/api/grows/${growId}/addback/calculate`, {
        method: 'POST',
        body: JSON.stringify({
          reservoirLiters: parseNullableNumber(form.reservoirLiters),
          ecIst: parseNullableNumber(form.ecIst),
          ecZiel: parseNullableNumber(form.ecZiel),
          ecStock: parseNullableNumber(form.ecStock),
        }),
      })
      setResult(nextResult)
      if (nextResult.errorMessage) {
        setError(nextResult.errorMessage)
        return null
      }
      return nextResult
    } catch (caught) {
      const message = caught instanceof ApiRequestError ? caught.message : 'Addback konnte nicht berechnet werden.'
      setError(message)
      return null
    } finally {
      setCalculating(false)
    }
  }

  async function goNext() {
    setError(null)
    if (step === 2) {
      const validation = validateActuals(form)
      if (validation) {
        setError(validation)
        return
      }
    }

    if (step === 3) {
      const calculation = await calculateAddback()
      if (!calculation) return
    }

    setStep((current) => Math.min(6, current + 1) as AddbackStep)
  }

  function goBack() {
    setError(null)
    setStep((current) => Math.max(1, current - 1) as AddbackStep)
  }

  async function handleSave() {
    if (!growId) return

    let calculation = result
    if (!calculation || calculation.errorMessage) {
      calculation = await calculateAddback()
      if (!calculation || calculation.errorMessage) return
    }

    setSaving(true)
    setError(null)
    setSuccess(null)
    try {
      const payload: CreateAddbackLogRequest = {
        kind: 'Addback',
        performedAtUtc: new Date().toISOString(),
        reservoirLiters: parseNullableNumber(form.reservoirLiters),
        ecBefore: parseNullableNumber(form.ecIst),
        ecTarget: parseNullableNumber(form.ecZiel),
        ecStock: parseNullableNumber(form.ecStock),
        ecAfter: parseNullableNumber(form.ecAfter),
        phBefore: parseNullableNumber(form.phBefore),
        phAfter: parseNullableNumber(form.phAfter),
        litersAdded: calculation.litersToAdd,
        newReservoirVolumeLiters: calculation.newReservoirVolume,
        usedHydroSetupVolume: defaults?.suggestedReservoirLiters != null,
        notes: buildNotes(selectedProgram, selectedStage, components, form.notes),
      }

      const created = await apiFetch<AddbackLogDto>(`/api/grows/${growId}/addback/logs`, {
        method: 'POST',
        body: JSON.stringify(payload),
      })

      setLogs((current) => [created, ...current])
      setSuccess('Addback gespeichert.')
      // Zurück zum Start des Assistenten statt in der Speichern-Maske zu verharren;
      // der neue Log erscheint im Kontext-Rail als "Letzter Log".
      setStep(1)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Addback konnte nicht gespeichert werden.')
    } finally {
      setSaving(false)
    }
  }

  if (!growId) {
    return (
      <V1Page eyebrow="Reservoir" title="Addback">
        <V1Alert tone="warn" message="Kein Grow ausgewählt." />
        <V1LinkButton to="/addback" variant="primary">Zur Grow-Auswahl</V1LinkButton>
      </V1Page>
    )
  }

  return (
    <V1Page
      eyebrow="Reservoir"
      title="Addback"
      subtitle={grow ? `${grow.name}${grow.tentName ? ` · ${grow.tentName}` : ''}` : undefined}
      action={<V1LinkButton to="/addback" variant="ghost">Grow wechseln</V1LinkButton>}
      className="addback-assistant-page"
    >
      {error && <V1Alert title="Hinweis" message={error} tone="warn" />}
      {success && <V1Alert title="Gespeichert" message={success} tone="ok" />}

      <div data-audit="addback-flow">
        {loading ? (
          <V1Empty title="Lade Addback..." />
        ) : (
          <>
          <MobileStepper step={step} steps={steps} />
          <div className="addback-desktop-stepper" data-audit="addback-stepper">
            <V1Wizard steps={steps} currentStep={step} onStep={(nextStep) => setStep(nextStep as AddbackStep)} />
          </div>

          <div className="addback-assistant-layout">
            <aside className="addback-context-rail">
              <V1Card>
                <span className="v1-card-kicker">System</span>
                <h2>{grow?.hydroStyle ?? 'Hydro'}</h2>
                <div className="addback-context-grid">
                  <ContextItem label="Reservoir" value={formatLiters(parseNullableNumber(form.reservoirLiters) ?? defaults?.suggestedReservoirLiters)} />
                  <ContextItem label="Programm" value={selectedProgram?.name ?? grow?.nutrients ?? 'Eigenes'} />
                  <ContextItem label="Phase" value={grow?.latestMeasurement?.stage ?? grow?.entryPoint ?? 'offen'} />
                  <ContextItem label="Letzter Log" value={formatShortDateTime(lastLog?.performedAtUtc)} />
                </div>
              </V1Card>

              <V1Card className="addback-mini-flow-card">
                <span className="v1-card-kicker">Ablauf</span>
                {steps.map((item, index) => (
                  <button
                    key={item}
                    type="button"
                    className={classNames('addback-rail-step', step === index + 1 && 'active', step > index + 1 && 'done')}
                    onClick={() => setStep((index + 1) as AddbackStep)}
                  >
                    <span>{index + 1}</span>
                    <strong>{item}</strong>
                  </button>
                ))}
              </V1Card>
            </aside>

            <section className="addback-step-panel">
              {step === 1 && (
                <V1Section title="Grow & Reservoir">
                  <div className="addback-summary-grid">
                    <V1Stat label="Grow" value={grow?.name ?? defaults?.growName ?? '–'} hint={grow?.strain ?? 'Sorte offen'} />
                    <V1Stat label="Zelt" value={grow?.tentName ?? '–'} hint={grow?.hydroStyle ?? null} />
                    <V1Stat label="Volumen" value={formatNumber(parseNullableNumber(form.reservoirLiters) ?? defaults?.suggestedReservoirLiters, 1)} unit="L" hint="aus Hydro-Setup oder manuell" />
                    <V1Stat label="EC aktuell" value={formatNumber(parseNullableNumber(form.ecIst), 2)} unit="mS/cm" hint="letzte Messung / manuell" />
                  </div>
                  <div className="addback-action-row">
                    <V1Button variant="primary" onClick={goNext}>Istwerte prüfen</V1Button>
                  </div>
                </V1Section>
              )}

              {step === 2 && (
                <V1Section title="Istwerte">
                  <div className="addback-form-grid">
                    <NumberField label="Aktuelles Volumen" unit="L" value={form.reservoirLiters} onChange={(value) => updateForm('reservoirLiters', value)} hint={defaults?.suggestedReservoirLiters == null ? 'Manuell eintragen' : `Hydro-Vorschlag: ${formatNumber(defaults.suggestedReservoirLiters, 1)} L`} />
                    <NumberField label="EC aktuell" unit="mS/cm" value={form.ecIst} onChange={(value) => updateForm('ecIst', value)} hint={defaults?.suggestedEcIst == null ? 'Nachmessen' : `Letzte Messung: ${formatNumber(defaults.suggestedEcIst, 2)}`} />
                    <NumberField label="pH aktuell" unit="pH" value={form.phBefore} onChange={(value) => updateForm('phBefore', value)} hint="optional, aber empfohlen" />
                    <ReadOnlyMetric label="Wasser" value={grow?.latestMeasurement?.reservoirWaterTempC} unit="°C" />
                    <ReadOnlyMetric label="ORP" value={grow?.latestMeasurement?.orpMv} unit="mV" />
                    <ReadOnlyMetric label="DO" value={grow?.latestMeasurement?.dissolvedOxygenMgL} unit="mg/L" />
                  </div>
                  <NavButtons onBack={goBack} onNext={goNext} nextLabel="Zielwerte" />
                </V1Section>
              )}

              {step === 3 && (
                <V1Section title="Zielwerte">
                  <div className="addback-program-box">
                    <V1Field label="Nährstoffprogramm">
                      <select value={programKey} onChange={(event) => handleProgramChange(event.target.value)}>
                        <option value="custom">Eigenes / unbekannt</option>
                        {programs.map((program) => <option key={program.key} value={program.key}>{program.name}</option>)}
                      </select>
                    </V1Field>
                    {selectedProgram && (
                      <div className="addback-program-summary">
                        <strong>{selectedProgram.manufacturer}</strong>
                        <span>{selectedStage?.target ?? selectedProgram.ecGuidance}</span>
                      </div>
                    )}
                  </div>
                  <div className="addback-form-grid">
                    <NumberField label="Ziel-EC" unit="mS/cm" value={form.ecZiel} onChange={(value) => updateForm('ecZiel', value)} hint={defaults?.suggestedEcZiel == null ? 'Manuell festlegen' : `Sollwert: ${formatNumber(defaults.suggestedEcZiel, 2)}`} />
                    <NumberField label="Ziel-pH" unit="pH" value={form.phTarget} onChange={(value) => updateForm('phTarget', value)} hint={selectedProgram?.phGuidance ?? 'typisch 5,7–6,0'} />
                    <NumberField label="Addback-EC" unit="mS/cm" value={form.ecStock} onChange={(value) => updateForm('ecStock', value)} hint="EC der vorgemischten Addback-Lösung" />
                  </div>
                  <NavButtons onBack={goBack} onNext={goNext} nextLabel={calculating ? 'Berechne...' : 'Dosierung'} disabledNext={calculating} />
                </V1Section>
              )}

              {step === 4 && (
                <V1Section title="Dosierung">
                  <div className="addback-result-card">
                    {result?.errorMessage ? (
                      <V1Alert tone="warn" message={result.errorMessage} />
                    ) : !hasCalculatedResult ? (
                      <V1Empty title="Noch keine Berechnung" action={<V1Button variant="primary" onClick={() => void calculateAddback()} disabled={calculating}>{calculating ? 'Berechnet...' : 'Berechnen'}</V1Button>} />
                    ) : result?.needsAddback ? (
                      <>
                        <span className="v1-card-kicker">Addback-Menge</span>
                        <strong>{formatNumber(result.litersToAdd, 2)} <em>L</em></strong>
                        <p>Reservoir danach: {formatNumber(result.newReservoirVolume, 1)} L</p>
                      </>
                    ) : (
                      <>
                        <span className="v1-card-kicker">Status</span>
                        <strong>Kein Addback nötig</strong>
                        <p>EC liegt bereits im Zielbereich oder darüber.</p>
                      </>
                    )}
                  </div>

                  <div className="addback-components">
                    <header>
                      <h3>Komponenten</h3>
                      <span>Reihenfolge abarbeiten und optional Mengen eintragen.</span>
                    </header>
                    {components.map((component, index) => (
                      <div key={component.id} className={classNames('addback-component-row', component.done && 'done')}>
                        <button type="button" onClick={() => updateComponent(component.id, { done: !component.done })}>{component.done ? '✓' : index + 1}</button>
                        <input value={component.name} onChange={(event) => updateComponent(component.id, { name: event.target.value })} />
                        <div className="addback-amount-input">
                          <input inputMode="decimal" value={component.amountMl} onChange={(event) => updateComponent(component.id, { amountMl: event.target.value })} placeholder="ml" />
                          <span>ml</span>
                        </div>
                      </div>
                    ))}
                  </div>

                  <NavButtons onBack={goBack} onNext={goNext} nextLabel="Nachmessung" />
                </V1Section>
              )}

              {step === 5 && (
                <V1Section title="Nachmessung">
                  <div className="addback-form-grid">
                    <NumberField label="EC nach Addback" unit="mS/cm" value={form.ecAfter} onChange={(value) => updateForm('ecAfter', value)} hint="nach Durchmischung messen" />
                    <NumberField label="pH nach Addback" unit="pH" value={form.phAfter} onChange={(value) => updateForm('phAfter', value)} hint="erst nach EC stabilisieren" />
                    <V1Field label="Notizen" wide>
                      <textarea rows={5} value={form.notes} onChange={(event) => updateForm('notes', event.target.value)} placeholder="Beobachtung, Abweichung, Reihenfolge, Reaktion der Pflanzen..." />
                    </V1Field>
                  </div>
                  <NavButtons onBack={goBack} onNext={goNext} nextLabel="Prüfen" />
                </V1Section>
              )}

              {step === 6 && (
                <V1Section title="Prüfen & Speichern">
                  <div className="addback-review-grid">
                    <Review label="Grow" value={grow?.name ?? defaults?.growName ?? '–'} />
                    <Review label="Programm" value={selectedProgram?.name ?? grow?.nutrients ?? 'Eigenes'} />
                    <Review label="Reservoir" value={`${formatNumber(parseNullableNumber(form.reservoirLiters), 1)} L`} />
                    <Review label="EC" value={`${formatNumber(parseNullableNumber(form.ecIst), 2)} → ${formatNumber(parseNullableNumber(form.ecAfter), 2)} mS/cm`} />
                    <Review label="pH" value={`${formatNumber(parseNullableNumber(form.phBefore), 2)} → ${formatNumber(parseNullableNumber(form.phAfter), 2)}`} />
                    <Review label="Addback" value={result?.needsAddback ? `${formatNumber(result.litersToAdd, 2)} L` : '0 L'} />
                  </div>
                  <div className="addback-action-row">
                    <V1Button variant="secondary" onClick={goBack}>Zurück</V1Button>
                    <V1Button variant="primary" onClick={handleSave} disabled={saving || calculating}>{saving ? 'Speichert...' : 'Addback speichern'}</V1Button>
                  </div>
                  {logs.length > 0 && (
                    <div className="addback-log-list" data-audit="addback-log-list">
                      <h3>Letzte Addbacks</h3>
                      {logs.slice(0, 4).map((log) => (
                        <div key={log.id}>
                          <strong>{formatShortDateTime(log.performedAtUtc)}</strong>
                          <span>{formatNumber(log.litersAdded, 2)} L · EC {formatNumber(log.ecBefore, 2)} → {formatNumber(log.ecAfter, 2)}</span>
                        </div>
                      ))}
                    </div>
                  )}
                </V1Section>
              )}
            </section>
          </div>
          </>
        )}
      </div>
    </V1Page>
  )
}

function MobileStepper({ step, steps }: { step: AddbackStep; steps: string[] }) {
  return (
    <div className="addback-mobile-stepper" data-audit="addback-mobile-stepper" aria-label={`Schritt ${step} von ${steps.length}: ${steps[step - 1]}`}>
      <div>
        <span>Schritt {step} / {steps.length}</span>
        <strong>{steps[step - 1]}</strong>
      </div>
      <ol>
        {steps.map((item, index) => (
          <li key={item} className={classNames(step === index + 1 && 'active', step > index + 1 && 'done')} aria-label={`${index + 1}. ${item}`} />
        ))}
      </ol>
    </div>
  )
}

function NavButtons({ onBack, onNext, nextLabel, disabledNext }: { onBack: () => void; onNext: () => void; nextLabel: string; disabledNext?: boolean }) {
  return (
    <div className="addback-action-row">
      <V1Button variant="secondary" onClick={onBack}>Zurück</V1Button>
      <V1Button variant="primary" onClick={onNext} disabled={disabledNext}>{nextLabel}</V1Button>
    </div>
  )
}

function NumberField({ label, unit, value, onChange, hint }: { label: string; unit: string; value: string; onChange: (value: string) => void; hint?: string | null }) {
  return (
    <V1Field label={label} hint={hint}>
      <div className="addback-number-field">
        <input inputMode="decimal" value={value} onChange={(event) => onChange(event.target.value)} />
        <span>{unit}</span>
      </div>
    </V1Field>
  )
}

function ReadOnlyMetric({ label, value, unit }: { label: string; value: number | null | undefined; unit: string }) {
  return <V1Stat label={label} value={formatNumber(value, unit === '°C' ? 1 : 2)} unit={unit} hint="letzte Messung" />
}

function ContextItem({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>
}

function Review({ label, value }: { label: string; value: string }) {
  return <div className="addback-review-item"><span>{label}</span><strong>{value}</strong></div>
}

function createComponents(names: string[]): ComponentDraft[] {
  return names.map((name, index) => ({ id: `${index}-${name}`, name, amountMl: '', done: false }))
}

function componentNamesForProgram(program: NutrientProgramDto | null): string[] {
  const text = `${program?.name ?? ''} ${program?.manufacturer ?? ''}`.toLowerCase()
  if (text.includes('canna')) {
    return ['Silikat / optional', 'CANNA Aqua A', 'CANNA Aqua B', 'CalMag / Bedarf', 'Cannazym / Additiv', 'pH-Korrektur']
  }
  if (text.includes('athena')) {
    return ['Balance / pH Basis', 'Cleanse / System', 'CaMg', 'Grow/Bloom A', 'Grow/Bloom B', 'pH-Korrektur']
  }
  if (text.includes('vbx') || text.includes('hydroponic')) {
    return ['VBX', 'Shine', 'Life', 'CalMag / Bedarf', 'pH-Korrektur']
  }
  return genericComponents
}

function matchProgram(programs: NutrientProgramDto[], nutrients: string | null | undefined): NutrientProgramDto | null {
  const text = (nutrients ?? '').toLowerCase()
  if (!text) return null
  return programs.find((program) => {
    const haystack = `${program.key} ${program.name} ${program.manufacturer}`.toLowerCase()
    return haystack.includes(text) || text.includes(program.key.toLowerCase()) || text.includes(program.name.toLowerCase()) || text.includes(program.manufacturer.toLowerCase())
  }) ?? null
}

function findProgramStage(program: NutrientProgramDto | null, stage: string | null): NutrientProgramStageDto | null {
  if (!program || !stage) return null
  const normalized = stage.toLowerCase()
  return program.stages.find((item) => item.stage.toLowerCase().includes(normalized) || normalized.includes(item.stage.toLowerCase())) ?? program.stages[0] ?? null
}

function derivePhTarget(program: NutrientProgramDto | null): string | null {
  const text = program?.phGuidance ?? ''
  const matches = Array.from(text.matchAll(/\d+(?:[.,]\d+)?/g)).map((match) => Number(match[0].replace(',', '.'))).filter(Number.isFinite)
  if (matches.length === 0) return null
  const relevant = matches.filter((value) => value >= 4 && value <= 8)
  if (relevant.length === 0) return null
  const average = relevant.reduce((sum, value) => sum + value, 0) / relevant.length
  return average.toFixed(1)
}

function validateActuals(form: AddbackFormState): string | null {
  const reservoir = parseNullableNumber(form.reservoirLiters)
  const ec = parseNullableNumber(form.ecIst)
  const ph = parseNullableNumber(form.phBefore)
  if (reservoir == null || reservoir <= 0) return 'Reservoir-Volumen ist erforderlich.'
  if (ec == null || ec < 0) return 'Aktueller EC ist erforderlich.'
  if (ph != null && (ph < 0 || ph > 14)) return 'pH muss zwischen 0 und 14 liegen.'
  return null
}

function validateCalculation(form: AddbackFormState): string | null {
  const actualValidation = validateActuals(form)
  if (actualValidation) return actualValidation
  const ecIst = parseNullableNumber(form.ecIst)
  const ecZiel = parseNullableNumber(form.ecZiel)
  const ecStock = parseNullableNumber(form.ecStock)
  if (ecZiel == null || ecZiel < 0) return 'Ziel-EC ist erforderlich.'
  if (ecStock == null || ecStock <= 0) return 'Addback-EC ist erforderlich.'
  if (ecIst != null && ecStock <= ecZiel) return 'Addback-EC muss höher sein als Ziel-EC.'
  return null
}

function buildNotes(program: NutrientProgramDto | null, stage: NutrientProgramStageDto | null, components: ComponentDraft[], notes: string): string {
  const componentLines = components
    .filter((component) => component.name.trim().length > 0)
    .map((component, index) => `${index + 1}. ${component.name.trim()}${component.amountMl.trim() ? ` — ${component.amountMl.trim()} ml` : ''}${component.done ? ' — erledigt' : ''}`)

  return [
    'Addback-Assistent',
    `Programm: ${program?.name ?? 'Eigenes / unbekannt'}`,
    stage ? `Stage: ${stage.stage} — ${stage.target}` : null,
    componentLines.length > 0 ? `Komponenten:\n${componentLines.join('\n')}` : null,
    notes.trim() ? `Notizen:\n${notes.trim()}` : null,
  ].filter(Boolean).join('\n\n')
}

function formatLiters(value: number | null | undefined): string {
  return value == null ? '–' : `${formatNumber(value, 1)} L`
}

function formatShortDateTime(value: string | null | undefined) {
  if (!value) return '–'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '–'
  return new Intl.DateTimeFormat('de-DE', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
}

function draftNumber(value: number | null | undefined): string {
  return value == null || Number.isNaN(value) ? '' : String(value)
}

function parseNullableNumber(value: string): number | null {
  const trimmed = value.trim().replace(',', '.')
  if (!trimmed) return null
  const parsed = Number.parseFloat(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

export default AddbackPage
