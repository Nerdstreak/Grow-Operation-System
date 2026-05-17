import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { CreateTentRequest, HydroSetupDto, TentDto, TentType, UpdateTentRequest, UpdateTentSensorRequest } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1LinkButton, V1Page, V1Section, V1Stat, V1Switch, toNullableInt, toNullableString } from '../components/v1'

const tentTypes: TentType[] = ['Production', 'Mother', 'Propagation', 'Quarantine', 'MultiPurpose']

type TentDraft = {
  name: string
  kind: string
  tentType: TentType
  displayOrder: string
  widthCm: string
  depthCm: string
  tentHeightCm: string
  lightType: string
  lightWatt: string
  exhaustFanCount: string
  exhaustM3h: string
  circulationFanCount: string
  co2Available: boolean
  notes: string
}

function TentsPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const routeCreateMode = location.pathname.endsWith('/new')

  const [tents, setTents] = useState<TentDto[]>([])
  const [hydroSetups, setHydroSetups] = useState<HydroSetupDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(routeCreateMode)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [draft, setDraft] = useState<TentDraft>(() => createDraft())
  const [saving, setSaving] = useState<string | null>(null)

  useEffect(() => { void loadTents() }, [])

  async function loadTents() {
    setLoading(true)
    setError(null)
    try {
      const [tentData, setupData] = await Promise.all([
        apiFetch<TentDto[]>('/api/settings/tents?includeArchived=true'),
        apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true'),
      ])
      setTents(sortTents(tentData))
      setHydroSetups(setupData)
      if (routeCreateMode) setDraft(createDraft(tentData.length + 1))
    } catch (caught) {
      setError(formatApiError(caught, 'Zelte konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }

  const activeTents = useMemo(() => tents.filter((tent) => tent.status === 'Active'), [tents])
  const physicalVolumeKnown = useMemo(() => activeTents.filter((tent) => tent.widthCm && tent.depthCm && tent.tentHeightCm).length, [activeTents])
  const activeHydroCount = useMemo(() => hydroSetups.filter((setup) => setup.status === 'Active').length, [hydroSetups])

  function openCreate() {
    setEditingId(null)
    setDraft(createDraft(tents.length + 1))
    setFormOpen(true)
  }

  function closeForm() {
    setFormOpen(false)
    setEditingId(null)
    if (routeCreateMode) navigate('/zelte')
  }

  function openEdit(tent: TentDto) {
    setEditingId(tent.id)
    setDraft(createDraftFromTent(tent))
    setFormOpen(true)
  }

  async function saveTent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!draft.name.trim()) {
      setError('Bitte gib einen Zeltnamen ein.')
      return
    }

    setSaving('tent')
    setError(null)
    try {
      const request = draftToRequest(draft)
      if (editingId) {
        const existing = tents.find((tent) => tent.id === editingId)
        if (!existing) throw new Error('Zelt nicht gefunden.')
        const updated = await apiFetch<TentDto>(`/api/settings/tents/${editingId}`, {
          method: 'PUT',
          body: JSON.stringify({ ...request, status: existing.status, sensors: mapSensors(existing) } satisfies UpdateTentRequest),
        })
        setTents((current) => sortTents(current.map((tent) => (tent.id === updated.id ? updated : tent))))
      } else {
        const created = await apiFetch<TentDto>('/api/settings/tents', {
          method: 'POST',
          body: JSON.stringify({ ...request, status: 'Active', sensors: [] } satisfies CreateTentRequest),
        })
        setTents((current) => sortTents([...current, created]))
      }
      closeForm()
    } catch (caught) {
      setError(formatApiError(caught, 'Zelt konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function toggleArchive(tent: TentDto) {
    setSaving(`archive-${tent.id}`)
    setError(null)
    try {
      const saved = await apiFetch<TentDto>(`/api/settings/tents/${tent.id}`, {
        method: 'PUT',
        body: JSON.stringify({ ...tentToRequest(tent), status: tent.status === 'Active' ? 'Archived' : 'Active', sensors: mapSensors(tent) } satisfies UpdateTentRequest),
      })
      setTents((current) => sortTents(current.map((item) => (item.id === saved.id ? saved : item))))
    } catch (caught) {
      setError(formatApiError(caught, 'Zeltstatus konnte nicht geändert werden.'))
    } finally {
      setSaving(null)
    }
  }

  if (formOpen) {
    return (
      <V1Page
        eyebrow="Physischer Raum"
        title={editingId ? 'Zelt bearbeiten' : 'Zelt anlegen'}
        subtitle="Nur Raum, Größe und verbaute Technik. Home-Assistant-Entities werden separat gemappt."
        action={<V1Button onClick={closeForm}>Schließen</V1Button>}
        className="rc2-focused-form"
      >
        {error && <V1Alert message={error} tone="warn" />}
        <div className="rc2-focused-layout">
          <V1Section title="Basis">
            <form className="v1-form-grid rc2-tent-form" onSubmit={(event) => void saveTent(event)}>
              <V1Field label="Name" wide><input value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} placeholder="Hauptzelt" /></V1Field>
              <V1Field label="Zweck"><select value={draft.tentType} onChange={(event) => setDraft((current) => ({ ...current, tentType: event.target.value as TentType }))}>{tentTypes.map((type) => <option key={type} value={type}>{formatTentType(type)}</option>)}</select></V1Field>
              <V1Field label="Typ"><input value={draft.kind} onChange={(event) => setDraft((current) => ({ ...current, kind: event.target.value }))} placeholder="Grow Tent" /></V1Field>
              <V1Field label="Reihenfolge"><input type="number" value={draft.displayOrder} onChange={(event) => setDraft((current) => ({ ...current, displayOrder: event.target.value }))} /></V1Field>

              <div className="v1-form-divider">Größe</div>
              <V1Field label="Breite cm"><input type="number" value={draft.widthCm} onChange={(event) => setDraft((current) => ({ ...current, widthCm: event.target.value }))} /></V1Field>
              <V1Field label="Tiefe cm"><input type="number" value={draft.depthCm} onChange={(event) => setDraft((current) => ({ ...current, depthCm: event.target.value }))} /></V1Field>
              <V1Field label="Höhe cm"><input type="number" value={draft.tentHeightCm} onChange={(event) => setDraft((current) => ({ ...current, tentHeightCm: event.target.value }))} /></V1Field>

              <div className="v1-form-divider">Technik im Raum</div>
              <V1Field label="Lichttyp"><input value={draft.lightType} onChange={(event) => setDraft((current) => ({ ...current, lightType: event.target.value }))} placeholder="LED" /></V1Field>
              <V1Field label="Watt"><input type="number" value={draft.lightWatt} onChange={(event) => setDraft((current) => ({ ...current, lightWatt: event.target.value }))} /></V1Field>
              <V1Field label="Abluft Anzahl"><input type="number" value={draft.exhaustFanCount} onChange={(event) => setDraft((current) => ({ ...current, exhaustFanCount: event.target.value }))} /></V1Field>
              <V1Field label="Abluft m³/h"><input type="number" value={draft.exhaustM3h} onChange={(event) => setDraft((current) => ({ ...current, exhaustM3h: event.target.value }))} /></V1Field>
              <V1Field label="Umluft Anzahl"><input type="number" value={draft.circulationFanCount} onChange={(event) => setDraft((current) => ({ ...current, circulationFanCount: event.target.value }))} /></V1Field>
              <V1Switch label="CO₂ vorhanden" checked={draft.co2Available} onChange={(checked) => setDraft((current) => ({ ...current, co2Available: checked }))} />
              <V1Field label="Notizen" wide><textarea rows={3} value={draft.notes} onChange={(event) => setDraft((current) => ({ ...current, notes: event.target.value }))} /></V1Field>
              <div className="v1-form-actions"><V1Button variant="ghost" onClick={closeForm}>Abbrechen</V1Button><V1Button type="submit" variant="primary" disabled={saving === 'tent'}>{saving === 'tent' ? 'Speichert...' : 'Speichern'}</V1Button></div>
            </form>
          </V1Section>

          <V1Section title="Nicht hier">
            <V1Card className="rc2-info-card">
              <span className="v1-card-kicker">Home Assistant getrennt</span>
              <h2>Keine Entity-IDs beim Zelt anlegen</h2>
              <p>Kamera, pH, EC, VPD, Licht- und Klima-Entities gehören in das HA-Mapping. Das Zelt bleibt ein physischer Raum.</p>
              <V1LinkButton to="/home-assistant">HA-Mapping öffnen</V1LinkButton>
            </V1Card>
          </V1Section>
        </div>
      </V1Page>
    )
  }

  return (
    <V1Page eyebrow="Physische Räume" title="Zelte" action={<V1Button variant="primary" onClick={openCreate}>Zelt anlegen</V1Button>}>
      {error && <V1Alert message={error} tone="warn" />}

      <section className="v1-kpi-grid">
        <V1Stat label="Aktive Zelte" value={activeTents.length} />
        <V1Stat label="Größe gepflegt" value={physicalVolumeKnown} />
        <V1Stat label="Aktive Grows" value={activeTents.reduce((sum, tent) => sum + tent.activeGrowCount, 0)} />
        <V1Stat label="Hydro-Setups" value={activeHydroCount} />
      </section>

      {loading ? <V1Empty title="Lade Zelte..." /> : activeTents.length === 0 ? <V1Empty title="Noch kein Zelt" action={<V1Button variant="primary" onClick={openCreate}>Erstes Zelt anlegen</V1Button>} /> : (
        <section className="v1-card-grid">
          {tents.map((tent) => <TentCard key={tent.id} tent={tent} hydroCount={countHydroForTent(hydroSetups, tent.id)} saving={saving === `archive-${tent.id}`} onEdit={openEdit} onArchive={toggleArchive} />)}
        </section>
      )}
    </V1Page>
  )
}

function TentCard({ tent, hydroCount, saving, onEdit, onArchive }: { tent: TentDto; hydroCount: number; saving: boolean; onEdit: (tent: TentDto) => void; onArchive: (tent: TentDto) => void }) {
  const archived = tent.status === 'Archived'
  return (
    <V1Card className="v1-tent-card" tone={archived ? 'neutral' : 'ok'}>
      <div className="v1-card-title-row"><div><span className="v1-card-kicker">{formatTentType(tent.tentType)}</span><h2>{tent.name}</h2></div><V1Badge tone={archived ? 'neutral' : 'ok'}>{archived ? 'Archiv' : 'aktiv'}</V1Badge></div>
      <div className="v1-info-grid">
        <Info label="Größe" value={formatSize(tent)} />
        <Info label="Licht" value={tent.lightWatt ? `${tent.lightWatt} W` : tent.lightType ?? 'offen'} />
        <Info label="Klima" value={`${tent.exhaustFanCount ?? 0} Abluft · ${tent.circulationFanCount ?? 0} Umluft`} />
        <Info label="CO₂" value={tent.co2Available ? 'ja' : 'nein'} />
        <Info label="Grows" value={String(tent.activeGrowCount)} />
        <Info label="Hydro" value={String(hydroCount)} />
      </div>
      <div className="v1-action-row"><V1Button onClick={() => onEdit(tent)}>Bearbeiten</V1Button><V1Button variant="ghost" disabled={saving} onClick={() => void onArchive(tent)}>{archived ? 'Aktivieren' : 'Archivieren'}</V1Button></div>
    </V1Card>
  )
}

function Info({ label, value }: { label: string; value: string }) { return <div className="v1-info"><span>{label}</span><strong>{value}</strong></div> }
function sortTents(items: TentDto[]) { return [...items].sort((a, b) => a.status.localeCompare(b.status) || a.displayOrder - b.displayOrder || a.name.localeCompare(b.name)) }
function countHydroForTent(items: HydroSetupDto[], tentId: number) { return items.filter((setup) => setup.tentId === tentId && setup.status === 'Active').length }
function mapSensors(tent: TentDto): UpdateTentSensorRequest[] { return tent.sensors.map((sensor) => ({ id: sensor.id, metricType: sensor.metricType, haEntityId: sensor.haEntityId, displayLabel: sensor.displayLabel, isActive: sensor.isActive })) }
function createDraft(displayOrder = 1): TentDraft { return { name: '', kind: 'Grow Tent', tentType: 'Production', notes: '', displayOrder: String(displayOrder), widthCm: '', depthCm: '', tentHeightCm: '', lightType: '', lightWatt: '', exhaustFanCount: '', exhaustM3h: '', circulationFanCount: '', co2Available: false } }
function createDraftFromTent(tent: TentDto): TentDraft { return { name: tent.name, kind: tent.kind, tentType: tent.tentType, notes: tent.notes ?? '', displayOrder: String(tent.displayOrder), widthCm: String(tent.widthCm ?? ''), depthCm: String(tent.depthCm ?? ''), tentHeightCm: String(tent.tentHeightCm ?? ''), lightType: tent.lightType ?? '', lightWatt: String(tent.lightWatt ?? ''), exhaustFanCount: String(tent.exhaustFanCount ?? ''), exhaustM3h: String(tent.exhaustM3h ?? ''), circulationFanCount: String(tent.circulationFanCount ?? ''), co2Available: tent.co2Available } }
function draftToRequest(draft: TentDraft) { return { name: draft.name.trim(), kind: draft.kind.trim() || 'Grow Tent', tentType: draft.tentType, notes: toNullableString(draft.notes), displayOrder: toNullableInt(draft.displayOrder) ?? 0, accentColor: '#22c55e', widthCm: toNullableInt(draft.widthCm), depthCm: toNullableInt(draft.depthCm), tentHeightCm: toNullableInt(draft.tentHeightCm), lightType: toNullableString(draft.lightType), lightWatt: toNullableInt(draft.lightWatt), lightController: null, lightControllerEntityId: null, exhaustFanCount: toNullableInt(draft.exhaustFanCount), exhaustM3h: toNullableInt(draft.exhaustM3h), circulationFanCount: toNullableInt(draft.circulationFanCount), hvacController: null, hvacControllerEntityId: null, co2Available: draft.co2Available, cameraEntityId: null } }
function tentToRequest(tent: TentDto) { return { name: tent.name, kind: tent.kind, tentType: tent.tentType, notes: tent.notes, displayOrder: tent.displayOrder, accentColor: tent.accentColor, widthCm: tent.widthCm, depthCm: tent.depthCm, tentHeightCm: tent.tentHeightCm, lightType: tent.lightType, lightWatt: tent.lightWatt, lightController: tent.lightController, lightControllerEntityId: tent.lightControllerEntityId, exhaustFanCount: tent.exhaustFanCount, exhaustM3h: tent.exhaustM3h, circulationFanCount: tent.circulationFanCount, hvacController: tent.hvacController, hvacControllerEntityId: tent.hvacControllerEntityId, co2Available: tent.co2Available, cameraEntityId: tent.cameraEntityId } }
function formatTentType(value: TentType) { return value === 'Production' ? 'Blüte / Run' : value === 'Mother' ? 'Mutter' : value === 'Propagation' ? 'Anzucht' : value === 'Quarantine' ? 'Quarantäne' : 'Mehrzweck' }
function formatSize(tent: TentDto) { return !tent.widthCm && !tent.depthCm && !tent.tentHeightCm ? 'offen' : `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm` }
function formatApiError(caught: unknown, fallback: string) { return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback }

export default TentsPage
