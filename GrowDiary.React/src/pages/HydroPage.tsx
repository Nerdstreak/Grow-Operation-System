import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { CreateHydroSetupRequest, GrowSummary, HydroSetupDto, HydroSetupLayoutType, ReservoirPosition, SelectableHydroStyle, TentDto, UpdateHydroSetupRequest } from '../types'
import { RdwcPreview } from '../components/RdwcPreview'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1LinkButton, V1Page, V1Section, V1Stat, V1Switch, V1Wizard } from '../components/v1'
import { draftNumber, formatLiters, toNullableFloat, toNullableInt, toNullableString } from '../components/v1-utils'
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
  const navigate = useNavigate()
  const location = useLocation()
  const routeCreateMode = location.pathname.endsWith('/new')

  const [tents, setTents] = useState<TentDto[]>([])
  const [setups, setSetups] = useState<HydroSetupDto[]>([])
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(routeCreateMode)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [draft, setDraft] = useState<HydroDraft>(() => createDraft())
  const [step, setStep] = useState(1)
  const [saving, setSaving] = useState<string | null>(null)
  const [selectedSetupId, setSelectedSetupId] = useState<number | null>(null)
  const [blockedDeleteSetupId, setBlockedDeleteSetupId] = useState<number | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [tentData, setupData] = await Promise.all([apiFetch<TentDto[]>('/api/settings/tents'), apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true')])
      const growData = await apiFetch<GrowSummary[]>('/api/grows?archived=false').catch(() => [])
      setTents(tentData)
      setGrows(growData)
      const sorted = sortSetups(setupData)
      setSetups(sorted)
      setSelectedSetupId((current) => sorted.some((setup) => setup.id === current) ? current : sorted.find((setup) => setup.status === 'Active')?.id ?? sorted[0]?.id ?? null)
      if (routeCreateMode) setDraft(createDraft(sorted.length + 1, tentData[0]?.id ?? null))
    } catch (caught) {
      setError(formatApiError(caught, 'Hydro-Daten konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }, [routeCreateMode])

  useEffect(() => {
    let active = true
    queueMicrotask(() => {
      if (active) void load()
    })
    return () => { active = false }
  }, [load])

  const activeSetups = useMemo(() => setups.filter((setup) => setup.status === 'Active'), [setups])
  const selectedSetup = useMemo(() => setups.find((setup) => setup.id === selectedSetupId) ?? activeSetups[0] ?? setups[0] ?? null, [activeSetups, selectedSetupId, setups])
  const totalVolume = calculateTotalVolume(draft)

  function openCreate() {
    setEditingId(null)
    setDraft(createDraft(setups.length + 1, tents[0]?.id ?? null))
    setStep(1)
    setFormOpen(true)
  }

  function closeForm() {
    setFormOpen(false)
    setEditingId(null)
    if (routeCreateMode) navigate('/hydro')
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
      closeForm()
    } catch (caught) {
      setError(formatApiError(caught, 'Hydro-Setup konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function deleteSetup(setup: HydroSetupDto) {
    if (saving) return
    const linkedGrows = getGrowsForSetup(grows, setup)
    if (linkedGrows.length > 0) {
      const showDependencyPanel = true
      if (showDependencyPanel) {
        setError(null)
        setBlockedDeleteSetupId(setup.id)
        return
      }
      setError(`${setup.name} ist mit aktiven Grows verknüpft. Öffne die betroffenen Grows oder archiviere das Setup: ${linkedGrows.map((grow) => grow.name).join(', ')}`)
      return
    }
    const confirmed = window.confirm(`${setup.name} endgültig löschen?`)
    if (!confirmed) return
    setSaving(`delete-${setup.id}`)
    setError(null)
    try {
      const response = await fetch(`/api/hydro-setups/${setup.id}`, { method: 'DELETE' })
      if (response.status === 204) {
        setSetups((current) => current.filter((item) => item.id !== setup.id))
        setSelectedSetupId((current) => current === setup.id ? null : current)
        setBlockedDeleteSetupId((current) => current === setup.id ? null : current)
        await load()
        return
      }
      if (response.status === 404) {
        setSetups((current) => current.filter((item) => item.id !== setup.id))
        setSelectedSetupId((current) => current === setup.id ? null : current)
        setBlockedDeleteSetupId((current) => current === setup.id ? null : current)
        await load()
        return
      }
      if (response.status === 409) {
        setBlockedDeleteSetupId(setup.id)
        await load()
        return
      }
      if (!response.ok) throw new Error(`Hydro-Setup konnte nicht gelöscht werden (${response.status})`)
      const saved = await response.json() as HydroSetupDto
      setSetups((current) => sortSetups(current.map((item) => item.id === saved.id ? saved : item)))
      setSelectedSetupId(saved.id)
      await load()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Hydro-Setup konnte nicht gelöscht werden.')
    } finally {
      setSaving(null)
    }
  }

  async function archiveSetup(setup: HydroSetupDto) {
    if (saving) return
    const confirmed = window.confirm(`${setup.name} archivieren?`)
    if (!confirmed) return
    setSaving(`archive-${setup.id}`)
    setError(null)
    try {
      const saved = await apiFetch<HydroSetupDto>(`/api/hydro-setups/${setup.id}/archive`, { method: 'POST' })
      setSetups((current) => sortSetups(current.map((item) => item.id === saved.id ? saved : item)))
      setSelectedSetupId(saved.id)
      await load()
    } catch (caught) {
      if (isNotFound(caught)) {
        setSetups((current) => current.filter((item) => item.id !== setup.id))
        setSelectedSetupId((current) => current === setup.id ? null : current)
        setBlockedDeleteSetupId((current) => current === setup.id ? null : current)
        await load()
        return
      }
      setError(formatApiError(caught, 'Hydro-Setup konnte nicht archiviert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function archiveLinkedGrow(grow: GrowSummary) {
    if (saving) return
    const confirmed = window.confirm(`${grow.name} beenden und archivieren?`)
    if (!confirmed) return
    setSaving(`grow-archive-${grow.id}`)
    setError(null)
    try {
      await apiFetch(`/api/grows/${grow.id}/archive`, { method: 'POST' })
      setBlockedDeleteSetupId(null)
      await load()
    } catch (caught) {
      if (isNotFound(caught)) {
        setBlockedDeleteSetupId(null)
        await load()
        return
      }
      setError(formatApiError(caught, 'Grow konnte nicht beendet werden.'))
    } finally {
      setSaving(null)
    }
  }

  if (formOpen) {
    return (
      <V1Page eyebrow="DWC/RDWC-System" title={editingId ? 'Hydro-Setup bearbeiten' : 'Hydro-Setup anlegen'} subtitle="Fokussierter Assistent. Bestehende Setups bleiben während des Anlegens ausgeblendet." action={<V1Button onClick={closeForm}>Schließen</V1Button>}>
        {error && <V1Alert message={error} tone="warn" />}
        <V1Section title={editingId ? 'Setup bearbeiten' : 'Setup anlegen'}>
          <V1Wizard steps={wizardSteps} currentStep={step} />
          <div className="v1-wizard-body">
            {step === 1 && <StepSystem draft={draft} tents={tents} onStyle={setHydroStyle} onDraft={setDraft} />}
            {step === 2 && <StepVolume draft={draft} totalVolume={totalVolume} onDraft={setDraft} />}
            {step === 3 && <StepLayout draft={draft} onDraft={setDraft} />}
            {step === 4 && <StepTech draft={draft} onDraft={setDraft} />}
            {step === 5 && <StepReview draft={draft} tents={tents} totalVolume={totalVolume} />}
          </div>
          <div className="v1-form-actions">
            <V1Button variant="ghost" onClick={() => step === 1 ? closeForm() : setStep((current) => Math.max(1, current - 1))}>{step === 1 ? 'Abbrechen' : 'Zurück'}</V1Button>
            {step < wizardSteps.length ? <V1Button variant="primary" onClick={next}>Weiter</V1Button> : <V1Button variant="primary" disabled={saving === 'setup'} onClick={() => void saveSetup()}>{saving === 'setup' ? 'Speichert...' : 'Speichern'}</V1Button>}
          </div>
        </V1Section>
      </V1Page>
    )
  }

  return (
    <V1Page eyebrow="DWC/RDWC-Systeme" title="Hydro" action={<V1Button variant="primary" onClick={openCreate}>Hydro-Setup anlegen</V1Button>}>
      {error && <V1Alert message={error} tone="warn" />}
      <section className="v1-kpi-grid">
        <V1Stat label="Aktive Setups" value={activeSetups.length} />
        <V1Stat label="Sites" value={activeSetups.reduce((sum, setup) => sum + (setup.potCount ?? 0), 0)} />
        <V1Stat label="Gesamtvolumen" value={formatNumber(activeSetups.reduce((sum, setup) => sum + (setup.totalVolumeLiters ?? 0), 0), 0)} unit="L" />
        <V1Stat label="Ohne Zelt" value={activeSetups.filter((setup) => setup.tentId == null).length} />
      </section>

      {loading ? <V1Empty title="Lade Hydro-Setups..." /> : setups.length === 0 ? <V1Empty title="Noch kein Hydro-Setup" action={<V1Button variant="primary" onClick={openCreate}>Erstes Setup anlegen</V1Button>} /> : (
        <section className="v1-hydro-layout">
          <V1Section title="Setups">
            <div className="v1-hydro-list">
              {setups.map((setup) => <button key={setup.id} type="button" className={classNames('v1-hydro-list-item', selectedSetup?.id === setup.id && 'active')} onClick={() => setSelectedSetupId(setup.id)}><strong>{setup.name}</strong><span>{setup.hydroStyle} · {setup.tentName ?? 'ohne Zelt'} · {formatLiters(setup.totalVolumeLiters)}</span></button>)}
            </div>
          </V1Section>
          {selectedSetup && <HydroDetail setup={selectedSetup} linkedGrows={getGrowsForSetup(grows, selectedSetup)} deleteBlocked={blockedDeleteSetupId === selectedSetup.id} saving={saving === `delete-${selectedSetup.id}` || saving === `archive-${selectedSetup.id}`} savingKey={saving} onEdit={openEdit} onDelete={deleteSetup} onArchive={archiveSetup} onArchiveGrow={archiveLinkedGrow} />}
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
  const isDwc = draft.hydroStyle === 'DWC'
  const safePotCount = toNullableInt(draft.potCount) ?? (isDwc ? 1 : 4)

  return (
    <div className="hydro-layout-step">
      <div className="hydro-layout-controls" data-audit="hydro-layout-controls">
        <V1Field label="Sites">
          <input
            data-audit="hydro-pot-count"
            type="number"
            min={isDwc ? 1 : 2}
            max={12}
            value={safePotCount}
            disabled={isDwc}
            onChange={(event) => onDraft((current) => ({ ...current, potCount: String(Number.parseInt(event.target.value, 10) || (isDwc ? 1 : 2)) }))}
          />
        </V1Field>
        <V1Field label="Layout-Typ">
          <select
            data-audit="hydro-layout-select"
            value={isDwc ? 'SingleBucket' : draft.layoutType}
            disabled={isDwc}
            onChange={(event) => onDraft((current) => ({ ...current, layoutType: event.target.value as HydroSetupLayoutType }))}
          >
            {layoutOptions.map((value) => <option key={value} value={value}>{formatLayout(value)}</option>)}
          </select>
        </V1Field>
        <V1Field label="Tankposition">
          <select
            data-audit="hydro-reservoir-select"
            value={isDwc ? 'None' : draft.reservoirPosition}
            disabled={isDwc}
            onChange={(event) => onDraft((current) => ({ ...current, reservoirPosition: event.target.value as ReservoirPosition }))}
          >
            {reservoirPositions.map((value) => <option key={value} value={value}>{formatReservoirPosition(value)}</option>)}
          </select>
        </V1Field>
      </div>
      <RdwcPreview
        hydroStyle={draft.hydroStyle}
        layoutType={draft.layoutType}
        potCount={safePotCount}
        reservoirPosition={draft.reservoirPosition}
      />
    </div>
  )
}

function StepTech({ draft, onDraft }: { draft: HydroDraft; onDraft: (setter: (current: HydroDraft) => HydroDraft) => void }) {
  return <div className="v1-form-grid"><V1Switch label="Umwälzpumpe" checked={draft.hasCirculationPump} onChange={(checked) => onDraft((current) => ({ ...current, hasCirculationPump: checked }))} /><V1Field label="Pumpen-Notiz"><input value={draft.circulationPumpNotes} onChange={(event) => onDraft((current) => ({ ...current, circulationPumpNotes: event.target.value }))} /></V1Field><V1Switch label="Luftpumpe" checked={draft.hasAirPump} onChange={(checked) => onDraft((current) => ({ ...current, hasAirPump: checked }))} /><V1Field label="Luftsteine"><input type="number" min="0" value={draft.airStoneCount} onChange={(event) => onDraft((current) => ({ ...current, airStoneCount: event.target.value }))} /></V1Field><V1Switch label="Chiller" checked={draft.hasChiller} onChange={(checked) => onDraft((current) => ({ ...current, hasChiller: checked }))} /><V1Switch label="UV-C" checked={draft.hasUvSterilizer} onChange={(checked) => onDraft((current) => ({ ...current, hasUvSterilizer: checked }))} /><V1Field label="Notizen" wide><textarea rows={3} value={draft.notes} onChange={(event) => onDraft((current) => ({ ...current, notes: event.target.value }))} /></V1Field></div>
}

function StepReview({ draft, tents, totalVolume }: { draft: HydroDraft; tents: TentDto[]; totalVolume: number | null }) {
  const tent = tents.find((item) => String(item.id) === draft.tentId)
  return <div className="v1-review-layout hydro-review-layout"><V1Card><div className="v1-info-grid"><Info label="Name" value={draft.name || '–'} /><Info label="Zelt" value={tent?.name ?? '–'} /><Info label="Typ" value={draft.hydroStyle} /><Info label="Sites" value={draft.potCount || '–'} /><Info label="Topf" value={`${draft.potSizeLiters || '–'} L`} /><Info label="Tank" value={`${draft.reservoirLiters || '–'} L`} /><Info label="Gesamt" value={`${formatNumber(totalVolume, 1)} L`} /><Info label="Layout" value={formatLayout(draft.layoutType)} /><Info label="Tankposition" value={formatReservoirPosition(draft.reservoirPosition)} /></div></V1Card><RdwcPreview hydroStyle={draft.hydroStyle} layoutType={draft.layoutType} potCount={toNullableInt(draft.potCount) ?? 1} reservoirPosition={draft.reservoirPosition} /></div>
}

function HydroDetail({ setup, linkedGrows, deleteBlocked, saving, savingKey, onEdit, onArchive, onDelete, onArchiveGrow }: { setup: HydroSetupDto; linkedGrows: GrowSummary[]; deleteBlocked: boolean; saving: boolean; savingKey: string | null; onEdit: (setup: HydroSetupDto) => void; onArchive: (setup: HydroSetupDto) => void; onDelete: (setup: HydroSetupDto) => void; onArchiveGrow: (grow: GrowSummary) => void }) {
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
            <V1LinkButton to={`/hydro/${setup.id}`} variant="primary">Öffnen</V1LinkButton>
            <V1Button onClick={() => onEdit(setup)}>Bearbeiten</V1Button>
            <V1Button disabled={saving} onClick={() => void onArchive(setup)}>Archivieren</V1Button>
            <V1Button variant="danger" disabled={saving} onClick={() => void onDelete(setup)}>{saving ? 'Löscht...' : 'Löschen'}</V1Button>
          </div>
          {linkedGrows.length > 0 && <div className="v1-list">{linkedGrows.map((grow) => <Link key={grow.id} to={`/grows/${grow.id}`} className="v1-list-row"><strong>{grow.name}</strong><span>Verknüpfter aktiver Grow</span></Link>)}</div>}
          {deleteBlocked && linkedGrows.length > 0 && (
            <div className={classNames('dependency-panel', deleteBlocked && 'active')} data-audit="hydro-delete-blocked">
              <strong>{deleteBlocked ? 'Löschen blockiert' : 'Aktive Grows'}</strong>
              <p>Dieses Hydro-Setup ist mit aktiven oder geplanten Grows verknüpft. Beende oder verwalte die betroffenen Grows, danach ist Löschen erneut möglich.</p>
              <div className="v1-list">
                {linkedGrows.map((grow) => (
                  <div key={grow.id} className="v1-list-row dependency-row">
                    <div>
                      <strong>{grow.name}</strong>
                      <span>{grow.status ?? 'aktiv'}</span>
                    </div>
                    <div className="dependency-row-actions">
                      <V1LinkButton to={`/grows/${grow.id}`} variant="primary">Verwalten</V1LinkButton>
                      <V1LinkButton to={`/grows/${grow.id}/setup`}>Bearbeiten</V1LinkButton>
                      <V1Button disabled={savingKey === `grow-archive-${grow.id}`} onClick={() => void onArchiveGrow(grow)}>{savingKey === `grow-archive-${grow.id}` ? 'Beendet...' : 'Beenden'}</V1Button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </V1Card>
        <RdwcPreview compact hydroStyle={setup.hydroStyle === 'DWC' ? 'DWC' : 'RDWC'} layoutType={setup.layoutType} potCount={setup.potCount ?? 1} reservoirPosition={setup.reservoirPosition} />
      </div>
    </V1Section>
  )
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
function formatLayout(value: HydroSetupLayoutType) { return value === 'SingleBucket' ? 'Einzeleimer' : value === 'Row' ? 'Reihe' : value === 'Grid2x2' ? '2×2' : value === 'Grid2x3' ? '2×3' : value === 'Grid2x4' ? '2×4' : 'Flexibel' }
function getGrowsForSetup(grows: GrowSummary[], setup: HydroSetupDto) {
  const activeGrows = grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning')
  const direct = activeGrows.filter((grow) => grow.systemId === setup.id || grow.setupId === setup.id)
  if (direct.length > 0 || !setup.activeGrowCount) return direct
  return activeGrows.filter((grow) => grow.tentId === setup.tentId)
}
function formatReservoirPosition(value: ReservoirPosition) { return value === 'None' ? 'keiner' : value === 'Left' ? 'links' : value === 'Right' ? 'rechts' : value === 'Top' ? 'oben' : value === 'Bottom' ? 'unten' : 'extern' }
function formatApiError(caught: unknown, fallback: string) { return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback }
function isNotFound(caught: unknown) { return caught instanceof ApiRequestError && caught.status === 404 }

export default HydroPage
