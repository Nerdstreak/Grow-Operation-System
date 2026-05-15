import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type {
  CreateHydroSetupRequest,
  HydroSetupDto,
  HydroSetupLayoutType,
  ReservoirPosition,
  SelectableHydroStyle,
  TentDto,
  UpdateHydroSetupRequest,
} from '../types'
import { classNames, formatNumber } from '../utils'

const hydroStyleOptions: SelectableHydroStyle[] = ['DWC', 'RDWC']
const layoutOptions: HydroSetupLayoutType[] = ['SingleBucket', 'Row', 'Grid2x2', 'Grid2x3', 'Grid2x4', 'Custom']
const reservoirPositionOptions: ReservoirPosition[] = ['None', 'Left', 'Right', 'Top', 'Bottom', 'External']

interface HydroDraft {
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
}

type SavingState = 'setup' | `archive-${number}` | null

function HydroPage() {
  const [setups, setSetups] = useState<HydroSetupDto[]>([])
  const [tents, setTents] = useState<TentDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editingSetupId, setEditingSetupId] = useState<number | null>(null)
  const [draft, setDraft] = useState<HydroDraft>(() => createHydroDraft())
  const [step, setStep] = useState(1)
  const [saving, setSaving] = useState<SavingState>(null)
  const [formError, setFormError] = useState<string | null>(null)

  useEffect(() => {
    void loadData()
  }, [])

  async function loadData() {
    setLoading(true)
    setError(null)
    try {
      const [tentItems, setupItems] = await Promise.all([
        apiFetch<TentDto[]>('/api/settings/tents'),
        apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true'),
      ])
      setTents(sortTents(tentItems))
      setSetups(sortSetups(setupItems))
    } catch (caught) {
      setError(formatApiError(caught, 'Hydro-Setups konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }

  const activeSetups = useMemo(() => setups.filter((setup) => setup.status === 'Active'), [setups])
  const archivedSetups = useMemo(() => setups.filter((setup) => setup.status === 'Archived'), [setups])
  const selectedTent = useMemo(() => tents.find((tent) => String(tent.id) === draft.tentId) ?? null, [draft.tentId, tents])
  const calculatedTotalVolume = useMemo(() => calculateTotalVolume(draft), [draft])
  const selectedSetup = useMemo(() => setups.find((setup) => setup.id === editingSetupId) ?? null, [editingSetupId, setups])

  function openCreateForm() {
    setFormError(null)
    setEditingSetupId(null)
    setDraft(createHydroDraft(setups.length + 1, tents[0]?.id ?? null))
    setStep(1)
    setFormOpen(true)
  }

  function openEditForm(setup: HydroSetupDto) {
    setFormError(null)
    setEditingSetupId(setup.id)
    setDraft(createHydroDraftFromSetup(setup))
    setStep(1)
    setFormOpen(true)
  }

  function closeForm() {
    setFormOpen(false)
    setFormError(null)
    setEditingSetupId(null)
    setStep(1)
  }

  function updateHydroStyle(value: SelectableHydroStyle) {
    setDraft((current) => {
      if (value === 'DWC') {
        return {
          ...current,
          hydroStyle: 'DWC',
          potCount: '1',
          layoutType: 'SingleBucket',
          reservoirPosition: 'None',
        }
      }

      return {
        ...current,
        hydroStyle: 'RDWC',
        potCount: toMinimumString(current.potCount, 2),
        layoutType: current.layoutType === 'SingleBucket' ? 'Grid2x2' : current.layoutType,
        reservoirPosition: current.reservoirPosition === 'None' ? 'Left' : current.reservoirPosition,
      }
    })
  }

  function goNext() {
    const message = validateStep(draft, step)
    if (message) {
      setFormError(message)
      return
    }

    setFormError(null)
    setStep((current) => Math.min(5, current + 1))
  }

  function goBack() {
    setFormError(null)
    setStep((current) => Math.max(1, current - 1))
  }

  async function handleSaveSetup() {
    if (step !== 5) {
      setFormError('Speichern ist erst im Schritt Prüfen möglich.')
      return
    }

    const message = validateAll(draft)
    if (message) {
      setFormError(message)
      return
    }

    setSaving('setup')
    setFormError(null)
    try {
      const request = hydroDraftToRequest(draft)

      if (editingSetupId && selectedSetup) {
        const updateRequest: UpdateHydroSetupRequest = {
          ...request,
          status: selectedSetup.status,
        }
        const saved = await apiFetch<HydroSetupDto>(`/api/hydro-setups/${editingSetupId}`, {
          method: 'PUT',
          body: JSON.stringify(updateRequest),
        })
        setSetups((current) => sortSetups(current.map((setup) => (setup.id === saved.id ? saved : setup))))
      } else {
        const created = await apiFetch<HydroSetupDto>('/api/hydro-setups', {
          method: 'POST',
          body: JSON.stringify(request),
        })
        setSetups((current) => sortSetups([...current, created]))
      }

      closeForm()
    } catch (caught) {
      setFormError(formatApiError(caught, 'Hydro-Setup konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function handleArchiveSetup(setup: HydroSetupDto) {
    setSaving(`archive-${setup.id}`)
    setError(null)
    try {
      const saved = await apiFetch<HydroSetupDto>(`/api/hydro-setups/${setup.id}/archive`, {
        method: 'POST',
      })
      setSetups((current) => sortSetups(current.map((item) => (item.id === saved.id ? saved : item))))
    } catch (caught) {
      setError(formatApiError(caught, 'Hydro-Setup konnte nicht archiviert werden.'))
    } finally {
      setSaving(null)
    }
  }

  return (
    <main className="page-scroll app-page hydro-page">
      <header className="control-header">
        <div>
          <span className="control-kicker">DWC/RDWC-Systeme</span>
          <h1>Hydro</h1>
        </div>
        <button type="button" className="btn btn-primary" onClick={openCreateForm}>Hydro-Setup anlegen</button>
      </header>

      {error && <AlertBar title="Fehler" message={error} />}
      {formError && <AlertBar title="Hinweis" message={formError} />}

      <section className="stats-row">
        <div className="stat-chip"><strong>{setups.length}</strong>Setups</div>
        <div className="stat-chip"><strong>{activeSetups.length}</strong>Aktiv</div>
        <div className="stat-chip"><strong>{archivedSetups.length}</strong>Archiv</div>
        <div className="stat-chip"><strong>{tents.length}</strong>Zelte</div>
      </section>

      {formOpen && (
        <section className="card systems-form-card hydro-builder-card">
          <div className="card-header">
            <span className="card-title">{editingSetupId ? 'Hydro-Setup bearbeiten' : 'Hydro-Setup anlegen'}</span>
            <button type="button" className="btn" onClick={closeForm}>Schließen</button>
          </div>

          <HydroStepper currentStep={step} />

          <div className="hydro-builder-body">
            {step === 1 && (
              <div className="systems-form entity-form-grid">
                <div className="form-section-title systems-form-wide">System</div>
                <label className="field">
                  <span>Name</span>
                  <input value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} placeholder="RDWC 4-Site" />
                </label>
                <label className="field">
                  <span>Zelt</span>
                  <select value={draft.tentId} onChange={(event) => setDraft((current) => ({ ...current, tentId: event.target.value }))}>
                    <option value="">Zelt wählen</option>
                    {tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}
                  </select>
                </label>
                <div className="hydro-choice systems-form-wide">
                  {hydroStyleOptions.map((style) => (
                    <button
                      key={style}
                      type="button"
                      className={classNames('hydro-choice-card', draft.hydroStyle === style && 'active')}
                      onClick={() => updateHydroStyle(style)}
                    >
                      <strong>{style}</strong>
                      <span>{style === 'DWC' ? 'Einzelner Bucket / Eimer' : 'Rezirkulierendes DWC-System'}</span>
                    </button>
                  ))}
                </div>
              </div>
            )}

            {step === 2 && (
              <div className="systems-form entity-form-grid">
                <div className="form-section-title systems-form-wide">Volumen & Sites</div>
                <label className="field">
                  <span>Sites / Töpfe</span>
                  <input
                    type="number"
                    min={draft.hydroStyle === 'RDWC' ? 2 : 1}
                    value={draft.potCount}
                    disabled={draft.hydroStyle === 'DWC'}
                    onChange={(event) => setDraft((current) => ({ ...current, potCount: event.target.value }))}
                  />
                </label>
                <label className="field">
                  <span>Liter pro Topf</span>
                  <input type="number" min="0" step="0.1" value={draft.potSizeLiters} onChange={(event) => setDraft((current) => ({ ...current, potSizeLiters: event.target.value }))} placeholder="19" />
                </label>
                <label className="field">
                  <span>Reservoir / Tank L</span>
                  <input type="number" min="0" step="0.1" value={draft.reservoirLiters} onChange={(event) => setDraft((current) => ({ ...current, reservoirLiters: event.target.value }))} placeholder="60" />
                </label>
                <div className="hydro-total-card">
                  <span>Gesamtvolumen</span>
                  <strong>{formatNumber(calculatedTotalVolume, 1)} L</strong>
                </div>
              </div>
            )}

            {step === 3 && (
              <div className="systems-form entity-form-grid">
                <div className="form-section-title systems-form-wide">Layout & Tank</div>
                <label className="field">
                  <span>Layout</span>
                  <select
                    value={draft.layoutType}
                    disabled={draft.hydroStyle === 'DWC'}
                    onChange={(event) => setDraft((current) => ({ ...current, layoutType: event.target.value as HydroSetupLayoutType }))}
                  >
                    {layoutOptions.map((value) => <option key={value} value={value}>{formatLayout(value)}</option>)}
                  </select>
                </label>
                <label className="field">
                  <span>Tankposition</span>
                  <select
                    value={draft.reservoirPosition}
                    disabled={draft.hydroStyle === 'DWC'}
                    onChange={(event) => setDraft((current) => ({ ...current, reservoirPosition: event.target.value as ReservoirPosition }))}
                  >
                    {reservoirPositionOptions.map((value) => <option key={value} value={value}>{formatReservoirPosition(value)}</option>)}
                  </select>
                </label>
                <div className="systems-form-wide">
                  <RdwcLayoutPreview draft={draft} />
                </div>
              </div>
            )}

            {step === 4 && (
              <div className="systems-form entity-form-grid">
                <div className="form-section-title systems-form-wide">Technik</div>
                <label className="switch-row entity-switch-row">
                  <input type="checkbox" checked={draft.hasCirculationPump} onChange={(event) => setDraft((current) => ({ ...current, hasCirculationPump: event.target.checked }))} />
                  <span>Umwälzpumpe vorhanden</span>
                </label>
                <label className="field">
                  <span>Pumpen-Notiz</span>
                  <input value={draft.circulationPumpNotes} onChange={(event) => setDraft((current) => ({ ...current, circulationPumpNotes: event.target.value }))} placeholder="DC Pumpe, 2000 l/h" />
                </label>
                <label className="switch-row entity-switch-row">
                  <input type="checkbox" checked={draft.hasAirPump} onChange={(event) => setDraft((current) => ({ ...current, hasAirPump: event.target.checked }))} />
                  <span>Luftpumpe vorhanden</span>
                </label>
                <label className="field">
                  <span>Luftsteine</span>
                  <input type="number" min="0" value={draft.airStoneCount} onChange={(event) => setDraft((current) => ({ ...current, airStoneCount: event.target.value }))} />
                </label>
                <label className="field systems-form-wide">
                  <span>Luft-Notiz</span>
                  <input value={draft.airPumpNotes} onChange={(event) => setDraft((current) => ({ ...current, airPumpNotes: event.target.value }))} placeholder="Membranpumpe / Ausströmer" />
                </label>
                <label className="switch-row entity-switch-row">
                  <input type="checkbox" checked={draft.hasChiller} onChange={(event) => setDraft((current) => ({ ...current, hasChiller: event.target.checked }))} />
                  <span>Chiller vorhanden</span>
                </label>
                <label className="switch-row entity-switch-row">
                  <input type="checkbox" checked={draft.hasUvSterilizer} onChange={(event) => setDraft((current) => ({ ...current, hasUvSterilizer: event.target.checked }))} />
                  <span>UV-C vorhanden</span>
                </label>
                <label className="field systems-form-wide">
                  <span>Notizen</span>
                  <textarea rows={3} value={draft.notes} onChange={(event) => setDraft((current) => ({ ...current, notes: event.target.value }))} />
                </label>
              </div>
            )}

            {step === 5 && (
              <div className="hydro-review-grid">
                <section className="admin-card">
                  <div className="section-label">Prüfen</div>
                  <h2>{draft.name || 'Unbenanntes Setup'}</h2>
                  <div className="hydro-review-list">
                    <ReviewRow label="Zelt" value={selectedTent?.name ?? '–'} />
                    <ReviewRow label="Typ" value={draft.hydroStyle} />
                    <ReviewRow label="Sites" value={draft.potCount || '–'} />
                    <ReviewRow label="Topfvolumen" value={`${draft.potSizeLiters || '–'} L`} />
                    <ReviewRow label="Tank" value={`${draft.reservoirLiters || '–'} L`} />
                    <ReviewRow label="Gesamt" value={`${formatNumber(calculatedTotalVolume, 1)} L`} />
                    <ReviewRow label="Layout" value={formatLayout(draft.layoutType)} />
                    <ReviewRow label="Tankposition" value={formatReservoirPosition(draft.reservoirPosition)} />
                  </div>
                </section>
                <section className="admin-card">
                  <div className="section-label">Vorschau</div>
                  <RdwcLayoutPreview draft={draft} />
                  <div className="hydro-chip-row">
                    {draft.hasCirculationPump && <span className="meta-chip">Pumpe</span>}
                    {draft.hasAirPump && <span className="meta-chip">Luft</span>}
                    {draft.hasChiller && <span className="meta-chip">Chiller</span>}
                    {draft.hasUvSterilizer && <span className="meta-chip">UV-C</span>}
                  </div>
                </section>
              </div>
            )}
          </div>

          <div className="systems-form-actions">
            <button type="button" className="btn" onClick={step === 1 ? closeForm : goBack}>{step === 1 ? 'Abbrechen' : 'Zurück'}</button>
            {step < 5 ? (
              <button type="button" className="btn btn-primary" onClick={goNext}>Weiter</button>
            ) : (
              <button type="button" className="btn btn-primary" disabled={saving === 'setup'} onClick={() => void handleSaveSetup()}>
                {saving === 'setup' ? 'Speichert...' : editingSetupId ? 'Speichern' : 'Anlegen'}
              </button>
            )}
          </div>
        </section>
      )}

      {loading ? (
        <div className="empty-hint">Lade Hydro-Setups...</div>
      ) : setups.length === 0 ? (
        <section className="systems-empty">
          <strong>Noch kein Hydro-Setup angelegt.</strong>
          <span>DWC/RDWC-Systeme werden separat von Zelten gepflegt.</span>
          <button type="button" className="btn btn-primary" onClick={openCreateForm}>Hydro-Setup anlegen</button>
        </section>
      ) : (
        <section className="hydro-setup-grid">
          {activeSetups.map((setup) => (
            <HydroSetupCard key={setup.id} setup={setup} onEdit={openEditForm} onArchive={(item) => void handleArchiveSetup(item)} saving={saving === `archive-${setup.id}`} />
          ))}
          {archivedSetups.length > 0 && (
            <div className="systems-form-wide hydro-archive-list">
              <div className="section-label">Archiv</div>
              {archivedSetups.map((setup) => <HydroSetupCard key={setup.id} setup={setup} onEdit={openEditForm} saving={false} />)}
            </div>
          )}
        </section>
      )}

      {tents.length === 0 && !loading && (
        <section className="empty-hint">
          Hydro-Setups brauchen ein Zelt. <Link to="/zelte">Erstes Zelt anlegen</Link>
        </section>
      )}
    </main>
  )
}

function HydroSetupCard({ setup, onEdit, onArchive, saving }: { setup: HydroSetupDto; onEdit: (setup: HydroSetupDto) => void; onArchive?: (setup: HydroSetupDto) => void; saving: boolean }) {
  return (
    <article className="admin-card hydro-setup-card">
      <div className="card-header">
        <div>
          <span className="section-label">{setup.hydroStyle}</span>
          <h2>{setup.name}</h2>
        </div>
        <span className={classNames('status-pill', setup.status === 'Active' ? 'status-running' : 'status-muted')}>{formatStatus(setup.status)}</span>
      </div>
      <div className="hydro-card-grid">
        <InfoTile label="Zelt" value={setup.tentName ?? '–'} />
        <InfoTile label="Sites" value={String(setup.potCount ?? '–')} />
        <InfoTile label="Topf" value={setup.potSizeLiters !== null ? `${formatNumber(setup.potSizeLiters, 1)} L` : '–'} />
        <InfoTile label="Tank" value={setup.reservoirLiters !== null ? `${formatNumber(setup.reservoirLiters, 1)} L` : '–'} />
        <InfoTile label="Gesamt" value={setup.totalVolumeLiters !== null ? `${formatNumber(setup.totalVolumeLiters, 1)} L` : '–'} />
        <InfoTile label="Layout" value={formatLayout(setup.layoutType)} />
      </div>
      <div className="hydro-chip-row">
        {setup.hasCirculationPump && <span className="meta-chip">Pumpe</span>}
        {setup.hasAirPump && <span className="meta-chip">Luft</span>}
        {setup.hasChiller && <span className="meta-chip">Chiller</span>}
        {setup.hasUvSterilizer && <span className="meta-chip">UV-C</span>}
      </div>
      <RdwcLayoutPreview setup={setup} />
      <div className="systems-form-actions">
        <button type="button" className="btn" onClick={() => onEdit(setup)}>Bearbeiten</button>
        {onArchive && setup.status === 'Active' && (
          <button type="button" className="btn" disabled={saving} onClick={() => onArchive(setup)}>{saving ? 'Archiviert...' : 'Archivieren'}</button>
        )}
      </div>
    </article>
  )
}

function HydroStepper({ currentStep }: { currentStep: number }) {
  const steps = ['System', 'Volumen', 'Layout', 'Technik', 'Prüfen']
  return (
    <div className="hydro-stepper">
      {steps.map((label, index) => {
        const stepNumber = index + 1
        return (
          <div key={label} className={classNames('hydro-step', stepNumber === currentStep && 'active', stepNumber < currentStep && 'done')}>
            <span>{stepNumber}</span>
            <strong>{label}</strong>
          </div>
        )
      })}
    </div>
  )
}

function RdwcLayoutPreview({ draft, setup }: { draft?: HydroDraft; setup?: HydroSetupDto }) {
  const hydroStyle = draft?.hydroStyle ?? setup?.hydroStyle ?? 'DWC'
  const layoutType = draft?.layoutType ?? setup?.layoutType ?? 'SingleBucket'
  const reservoirPosition = draft?.reservoirPosition ?? setup?.reservoirPosition ?? 'None'
  const potCount = Math.max(1, toIntOrNull(draft?.potCount ?? '') ?? setup?.potCount ?? 1)

  if (hydroStyle === 'DWC') {
    return (
      <div className="rdwc-preview dwc-preview">
        <div className="rdwc-site">DWC</div>
      </div>
    )
  }

  const columns = layoutColumns(layoutType, potCount)
  const rows = Math.ceil(potCount / columns)
  const sites = Array.from({ length: potCount }, (_, index) => index + 1)

  return (
    <div className={classNames('rdwc-preview', `tank-${reservoirPosition.toLowerCase()}`)}>
      {reservoirPosition !== 'None' && <div className="rdwc-tank">Tank</div>}
      <div className="rdwc-site-grid" style={{ gridTemplateColumns: `repeat(${columns}, minmax(56px, 1fr))` }}>
        {sites.map((site) => (
          <div key={site} className="rdwc-site">Site {site}</div>
        ))}
      </div>
      <div className="rdwc-loop-hint">{columns} × {rows} · {formatReservoirPosition(reservoirPosition)}</div>
    </div>
  )
}

function ReviewRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="review-row">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function InfoTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="info-tile">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function AlertBar({ title, message }: { title: string; message: string }) {
  return (
    <div className="alert-bar">
      <div className="alert-dot" />
      <strong>{title}</strong>
      <span>{message}</span>
    </div>
  )
}

function createHydroDraft(displayOrder = 1, tentId: number | null = null): HydroDraft {
  return {
    name: '',
    tentId: tentId ? String(tentId) : '',
    hydroStyle: 'RDWC',
    potCount: '4',
    potSizeLiters: '19',
    reservoirLiters: '60',
    layoutType: 'Grid2x2',
    reservoirPosition: 'Left',
    hasCirculationPump: true,
    circulationPumpNotes: '',
    hasAirPump: true,
    airPumpNotes: '',
    airStoneCount: '4',
    hasChiller: false,
    hasUvSterilizer: false,
    notes: '',
    displayOrder: String(displayOrder),
  }
}

function createHydroDraftFromSetup(setup: HydroSetupDto): HydroDraft {
  return {
    name: setup.name,
    tentId: setup.tentId ? String(setup.tentId) : '',
    hydroStyle: setup.hydroStyle === 'DWC' ? 'DWC' : 'RDWC',
    potCount: setup.potCount !== null ? String(setup.potCount) : '',
    potSizeLiters: setup.potSizeLiters !== null ? String(setup.potSizeLiters) : '',
    reservoirLiters: setup.reservoirLiters !== null ? String(setup.reservoirLiters) : '',
    layoutType: setup.layoutType,
    reservoirPosition: setup.reservoirPosition,
    hasCirculationPump: setup.hasCirculationPump,
    circulationPumpNotes: setup.circulationPumpNotes ?? '',
    hasAirPump: setup.hasAirPump,
    airPumpNotes: setup.airPumpNotes ?? '',
    airStoneCount: setup.airStoneCount !== null ? String(setup.airStoneCount) : '',
    hasChiller: setup.hasChiller,
    hasUvSterilizer: setup.hasUvSterilizer,
    notes: setup.notes ?? '',
    displayOrder: String(setup.displayOrder),
  }
}

function hydroDraftToRequest(draft: HydroDraft): CreateHydroSetupRequest {
  const hydroStyle = draft.hydroStyle

  return {
    tentId: toIntOrNull(draft.tentId),
    name: draft.name.trim(),
    hydroStyle,
    potCount: hydroStyle === 'DWC' ? 1 : toIntOrNull(draft.potCount),
    potSizeLiters: toNumberOrNull(draft.potSizeLiters),
    reservoirLiters: toNumberOrNull(draft.reservoirLiters),
    layoutType: hydroStyle === 'DWC' ? 'SingleBucket' : draft.layoutType,
    reservoirPosition: hydroStyle === 'DWC' ? 'None' : draft.reservoirPosition,
    hasCirculationPump: draft.hasCirculationPump,
    circulationPumpNotes: toNullableString(draft.circulationPumpNotes),
    hasAirPump: draft.hasAirPump,
    airPumpNotes: toNullableString(draft.airPumpNotes),
    airStoneCount: toIntOrNull(draft.airStoneCount),
    hasChiller: draft.hasChiller,
    hasUvSterilizer: draft.hasUvSterilizer,
    notes: toNullableString(draft.notes),
    displayOrder: toIntOrDefault(draft.displayOrder, 0),
  }
}

function validateStep(draft: HydroDraft, currentStep: number): string | null {
  if (currentStep === 1) {
    if (!draft.name.trim()) return 'Bitte gib dem Hydro-Setup einen Namen.'
    if (!draft.tentId) return 'Bitte wähle ein Zelt.'
  }

  if (currentStep === 2) {
    if (draft.hydroStyle === 'RDWC' && (toIntOrNull(draft.potCount) ?? 0) < 2) return 'RDWC braucht mindestens zwei Sites.'
    if ((toNumberOrNull(draft.potSizeLiters) ?? 0) <= 0) return 'Bitte gib das Topf-/Site-Volumen an.'
    if (draft.hydroStyle === 'RDWC' && (toNumberOrNull(draft.reservoirLiters) ?? 0) <= 0) return 'Bitte gib das Reservoirvolumen an.'
  }

  if (currentStep === 3 && draft.hydroStyle === 'RDWC') {
    if (draft.layoutType === 'SingleBucket') return 'RDWC braucht ein RDWC-Layout.'
    if (draft.reservoirPosition === 'None') return 'RDWC braucht eine Tankposition.'
  }

  if (currentStep === 4 && (toIntOrNull(draft.airStoneCount) ?? 0) < 0) {
    return 'Luftstein-Anzahl darf nicht negativ sein.'
  }

  return null
}

function validateAll(draft: HydroDraft): string | null {
  for (let currentStep = 1; currentStep <= 4; currentStep += 1) {
    const message = validateStep(draft, currentStep)
    if (message) return message
  }

  return null
}

function calculateTotalVolume(draft: HydroDraft): number | null {
  const potCount = draft.hydroStyle === 'DWC' ? 1 : toIntOrNull(draft.potCount)
  const potSize = toNumberOrNull(draft.potSizeLiters)
  const reservoir = toNumberOrNull(draft.reservoirLiters) ?? 0

  if (!potCount || potSize === null) return reservoir > 0 ? reservoir : null

  return potCount * potSize + reservoir
}

function layoutColumns(layoutType: HydroSetupLayoutType, potCount: number): number {
  if (layoutType === 'Row') return potCount
  if (layoutType === 'Grid2x2') return 2
  if (layoutType === 'Grid2x3') return 3
  if (layoutType === 'Grid2x4') return 4
  return Math.min(Math.max(1, potCount), 4)
}

function sortSetups(items: HydroSetupDto[]): HydroSetupDto[] {
  return [...items].sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name))
}

function sortTents(items: TentDto[]): TentDto[] {
  return [...items].sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name))
}

function formatLayout(value: HydroSetupLayoutType): string {
  switch (value) {
    case 'SingleBucket':
      return 'Einzelbucket'
    case 'Row':
      return 'Reihe'
    case 'Grid2x2':
      return '2 × 2'
    case 'Grid2x3':
      return '2 × 3'
    case 'Grid2x4':
      return '2 × 4'
    case 'Custom':
      return 'Custom'
    default:
      return value
  }
}

function formatReservoirPosition(value: ReservoirPosition): string {
  switch (value) {
    case 'None':
      return 'Kein Tank'
    case 'Left':
      return 'links'
    case 'Right':
      return 'rechts'
    case 'Top':
      return 'oben'
    case 'Bottom':
      return 'unten'
    case 'External':
      return 'extern'
    default:
      return value
  }
}

function formatStatus(value: HydroSetupDto['status']): string {
  return value === 'Archived' ? 'Archiviert' : 'Aktiv'
}

function formatApiError(caught: unknown, fallback: string): string {
  if (!(caught instanceof ApiRequestError)) return fallback
  const fieldErrors = caught.payload?.fieldErrors
  if (!fieldErrors) return caught.message
  const messages = Object.values(fieldErrors).flat()
  return messages.length > 0 ? messages.join(' ') : caught.message
}

function toMinimumString(value: string, minimum: number): string {
  const parsed = toIntOrNull(value)
  return String(Math.max(parsed ?? minimum, minimum))
}

function toNullableString(value: string): string | null {
  const trimmed = value.trim()
  return trimmed ? trimmed : null
}

function toIntOrNull(value: string): number | null {
  if (!value.trim()) return null
  const parsed = Number.parseInt(value, 10)
  return Number.isFinite(parsed) ? parsed : null
}

function toIntOrDefault(value: string, fallback: number): number {
  return toIntOrNull(value) ?? fallback
}

function toNumberOrNull(value: string): number | null {
  if (!value.trim()) return null
  const parsed = Number.parseFloat(value.replace(',', '.'))
  return Number.isFinite(parsed) ? parsed : null
}

export default HydroPage
