import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type { CreateTentRequest, HvacControllerType, LightControllerType, TentDto, TentType, UpdateTentRequest, UpdateTentSensorRequest } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section, V1Stat, V1Switch, toNullableInt, toNullableString } from '../components/v1'

const tentTypes: TentType[] = ['Production', 'Mother', 'Propagation', 'Quarantine', 'MultiPurpose']
const controllers: Array<LightControllerType | ''> = ['', 'AcInfinityPro69', 'AcInfinityCloudline', 'GenericRelay', 'Manual', 'Other']
const hvacControllers: Array<HvacControllerType | ''> = ['', 'AcInfinityPro69', 'AcInfinityCloudline', 'GenericRelay', 'Manual', 'Other']

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
  lightController: LightControllerType | ''
  lightControllerEntityId: string
  exhaustFanCount: string
  exhaustM3h: string
  circulationFanCount: string
  hvacController: HvacControllerType | ''
  hvacControllerEntityId: string
  co2Available: boolean
  cameraEntityId: string
  notes: string
}

function TentsPage() {
  const [tents, setTents] = useState<TentDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [draft, setDraft] = useState<TentDraft>(() => createDraft())
  const [saving, setSaving] = useState<string | null>(null)

  useEffect(() => { void loadTents() }, [])

  async function loadTents() {
    setLoading(true)
    setError(null)
    try {
      const result = await apiFetch<TentDto[]>('/api/settings/tents?includeArchived=true')
      setTents(sortTents(result))
    } catch (caught) {
      setError(formatApiError(caught, 'Zelte konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }

  const activeTents = useMemo(() => tents.filter((tent) => tent.status === 'Active'), [tents])
  const cameraCount = useMemo(() => activeTents.filter((tent) => Boolean(tent.cameraEntityId)).length, [activeTents])
  const sensorCount = useMemo(() => activeTents.reduce((sum, tent) => sum + tent.sensors.filter((sensor) => sensor.isActive).length, 0), [activeTents])

  function openCreate() {
    setEditingId(null)
    setDraft(createDraft(tents.length + 1))
    setFormOpen(true)
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
      setFormOpen(false)
      setEditingId(null)
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

  return (
    <V1Page eyebrow="Physische Räume" title="Zelte" action={<V1Button variant="primary" onClick={openCreate}>Zelt anlegen</V1Button>}>
      {error && <V1Alert message={error} tone="warn" />}

      <section className="v1-kpi-grid">
        <V1Stat label="Zelte" value={activeTents.length} />
        <V1Stat label="Kameras" value={cameraCount} />
        <V1Stat label="Sensoren" value={sensorCount} />
        <V1Stat label="Aktive Grows" value={activeTents.reduce((sum, tent) => sum + tent.activeGrowCount, 0)} />
      </section>

      {formOpen && (
        <V1Section title={editingId ? 'Zelt bearbeiten' : 'Zelt anlegen'} action={<V1Button onClick={() => setFormOpen(false)}>Schließen</V1Button>}>
          <form className="v1-form-grid" onSubmit={(event) => void saveTent(event)}>
            <V1Field label="Name"><input value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} placeholder="Hauptzelt" /></V1Field>
            <V1Field label="Zweck"><select value={draft.tentType} onChange={(event) => setDraft((current) => ({ ...current, tentType: event.target.value as TentType }))}>{tentTypes.map((type) => <option key={type} value={type}>{formatTentType(type)}</option>)}</select></V1Field>
            <V1Field label="Typ"><input value={draft.kind} onChange={(event) => setDraft((current) => ({ ...current, kind: event.target.value }))} placeholder="Grow Tent" /></V1Field>
            <V1Field label="Reihenfolge"><input type="number" value={draft.displayOrder} onChange={(event) => setDraft((current) => ({ ...current, displayOrder: event.target.value }))} /></V1Field>

            <div className="v1-form-divider">Größe</div>
            <V1Field label="Breite cm"><input type="number" value={draft.widthCm} onChange={(event) => setDraft((current) => ({ ...current, widthCm: event.target.value }))} /></V1Field>
            <V1Field label="Tiefe cm"><input type="number" value={draft.depthCm} onChange={(event) => setDraft((current) => ({ ...current, depthCm: event.target.value }))} /></V1Field>
            <V1Field label="Höhe cm"><input type="number" value={draft.tentHeightCm} onChange={(event) => setDraft((current) => ({ ...current, tentHeightCm: event.target.value }))} /></V1Field>

            <div className="v1-form-divider">Licht & Klima</div>
            <V1Field label="Lichttyp"><input value={draft.lightType} onChange={(event) => setDraft((current) => ({ ...current, lightType: event.target.value }))} placeholder="LED" /></V1Field>
            <V1Field label="Watt"><input type="number" value={draft.lightWatt} onChange={(event) => setDraft((current) => ({ ...current, lightWatt: event.target.value }))} /></V1Field>
            <V1Field label="Lichtcontroller"><select value={draft.lightController} onChange={(event) => setDraft((current) => ({ ...current, lightController: event.target.value as LightControllerType | '' }))}>{controllers.map((value) => <option key={value || 'none'} value={value}>{value || 'keiner'}</option>)}</select></V1Field>
            <V1Field label="Licht Entity"><input value={draft.lightControllerEntityId} onChange={(event) => setDraft((current) => ({ ...current, lightControllerEntityId: event.target.value }))} placeholder="light.main" /></V1Field>
            <V1Field label="Abluft Anzahl"><input type="number" value={draft.exhaustFanCount} onChange={(event) => setDraft((current) => ({ ...current, exhaustFanCount: event.target.value }))} /></V1Field>
            <V1Field label="Abluft m³/h"><input type="number" value={draft.exhaustM3h} onChange={(event) => setDraft((current) => ({ ...current, exhaustM3h: event.target.value }))} /></V1Field>
            <V1Field label="Umluft Anzahl"><input type="number" value={draft.circulationFanCount} onChange={(event) => setDraft((current) => ({ ...current, circulationFanCount: event.target.value }))} /></V1Field>
            <V1Field label="Klima Controller"><select value={draft.hvacController} onChange={(event) => setDraft((current) => ({ ...current, hvacController: event.target.value as HvacControllerType | '' }))}>{hvacControllers.map((value) => <option key={value || 'none'} value={value}>{value || 'keiner'}</option>)}</select></V1Field>
            <V1Field label="Klima Entity"><input value={draft.hvacControllerEntityId} onChange={(event) => setDraft((current) => ({ ...current, hvacControllerEntityId: event.target.value }))} placeholder="fan.exhaust" /></V1Field>
            <V1Switch label="CO₂ vorhanden" checked={draft.co2Available} onChange={(checked) => setDraft((current) => ({ ...current, co2Available: checked }))} />
            <V1Field label="Kamera Entity"><input value={draft.cameraEntityId} onChange={(event) => setDraft((current) => ({ ...current, cameraEntityId: event.target.value }))} placeholder="camera.hauptzelt" /></V1Field>
            <V1Field label="Notizen" wide><textarea rows={3} value={draft.notes} onChange={(event) => setDraft((current) => ({ ...current, notes: event.target.value }))} /></V1Field>
            <div className="v1-form-actions"><V1Button variant="ghost" onClick={() => setFormOpen(false)}>Abbrechen</V1Button><V1Button type="submit" variant="primary" disabled={saving === 'tent'}>{saving === 'tent' ? 'Speichert...' : 'Speichern'}</V1Button></div>
          </form>
        </V1Section>
      )}

      {loading ? <V1Empty title="Lade Zelte..." /> : activeTents.length === 0 ? <V1Empty title="Noch kein Zelt" action={<V1Button variant="primary" onClick={openCreate}>Erstes Zelt anlegen</V1Button>} /> : (
        <section className="v1-card-grid">
          {tents.map((tent) => <TentCard key={tent.id} tent={tent} saving={saving === `archive-${tent.id}`} onEdit={openEdit} onArchive={toggleArchive} />)}
        </section>
      )}
    </V1Page>
  )
}

function TentCard({ tent, saving, onEdit, onArchive }: { tent: TentDto; saving: boolean; onEdit: (tent: TentDto) => void; onArchive: (tent: TentDto) => void }) {
  const archived = tent.status === 'Archived'
  return (
    <V1Card className="v1-tent-card" tone={archived ? 'neutral' : 'ok'}>
      <div className="v1-card-title-row"><div><span className="v1-card-kicker">{formatTentType(tent.tentType)}</span><h2>{tent.name}</h2></div><V1Badge tone={archived ? 'neutral' : 'ok'}>{archived ? 'Archiv' : 'aktiv'}</V1Badge></div>
      <div className="v1-info-grid">
        <Info label="Größe" value={formatSize(tent)} />
        <Info label="Licht" value={tent.lightWatt ? `${tent.lightWatt} W` : tent.lightType ?? 'offen'} />
        <Info label="Klima" value={`${tent.exhaustFanCount ?? 0} Abluft · ${tent.circulationFanCount ?? 0} Umluft`} />
        <Info label="CO₂" value={tent.co2Available ? 'ja' : 'nein'} />
        <Info label="Kamera" value={tent.cameraEntityId ? 'gemappt' : 'offen'} />
        <Info label="Sensoren" value={String(tent.sensors.filter((sensor) => sensor.isActive).length)} />
      </div>
      <div className="v1-action-row"><V1Button onClick={() => onEdit(tent)}>Bearbeiten</V1Button><V1Button variant="ghost" disabled={saving} onClick={() => void onArchive(tent)}>{archived ? 'Aktivieren' : 'Archivieren'}</V1Button></div>
    </V1Card>
  )
}

function Info({ label, value }: { label: string; value: string }) { return <div className="v1-info"><span>{label}</span><strong>{value}</strong></div> }
function sortTents(items: TentDto[]) { return [...items].sort((a, b) => a.status.localeCompare(b.status) || a.displayOrder - b.displayOrder || a.name.localeCompare(b.name)) }
function mapSensors(tent: TentDto): UpdateTentSensorRequest[] { return tent.sensors.map((sensor) => ({ id: sensor.id, metricType: sensor.metricType, haEntityId: sensor.haEntityId, displayLabel: sensor.displayLabel, isActive: sensor.isActive })) }
function createDraft(displayOrder = 1): TentDraft { return { name: '', kind: 'Grow Tent', tentType: 'Production', notes: '', displayOrder: String(displayOrder), widthCm: '', depthCm: '', tentHeightCm: '', lightType: '', lightWatt: '', lightController: '', lightControllerEntityId: '', exhaustFanCount: '', exhaustM3h: '', circulationFanCount: '', hvacController: '', hvacControllerEntityId: '', co2Available: false, cameraEntityId: '' } }
function createDraftFromTent(tent: TentDto): TentDraft { return { name: tent.name, kind: tent.kind, tentType: tent.tentType, notes: tent.notes ?? '', displayOrder: String(tent.displayOrder), widthCm: String(tent.widthCm ?? ''), depthCm: String(tent.depthCm ?? ''), tentHeightCm: String(tent.tentHeightCm ?? ''), lightType: tent.lightType ?? '', lightWatt: String(tent.lightWatt ?? ''), lightController: tent.lightController ?? '', lightControllerEntityId: tent.lightControllerEntityId ?? '', exhaustFanCount: String(tent.exhaustFanCount ?? ''), exhaustM3h: String(tent.exhaustM3h ?? ''), circulationFanCount: String(tent.circulationFanCount ?? ''), hvacController: tent.hvacController ?? '', hvacControllerEntityId: tent.hvacControllerEntityId ?? '', co2Available: tent.co2Available, cameraEntityId: tent.cameraEntityId ?? '' } }
function draftToRequest(draft: TentDraft) { return { name: draft.name.trim(), kind: draft.kind.trim() || 'Grow Tent', tentType: draft.tentType, notes: toNullableString(draft.notes), displayOrder: toNullableInt(draft.displayOrder) ?? 0, accentColor: '#22c55e', widthCm: toNullableInt(draft.widthCm), depthCm: toNullableInt(draft.depthCm), tentHeightCm: toNullableInt(draft.tentHeightCm), lightType: toNullableString(draft.lightType), lightWatt: toNullableInt(draft.lightWatt), lightController: draft.lightController || null, lightControllerEntityId: toNullableString(draft.lightControllerEntityId), exhaustFanCount: toNullableInt(draft.exhaustFanCount), exhaustM3h: toNullableInt(draft.exhaustM3h), circulationFanCount: toNullableInt(draft.circulationFanCount), hvacController: draft.hvacController || null, hvacControllerEntityId: toNullableString(draft.hvacControllerEntityId), co2Available: draft.co2Available, cameraEntityId: toNullableString(draft.cameraEntityId) } }
function tentToRequest(tent: TentDto) { return { name: tent.name, kind: tent.kind, tentType: tent.tentType, notes: tent.notes, displayOrder: tent.displayOrder, accentColor: tent.accentColor, widthCm: tent.widthCm, depthCm: tent.depthCm, tentHeightCm: tent.tentHeightCm, lightType: tent.lightType, lightWatt: tent.lightWatt, lightController: tent.lightController, lightControllerEntityId: tent.lightControllerEntityId, exhaustFanCount: tent.exhaustFanCount, exhaustM3h: tent.exhaustM3h, circulationFanCount: tent.circulationFanCount, hvacController: tent.hvacController, hvacControllerEntityId: tent.hvacControllerEntityId, co2Available: tent.co2Available, cameraEntityId: tent.cameraEntityId } }
function formatTentType(value: TentType) { return value === 'Production' ? 'Blüte / Run' : value === 'Mother' ? 'Mutter' : value === 'Propagation' ? 'Anzucht' : value === 'Quarantine' ? 'Quarantäne' : 'Mehrzweck' }
function formatSize(tent: TentDto) { return !tent.widthCm && !tent.depthCm && !tent.tentHeightCm ? 'offen' : `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm` }
function formatApiError(caught: unknown, fallback: string) { return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback }

export default TentsPage
