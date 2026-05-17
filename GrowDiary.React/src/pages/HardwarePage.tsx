import { useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch } from '../api'
import type { CreateHardwareItemRequest, HardwareItemCriticality, HardwareItemDto, HardwareItemStatus, TentDto, UpdateHardwareItemRequest } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section, V1Tabs } from '../components/v1'

type Tab = 'overview' | 'inventory'
type HardwareDraft = { name: string; category: string; criticality: HardwareItemCriticality; tentId: string; manufacturer: string; model: string; notes: string }
const criticalityOptions: HardwareItemCriticality[] = ['Low', 'Medium', 'High', 'Critical']

function HardwarePage() {
  const [hardware, setHardware] = useState<HardwareItemDto[]>([])
  const [tents, setTents] = useState<TentDto[]>([])
  const [tab, setTab] = useState<Tab>('overview')
  const [draft, setDraft] = useState<HardwareDraft>(() => createDraft())
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => { void load() }, [])

  async function load() {
    setLoading(true)
    setError(null)
    try {
      const [items, tentData] = await Promise.all([apiFetch<HardwareItemDto[]>('/api/hardware-items'), apiFetch<TentDto[]>('/api/settings/tents')])
      setHardware(items)
      setTents(tentData)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Sensoren konnten nicht geladen werden.')
    } finally {
      setLoading(false)
    }
  }

  const sensors = useMemo(() => hardware.filter((item) => isSensorLike(item)), [hardware])
  const offline = sensors.filter((item) => item.status === 'Offline' || item.status === 'Retired').length
  const trust = sensors.length === 0 ? 0 : Math.max(0, 100 - offline * 25)

  async function saveHardware(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!draft.name.trim()) {
      setError('Bitte Gerätename eingeben.')
      return
    }

    setSaving('hardware')
    setError(null)
    setMessage(null)
    const request: CreateHardwareItemRequest = {
      name: draft.name.trim(),
      category: draft.category.trim() || 'Sensor',
      status: 'Active',
      criticality: draft.criticality,
      tentId: toIntOrNull(draft.tentId),
      haEntityId: null,
      manufacturer: nullable(draft.manufacturer),
      model: nullable(draft.model),
      notes: nullable(draft.notes),
      installedAtUtc: new Date().toISOString(),
    }

    try {
      await apiFetch<HardwareItemDto>('/api/hardware-items', { method: 'POST', body: JSON.stringify(request) })
      setMessage('Hardware angelegt. Entity-Verknüpfung erfolgt separat im Home-Assistant-Mapping.')
      setDraft(createDraft())
      await load()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Hardware konnte nicht angelegt werden.')
    } finally {
      setSaving(null)
    }
  }

  async function updateHardwareStatus(item: HardwareItemDto, status: HardwareItemStatus) {
    setSaving(`hardware-${item.id}`)
    setError(null)
    const request: UpdateHardwareItemRequest = { name: item.name, category: item.category, status, criticality: item.criticality, tentId: item.tentId, haEntityId: item.haEntityId, manufacturer: item.manufacturer, model: item.model, notes: item.notes, installedAtUtc: item.installedAtUtc, retiredAtUtc: item.retiredAtUtc, wearTemplateId: item.wearTemplateId }
    try {
      await apiFetch<HardwareItemDto>(`/api/hardware-items/${item.id}`, { method: 'PUT', body: JSON.stringify(request) })
      await load()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Hardwarestatus konnte nicht geändert werden.')
    } finally {
      setSaving(null)
    }
  }

  return (
    <V1Page eyebrow="Ops" title="Sensoren" subtitle="Physisches Inventar, Zeltzuordnung und Wartungsstatus. HA-Entities werden separat unter Home Assistant gemappt.">
      {error && <V1Alert title="Fehler" message={error} tone="warn" />}
      {message && <V1Alert message={message} tone="ok" />}

      <section className="v1-kpi-grid">
        <V1Card tone={sensors.length === 0 ? 'neutral' : trust < 60 ? 'critical' : trust < 85 ? 'warn' : 'ok'}><span className="v1-card-kicker">Sensorvertrauen</span><h2>{sensors.length === 0 ? 'offen' : `${trust}%`}</h2><p>{sensors.length} Sensoren · {offline} offline</p></V1Card>
        <V1Card><span className="v1-card-kicker">Inventar</span><h2>{hardware.length}</h2><p>Geräte</p></V1Card>
        <V1Card><span className="v1-card-kicker">Zelte</span><h2>{tents.length}</h2><p>Zuordnung möglich</p></V1Card>
      </section>

      <V1Tabs<Tab> label="Sensoren Bereich" active={tab} onChange={setTab} items={[{ value: 'overview', label: 'Status', meta: `${sensors.length} Sensoren` }, { value: 'inventory', label: 'Inventar', meta: `${hardware.length} Geräte` }]} />

      {loading ? <V1Empty title="Lade Sensoren..." /> : tab === 'overview' ? (
        <V1Section title="Sensorstatus">
          {sensors.length === 0 ? <V1Empty title="Noch keine Sensor-Hardware" text="Lege pH-, EC-, ORP-, DO- oder Klima-Sensoren im Inventar an." action={<V1Button variant="primary" onClick={() => setTab('inventory')}>Inventar öffnen</V1Button>} /> : (
            <div className="ops1b-sensor-grid">
              {sensors.map((item) => <HardwareCard key={item.id} item={item} saving={saving === `hardware-${item.id}`} onStatus={updateHardwareStatus} />)}
            </div>
          )}
        </V1Section>
      ) : (
        <div className="ops1b-workflow-grid">
          <V1Section title="Inventar">
            {hardware.length === 0 ? <V1Empty title="Keine Hardware angelegt" /> : (
              <div className="ops1b-inventory-grid">
                {hardware.map((item) => <HardwareCard key={item.id} item={item} saving={saving === `hardware-${item.id}`} onStatus={updateHardwareStatus} />)}
              </div>
            )}
          </V1Section>

          <V1Section title="Sensor oder Gerät anlegen">
            <form className="ops1b-form" onSubmit={(event) => void saveHardware(event)}>
              <div className="ops1b-form-grid">
                <V1Field label="Name" wide><input value={draft.name} onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))} placeholder="pH Sonde Hauptzelt" /></V1Field>
                <V1Field label="Kategorie"><input value={draft.category} onChange={(event) => setDraft((current) => ({ ...current, category: event.target.value }))} placeholder="Sensor / Pumpe / Chiller" /></V1Field>
                <V1Field label="Kritikalität"><select value={draft.criticality} onChange={(event) => setDraft((current) => ({ ...current, criticality: event.target.value as HardwareItemCriticality }))}>{criticalityOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></V1Field>
                <V1Field label="Zelt"><select value={draft.tentId} onChange={(event) => setDraft((current) => ({ ...current, tentId: event.target.value }))}><option value="">Kein Zelt</option>{tents.map((tent) => <option key={tent.id} value={tent.id}>{tent.name}</option>)}</select></V1Field>
                <V1Field label="Hersteller"><input value={draft.manufacturer} onChange={(event) => setDraft((current) => ({ ...current, manufacturer: event.target.value }))} /></V1Field>
                <V1Field label="Modell"><input value={draft.model} onChange={(event) => setDraft((current) => ({ ...current, model: event.target.value }))} /></V1Field>
                <V1Field label="Notizen" wide><textarea value={draft.notes} onChange={(event) => setDraft((current) => ({ ...current, notes: event.target.value }))} rows={3} /></V1Field>
              </div>
              <div className="ops1b-sticky-actions"><V1Button type="submit" variant="primary" disabled={saving === 'hardware'}>{saving === 'hardware' ? 'Speichert...' : 'Hardware anlegen'}</V1Button></div>
            </form>
          </V1Section>
        </div>
      )}
    </V1Page>
  )
}

function HardwareCard({ item, saving, onStatus }: { item: HardwareItemDto; saving: boolean; onStatus: (item: HardwareItemDto, status: HardwareItemStatus) => void }) {
  const tone = item.status === 'Active' ? 'ok' : item.status === 'MaintenanceDue' ? 'warn' : 'critical'
  return (
    <V1Card tone={tone}>
      <div className="v1-card-title-row"><div><span className="v1-card-kicker">{item.category}</span><h2>{item.name}</h2></div><V1Badge tone={tone}>{item.status}</V1Badge></div>
      <p>{item.manufacturer ?? 'Hersteller offen'} {item.model ?? ''}</p>
      <p>{item.haEntityId ? `HA: ${item.haEntityId}` : 'HA-Entity im HA-Mapping verknüpfen.'}</p>
      <div className="v1-action-row">
        <V1Button disabled={saving} onClick={() => void onStatus(item, item.status === 'Offline' ? 'Active' : 'Offline')}>{item.status === 'Offline' ? 'Aktivieren' : 'Offline setzen'}</V1Button>
        <V1Button disabled={saving} onClick={() => void onStatus(item, 'MaintenanceDue')}>Wartung</V1Button>
      </div>
    </V1Card>
  )
}

function createDraft(): HardwareDraft {
  return { name: '', category: 'Sensor', criticality: 'High', tentId: '', manufacturer: '', model: '', notes: '' }
}

function isSensorLike(item: HardwareItemDto) {
  const text = `${item.name} ${item.category}`.toLowerCase()
  return ['sensor', 'sonde', 'probe', 'ph', 'ec', 'orp', 'do', 'temperatur', 'level'].some((term) => text.includes(term))
}

function nullable(value: string) {
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : null
}

function toIntOrNull(value: string) {
  const parsed = Number.parseInt(value, 10)
  return Number.isFinite(parsed) ? parsed : null
}

export default HardwarePage
