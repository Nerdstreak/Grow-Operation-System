import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type {
  CreateHydroSetupRequest,
  CreateTentRequest,
  HydroSetupDto,
  HydroSetupLayoutType,
  HydroSetupStatus,
  ReservoirPosition,
  SelectableHydroStyle,
  TentDto,
  TentLivePayload,
  TentType,
  UpdateHydroSetupRequest,
} from '../types'

const tentTypeOptions: TentType[] = ['Production', 'Mother', 'Propagation', 'Quarantine', 'MultiPurpose']
const hydroStyleOptions: SelectableHydroStyle[] = ['DWC', 'RDWC']
const layoutOptions: HydroSetupLayoutType[] = ['SingleBucket', 'Row', 'Grid2x2', 'Grid2x3', 'Grid2x4', 'Custom']
const rdwcLayoutOptions: HydroSetupLayoutType[] = ['Row', 'Grid2x2', 'Grid2x3', 'Grid2x4', 'Custom']
const reservoirPositionOptions: ReservoirPosition[] = ['None', 'Left', 'Right', 'Top', 'Bottom', 'External']
const rdwcReservoirPositionOptions: ReservoirPosition[] = ['Left', 'Right', 'Top', 'Bottom', 'External']
const hydroBuilderSteps = ['Systemtyp', 'Volumen & Sites', 'Layout & Tank', 'Technik', 'Vorschau'] as const

interface TentDraft {
  name: string
  kind: string
  tentType: TentType
  notes: string
  displayOrder: string
}

interface HydroSetupDraft {
  name: string
  tentId: string
  hydroStyle: SelectableHydroStyle
  potCount: string
  potSizeLiters: string
  reservoirLiters: string
  layoutType: HydroSetupLayoutType
  reservoirPosition: ReservoirPosition
  hasCirculationPump: boolean
  circulationPumpNotes: string
  hasAirPump: boolean
  airPumpNotes: string
  airStoneCount: string
  hasChiller: boolean
  hasUvSterilizer: boolean
  notes: string
  displayOrder: string
  status: HydroSetupStatus
}

function TentsPage() {
  const [tents, setTents] = useState<TentDto[]>([])
  const [hydroSetups, setHydroSetups] = useState<HydroSetupDto[]>([])
  const [liveByTentId, setLiveByTentId] = useState<Record<number, TentLivePayload>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formMode, setFormMode] = useState<'tent' | 'hydro' | null>(null)
  const [editingHydroSetupId, setEditingHydroSetupId] = useState<number | null>(null)
  const [tentDraft, setTentDraft] = useState<TentDraft>(() => createTentDraft())
  const [hydroDraft, setHydroDraft] = useState<HydroSetupDraft>(() => createHydroDraft())
  const [hydroStep, setHydroStep] = useState(1)
  const [saving, setSaving] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  useEffect(() => {
    void loadPageData()
  }, [])

  async function loadPageData() {
    setLoading(true)
    setError(null)
    try {
      const [tentItems, setupItems] = await Promise.all([
        apiFetch<TentDto[]>('/api/settings/tents'),
        apiFetch<HydroSetupDto[]>('/api/hydro-setups'),
      ])

      const sortedTents = [...tentItems].sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name))
      const sortedSetups = [...setupItems].sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name))
      setTents(sortedTents)
      setHydroSetups(sortedSetups)

      const liveEntries = await Promise.all(
        sortedTents.map(async (tent) => {
          try {
            const payload = await apiFetch<TentLivePayload>(`/api/live/tents/${tent.id}`)
            return [tent.id, payload] as const
          } catch {
            return [tent.id, null] as const
          }
        }),
      )
      setLiveByTentId(Object.fromEntries(liveEntries.filter((entry): entry is readonly [number, TentLivePayload] => entry[1] !== null)))
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Zelte und Hydro-Setups konnten nicht geladen werden.')
    } finally {
      setLoading(false)
    }
  }

  const hydroSetupsByTent = useMemo(() => {
    const grouped = new Map<number | null, HydroSetupDto[]>()
    for (const setup of hydroSetups) {
      const key = setup.tentId
      grouped.set(key, [...(grouped.get(key) ?? []), setup])
    }
    return grouped
  }, [hydroSetups])

  const activeHydroSetupCount = useMemo(() => hydroSetups.filter((setup) => setup.status === 'Active').length, [hydroSetups])
  const totalVolume = calculateTotalVolume(hydroDraft)

  function openTentForm() {
    setFormError(null)
    setTentDraft(createTentDraft(tents.length + 1))
    setEditingHydroSetupId(null)
    setFormMode('tent')
  }

  function openHydroForm(tentId?: number, setup?: HydroSetupDto) {
    setFormError(null)
    setHydroStep(1)
    setEditingHydroSetupId(setup?.id ?? null)
    setHydroDraft(setup ? createHydroDraftFromSetup(setup) : createHydroDraft(tentId, hydroSetups.length + 1))
    setFormMode('hydro')
  }

  async function handleCreateTent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving('tent')
    setFormError(null)
    try {
      const request: CreateTentRequest = {
        name: tentDraft.name.trim(),
        kind: tentDraft.kind.trim() || 'Grow Tent',
        tentType: tentDraft.tentType,
        notes: toNullableString(tentDraft.notes),
        displayOrder: toIntOrDefault(tentDraft.displayOrder, 99),
        accentColor: '#69b578',
      }
      const created = await apiFetch<TentDto>('/api/settings/tents', {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setTents((current) => [...current, created].sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name)))
      setTentDraft(createTentDraft(tents.length + 2))
      setFormMode(null)
    } catch (caught) {
      setFormError(formatApiError(caught, 'Zelt konnte nicht angelegt werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function handleSaveHydroSetup(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const invalid = getFirstInvalidHydroStep(hydroDraft)
    if (invalid) {
      setHydroStep(invalid.step)
      setFormError(invalid.message)
      return
    }

    setSaving('hydro')
    setFormError(null)
    try {
      const request = hydroSetupDraftToRequest(hydroDraft)
      const saved = editingHydroSetupId
        ? await apiFetch<HydroSetupDto>(`/api/hydro-setups/${editingHydroSetupId}`, {
            method: 'PUT',
            body: JSON.stringify({ ...request, status: hydroDraft.status } satisfies UpdateHydroSetupRequest),
          })
        : await apiFetch<HydroSetupDto>('/api/hydro-setups', {
            method: 'POST',
            body: JSON.stringify(request),
          })

      setHydroSetups((current) => {
        const next = editingHydroSetupId ? current.map((item) => (item.id === saved.id ? saved : item)) : [...current, saved]
        return next.sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name))
      })
      setHydroDraft(createHydroDraft(undefined, hydroSetups.length + 2))
      setHydroStep(1)
      setEditingHydroSetupId(null)
      setFormMode(null)
    } catch (caught) {
      setFormError(formatApiError(caught, 'Hydro-Setup konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function archiveHydroSetup(setup: HydroSetupDto) {
    setSaving(`archive-${setup.id}`)
    setFormError(null)
    try {
      const archived = await apiFetch<HydroSetupDto>(`/api/hydro-setups/${setup.id}/archive`, { method: 'POST' })
      setHydroSetups((current) => current.map((item) => (item.id === setup.id ? archived : item)))
    } catch (caught) {
      setFormError(formatApiError(caught, 'Hydro-Setup konnte nicht archiviert werden.'))
    } finally {
      setSaving(null)
    }
  }

  function updateHydroStyle(hydroStyle: SelectableHydroStyle) {
    setHydroDraft((current) => {
      if (hydroStyle === 'DWC') {
        return {
          ...current,
          hydroStyle,
          potCount: '1',
          reservoirLiters: '',
          layoutType: 'SingleBucket',
          reservoirPosition: 'None',
        }
      }

      return {
        ...current,
        hydroStyle,
        potCount: toIntOrDefault(current.potCount, 0) < 2 ? '2' : current.potCount,
        layoutType: current.layoutType === 'SingleBucket' ? 'Grid2x2' : current.layoutType,
        reservoirPosition: current.reservoirPosition === 'None' ? 'Left' : current.reservoirPosition,
      }
    })
  }

  function goToHydroStep(nextStep: number) {
    setFormError(null)
    if (nextStep > hydroStep) {
      const message = validateHydroStep(hydroDraft, hydroStep)
      if (message) {
        setFormError(message)
        return
      }
    }
    setHydroStep(nextStep)
  }

  return (
    <div className="page-scroll">
      <div className="systems-page">
        {error && (
          <div className="alert-bar">
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}

        <header className="systems-header">
          <div>
            <div className="live-kicker">Grow OS</div>
            <h1>Zelte &amp; Systeme</h1>
            <p>Zelte sind dein physischer Raum. Hydro-Setups sind deine DWC/RDWC-Systeme.</p>
          </div>
          <div className="systems-header-actions">
            <button type="button" className="btn" onClick={openTentForm}>Zelt anlegen</button>
            <button type="button" className="btn btn-primary" onClick={() => openHydroForm(tents[0]?.id)} disabled={tents.length === 0}>
              Hydro-Setup anlegen
            </button>
          </div>
        </header>

        <div className="stats-row">
          <div className="stat-chip"><strong>{tents.length}</strong>Zelte</div>
          <div className="stat-chip"><strong>{activeHydroSetupCount}</strong>Aktive Hydro-Setups</div>
          <div className="stat-chip"><strong>{hydroSetups.length}</strong>DWC/RDWC-Systeme gesamt</div>
        </div>

        {formMode === 'tent' && (
          <section className="card systems-form-card">
            <div className="card-header">
              <span className="card-title">Zelt anlegen</span>
              <button type="button" className="btn" onClick={() => setFormMode(null)}>Schließen</button>
            </div>
            <form className="systems-form" onSubmit={(event) => void handleCreateTent(event)}>
              <label className="field">
                <span>Name</span>
                <input value={tentDraft.name} onChange={(event) => setTentDraft((current) => ({ ...current, name: event.target.value }))} placeholder="Mutter Zelt 1" />
              </label>
              <label className="field">
                <span>Typ</span>
                <select value={tentDraft.tentType} onChange={(event) => setTentDraft((current) => ({ ...current, tentType: event.target.value as TentType }))}>
                  {tentTypeOptions.map((value) => <option key={value} value={value}>{formatTentType(value)}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Typbezeichnung</span>
                <input value={tentDraft.kind} onChange={(event) => setTentDraft((current) => ({ ...current, kind: event.target.value }))} placeholder="Grow Tent" />
              </label>
              <label className="field">
                <span>Anzeige-Reihenfolge</span>
                <input type="number" value={tentDraft.displayOrder} onChange={(event) => setTentDraft((current) => ({ ...current, displayOrder: event.target.value }))} />
              </label>
              <label className="field systems-form-wide">
                <span>Notizen</span>
                <textarea rows={3} value={tentDraft.notes} onChange={(event) => setTentDraft((current) => ({ ...current, notes: event.target.value }))} placeholder="Wofür ist dieses Zelt gedacht?" />
              </label>
              {formError && <div className="systems-form-error">{formError}</div>}
              <div className="systems-form-actions">
                <button type="button" className="btn" onClick={() => setFormMode(null)}>Abbrechen</button>
                <button type="submit" className="btn btn-primary" disabled={saving === 'tent'}>{saving === 'tent' ? 'Speichert...' : 'Zelt speichern'}</button>
              </div>
            </form>
          </section>
        )}

        {formMode === 'hydro' && (
          <section className="card systems-form-card">
            <div className="card-header">
              <span className="card-title">{editingHydroSetupId ? 'Hydro-Setup bearbeiten' : 'Hydro-Setup anlegen'}</span>
              <button type="button" className="btn" onClick={() => setFormMode(null)}>Schließen</button>
            </div>
            <form className="hydro-builder" onSubmit={(event) => void handleSaveHydroSetup(event)}>
              <div className="hydro-stepper" aria-label="Hydro-Setup Builder Schritte">
                {hydroBuilderSteps.map((label, index) => {
                  const step = index + 1
                  return (
                    <button
                      key={label}
                      type="button"
                      className={`hydro-step ${hydroStep === step ? 'is-active' : ''} ${hydroStep > step ? 'is-complete' : ''}`}
                      onClick={() => goToHydroStep(step)}
                    >
                      <span>{step}</span>
                      <strong>{label}</strong>
                    </button>
                  )
                })}
              </div>

              {hydroStep === 1 && (
                <div className="hydro-builder-step">
                  <div className="hydro-step-copy">
                    <strong>Systemtyp</strong>
                    <p>DWC ist ein einzelner Behälter/Eimer mit Nährlösung. RDWC verbindet mehrere Sites/Töpfe mit gemeinsamem Reservoir/Tank.</p>
                  </div>
                  <label className="field">
                    <span>Name</span>
                    <input value={hydroDraft.name} onChange={(event) => setHydroDraft((current) => ({ ...current, name: event.target.value }))} placeholder="RDWC 4-Site mit 60L Tank" />
                  </label>
                  <label className="field">
                    <span>Zelt</span>
                    <select value={hydroDraft.tentId} onChange={(event) => setHydroDraft((current) => ({ ...current, tentId: event.target.value }))}>
                      <option value="">Zelt wählen</option>
                      {tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}
                    </select>
                  </label>
                  <div className="hydro-choice-grid">
                    {hydroStyleOptions.map((value) => (
                      <button
                        key={value}
                        type="button"
                        className={`hydro-choice ${hydroDraft.hydroStyle === value ? 'is-selected' : ''}`}
                        onClick={() => updateHydroStyle(value)}
                      >
                        <strong>{value}</strong>
                        <span>{value === 'DWC' ? 'Ein einzelner Behälter oder Eimer mit Nährlösung.' : 'Mehrere Sites/Töpfe mit gemeinsamem Reservoir/Tank.'}</span>
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {hydroStep === 2 && (
                <div className="hydro-builder-step">
                  <div className="hydro-step-copy">
                    <strong>Volumen &amp; Sites</strong>
                    <p>{hydroDraft.hydroStyle === 'DWC' ? 'Für DWC zählt das Systemvolumen des Behälters. Der Tank bleibt leer oder 0.' : 'Für RDWC berechnet sich das Gesamtvolumen aus Sites, Topfvolumen und Tank.'}</p>
                  </div>
                  <label className="field">
                    <span>{hydroDraft.hydroStyle === 'DWC' ? 'Anzahl Behälter/Sites' : 'Anzahl Töpfe/Sites'}</span>
                    <input type="number" min={hydroDraft.hydroStyle === 'RDWC' ? 2 : 1} value={hydroDraft.potCount} onChange={(event) => setHydroDraft((current) => ({ ...current, potCount: event.target.value }))} />
                  </label>
                  <label className="field">
                    <span>{hydroDraft.hydroStyle === 'DWC' ? 'Systemvolumen Liter' : 'Liter pro Topf'}</span>
                    <input type="number" min="0" step="0.1" value={hydroDraft.potSizeLiters} onChange={(event) => setHydroDraft((current) => ({ ...current, potSizeLiters: event.target.value }))} />
                  </label>
                  <label className="field">
                    <span>Reservoir-/Tankvolumen</span>
                    <input type="number" min="0" step="0.1" value={hydroDraft.reservoirLiters} onChange={(event) => setHydroDraft((current) => ({ ...current, reservoirLiters: event.target.value }))} disabled={hydroDraft.hydroStyle === 'DWC'} />
                  </label>
                  <div className="systems-summary-card hydro-step-summary">
                    <div>
                      <span>Gesamtvolumen</span>
                      <strong>{formatLiters(totalVolume)}</strong>
                    </div>
                  </div>
                </div>
              )}

              {hydroStep === 3 && (
                <div className="hydro-builder-step">
                  <div className="hydro-step-copy">
                    <strong>Layout &amp; Tank</strong>
                    <p>{hydroDraft.hydroStyle === 'DWC' ? 'DWC nutzt einen einzelnen Bucket ohne Tankposition.' : 'Wähle die grobe Site-Anordnung und wo der Reservoir-/Tank steht.'}</p>
                  </div>
                  <label className="field">
                    <span>Layout</span>
                    <select
                      value={hydroDraft.layoutType}
                      onChange={(event) => setHydroDraft((current) => ({ ...current, layoutType: event.target.value as HydroSetupLayoutType }))}
                      disabled={hydroDraft.hydroStyle === 'DWC'}
                    >
                      {(hydroDraft.hydroStyle === 'DWC' ? layoutOptions : rdwcLayoutOptions).map((value) => <option key={value} value={value}>{formatLayout(value)}</option>)}
                    </select>
                  </label>
                  <label className="field">
                    <span>Tankposition</span>
                    <select
                      value={hydroDraft.reservoirPosition}
                      onChange={(event) => setHydroDraft((current) => ({ ...current, reservoirPosition: event.target.value as ReservoirPosition }))}
                      disabled={hydroDraft.hydroStyle === 'DWC'}
                    >
                      {(hydroDraft.hydroStyle === 'DWC' ? reservoirPositionOptions : rdwcReservoirPositionOptions).map((value) => <option key={value} value={value}>{formatReservoirPosition(value)}</option>)}
                    </select>
                  </label>
                  <div className="systems-summary-card hydro-step-summary">
                    <div>
                      <span>Layout-Vorschau</span>
                      <strong>{formatLayout(hydroDraft.layoutType)}</strong>
                    </div>
                    <LayoutPreview draft={hydroDraft} />
                  </div>
                </div>
              )}

              {hydroStep === 4 && (
                <div className="hydro-builder-step">
                  <div className="hydro-step-copy">
                    <strong>Technik</strong>
                    <p>Halte nur die Komponenten fest, die wirklich am DWC/RDWC-System hängen.</p>
                  </div>
                  <div className="systems-toggle-grid systems-form-wide">
                    <label><input type="checkbox" checked={hydroDraft.hasCirculationPump} onChange={(event) => setHydroDraft((current) => ({ ...current, hasCirculationPump: event.target.checked }))} /> Umwälzpumpe</label>
                    <label><input type="checkbox" checked={hydroDraft.hasAirPump} onChange={(event) => setHydroDraft((current) => ({ ...current, hasAirPump: event.target.checked }))} /> Luftpumpe</label>
                    <label><input type="checkbox" checked={hydroDraft.hasChiller} onChange={(event) => setHydroDraft((current) => ({ ...current, hasChiller: event.target.checked }))} /> Chiller</label>
                    <label><input type="checkbox" checked={hydroDraft.hasUvSterilizer} onChange={(event) => setHydroDraft((current) => ({ ...current, hasUvSterilizer: event.target.checked }))} /> UV-C</label>
                  </div>
                  <label className="field">
                    <span>Luftstein-Anzahl</span>
                    <input type="number" min="0" value={hydroDraft.airStoneCount} onChange={(event) => setHydroDraft((current) => ({ ...current, airStoneCount: event.target.value }))} />
                  </label>
                  <label className="field systems-form-wide">
                    <span>Notizen</span>
                    <textarea rows={3} value={hydroDraft.notes} onChange={(event) => setHydroDraft((current) => ({ ...current, notes: event.target.value }))} placeholder="Pumpen, Verteiler, Besonderheiten" />
                  </label>
                </div>
              )}

              {hydroStep === 5 && (
                <div className="hydro-builder-step">
                  <div className="hydro-step-copy">
                    <strong>Vorschau &amp; Speichern</strong>
                    <p>Prüfe das DWC/RDWC-System. Gespeichert wird erst mit dem Button unten.</p>
                  </div>
                  <div className="hydro-review-grid">
                    <div className="hydro-review-card">
                      <Fact label="Name" value={hydroDraft.name.trim() || '–'} />
                      <Fact label="Zelt" value={getTentName(tents, hydroDraft.tentId)} />
                      <Fact label="Typ" value={hydroDraft.hydroStyle} />
                      <Fact label="Sites/Töpfe" value={hydroDraft.potCount || '–'} />
                      <Fact label={hydroDraft.hydroStyle === 'DWC' ? 'Systemvolumen' : 'Topfvolumen'} value={formatLiters(toNumberOrNull(hydroDraft.potSizeLiters))} />
                      <Fact label="Tankvolumen" value={formatLiters(toNumberOrNull(hydroDraft.reservoirLiters))} />
                      <Fact label="Gesamtvolumen" value={formatLiters(totalVolume)} />
                      <Fact label="Layout" value={formatLayout(hydroDraft.layoutType)} />
                      <Fact label="Tankposition" value={formatReservoirPosition(hydroDraft.reservoirPosition)} />
                    </div>
                    <div className="hydro-review-side">
                      <LayoutPreview draft={hydroDraft} />
                      <div className="system-chip-row">
                        {getHydroTechniqueChips(hydroDraft).map((chip) => <span key={chip}>{chip}</span>)}
                      </div>
                    </div>
                  </div>
                </div>
              )}

              {formError && <div className="systems-form-error">{formError}</div>}
              <div className="hydro-builder-actions">
                <button type="button" className="btn" onClick={() => setFormMode(null)}>Abbrechen</button>
                <button type="button" className="btn" onClick={() => goToHydroStep(Math.max(1, hydroStep - 1))} disabled={hydroStep === 1}>Zurück</button>
                {hydroStep < hydroBuilderSteps.length ? (
                  <button type="button" className="btn btn-primary" onClick={() => goToHydroStep(hydroStep + 1)}>Weiter</button>
                ) : (
                  <button type="submit" className="btn btn-primary" disabled={saving === 'hydro'}>{saving === 'hydro' ? 'Speichert...' : 'Speichern'}</button>
                )}
              </div>
            </form>
          </section>
        )}

        {loading ? (
          <div className="empty-hint">Lade Zelte und Hydro-Setups...</div>
        ) : (
          <>
            <section>
              <div className="systems-section-header">
                <div>
                  <div className="section-label">Zelte</div>
                  <p>Physischer Raum, Klima, Licht, Kamera und Sensorik.</p>
                </div>
              </div>
              {tents.length === 0 ? (
                <div className="systems-empty">
                  <strong>Noch kein Zelt eingerichtet.</strong>
                  <button type="button" className="btn btn-primary" onClick={openTentForm}>Erstes Zelt anlegen</button>
                </div>
              ) : (
                <div className="systems-tent-grid">
                  {tents.map((tent) => {
                    const setupCount = hydroSetupsByTent.get(tent.id)?.length ?? 0
                    const live = liveByTentId[tent.id]
                    return (
                      <article key={tent.id} className="system-card">
                        <div className="system-card-header">
                          <div>
                            <h2>{tent.name}</h2>
                            <p>{tent.kind} · {formatTentType(tent.tentType)}</p>
                          </div>
                          <span className={`badge ${live?.stateTone === 'critical' ? 'badge-crit' : live?.stateTone === 'attention' ? 'badge-warn' : live ? 'badge-ok' : 'badge-neutral'}`}>
                            {live?.stateLabel ?? 'kein Live-Status'}
                          </span>
                        </div>
                        <div className="system-facts">
                          <Fact label="Maße" value={formatTentDimensions(tent)} />
                          <Fact label="Aktive Grows" value={String(tent.activeGrowCount)} />
                          <Fact label="Plant-Setups" value={String(tent.activeSetupCount)} />
                          <Fact label="Hydro-Setups" value={String(setupCount)} />
                        </div>
                        {tent.notes && <p className="system-note">{tent.notes}</p>}
                        <div className="system-actions">
                          <Link className="btn" to={`/zelte/${tent.id}`}>Details öffnen</Link>
                          <button type="button" className="btn" onClick={() => openHydroForm(tent.id)}>Hydro-Setup hinzufügen</button>
                        </div>
                      </article>
                    )
                  })}
                </div>
              )}
            </section>

            <section>
              <div className="systems-section-header">
                <div>
                  <div className="section-label">Hydro-Setups</div>
                  <p>DWC/RDWC-Systeme mit Sites, Tank, Layout und technischer Ausstattung.</p>
                </div>
              </div>
              {tents.length === 0 ? (
                <div className="systems-empty">Lege zuerst ein Zelt an, danach kannst du DWC/RDWC-Systeme zuordnen.</div>
              ) : (
                <div className="systems-group-list">
                  {tents.map((tent) => {
                    const setups = hydroSetupsByTent.get(tent.id) ?? []
                    return (
                      <div key={tent.id} className="systems-group">
                        <div className="systems-group-title">
                          <h2>{tent.name}</h2>
                          <span>{setups.length} Hydro-Setup{setups.length === 1 ? '' : 's'}</span>
                        </div>
                        {setups.length === 0 ? (
                          <div className="systems-empty systems-empty-compact">
                            <span>Noch kein Hydro-Setup für dieses Zelt.</span>
                            <button type="button" className="btn" onClick={() => openHydroForm(tent.id)}>DWC/RDWC-System anlegen</button>
                          </div>
                        ) : (
                          <div className="systems-setup-grid">
                            {setups.map((setup) => (
                              <HydroSetupCard
                                key={setup.id}
                                setup={setup}
                                onEdit={() => openHydroForm(setup.tentId ?? undefined, setup)}
                                onArchive={() => void archiveHydroSetup(setup)}
                                archiving={saving === `archive-${setup.id}`}
                              />
                            ))}
                          </div>
                        )}
                      </div>
                    )
                  })}
                  {(hydroSetupsByTent.get(null)?.length ?? 0) > 0 && (
                    <div className="systems-group">
                      <div className="systems-group-title"><h2>Ohne Zelt</h2><span>Migration/Altbestand</span></div>
                      <div className="systems-setup-grid">
                        {hydroSetupsByTent.get(null)!.map((setup) => (
                          <HydroSetupCard
                            key={setup.id}
                            setup={setup}
                            onEdit={() => openHydroForm(undefined, setup)}
                            onArchive={() => void archiveHydroSetup(setup)}
                            archiving={saving === `archive-${setup.id}`}
                          />
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )}
            </section>
          </>
        )}
      </div>
    </div>
  )
}

function HydroSetupCard({ setup, onEdit, onArchive, archiving }: { setup: HydroSetupDto; onEdit: () => void; onArchive: () => void; archiving: boolean }) {
  const chips = [
    setup.hasCirculationPump && 'Umwälzpumpe',
    setup.hasAirPump && 'Luft',
    setup.hasChiller && 'Chiller',
    setup.hasUvSterilizer && 'UV-C',
  ].filter(Boolean) as string[]

  return (
    <article className={`system-card ${setup.status === 'Archived' ? 'is-archived' : ''}`}>
      <div className="system-card-header">
        <div>
          <h2>{setup.name}</h2>
          <p>{setup.tentName ?? 'Kein Zelt zugeordnet'} · {formatLayout(setup.layoutType)}</p>
        </div>
        <span className={`badge ${setup.status === 'Active' ? 'badge-ok' : 'badge-neutral'}`}>{setup.status === 'Active' ? 'Aktiv' : 'Archiviert'}</span>
      </div>
      <div className="system-badge-row">
        <span className="badge badge-info">{setup.hydroStyle}</span>
        <span className="badge badge-neutral">Tank {formatReservoirPosition(setup.reservoirPosition)}</span>
      </div>
      <div className="system-facts">
        <Fact label="Sites" value={formatNullableNumber(setup.potCount)} />
        <Fact label="Topfvolumen" value={formatLiters(setup.potSizeLiters)} />
        <Fact label="Tank" value={formatLiters(setup.reservoirLiters)} />
        <Fact label="Gesamt" value={formatLiters(setup.totalVolumeLiters)} />
      </div>
      {chips.length > 0 && <div className="system-chip-row">{chips.map((chip) => <span key={chip}>{chip}</span>)}</div>}
      {setup.notes && <p className="system-note">{setup.notes}</p>}
      <div className="system-actions">
        <button type="button" className="btn" onClick={onEdit}>Bearbeiten</button>
        <button type="button" className="btn" disabled={setup.status === 'Archived' || archiving} onClick={onArchive}>
          {archiving ? 'Archiviert...' : 'Archivieren'}
        </button>
      </div>
    </article>
  )
}

function LayoutPreview({ draft }: { draft: HydroSetupDraft }) {
  if (draft.hydroStyle === 'DWC') {
    return (
      <div className="layout-preview" aria-label="Layout-Vorschau">
        <div className="layout-bucket">Bucket</div>
      </div>
    )
  }

  if (draft.layoutType === 'Custom') {
    return (
      <div className="layout-preview" aria-label="Layout-Vorschau">
        <div className="layout-custom-preview">Custom Layout</div>
      </div>
    )
  }

  const siteCount = Math.max(2, Math.min(toIntOrDefault(draft.potCount, 2), 8))
  const sites = Array.from({ length: siteCount }, (_, index) => index + 1)

  return (
    <div className="layout-preview" aria-label="Layout-Vorschau">
      {draft.reservoirPosition !== 'None' && <div className={`layout-tank tank-${draft.reservoirPosition.toLowerCase()}`}>Tank</div>}
      <div className={`layout-sites layout-${draft.layoutType.toLowerCase()}`}>
        {sites.map((site) => <span key={site}>{site}</span>)}
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

function createTentDraft(order = 99): TentDraft {
  return {
    name: '',
    kind: 'Grow Tent',
    tentType: 'Production',
    notes: '',
    displayOrder: String(order),
  }
}

function createHydroDraft(tentId?: number, order = 99): HydroSetupDraft {
  return {
    name: '',
    tentId: tentId ? String(tentId) : '',
    hydroStyle: 'RDWC',
    potCount: '2',
    potSizeLiters: '',
    reservoirLiters: '',
    layoutType: 'Grid2x2',
    reservoirPosition: 'Left',
    hasCirculationPump: true,
    circulationPumpNotes: '',
    hasAirPump: true,
    airPumpNotes: '',
    airStoneCount: '',
    hasChiller: false,
    hasUvSterilizer: false,
    notes: '',
    displayOrder: String(order),
    status: 'Active',
  }
}

function createHydroDraftFromSetup(setup: HydroSetupDto): HydroSetupDraft {
  return {
    name: setup.name,
    tentId: setup.tentId ? String(setup.tentId) : '',
    hydroStyle: setup.hydroStyle === 'DWC' ? 'DWC' : 'RDWC',
    potCount: setup.potCount?.toString() ?? '',
    potSizeLiters: setup.potSizeLiters?.toString() ?? '',
    reservoirLiters: setup.reservoirLiters?.toString() ?? '',
    layoutType: setup.layoutType,
    reservoirPosition: setup.reservoirPosition,
    hasCirculationPump: setup.hasCirculationPump,
    circulationPumpNotes: setup.circulationPumpNotes ?? '',
    hasAirPump: setup.hasAirPump,
    airPumpNotes: setup.airPumpNotes ?? '',
    airStoneCount: setup.airStoneCount?.toString() ?? '',
    hasChiller: setup.hasChiller,
    hasUvSterilizer: setup.hasUvSterilizer,
    notes: setup.notes ?? '',
    displayOrder: setup.displayOrder.toString(),
    status: setup.status,
  }
}

function hydroSetupDraftToRequest(draft: HydroSetupDraft): CreateHydroSetupRequest {
  return {
    tentId: toIntOrNull(draft.tentId),
    name: draft.name.trim(),
    hydroStyle: draft.hydroStyle,
    potCount: toIntOrNull(draft.potCount),
    potSizeLiters: toNumberOrNull(draft.potSizeLiters),
    reservoirLiters: draft.hydroStyle === 'DWC' ? 0 : toNumberOrNull(draft.reservoirLiters),
    layoutType: draft.hydroStyle === 'DWC' ? 'SingleBucket' : draft.layoutType,
    reservoirPosition: draft.hydroStyle === 'DWC' ? 'None' : draft.reservoirPosition,
    hasCirculationPump: draft.hasCirculationPump,
    circulationPumpNotes: toNullableString(draft.circulationPumpNotes),
    hasAirPump: draft.hasAirPump,
    airPumpNotes: toNullableString(draft.airPumpNotes),
    airStoneCount: toIntOrNull(draft.airStoneCount),
    hasChiller: draft.hasChiller,
    hasUvSterilizer: draft.hasUvSterilizer,
    notes: toNullableString(draft.notes),
    displayOrder: toIntOrDefault(draft.displayOrder, 99),
  }
}

function calculateTotalVolume(draft: HydroSetupDraft): number | null {
  const potCount = toNumberOrNull(draft.potCount) ?? 0
  const potSize = toNumberOrNull(draft.potSizeLiters) ?? 0
  const reservoir = draft.hydroStyle === 'DWC' ? 0 : toNumberOrNull(draft.reservoirLiters) ?? 0
  const total = potCount * potSize + reservoir
  return total > 0 ? Number(total.toFixed(1)) : null
}

function validateHydroStep(draft: HydroSetupDraft, step: number): string | null {
  if (step === 1) {
    if (draft.name.trim().length === 0) return 'Bitte gib einen Namen für das Hydro-Setup ein.'
    if (!toIntOrNull(draft.tentId)) return 'Bitte wähle ein Zelt aus.'
  }

  if (step === 2) {
    const potCount = toIntOrNull(draft.potCount)
    const potSize = toNumberOrNull(draft.potSizeLiters)
    const reservoir = toNumberOrNull(draft.reservoirLiters) ?? 0
    if (draft.hydroStyle === 'DWC' && (!potSize || potSize <= 0)) return 'DWC braucht ein Systemvolumen größer 0 Liter.'
    if (draft.hydroStyle === 'RDWC' && (!potCount || potCount < 2)) return 'RDWC braucht mindestens 2 Sites/Töpfe.'
    if (draft.hydroStyle === 'RDWC' && (!potSize || potSize <= 0)) return 'RDWC braucht Liter pro Topf größer 0.'
    if (reservoir < 0) return 'Reservoir-/Tankvolumen darf nicht negativ sein.'
  }

  if (step === 3 && draft.hydroStyle === 'RDWC') {
    if (draft.layoutType === 'SingleBucket') return 'RDWC braucht ein Layout für mehrere Sites.'
    if (draft.reservoirPosition === 'None') return 'RDWC braucht eine Tankposition.'
  }

  if (step === 4) {
    const airStones = toIntOrNull(draft.airStoneCount)
    if (airStones !== null && airStones < 0) return 'Luftstein-Anzahl darf nicht negativ sein.'
  }

  return null
}

function getFirstInvalidHydroStep(draft: HydroSetupDraft): { step: number; message: string } | null {
  for (let step = 1; step <= hydroBuilderSteps.length - 1; step += 1) {
    const message = validateHydroStep(draft, step)
    if (message) return { step, message }
  }
  return null
}

function getTentName(tents: TentDto[], tentId: string): string {
  const id = toIntOrNull(tentId)
  return tents.find((tent) => tent.id === id)?.name ?? '–'
}

function getHydroTechniqueChips(draft: HydroSetupDraft): string[] {
  const chips = [
    draft.hasCirculationPump && 'Umwälzpumpe',
    draft.hasAirPump && 'Luftpumpe',
    draft.airStoneCount.trim() && `${draft.airStoneCount.trim()} Luftsteine`,
    draft.hasChiller && 'Chiller',
    draft.hasUvSterilizer && 'UV-C',
  ].filter(Boolean) as string[]
  return chips.length > 0 ? chips : ['Keine Technik markiert']
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

function formatLayout(value: HydroSetupLayoutType): string {
  switch (value) {
    case 'SingleBucket': return 'Einzeleimer'
    case 'Row': return 'Reihe'
    case 'Grid2x2': return '2x2'
    case 'Grid2x3': return '2x3'
    case 'Grid2x4': return '2x4'
    case 'Custom': return 'Custom'
  }
}

function formatReservoirPosition(value: ReservoirPosition): string {
  switch (value) {
    case 'None': return 'keiner'
    case 'Left': return 'links'
    case 'Right': return 'rechts'
    case 'Top': return 'oben'
    case 'Bottom': return 'unten'
    case 'External': return 'extern'
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

function toNullableString(value: string): string | null {
  const trimmed = value.trim()
  return trimmed.length === 0 ? null : trimmed
}

function toIntOrNull(value: string): number | null {
  if (value.trim() === '') return null
  const parsed = Number.parseInt(value, 10)
  return Number.isFinite(parsed) ? parsed : null
}

function toIntOrDefault(value: string, fallback: number): number {
  return toIntOrNull(value) ?? fallback
}

function toNumberOrNull(value: string): number | null {
  if (value.trim() === '') return null
  const parsed = Number.parseFloat(value.replace(',', '.'))
  return Number.isFinite(parsed) ? parsed : null
}

function formatApiError(caught: unknown, fallback: string): string {
  if (!(caught instanceof ApiRequestError)) return fallback
  const fieldErrors = caught.payload?.fieldErrors
  if (!fieldErrors) return caught.message
  const messages = Object.values(fieldErrors).flat()
  return messages.length > 0 ? messages.join(' ') : caught.message
}

export default TentsPage
