import { useEffect, useMemo, useState } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type { StrainDominance, StrainDto } from '../types'
import { V1Page, V1Section, V1Card, V1Field, V1Button, V1Alert, V1Empty, V1Badge, V1Stat } from '../components/v1'

type StrainDraft = {
  name: string
  breeder: string
  dominance: StrainDominance
  flowerWeeksMin: string
  flowerWeeksMax: string
  nutrientDemandFactor: string
  stretchFactor: string
  vpdPreferenceShift: string
  notes: string
}

const DOMINANCE: Array<{ value: StrainDominance; label: string }> = [
  { value: 'Unknown', label: 'Unbekannt' },
  { value: 'Indica', label: 'Indica' },
  { value: 'Sativa', label: 'Sativa' },
  { value: 'Hybrid', label: 'Hybrid' },
]

function emptyDraft(): StrainDraft {
  return { name: '', breeder: '', dominance: 'Unknown', flowerWeeksMin: '', flowerWeeksMax: '', nutrientDemandFactor: '', stretchFactor: '', vpdPreferenceShift: '', notes: '' }
}

function draftFrom(strain: StrainDto): StrainDraft {
  return {
    name: strain.name,
    breeder: strain.breeder ?? '',
    dominance: strain.dominance,
    flowerWeeksMin: strain.flowerWeeksMin != null ? String(strain.flowerWeeksMin) : '',
    flowerWeeksMax: strain.flowerWeeksMax != null ? String(strain.flowerWeeksMax) : '',
    nutrientDemandFactor: strain.nutrientDemandFactor != null ? String(strain.nutrientDemandFactor) : '',
    stretchFactor: strain.stretchFactor != null ? String(strain.stretchFactor) : '',
    vpdPreferenceShift: strain.vpdPreferenceShift != null ? String(strain.vpdPreferenceShift) : '',
    notes: strain.notes ?? '',
  }
}

function num(value: string): number | null {
  const parsed = Number.parseFloat(value.replace(',', '.'))
  return Number.isFinite(parsed) ? parsed : null
}

function int(value: string): number | null {
  const parsed = Number.parseInt(value, 10)
  return Number.isFinite(parsed) ? parsed : null
}

function draftToRequest(draft: StrainDraft) {
  return {
    name: draft.name.trim(),
    breeder: draft.breeder.trim() || null,
    dominance: draft.dominance,
    flowerWeeksMin: int(draft.flowerWeeksMin),
    flowerWeeksMax: int(draft.flowerWeeksMax),
    nutrientDemandFactor: num(draft.nutrientDemandFactor),
    stretchFactor: num(draft.stretchFactor),
    vpdPreferenceShift: num(draft.vpdPreferenceShift),
    notes: draft.notes.trim() || null,
  }
}

function dominanceLabel(value: StrainDominance): string {
  return DOMINANCE.find((item) => item.value === value)?.label ?? value
}

function flowerWeeks(strain: StrainDto): string | null {
  if (strain.flowerWeeksMin == null && strain.flowerWeeksMax == null) return null
  if (strain.flowerWeeksMin != null && strain.flowerWeeksMax != null) {
    return strain.flowerWeeksMin === strain.flowerWeeksMax
      ? `${strain.flowerWeeksMin} Wochen Blüte`
      : `${strain.flowerWeeksMin}–${strain.flowerWeeksMax} Wochen Blüte`
  }
  return `${strain.flowerWeeksMin ?? strain.flowerWeeksMax} Wochen Blüte`
}

/**
 * The strain library: your own catalogue of genetics, with the traits that actually change
 * how you grow them (feeding appetite, stretch, preferred VPD).
 */
function StrainsPage() {
  const [strains, setStrains] = useState<StrainDto[]>([])
  const [draft, setDraft] = useState<StrainDraft>(emptyDraft)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const list = await apiFetch<StrainDto[]>('/api/strains', { signal: controller.signal })
        if (!controller.signal.aborted) setStrains(list)
      } catch (caught) {
        if (!controller.signal.aborted) setError(caught instanceof ApiRequestError ? caught.message : 'Sorten konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [reloadKey])

  const sorted = useMemo(() => [...strains].sort((a, b) => a.name.localeCompare(b.name, 'de')), [strains])

  function startNew() {
    setDraft(emptyDraft())
    setEditingId(null)
    setFormOpen(true)
  }

  function startEdit(strain: StrainDto) {
    setDraft(draftFrom(strain))
    setEditingId(strain.id)
    setFormOpen(true)
  }

  async function save() {
    if (draft.name.trim() === '') {
      setError('Bitte einen Sortennamen eingeben.')
      return
    }
    setSaving(true)
    setError(null)
    setNotice(null)
    try {
      const body = JSON.stringify(draftToRequest(draft))
      if (editingId == null) await apiFetch('/api/strains', { method: 'POST', body })
      else await apiFetch(`/api/strains/${editingId}`, { method: 'PUT', body })
      setNotice(editingId == null ? `„${draft.name.trim()}" angelegt.` : `„${draft.name.trim()}" gespeichert.`)
      setFormOpen(false)
      setEditingId(null)
      setReloadKey((key) => key + 1)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Sorte konnte nicht gespeichert werden.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <V1Page
      eyebrow="Meine Grows"
      title="Sorten"
      subtitle="Deine Genetik-Bibliothek. Was du hier hinterlegst, beschreibt wie eine Sorte wachsen will — und lässt sich später mit deinen Pflanzen verknüpfen."
      action={<V1Button variant="primary" onClick={startNew}>Sorte anlegen</V1Button>}
    >
      {error && <V1Alert message={error} tone="critical" />}
      {notice && <V1Alert message={notice} tone="ok" />}

      <section className="v1-kpi-grid">
        <V1Stat label="Sorten" value={strains.length} />
        <V1Stat label="Indica" value={strains.filter((s) => s.dominance === 'Indica').length} />
        <V1Stat label="Sativa" value={strains.filter((s) => s.dominance === 'Sativa').length} />
        <V1Stat label="Hybrid" value={strains.filter((s) => s.dominance === 'Hybrid').length} />
      </section>

      {formOpen && (
        <V1Section title={editingId == null ? 'Neue Sorte' : 'Sorte bearbeiten'}>
          <V1Card>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))', gap: 12 }}>
              <V1Field label="Name"><input value={draft.name} onChange={(event) => setDraft((d) => ({ ...d, name: event.target.value }))} placeholder="z. B. Purple Lemonade" /></V1Field>
              <V1Field label="Züchter"><input value={draft.breeder} onChange={(event) => setDraft((d) => ({ ...d, breeder: event.target.value }))} placeholder="z. B. FastBuds" /></V1Field>
              <V1Field label="Typ">
                <select value={draft.dominance} onChange={(event) => setDraft((d) => ({ ...d, dominance: event.target.value as StrainDominance }))}>
                  {DOMINANCE.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
                </select>
              </V1Field>
              <V1Field label="Blüte von (Wochen)"><input inputMode="numeric" value={draft.flowerWeeksMin} onChange={(event) => setDraft((d) => ({ ...d, flowerWeeksMin: event.target.value }))} placeholder="8" /></V1Field>
              <V1Field label="Blüte bis (Wochen)"><input inputMode="numeric" value={draft.flowerWeeksMax} onChange={(event) => setDraft((d) => ({ ...d, flowerWeeksMax: event.target.value }))} placeholder="10" /></V1Field>
            </div>

            <p className="rc2-measurement-note" style={{ margin: '14px 0 8px' }}>
              Feinheiten — leer lassen, wenn du sie nicht kennst. Sie beschreiben, wie stark diese Sorte von einer
              Durchschnittspflanze abweicht.
            </p>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))', gap: 12 }}>
              <V1Field label="Nährstoffbedarf" hint="1,0 = normal. 1,2 = frisst 20 % mehr, 0,8 = empfindlich.">
                <input inputMode="decimal" value={draft.nutrientDemandFactor} onChange={(event) => setDraft((d) => ({ ...d, nutrientDemandFactor: event.target.value }))} placeholder="1.0" />
              </V1Field>
              <V1Field label="Streckung" hint="1,0 = normal. Über 1 streckt sie in der Blüte stärker.">
                <input inputMode="decimal" value={draft.stretchFactor} onChange={(event) => setDraft((d) => ({ ...d, stretchFactor: event.target.value }))} placeholder="1.0" />
              </V1Field>
              <V1Field label="VPD-Vorliebe (kPa)" hint="Verschiebung zum Standard. +0,1 = mag es etwas trockener.">
                <input inputMode="decimal" value={draft.vpdPreferenceShift} onChange={(event) => setDraft((d) => ({ ...d, vpdPreferenceShift: event.target.value }))} placeholder="0" />
              </V1Field>
            </div>

            <V1Field label="Notizen" wide>
              <textarea rows={3} value={draft.notes} onChange={(event) => setDraft((d) => ({ ...d, notes: event.target.value }))} placeholder="Geruch, Wuchsform, Erfahrungen …" />
            </V1Field>

            <div className="v1-action-row" style={{ marginTop: 12 }}>
              <V1Button variant="primary" onClick={() => void save()} disabled={saving}>{saving ? 'Speichert…' : 'Speichern'}</V1Button>
              <V1Button variant="ghost" onClick={() => { setFormOpen(false); setEditingId(null) }}>Abbrechen</V1Button>
            </div>
          </V1Card>
        </V1Section>
      )}

      <V1Section title="Bibliothek">
        {loading ? (
          <V1Card>Lädt…</V1Card>
        ) : sorted.length === 0 ? (
          <V1Empty title="Noch keine Sorte" text="Leg deine erste Sorte an — danach kannst du Grows und Pflanzen darauf verweisen." action={<V1Button variant="primary" onClick={startNew}>Sorte anlegen</V1Button>} />
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 12 }}>
            {sorted.map((strain) => (
              <V1Card key={strain.id}>
                <div className="v1-card-title-row">
                  <div>
                    <span className="v1-card-kicker">{strain.breeder ?? 'Züchter offen'}</span>
                    <h2>{strain.name}</h2>
                  </div>
                  <V1Badge tone={strain.dominance === 'Unknown' ? 'neutral' : 'accent'}>{dominanceLabel(strain.dominance)}</V1Badge>
                </div>
                {flowerWeeks(strain) && <p>{flowerWeeks(strain)}</p>}
                {(strain.nutrientDemandFactor != null || strain.stretchFactor != null || strain.vpdPreferenceShift != null) && (
                  <p className="rc2-measurement-note">
                    {[
                      strain.nutrientDemandFactor != null ? `Nährstoffe ×${strain.nutrientDemandFactor}` : null,
                      strain.stretchFactor != null ? `Streckung ×${strain.stretchFactor}` : null,
                      strain.vpdPreferenceShift != null ? `VPD ${strain.vpdPreferenceShift > 0 ? '+' : ''}${strain.vpdPreferenceShift} kPa` : null,
                    ].filter(Boolean).join(' · ')}
                  </p>
                )}
                {strain.notes && <p className="rc2-measurement-note">{strain.notes}</p>}
                <div className="v1-action-row" style={{ marginTop: 10 }}>
                  <V1Button variant="secondary" onClick={() => startEdit(strain)}>Bearbeiten</V1Button>
                </div>
              </V1Card>
            ))}
          </div>
        )}
      </V1Section>
    </V1Page>
  )
}

export default StrainsPage
