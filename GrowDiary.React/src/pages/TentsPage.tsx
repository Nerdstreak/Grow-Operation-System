import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { CreateTentRequest, GrowSummary, HydroSetupDto, TentDependencyError, TentDependencySummaryDto, TentDto, TentLivePayload, TentType, UpdateTentRequest, UpdateTentSensorRequest } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1LinkButton, V1Page, V1Section, V1Stat, V1Switch } from '../components/v1'
import { toNullableInt, toNullableString } from '../components/v1-utils'
import { classNames } from '../utils'

const tentTypes: TentType[] = ['Production', 'Mother', 'Propagation', 'Quarantine', 'MultiPurpose']

type LiveMetricKey = 'temperature' | 'humidity' | 'vpd' | 'light-cycle' | 'ppfd'

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
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [liveByTentId, setLiveByTentId] = useState<Record<number, TentLivePayload>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(routeCreateMode)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [draft, setDraft] = useState<TentDraft>(() => createDraft())
  const [saving, setSaving] = useState<string | null>(null)
  const [blockedDeleteTentId, setBlockedDeleteTentId] = useState<number | null>(null)
  const [deleteDependenciesByTentId, setDeleteDependenciesByTentId] = useState<Record<number, TentDependencySummaryDto | null>>({})

  const loadTents = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [tentData, setupData, growData] = await Promise.all([
        apiFetch<TentDto[]>('/api/settings/tents?includeArchived=true'),
        apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true'),
        apiFetch<GrowSummary[]>('/api/grows?archived=false').catch(() => []),
      ])
      const sortedTents = sortTents(tentData)
      const livePairs = await Promise.all(sortedTents.filter((tent) => tent.status === 'Active').map(async (tent) => {
        try { return [tent.id, await apiFetch<TentLivePayload>(`/api/live/tents/${tent.id}`)] as const }
        catch { return [tent.id, null] as const }
      }))
      setTents(sortedTents)
      setHydroSetups(setupData)
      setGrows(growData)
      setLiveByTentId(Object.fromEntries(livePairs.filter((pair): pair is readonly [number, TentLivePayload] => pair[1] !== null)))
      if (routeCreateMode) setDraft(createDraft(tentData.length + 1))
    } catch (caught) {
      setError(formatApiError(caught, 'Zelte konnten nicht geladen werden.'))
    } finally {
      setLoading(false)
    }
  }, [routeCreateMode])

  useEffect(() => {
    let active = true
    queueMicrotask(() => {
      if (active) void loadTents()
    })
    return () => { active = false }
  }, [loadTents])

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

  async function deleteTent(tent: TentDto) {
    const confirmed = window.confirm(`${tent.name} endgültig löschen?`)
    if (!confirmed) return
    setSaving(`delete-${tent.id}`)
    setError(null)
    try {
      await apiFetch<void>(`/api/settings/tents/${tent.id}`, { method: 'DELETE' })
      setTents((current) => current.filter((item) => item.id !== tent.id))
      setBlockedDeleteTentId((current) => current === tent.id ? null : current)
      setDeleteDependenciesByTentId((current) => ({ ...current, [tent.id]: null }))
    } catch (caught) {
      const payload = caught instanceof ApiRequestError ? caught.payload : null
      if (caught instanceof ApiRequestError && caught.status === 409 && isTentDependencyError(payload)) {
        setBlockedDeleteTentId(tent.id)
        setDeleteDependenciesByTentId((current) => ({ ...current, [tent.id]: payload.dependencies }))
        return
      }
      setError(formatApiError(caught, 'Zelt konnte nicht gelöscht werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function archiveTent(tent: TentDto) {
    const confirmed = window.confirm(`${tent.name} archivieren?`)
    if (!confirmed) return
    setSaving(`archive-${tent.id}`)
    setError(null)
    try {
      const saved = await apiFetch<TentDto>(`/api/settings/tents/${tent.id}/archive`, { method: 'POST' })
      setTents((current) => sortTents(current.map((item) => item.id === saved.id ? saved : item)))
    } catch (caught) {
      setError(formatApiError(caught, 'Zelt konnte nicht archiviert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function archiveLinkedGrow(grow: Pick<GrowSummary, 'id' | 'name'>) {
    const confirmed = window.confirm(`${grow.name} beenden und archivieren?`)
    if (!confirmed) return
    setSaving(`grow-archive-${grow.id}`)
    setError(null)
    try {
      await apiFetch(`/api/grows/${grow.id}/archive`, { method: 'POST' })
      setBlockedDeleteTentId(null)
      await loadTents()
    } catch (caught) {
      setError(formatApiError(caught, 'Grow konnte nicht beendet werden.'))
    } finally {
      setSaving(null)
    }
  }

  if (formOpen) {
    return (
      <V1Page
        eyebrow="Physischer Raum"
        title={editingId ? 'Zelt bearbeiten' : 'Zelt anlegen'}
        subtitle="Raum, Größe und verbaute Technik. Home-Assistant-Entities werden separat gemappt."
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

          <V1Section title="Home Assistant">
            <V1Card className="rc2-info-card">
              <span className="v1-card-kicker">Mapping getrennt</span>
              <h2>Sensoren nach dem Zelt anlegen mappen</h2>
              <p>Kamera, pH, EC, VPD, Licht- und Klima-Entities gehören in das HA-Mapping. Das Zelt bleibt ein physischer Raum.</p>
              <V1LinkButton to="/home-assistant">HA-Mapping öffnen</V1LinkButton>
            </V1Card>
          </V1Section>
        </div>
      </V1Page>
    )
  }

  return (
    <V1Page eyebrow="Räume" title="Zelte" action={<V1Button variant="primary" onClick={openCreate}>Zelt anlegen</V1Button>}>
      {error && <V1Alert message={error} tone="warn" />}

      <section className="v1-kpi-grid">
        <V1Stat label="Aktive Zelte" value={activeTents.length} />
        <V1Stat label="Größe gepflegt" value={physicalVolumeKnown} />
        <V1Stat label="Aktive Grows" value={activeTents.reduce((sum, tent) => sum + tent.activeGrowCount, 0)} />
        <V1Stat label="Hydro-Setups" value={activeHydroCount} />
      </section>

      {loading ? <V1Empty title="Lade Zelte..." /> : tents.length === 0 ? <V1Empty title="Noch kein Zelt" action={<V1Button variant="primary" onClick={openCreate}>Erstes Zelt anlegen</V1Button>} /> : (
        <section className="v1-card-grid">
          {tents.map((tent) => <TentCard key={tent.id} tent={tent} live={liveByTentId[tent.id] ?? null} hydroCount={countHydroForTent(hydroSetups, tent.id)} linkedGrows={getGrowsForTent(grows, tent.id)} linkedHydro={getHydroForTent(hydroSetups, tent.id)} deleteBlocked={blockedDeleteTentId === tent.id} deleteDependencies={deleteDependenciesByTentId[tent.id] ?? null} saving={saving === `delete-${tent.id}` || saving === `archive-${tent.id}`} savingKey={saving} onEdit={openEdit} onArchive={archiveTent} onDelete={deleteTent} onArchiveGrow={archiveLinkedGrow} />)}
        </section>
      )}
    </V1Page>
  )
}

function TentCard({ tent, live, hydroCount, linkedGrows, linkedHydro, deleteBlocked, deleteDependencies, saving, savingKey, onEdit, onArchive, onDelete, onArchiveGrow }: { tent: TentDto; live: TentLivePayload | null; hydroCount: number; linkedGrows: GrowSummary[]; linkedHydro: HydroSetupDto[]; deleteBlocked: boolean; deleteDependencies: TentDependencySummaryDto | null; saving: boolean; savingKey: string | null; onEdit: (tent: TentDto) => void; onArchive: (tent: TentDto) => void; onDelete: (tent: TentDto) => void; onArchiveGrow: (grow: Pick<GrowSummary, 'id' | 'name'>) => void }) {
  const archived = tent.status === 'Archived'
  const panelDependencies = deleteDependencies ?? createClientDependencySummary(linkedGrows, linkedHydro)
  const showDependencyPanel = deleteBlocked && hasDependencies(panelDependencies)
  return (
    <V1Card className="v1-tent-card" tone={archived ? 'neutral' : liveTone(live)}>
      <div className="v1-card-title-row"><div><span className="v1-card-kicker">{formatTentType(tent.tentType)}</span><h2>{tent.name}</h2></div><V1Badge tone={archived ? 'neutral' : liveTone(live)}>{archived ? 'Archiv' : live?.stateLabel ?? 'aktiv'}</V1Badge></div>
      <div className="tent-metric-groups" data-audit="tent-metrics">
        <MetricGroup title="Klima" items={[
          ['Temp', liveValue(live, 'temperature')],
          ['RLF', liveValue(live, 'humidity')],
          ['VPD', liveValue(live, 'vpd')],
        ]} />
        <MetricGroup title="Licht" items={[
          ['Zyklus', liveValue(live, 'light-cycle')],
          ['PPFD', liveValue(live, 'ppfd')],
          ['Watt', tent.lightWatt ? `${tent.lightWatt} W` : '–'],
        ]} />
        <MetricGroup title="Setup" items={[
          ['Hydro', String(hydroCount)],
          ['Grows', String(tent.activeGrowCount)],
          ['Größe', formatSize(tent)],
        ]} />
      </div>
      {linkedGrows.length > 0 && <p>{linkedGrows.length} aktive Grows verknüpft.</p>}
      <div className="v1-action-row rc-tent-actions"><V1LinkButton to={`/zelte/${tent.id}`} variant="primary">Öffnen</V1LinkButton><V1Button onClick={() => onEdit(tent)}>Bearbeiten</V1Button><V1Button disabled={saving} onClick={() => void onArchive(tent)}>Archivieren</V1Button><V1Button variant="danger" disabled={saving} onClick={() => void onDelete(tent)}>{saving ? 'Löscht...' : 'Löschen'}</V1Button></div>
      {showDependencyPanel && (
        <div className={classNames('dependency-panel', deleteBlocked && 'active')} data-audit="tent-delete-blocked">
          <strong>Löschen blockiert</strong>
          <p>Dieses Zelt ist mit aktiven Abhängigkeiten verknüpft. Verwalte sie direkt, danach ist Löschen erneut möglich.</p>
          <div className="v1-list">
            {panelDependencies.activeGrows.map((grow) => (
              <div key={`grow-${grow.id}`} className="v1-list-row dependency-row">
                <div>
                  <strong>{grow.name}</strong>
                  <span>{grow.status ?? 'aktiv'}</span>
                </div>
                <div className="dependency-row-actions">
                  <V1LinkButton to={`/grows/${grow.id}`} variant="primary">Verwalten</V1LinkButton>
                  <V1LinkButton to={`/grows/${grow.id}/setup`}>Bearbeiten</V1LinkButton>
                  <V1Button disabled={savingKey === `grow-archive-${grow.id}`} onClick={() => void onArchiveGrow({ id: grow.id, name: grow.name })}>{savingKey === `grow-archive-${grow.id}` ? 'Beendet...' : 'Beenden'}</V1Button>
                </div>
              </div>
            ))}
            {panelDependencies.hydroSetups.map((setup) => (
              <div key={`hydro-${setup.id}`} className="v1-list-row dependency-row">
                <div>
                  <strong>{setup.name}</strong>
                  <span>{setup.status}</span>
                </div>
                <div className="dependency-row-actions">
                  <V1LinkButton to={`/hydro/${setup.id}`} variant="primary">Öffnen</V1LinkButton>
                </div>
              </div>
            ))}
            {panelDependencies.sensors.map((sensor) => (
              <div key={`sensor-${sensor.id}`} className="v1-list-row dependency-row">
                <div>
                  <strong>{sensor.name}</strong>
                  <span>{sensor.status ?? 'verknüpft'}</span>
                </div>
                <div className="dependency-row-actions">
                  <V1LinkButton to="/hardware" variant="primary">Sensoren öffnen</V1LinkButton>
                </div>
              </div>
            ))}
            {panelDependencies.other.map((item) => (
              <div key={`other-${item.type}-${item.id}`} className="v1-list-row dependency-row">
                <div>
                  <strong>{item.name}</strong>
                  <span>{[item.type, item.status].filter(Boolean).join(' · ')}</span>
                </div>
                <div className="dependency-row-actions">
                  <V1LinkButton to="/hydro" variant="primary">Setups öffnen</V1LinkButton>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </V1Card>
  )
}

function isTentDependencyError(payload: unknown): payload is TentDependencyError {
  return Boolean(payload && typeof payload === 'object' && 'dependencies' in payload)
}

function createClientDependencySummary(linkedGrows: GrowSummary[], linkedHydro: HydroSetupDto[]): TentDependencySummaryDto {
  return {
    activeGrows: linkedGrows.map((grow) => ({ id: grow.id, name: grow.name, status: grow.status, type: 'Grow' })),
    archivedGrows: [],
    hydroSetups: linkedHydro.map((setup) => ({ id: setup.id, name: setup.name, status: setup.status, type: 'Hydro' })),
    sensors: [],
    measurements: [],
    other: [],
  }
}

function hasDependencies(dependencies: TentDependencySummaryDto) {
  return dependencies.activeGrows.length > 0
    || dependencies.hydroSetups.length > 0
    || dependencies.sensors.length > 0
    || dependencies.other.length > 0
}

function MetricGroup({ title, items }: { title: string; items: Array<[string, string]> }) {
  return (
    <section className="tent-metric-group">
      <h3>{title}</h3>
      <dl>
        {items.map(([label, value]) => (
          <div key={label} className="tent-metric-row">
            <dt>{label}</dt>
            <dd>{value || '–'}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}
function sortTents(items: TentDto[]) { return [...items].sort((a, b) => a.status.localeCompare(b.status) || a.displayOrder - b.displayOrder || a.name.localeCompare(b.name)) }
function countHydroForTent(items: HydroSetupDto[], tentId: number) { return items.filter((setup) => setup.tentId === tentId && setup.status === 'Active').length }
function getHydroForTent(items: HydroSetupDto[], tentId: number) { return items.filter((setup) => setup.tentId === tentId && setup.status === 'Active') }
function getGrowsForTent(items: GrowSummary[], tentId: number) { return items.filter((grow) => grow.tentId === tentId && (grow.status === 'Running' || grow.status === 'Planning')) }
function mapSensors(tent: TentDto): UpdateTentSensorRequest[] { return tent.sensors.map((sensor) => ({ id: sensor.id, metricType: sensor.metricType, haEntityId: sensor.haEntityId, displayLabel: sensor.displayLabel, isActive: sensor.isActive })) }
function createDraft(displayOrder = 1): TentDraft { return { name: '', kind: 'Grow Tent', tentType: 'Production', notes: '', displayOrder: String(displayOrder), widthCm: '', depthCm: '', tentHeightCm: '', lightType: '', lightWatt: '', exhaustFanCount: '', exhaustM3h: '', circulationFanCount: '', co2Available: false } }
function createDraftFromTent(tent: TentDto): TentDraft { return { name: tent.name, kind: tent.kind, tentType: tent.tentType, notes: tent.notes ?? '', displayOrder: String(tent.displayOrder), widthCm: String(tent.widthCm ?? ''), depthCm: String(tent.depthCm ?? ''), tentHeightCm: String(tent.tentHeightCm ?? ''), lightType: tent.lightType ?? '', lightWatt: String(tent.lightWatt ?? ''), exhaustFanCount: String(tent.exhaustFanCount ?? ''), exhaustM3h: String(tent.exhaustM3h ?? ''), circulationFanCount: String(tent.circulationFanCount ?? ''), co2Available: tent.co2Available } }
function draftToRequest(draft: TentDraft) { return { name: draft.name.trim(), kind: draft.kind.trim() || 'Grow Tent', tentType: draft.tentType, notes: toNullableString(draft.notes), displayOrder: toNullableInt(draft.displayOrder) ?? 0, accentColor: '#22c55e', widthCm: toNullableInt(draft.widthCm), depthCm: toNullableInt(draft.depthCm), tentHeightCm: toNullableInt(draft.tentHeightCm), lightType: toNullableString(draft.lightType), lightWatt: toNullableInt(draft.lightWatt), lightController: null, lightControllerEntityId: null, exhaustFanCount: toNullableInt(draft.exhaustFanCount), exhaustM3h: toNullableInt(draft.exhaustM3h), circulationFanCount: toNullableInt(draft.circulationFanCount), hvacController: null, hvacControllerEntityId: null, co2Available: draft.co2Available, cameraEntityId: null } }
function formatTentType(value: TentType) { return value === 'Production' ? 'Blüte / Run' : value === 'Mother' ? 'Mutter' : value === 'Propagation' ? 'Anzucht' : value === 'Quarantine' ? 'Quarantäne' : 'Mehrzweck' }
function formatSize(tent: TentDto) {
  return tent.widthCm && tent.depthCm && tent.tentHeightCm ? `${tent.widthCm}×${tent.depthCm}×${tent.tentHeightCm} cm` : '–'
}
function liveValue(live: TentLivePayload | null, key: LiveMetricKey) {
  const metric = live?.metrics.find((item) => item.key === key)
  return metric ? `${metric.value}${metric.unit && metric.value !== '–' ? ` ${metric.unit}` : ''}` : '–'
}
function liveTone(live: TentLivePayload | null) { return live?.stateTone === 'critical' ? 'critical' : live?.stateTone === 'warn' || live?.stateTone === 'warning' ? 'warn' : live ? 'ok' : 'neutral' }
function formatApiError(caught: unknown, fallback: string) { return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback }

export default TentsPage
