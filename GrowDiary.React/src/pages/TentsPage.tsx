import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type {
  CreateTentRequest,
  HvacControllerType,
  LightControllerType,
  TentDto,
  TentType,
  UpdateTentRequest,
} from '../types'

const tentTypeOptions: TentType[] = ['Production', 'Mother', 'Propagation', 'Quarantine', 'MultiPurpose']
const lightControllerOptions: Array<LightControllerType | ''> = ['', 'AcInfinityPro69', 'AcInfinityCloudline', 'GenericRelay', 'Manual', 'Other']
const hvacControllerOptions: Array<HvacControllerType | ''> = ['', 'AcInfinityPro69', 'AcInfinityCloudline', 'GenericRelay', 'Manual', 'Other']

interface TentDraft {
  name: string
  kind: string
  tentType: TentType
  notes: string
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
}

function TentsPage() {
  const [tents, setTents] = useState<TentDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editingTentId, setEditingTentId] = useState<number | null>(null)
  const [draft, setDraft] = useState<TentDraft>(() => createTentDraft())
  const [saving, setSaving] = useState<string | null>(null)
  const [formError, setFormError] = useState<string | null>(null)

  useEffect(() => {
    void loadTents()
  }, [])

  async function loadTents() {
    setLoading(true)
    setError(null)
    try {
      const items = await apiFetch<TentDto[]>('/api/settings/tents')
      setTents(sortTents(items))
    } catch (caught) {
      setError(formatApiError(caught, 'Zelte konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }

  const activeTentCount = useMemo(() => tents.filter((tent) => tent.status === 'Active').length, [tents])
  const totalActiveGrows = useMemo(() => tents.reduce((sum, tent) => sum + tent.activeGrowCount, 0), [tents])
  const tentsWithCamera = useMemo(() => tents.filter((tent) => Boolean(tent.cameraEntityId)).length, [tents])

  function openCreateForm() {
    setFormError(null)
    setEditingTentId(null)
    setDraft(createTentDraft(tents.length + 1))
    setFormOpen(true)
  }

  function openEditForm(tent: TentDto) {
    setFormError(null)
    setEditingTentId(tent.id)
    setDraft(createTentDraftFromTent(tent))
    setFormOpen(true)
  }

  async function handleSaveTent(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!draft.name.trim()) {
      setFormError('Bitte gib einen Zeltnamen ein.')
      return
    }

    setSaving('tent')
    setFormError(null)
    try {
      const baseRequest = tentDraftToRequest(draft)

      if (editingTentId) {
        const existing = tents.find((tent) => tent.id === editingTentId)
        if (!existing) throw new Error('Zelt nicht gefunden.')

        const request: UpdateTentRequest = {
          ...baseRequest,
          status: existing.status,
          sensors: existing.sensors.map((sensor) => ({
            id: sensor.id,
            metricType: sensor.metricType,
            haEntityId: sensor.haEntityId,
            displayLabel: sensor.displayLabel,
            isActive: sensor.isActive,
          })),
        }

        const saved = await apiFetch<TentDto>(`/api/settings/tents/${editingTentId}`, {
          method: 'PUT',
          body: JSON.stringify(request),
        })
        setTents((current) => sortTents(current.map((tent) => (tent.id === saved.id ? saved : tent))))
      } else {
        const created = await apiFetch<TentDto>('/api/settings/tents', {
          method: 'POST',
          body: JSON.stringify({ ...baseRequest, status: 'Active', sensors: [] } satisfies CreateTentRequest),
        })
        setTents((current) => sortTents([...current, created]))
      }

      setFormOpen(false)
      setEditingTentId(null)
      setDraft(createTentDraft(tents.length + 2))
    } catch (caught) {
      setFormError(formatApiError(caught, editingTentId ? 'Zelt konnte nicht gespeichert werden.' : 'Zelt konnte nicht angelegt werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function archiveTent(tent: TentDto) {
    setSaving(`archive-${tent.id}`)
    setFormError(null)
    try {
      const request: UpdateTentRequest = {
        ...tentToRequest(tent),
        status: tent.status === 'Archived' ? 'Active' : 'Archived',
        sensors: tent.sensors.map((sensor) => ({
          id: sensor.id,
          metricType: sensor.metricType,
          haEntityId: sensor.haEntityId,
          displayLabel: sensor.displayLabel,
          isActive: sensor.isActive,
        })),
      }
      const saved = await apiFetch<TentDto>(`/api/settings/tents/${tent.id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      })
      setTents((current) => sortTents(current.map((item) => (item.id === saved.id ? saved : item))))
    } catch (caught) {
      setFormError(formatApiError(caught, 'Zeltstatus konnte nicht geändert werden.'))
    } finally {
      setSaving(null)
    }
  }

  return (
    <main className="page-scroll app-page tents-clean-page">
      <header className="control-header">
        <div>
          <span className="control-kicker">Physische Räume</span>
          <h1>Zelte</h1>
        </div>
        <button type="button" className="btn btn-primary" onClick={openCreateForm}>Zelt anlegen</button>
      </header>

      {error && <AlertBar title="Fehler" message={error} />}
      {formError && <AlertBar title="Hinweis" message={formError} />}

      <section className="stats-row">
        <div className="stat-chip"><strong>{tents.length}</strong>Zelte</div>
        <div className="stat-chip"><strong>{activeTentCount}</strong>Aktiv</div>
        <div className="stat-chip"><strong>{totalActiveGrows}</strong>Aktive Grows</div>
        <div className="stat-chip"><strong>{tentsWithCamera}</strong>Kameras</div>
      </section>

      {formOpen && (
        <section className="card systems-form-card entity-form-card">
          <div className="card-header">
            <span className="card-title">{editingTentId ? 'Zelt bearbeiten' : 'Zelt anlegen'}</span>
            <button type="button" className="btn" onClick={() => setFormOpen(false)}>Schließen</button>
          </div>
          <form className="systems-form entity-form-grid" onSubmit={(event) => void handleSaveTent(event)}>
            <div className="form-section-title systems-form-wide">Basis</div>
            <label className="field">
              <span>Name</span>
              <input value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} placeholder="Hauptzelt" />
            </label>
            <label className="field">
              <span>Zweck</span>
              <select value={draft.tentType} onChange={(event) => setDraft((current) => ({ ...current, tentType: event.target.value as TentType }))}>
                {tentTypeOptions.map((value) => <option key={value} value={value}>{formatTentType(value)}</option>)}
              </select>
            </label>
            <label className="field">
              <span>Typbezeichnung</span>
              <input value={draft.kind} onChange={(event) => setDraft((current) => ({ ...current, kind: event.target.value }))} placeholder="Grow Tent" />
            </label>
            <label className="field">
              <span>Reihenfolge</span>
              <input type="number" value={draft.displayOrder} onChange={(event) => setDraft((current) => ({ ...current, displayOrder: event.target.value }))} />
            </label>

            <div className="form-section-title systems-form-wide">Größe</div>
            <label className="field">
              <span>Breite cm</span>
              <input type="number" min="0" value={draft.widthCm} onChange={(event) => setDraft((current) => ({ ...current, widthCm: event.target.value }))} placeholder="120" />
            </label>
            <label className="field">
              <span>Tiefe cm</span>
              <input type="number" min="0" value={draft.depthCm} onChange={(event) => setDraft((current) => ({ ...current, depthCm: event.target.value }))} placeholder="120" />
            </label>
            <label className="field">
              <span>Höhe cm</span>
              <input type="number" min="0" value={draft.tentHeightCm} onChange={(event) => setDraft((current) => ({ ...current, tentHeightCm: event.target.value }))} placeholder="200" />
            </label>
            <div />

            <div className="form-section-title systems-form-wide">Licht</div>
            <label className="field">
              <span>Lichttyp</span>
              <input value={draft.lightType} onChange={(event) => setDraft((current) => ({ ...current, lightType: event.target.value }))} placeholder="LED Board" />
            </label>
            <label className="field">
              <span>Watt</span>
              <input type="number" min="0" value={draft.lightWatt} onChange={(event) => setDraft((current) => ({ ...current, lightWatt: event.target.value }))} placeholder="480" />
            </label>
            <label className="field">
              <span>Lichtcontroller</span>
              <select value={draft.lightController} onChange={(event) => setDraft((current) => ({ ...current, lightController: event.target.value as LightControllerType | '' }))}>
                {lightControllerOptions.map((value) => <option key={value || 'none'} value={value}>{value ? formatController(value) : '–'}</option>)}
              </select>
            </label>
            <label className="field">
              <span>HA Lichtcontroller</span>
              <input value={draft.lightControllerEntityId} onChange={(event) => setDraft((current) => ({ ...current, lightControllerEntityId: event.target.value }))} placeholder="switch.licht" />
            </label>

            <div className="form-section-title systems-form-wide">Klima</div>
            <label className="field">
              <span>Abluft Anzahl</span>
              <input type="number" min="0" value={draft.exhaustFanCount} onChange={(event) => setDraft((current) => ({ ...current, exhaustFanCount: event.target.value }))} />
            </label>
            <label className="field">
              <span>Abluft m³/h</span>
              <input type="number" min="0" value={draft.exhaustM3h} onChange={(event) => setDraft((current) => ({ ...current, exhaustM3h: event.target.value }))} />
            </label>
            <label className="field">
              <span>Umluft Anzahl</span>
              <input type="number" min="0" value={draft.circulationFanCount} onChange={(event) => setDraft((current) => ({ ...current, circulationFanCount: event.target.value }))} />
            </label>
            <label className="field">
              <span>HVAC Controller</span>
              <select value={draft.hvacController} onChange={(event) => setDraft((current) => ({ ...current, hvacController: event.target.value as HvacControllerType | '' }))}>
                {hvacControllerOptions.map((value) => <option key={value || 'none'} value={value}>{value ? formatController(value) : '–'}</option>)}
              </select>
            </label>
            <label className="field">
              <span>HA HVAC Controller</span>
              <input value={draft.hvacControllerEntityId} onChange={(event) => setDraft((current) => ({ ...current, hvacControllerEntityId: event.target.value }))} placeholder="climate.zelt" />
            </label>
            <label className="switch-row entity-switch-row">
              <input type="checkbox" checked={draft.co2Available} onChange={(event) => setDraft((current) => ({ ...current, co2Available: event.target.checked }))} />
              <span>CO₂ vorhanden</span>
            </label>

            <div className="form-section-title systems-form-wide">Kamera & Notizen</div>
            <label className="field">
              <span>Kamera Entity</span>
              <input value={draft.cameraEntityId} onChange={(event) => setDraft((current) => ({ ...current, cameraEntityId: event.target.value }))} placeholder="camera.hauptzelt" />
            </label>
            <label className="field systems-form-wide">
              <span>Notizen</span>
              <textarea rows={3} value={draft.notes} onChange={(event) => setDraft((current) => ({ ...current, notes: event.target.value }))} />
            </label>

            <div className="systems-form-actions systems-form-wide">
              <button type="button" className="btn" onClick={() => setFormOpen(false)}>Abbrechen</button>
              <button type="submit" className="btn btn-primary" disabled={saving === 'tent'}>{saving === 'tent' ? 'Speichert...' : editingTentId ? 'Speichern' : 'Anlegen'}</button>
            </div>
          </form>
        </section>
      )}

      {loading ? (
        <div className="empty-hint">Lade Zelte...</div>
      ) : tents.length === 0 ? (
        <section className="systems-empty">
          <strong>Noch kein Zelt eingerichtet.</strong>
          <button type="button" className="btn btn-primary" onClick={openCreateForm}>Erstes Zelt anlegen</button>
        </section>
      ) : (
        <section className="systems-tent-grid tent-only-grid">
          {tents.map((tent) => (
            <article key={tent.id} className="system-card tent-room-card">
              <div className="system-card-header">
                <div>
                  <h2>{tent.name}</h2>
                  <p>{formatTentType(tent.tentType)} · {formatTentDimensions(tent)}</p>
                </div>
                <span className={tent.status === 'Active' ? 'badge badge-ok' : 'badge badge-neutral'}>{tent.status === 'Active' ? 'aktiv' : 'archiviert'}</span>
              </div>

              <div className="system-facts">
                <Fact label="Licht" value={formatLight(tent)} />
                <Fact label="Klima" value={formatAir(tent)} />
                <Fact label="CO₂" value={tent.co2Available ? 'ja' : 'nein'} />
                <Fact label="Kamera" value={tent.cameraEntityId ? 'HA' : '–'} />
                <Fact label="Grows" value={String(tent.activeGrowCount)} />
                <Fact label="Sensoren" value={String(tent.sensors.filter((sensor) => sensor.isActive).length)} />
              </div>

              {tent.notes && <p className="system-note">{tent.notes}</p>}

              <div className="system-actions">
                <Link className="btn" to={`/zelte/${tent.id}`}>Details</Link>
                <button type="button" className="btn" onClick={() => openEditForm(tent)}>Bearbeiten</button>
                <button type="button" className="btn" disabled={saving === `archive-${tent.id}`} onClick={() => void archiveTent(tent)}>{tent.status === 'Archived' ? 'Aktivieren' : 'Archivieren'}</button>
              </div>
            </article>
          ))}
        </section>
      )}
    </main>
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
    widthCm: '',
    depthCm: '',
    tentHeightCm: '',
    lightType: '',
    lightWatt: '',
    lightController: '',
    lightControllerEntityId: '',
    exhaustFanCount: '',
    exhaustM3h: '',
    circulationFanCount: '',
    hvacController: '',
    hvacControllerEntityId: '',
    co2Available: false,
    cameraEntityId: '',
  }
}

function createTentDraftFromTent(tent: TentDto): TentDraft {
  return {
    name: tent.name,
    kind: tent.kind,
    tentType: tent.tentType,
    notes: tent.notes ?? '',
    displayOrder: String(tent.displayOrder),
    widthCm: tent.widthCm?.toString() ?? '',
    depthCm: tent.depthCm?.toString() ?? '',
    tentHeightCm: tent.tentHeightCm?.toString() ?? '',
    lightType: tent.lightType ?? '',
    lightWatt: tent.lightWatt?.toString() ?? '',
    lightController: tent.lightController ?? '',
    lightControllerEntityId: tent.lightControllerEntityId ?? '',
    exhaustFanCount: tent.exhaustFanCount?.toString() ?? '',
    exhaustM3h: tent.exhaustM3h?.toString() ?? '',
    circulationFanCount: tent.circulationFanCount?.toString() ?? '',
    hvacController: tent.hvacController ?? '',
    hvacControllerEntityId: tent.hvacControllerEntityId ?? '',
    co2Available: tent.co2Available,
    cameraEntityId: tent.cameraEntityId ?? '',
  }
}

function tentDraftToRequest(draft: TentDraft): CreateTentRequest {
  return {
    name: draft.name.trim(),
    kind: draft.kind.trim() || 'Grow Tent',
    tentType: draft.tentType,
    notes: toNullableString(draft.notes),
    displayOrder: toIntOrDefault(draft.displayOrder, 99),
    accentColor: '#69b578',
    widthCm: toIntOrNull(draft.widthCm),
    depthCm: toIntOrNull(draft.depthCm),
    tentHeightCm: toIntOrNull(draft.tentHeightCm),
    lightType: toNullableString(draft.lightType),
    lightWatt: toIntOrNull(draft.lightWatt),
    lightController: draft.lightController || null,
    lightControllerEntityId: toNullableString(draft.lightControllerEntityId),
    exhaustFanCount: toIntOrNull(draft.exhaustFanCount),
    exhaustM3h: toIntOrNull(draft.exhaustM3h),
    circulationFanCount: toIntOrNull(draft.circulationFanCount),
    hvacController: draft.hvacController || null,
    hvacControllerEntityId: toNullableString(draft.hvacControllerEntityId),
    co2Available: draft.co2Available,
    cameraEntityId: toNullableString(draft.cameraEntityId),
    sensors: [],
  }
}

function tentToRequest(tent: TentDto): CreateTentRequest {
  return {
    name: tent.name,
    kind: tent.kind,
    tentType: tent.tentType,
    notes: tent.notes,
    displayOrder: tent.displayOrder,
    accentColor: tent.accentColor,
    widthCm: tent.widthCm,
    depthCm: tent.depthCm,
    tentHeightCm: tent.tentHeightCm,
    lightType: tent.lightType,
    lightWatt: tent.lightWatt,
    lightController: tent.lightController,
    lightControllerEntityId: tent.lightControllerEntityId,
    exhaustFanCount: tent.exhaustFanCount,
    exhaustM3h: tent.exhaustM3h,
    circulationFanCount: tent.circulationFanCount,
    hvacController: tent.hvacController,
    hvacControllerEntityId: tent.hvacControllerEntityId,
    co2Available: tent.co2Available,
    cameraEntityId: tent.cameraEntityId,
    sensors: [],
  }
}

function sortTents(items: TentDto[]): TentDto[] {
  return [...items].sort((left, right) => left.displayOrder - right.displayOrder || left.name.localeCompare(right.name))
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

function formatController(value: LightControllerType | HvacControllerType): string {
  switch (value) {
    case 'AcInfinityPro69': return 'AC Infinity 69 Pro'
    case 'AcInfinityCloudline': return 'AC Infinity Cloudline'
    case 'GenericRelay': return 'Relais'
    case 'Manual': return 'Manuell'
    case 'Other': return 'Sonstiges'
  }
}

function formatTentDimensions(tent: TentDto): string {
  if (!tent.widthCm && !tent.depthCm && !tent.tentHeightCm) return 'Größe offen'
  return `${tent.widthCm ?? '–'} × ${tent.depthCm ?? '–'} × ${tent.tentHeightCm ?? '–'} cm`
}

function formatLight(tent: TentDto): string {
  const parts = [tent.lightType, tent.lightWatt ? `${tent.lightWatt} W` : null].filter(Boolean)
  return parts.length > 0 ? parts.join(' · ') : '–'
}

function formatAir(tent: TentDto): string {
  const parts = [
    tent.exhaustFanCount ? `${tent.exhaustFanCount} Abluft` : null,
    tent.circulationFanCount ? `${tent.circulationFanCount} Umluft` : null,
  ].filter(Boolean)
  return parts.length > 0 ? parts.join(' · ') : '–'
}

function toNullableString(value: string): string | null {
  const trimmed = value.trim()
  return trimmed.length === 0 ? null : trimmed
}

function toIntOrNull(value: string): number | null {
  if (!value.trim()) return null
  const parsed = Number.parseInt(value, 10)
  return Number.isFinite(parsed) ? parsed : null
}

function toIntOrDefault(value: string, fallback: number): number {
  return toIntOrNull(value) ?? fallback
}

function formatApiError(caught: unknown, fallback: string): string {
  if (!(caught instanceof ApiRequestError)) return fallback
  const fieldErrors = caught.payload?.fieldErrors
  if (!fieldErrors) return caught.message
  const messages = Object.values(fieldErrors).flat()
  return messages.length > 0 ? messages.join(' ') : caught.message
}

export default TentsPage
