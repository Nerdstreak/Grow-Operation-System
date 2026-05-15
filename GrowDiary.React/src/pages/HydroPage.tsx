import { useEffect, useMemo, useState } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type { CreateHydroSetupRequest, HydroSetupDto, HydroSetupLayoutType, ReservoirPosition, SelectableHydroStyle, TentDto, UpdateHydroSetupRequest } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section, V1Stat, V1Switch, V1Wizard, draftNumber, formatLiters, toNullableFloat, toNullableInt, toNullableString } from '../components/v1'
import { classNames, formatNumber } from '../utils'

type HydroDraft = {
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

const wizardSteps = ['System', 'Volumen', 'Layout', 'Technik', 'Prüfen']
const layoutOptions: HydroSetupLayoutType[] = ['SingleBucket', 'Row', 'Grid2x2', 'Grid2x3', 'Grid2x4', 'Custom']
const reservoirPositions: ReservoirPosition[] = ['None', 'Left', 'Right', 'Top', 'Bottom', 'External']

function HydroPage() {
  const [tents, setTents] = useState<TentDto[]>([])
  const [setups, setSetups] = useState<HydroSetupDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [draft, setDraft] = useState<HydroDraft>(() => createDraft())
  const [step, setStep] = useState(1)
  const [saving, setSaving] = useState<string | null>(null)
  const [selectedSetupId, setSelectedSetupId] = useState<number | null>(null)

  useEffect(() => { void load() }, [])

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const [tentData, setupData] = await Promise.all([apiFetch<TentDto[]>('/api/settings/tents'), apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true')])
      setTents(tentData)
      const sorted = sortSetups(setupData)
      setSetups(sorted)
      setSelectedSetupId((current) => current ?? sorted.find((setup) => setup.status === 'Active')?.id ?? sorted[0]?.id ?? null)
    } catch (caught) {
      setError(formatApiError(caught, 'Hydro-Daten konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }

  const activeSetups = useMemo(() => setups.filter((setup) => setup.status === 'Active'), [setups])
  const selectedSetup = useMemo(() => setups.find((setup) => setup.id === selectedSetupId) ?? activeSetups[0] ?? setups[0] ?? null, [activeSetups, selectedSetupId, setups])
  const totalVolume = calculateTotalVolume(draft)

  function openCreate() {
    setEditingId(null)
    setDraft(createDraft(setups.length + 1, tents[0]?.id ?? null))
    setStep(1)
    setFormOpen(true)
  }

  function openEdit(setup: HydroSetupDto) {
    setEditingId(setup.id)
    setDraft(createDraftFromSetup(setup))
    setStep(1)
    setFormOpen(true)
  }

  function setHydroStyle(value: SelectableHydroStyle) {
    setDraft((current) => value === 'DWC'
      ? { ...current, hydroStyle: 'DWC', potCount: '1', reservoirPosition: 'None', layoutType: 'SingleBucket' }
      : { ...current, hydroStyle: 'RDWC', potCount: String(Math.max(2, toNullableInt(current.potCount) ?? 2)), reservoirPosition: current.reservoirPosition === 'None' ? 'Left' : current.reservoirPosition, layoutType: current.layoutType === 'SingleBucket' ? 'Grid2x2' : current.layoutType })
  }

  function next() {
    const message = validateStep(draft, step)
    if (message) { setError(message); return }
    setError(null)
    setStep((current) => Math.min(wizardSteps.length, current + 1))
  }

  async function saveSetup() {
    if (step !== wizardSteps.length) {
      setError('Gespeichert wird erst im Schritt Prüfen.')
      return
    }
    const message = validateAll(draft)
    if (message) { setError(message); return }
    setSaving('setup')
    setError(null)
    try {
      const request = draftToRequest(draft)
      if (editingId) {
        const existing = setups.find((setup) => setup.id === editingId)
        if (!existing) throw new Error('Hydro-Setup nicht gefunden.')
        const saved = await apiFetch<HydroSetupDto>(`/api/hydro-setups/${editingId}`, { method: 'PUT', body: JSON.stringify({ ...request, status: existing.status } satisfies UpdateHydroSetupRequest) })
        setSetups((current) => sortSetups(current.map((setup) => setup.id === saved.id ? saved : setup)))
        setSelectedSetupId(saved.id)
      } else {
        const created = await apiFetch<HydroSetupDto>('/api/hydro-setups', { method: 'POST', body: JSON.stringify(request) })
        setSetups((current) => sortSetups([...current, created]))
        setSelectedSetupId(created.id)
      }
      setFormOpen(false)
      setEditingId(null)
    } catch (caught) {
      setError(formatApiError(caught, 'Hydro-Setup konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function archiveSetup(setup: HydroSetupDto) {
    setSaving(`archive-${setup.id}`)
    setError(null)
    try {
      const saved = await apiFetch<HydroSetupDto>(`/api/hydro-setups/${setup.id}/archive`, { method: 'POST' })
      setSetups((current) => sortSetups(current.map((item) => item.id === saved.id ? saved : item)))
    } catch (caught) {
      setError(formatApiError(caught, 'Hydro-Setup konnte nicht archiviert werden.'))
    } finally {
      setSaving(null)
    }
  }

  return (
    <V1Page eyebrow="DWC/RDWC-Systeme" title="Hydro" action={<V1Button variant="primary" onClick={openCreate}>Hydro-Setup anlegen</V1Button>}>
      {error && <V1Alert message={error} tone="warn" />}
      <section className="v1-kpi-grid">
        <V1Stat label="Setups" value={setups.length} />
        <V1Stat label="Aktiv" value={activeSetups.length} />
        <V1Stat label="Gesamtvolumen" value={formatNumber(activeSetups.reduce((sum, setup) => sum + (setup.totalVolumeLiters ?? 0), 0), 0)} unit="L" />
        <V1Stat label="Zelte" value={tents.length} />
      </section>

      {formOpen && (
        <V1Section title={editingId ? 'Hydro-Setup bearbeiten' : 'Hydro-Setup anlegen'} action={<V1Button onClick={() => setFormOpen(false)}>Schließen</V1Button>}>
          <V1Wizard steps={wizardSteps} currentStep={step} />
          <div className="v1-wizard-body">
            {step === 1 && <StepSystem draft={draft} tents={tents} onStyle={setHydroStyle} onDraft={setDraft} />}
            {step === 2 && <StepVolume draft={draft} totalVolume={totalVolume} onDraft={setDraft} />}
            {step === 3 && <StepLayout draft={draft} onDraft={setDraft} />}
            {step === 4 && <StepTech draft={draft} onDraft={setDraft} />}
            {step === 5 && <StepReview draft={draft} tents={tents} totalVolume={totalVolume} />}
          </div>
          <div className="v1-form-actions">
            <V1Button variant="ghost" onClick={() => step === 1 ? setFormOpen(false) : setStep((current) => Math.max(1, current - 1))}>{step === 1 ? 'Abbrechen' : 'Zurück'}</V1Button>
            {step < wizardSteps.length ? <V1Button variant="primary" onClick={next}>Weiter</V1Button> : <V1Button variant="primary" disabled={saving === 'setup'} onClick={() => void saveSetup()}>{saving === 'setup' ? 'Speichert...' : 'Speichern'}</V1Button>}
          </div>
        </V1Section>
      )}

      {loading ? <V1Empty title="Lade Hydro-Setups..." /> : setups.length === 0 ? <V1Empty title="Noch kein Hydro-Setup" action={<V1Button variant="primary" onClick={openCreate}>Erstes Setup anlegen</V1Button>} /> : (
        <section className="v1-hydro-layout">
          <V1Section title="Setups">
            <div className="v1-hydro-list">
              {setups.map((setup) => <button key={setup.id} type="button" className={classNames('v1-hydro-list-item', selectedSetup?.id === setup.id && 'active')} onClick={() => setSelectedSetupId(setup.id)}><strong>{setup.name}</strong><span>{setup.hydroStyle} · {setup.tentName ?? 'ohne Zelt'} · {formatLiters(setup.totalVolumeLiters)}</span></button>)}
            </div>
          </V1Section>
          {selectedSetup && <HydroDetail setup={selectedSetup} saving={saving === `archive-${selectedSetup.id}`} onEdit={openEdit} onArchive={archiveSetup} />}
        </section>
      )}
    </V1Page>
  )
}

function StepSystem({ draft, tents, onStyle, onDraft }: { draft: HydroDraft; tents: TentDto[]; onStyle: (value: SelectableHydroStyle) => void; onDraft: (setter: (current: HydroDraft) => HydroDraft) => void }) {
  return <div className="v1-form-grid"><V1Field label="Name"><input value={draft.name} onChange={(event) => onDraft((current) => ({ ...current, name: event.target.value }))} placeholder="RDWC 4-Site" /></V1Field><V1Field label="Zelt"><select value={draft.tentId} onChange={(event) => onDraft((current) => ({ ...current, tentId: event.target.value }))}><option value="">Zelt wählen</option>{tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}</select></V1Field><div className="v1-choice-grid is-wide"><button type="button" className={classNames('v1-choice', draft.hydroStyle === 'DWC' && 'active')} onClick={() => onStyle('DWC')}><strong>DWC</strong><span>Ein Eimer / Bucket</span></button><button type="button" className={classNames('v1-choice', draft.hydroStyle === 'RDWC' && 'active')} onClick={() => onStyle('RDWC')}><strong>RDWC</strong><span>Rezirkulierendes System</span></button></div></div>
}

function StepVolume({ draft, totalVolume, onDraft }: { draft: HydroDraft; totalVolume: number | null; onDraft: (setter: (current: HydroDraft) => HydroDraft) => void }) {
  return <div className="v1-form-grid"><V1Field label="Sites / Töpfe"><input type="number" min="1" value={draft.potCount} onChange={(event) => onDraft((current) => ({ ...current, potCount: event.target.value }))} disabled={draft.hydroStyle === 'DWC'} /></V1Field><V1Field label="Liter pro Topf"><input inputMode="decimal" value={draft.potSizeLiters} onChange={(event) => onDraft((current) => ({ ...current, potSizeLiters: event.target.value }))} /></V1Field><V1Field label="Tank / Reservoir L"><input inputMode="decimal" value={draft.reservoirLiters} onChange={(event) => onDraft((current) => ({ ...current, reservoirLiters: event.target.value }))} /></V1Field><V1Card className="is-wide"><V1Stat label="Gesamtvolumen" value={formatNumber(totalVolume, 1)} unit="L" hint="Sites × Topf + Tank" /></V1Card></div>
}

function StepLayout({ draft, onDraft }: { draft: HydroDraft; onDraft: (setter: (current: HydroDraft) => HydroDraft) => void }) {
  return <div className="v1-form-grid"><V1Field label="Layout"><select value={draft.layoutType} onChange={(event) => onDraft((current) => ({ ...current, layoutType: event.target.value as HydroSetupLayoutType }))}>{layoutOptions.map((value) => <option key={value} value={value}>{formatLayout(value)}</option>)}</select></V1Field><V1Field label="Tankposition"><select value={draft.reservoirPosition} onChange={(event) => onDraft((current) => ({ ...current, reservoirPosition: event.target.value as ReservoirPosition }))}>{reservoirPositions.map((value) => <option key={value} value={value}>{formatReservoirPosition(value)}</option>)}</select></V1Field><div className="is-wide"><RdwcLayoutPreview draft={draft} /></div></div>
}

function StepTech({ draft, onDraft }: { draft: HydroDraft; onDraft: (setter: (current: HydroDraft) => HydroDraft) => void }) {
  return <div className="v1-form-grid"><V1Switch label="Umwälzpumpe" checked={draft.hasCirculationPump} onChange={(checked) => onDraft((current) => ({ ...current, hasCirculationPump: checked }))} /><V1Field label="Pumpen-Notiz"><input value={draft.circulationPumpNotes} onChange={(event) => onDraft((current) => ({ ...current, circulationPumpNotes: event.target.value }))} /></V1Field><V1Switch label="Luftpumpe" checked={draft.hasAirPump} onChange={(checked) => onDraft((current) => ({ ...current, hasAirPump: checked }))} /><V1Field label="Luftsteine"><input type="number" min="0" value={draft.airStoneCount} onChange={(event) => onDraft((current) => ({ ...current, airStoneCount: event.target.value }))} /></V1Field><V1Switch label="Chiller" checked={draft.hasChiller} onChange={(checked) => onDraft((current) => ({ ...current, hasChiller: checked }))} /><V1Switch label="UV-C" checked={draft.hasUvSterilizer} onChange={(checked) => onDraft((current) => ({ ...current, hasUvSterilizer: checked }))} /><V1Field label="Notizen" wide><textarea rows={3} value={draft.notes} onChange={(event) => onDraft((current) => ({ ...current, notes: event.target.value }))} /></V1Field></div>
}

function StepReview({ draft, tents, totalVolume }: { draft: HydroDraft; tents: TentDto[]; totalVolume: number | null }) {
  const tent = tents.find((item) => String(item.id) === draft.tentId)
  return <div className="v1-review-layout"><V1Card><div className="v1-info-grid"><Info label="Name" value={draft.name || '–'} /><Info label="Zelt" value={tent?.name ?? '–'} /><Info label="Typ" value={draft.hydroStyle} /><Info label="Sites" value={draft.potCount || '–'} /><Info label="Topf" value={`${draft.potSizeLiters || '–'} L`} /><Info label="Tank" value={`${draft.reservoirLiters || '–'} L`} /><Info label="Gesamt" value={`${formatNumber(totalVolume, 1)} L`} /><Info label="Layout" value={formatLayout(draft.layoutType)} /></div></V1Card><RdwcLayoutPreview draft={draft} /></div>
}

function HydroDetail({ setup, saving, onEdit, onArchive }: { setup: HydroSetupDto; saving: boolean; onEdit: (setup: HydroSetupDto) => void; onArchive: (setup: HydroSetupDto) => void }) {
  const facts = [
    ['Zelt', setup.tentName ?? '–'],
    ['Sites', String(setup.potCount ?? '–')],
    ['Topf', formatLiters(setup.potSizeLiters)],
    ['Tank', formatLiters(setup.reservoirLiters)],
    ['Gesamt', formatLiters(setup.totalVolumeLiters)],
    ['Layout', formatLayout(setup.layoutType)],
    ['Tankposition', formatReservoirPosition(setup.reservoirPosition)],
    ['Luftsteine', String(setup.airStoneCount ?? '–')],
  ]

  return (
    <V1Section title={setup.name} action={<V1Badge tone={setup.status === 'Active' ? 'ok' : 'neutral'}>{setup.status === 'Active' ? 'aktiv' : 'Archiv'}</V1Badge>} className="v1-hydro-detail-section">
      <div className="v1-hydro-detail rc2">
        <V1Card className="v1-hydro-summary rc2">
          <div className="v1-hydro-title-line rc2">
            <div>
              <span className="v1-card-kicker">{setup.hydroStyle}</span>
              <strong>{setup.name}</strong>
            </div>
            <V1Stat label="Volumen" value={formatNumber(setup.totalVolumeLiters, 0)} unit="L" />
          </div>

          <div className="v1-hydro-facts">
            {facts.map(([label, value]) => <Fact key={label} label={label} value={value} />)}
          </div>

          <div className="v1-chip-row">
            {setup.hasCirculationPump && <span>Umwälzpumpe</span>}
            {setup.hasAirPump && <span>Luftpumpe</span>}
            {setup.hasChiller && <span>Chiller</span>}
            {setup.hasUvSterilizer && <span>UV-C</span>}
            {!setup.hasCirculationPump && !setup.hasAirPump && !setup.hasChiller && !setup.hasUvSterilizer && <span>Technik offen</span>}
          </div>

          <div className="v1-action-row">
            <V1Button onClick={() => onEdit(setup)}>Bearbeiten</V1Button>
            {setup.status === 'Active' && <V1Button variant="ghost" disabled={saving} onClick={() => void onArchive(setup)}>{saving ? 'Archiviert...' : 'Archivieren'}</V1Button>}
          </div>
        </V1Card>
        <RdwcLayoutPreview setup={setup} />
      </div>
    </V1Section>
  )
}

function RdwcLayoutPreview({ draft, setup }: { draft?: HydroDraft; setup?: HydroSetupDto }) {
  const hydroStyle = draft?.hydroStyle ?? setup?.hydroStyle ?? 'DWC'
  const layoutType = draft?.layoutType ?? setup?.layoutType ?? 'SingleBucket'
  const reservoirPosition = draft?.reservoirPosition ?? setup?.reservoirPosition ?? 'None'
  const potCount = Math.max(1, toNullableInt(draft?.potCount ?? '') ?? setup?.potCount ?? 1)
  const columns = hydroStyle === 'DWC' ? 1 : layoutColumns(layoutType, potCount)
  const sites = Array.from({ length: potCount }, (_, index) => index + 1)
  return <div className={classNames('v1-rdwc-map', `tank-${reservoirPosition.toLowerCase()}`)}><div className="v1-rdwc-inner">{hydroStyle === 'RDWC' && reservoirPosition !== 'None' && <div className="v1-rdwc-tank">Tank</div>}<div className="v1-rdwc-grid" style={{ gridTemplateColumns: `repeat(${columns}, minmax(64px, 1fr))` }}>{sites.map((site) => <div key={site} className="v1-rdwc-site">{hydroStyle === 'DWC' ? 'DWC' : site}</div>)}</div></div><span>{formatLayout(layoutType)} · Tank {formatReservoirPosition(reservoirPosition)}</span></div>
}

function Info({ label, value }: { label: string; value: string }) { return <div className="v1-info"><span>{label}</span><strong>{value}</strong></div> }
function Fact({ label, value }: { label: string; value: string }) { return <div className="v1-fact"><span>{label}</span><strong>{value}</strong></div> }
function sortSetups(items: HydroSetupDto[]) { return [...items].sort((a, b) => a.status.localeCompare(b.status) || a.displayOrder - b.displayOrder || a.name.localeCompare(b.name)) }
function createDraft(displayOrder = 1, tentId: number | null = null): HydroDraft { return { name: '', tentId: tentId ? String(tentId) : '', hydroStyle: 'RDWC', potCount: '4', potSizeLiters: '19', reservoirLiters: '60', layoutType: 'Grid2x2', reservoirPosition: 'Left', hasCirculationPump: true, circulationPumpNotes: '', hasAirPump: true, airPumpNotes: '', airStoneCount: '4', hasChiller: false, hasUvSterilizer: false, notes: '', displayOrder: String(displayOrder) } }
function createDraftFromSetup(setup: HydroSetupDto): HydroDraft { return { name: setup.name, tentId: setup.tentId ? String(setup.tentId) : '', hydroStyle: setup.hydroStyle === 'DWC' ? 'DWC' : 'RDWC', potCount: draftNumber(setup.potCount), potSizeLiters: draftNumber(setup.potSizeLiters), reservoirLiters: draftNumber(setup.reservoirLiters), layoutType: setup.layoutType, reservoirPosition: setup.reservoirPosition, hasCirculationPump: setup.hasCirculationPump, circulationPumpNotes: setup.circulationPumpNotes ?? '', hasAirPump: setup.hasAirPump, airPumpNotes: setup.airPumpNotes ?? '', airStoneCount: draftNumber(setup.airStoneCount), hasChiller: setup.hasChiller, hasUvSterilizer: setup.hasUvSterilizer, notes: setup.notes ?? '', displayOrder: String(setup.displayOrder) } }
function calculateTotalVolume(draft: HydroDraft) { const count = toNullableFloat(draft.potCount) ?? 0; const pot = toNullableFloat(draft.potSizeLiters) ?? 0; const reservoir = toNullableFloat(draft.reservoirLiters) ?? 0; return count * pot + reservoir }
function validateStep(draft: HydroDraft, step: number) { if (step === 1 && !draft.name.trim()) return 'Bitte gib einen Namen ein.'; if (step === 1 && !draft.tentId) return 'Bitte wähle ein Zelt.'; if (step === 2 && (toNullableInt(draft.potCount) ?? 0) < (draft.hydroStyle === 'RDWC' ? 2 : 1)) return 'RDWC braucht mindestens zwei Sites.'; if (step === 2 && (toNullableFloat(draft.potSizeLiters) ?? 0) <= 0) return 'Topfvolumen fehlt.'; return null }
function validateAll(draft: HydroDraft) { for (let i = 1; i <= 4; i += 1) { const message = validateStep(draft, i); if (message) return message } return null }
function draftToRequest(draft: HydroDraft): CreateHydroSetupRequest { return { tentId: toNullableInt(draft.tentId), name: draft.name.trim(), hydroStyle: draft.hydroStyle, potCount: toNullableInt(draft.potCount), potSizeLiters: toNullableFloat(draft.potSizeLiters), reservoirLiters: toNullableFloat(draft.reservoirLiters), layoutType: draft.hydroStyle === 'DWC' ? 'SingleBucket' : draft.layoutType, reservoirPosition: draft.hydroStyle === 'DWC' ? 'None' : draft.reservoirPosition, hasCirculationPump: draft.hasCirculationPump, circulationPumpNotes: toNullableString(draft.circulationPumpNotes), hasAirPump: draft.hasAirPump, airPumpNotes: toNullableString(draft.airPumpNotes), airStoneCount: toNullableInt(draft.airStoneCount), hasChiller: draft.hasChiller, hasUvSterilizer: draft.hasUvSterilizer, notes: toNullableString(draft.notes), displayOrder: toNullableInt(draft.displayOrder) ?? 0 } }
function layoutColumns(layout: HydroSetupLayoutType, count: number) { if (layout === 'Grid2x2' || layout === 'Grid2x3' || layout === 'Grid2x4') return 2; if (layout === 'Row') return count; return Math.min(4, Math.max(1, Math.ceil(Math.sqrt(count)))) }
function formatLayout(value: HydroSetupLayoutType) { return value === 'SingleBucket' ? 'Einzeleimer' : value === 'Row' ? 'Reihe' : value === 'Grid2x2' ? '2×2' : value === 'Grid2x3' ? '2×3' : value === 'Grid2x4' ? '2×4' : 'Custom' }
function formatReservoirPosition(value: ReservoirPosition) { return value === 'None' ? 'keiner' : value === 'Left' ? 'links' : value === 'Right' ? 'rechts' : value === 'Top' ? 'oben' : value === 'Bottom' ? 'unten' : 'extern' }
function formatApiError(caught: unknown, fallback: string) { return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback }

export default HydroPage
