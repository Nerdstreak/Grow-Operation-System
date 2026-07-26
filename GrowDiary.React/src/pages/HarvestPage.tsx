import type { FormEvent } from 'react'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { HarvestDto } from '../types'
import { V1Alert, V1Button, V1Field, V1LinkButton, V1Page, V1Section, V1Skeleton } from '../components/v1'
import { summariseYield } from '../features/harvest/harvest-yield'

interface HarvestFormState {
  harvestedAtLocal: string
  wetWeightG: string
  dryWeightG: string
  dryDays: string
  yieldNotes: string
  rating: string
  flavorNotes: string
  effectNotes: string
  nugStructure: string
}

function HarvestPage() {
  const { growId } = useParams()
  const navigate = useNavigate()
  const [harvest, setHarvest] = useState<HarvestDto | null>(null)
  const [form, setForm] = useState<HarvestFormState | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState<'save' | 'complete' | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!growId) return
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      try {
        const nextHarvest = await apiFetch<HarvestDto>(`/api/grows/${growId}/harvest`, { signal: controller.signal })
        setHarvest(nextHarvest)
        setForm({
          harvestedAtLocal: nextHarvest.harvestedAtLocal,
          wetWeightG: formatDraftNumber(nextHarvest.wetWeightG),
          dryWeightG: formatDraftNumber(nextHarvest.dryWeightG),
          dryDays: formatDraftNumber(nextHarvest.dryDays),
          yieldNotes: nextHarvest.yieldNotes ?? '',
          rating: formatDraftNumber(nextHarvest.rating),
          flavorNotes: nextHarvest.flavorNotes ?? '',
          effectNotes: nextHarvest.effectNotes ?? '',
          nugStructure: nextHarvest.nugStructure ?? '',
        })
        setError(null)
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Ernte-Daten konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [growId])

  async function save(complete: boolean) {
    if (!growId || !form) return

    setSaving(complete ? 'complete' : 'save')
    setError(null)
    try {
      await apiFetch<HarvestDto>(`/api/grows/${growId}/harvest`, {
        method: 'PUT',
        body: JSON.stringify({
          harvestedAtLocal: form.harvestedAtLocal,
          wetWeightG: parseNullableNumber(form.wetWeightG),
          dryWeightG: parseNullableNumber(form.dryWeightG),
          dryDays: parseNullableInteger(form.dryDays),
          yieldNotes: trimToNull(form.yieldNotes),
          rating: parseNullableNumber(form.rating),
          flavorNotes: trimToNull(form.flavorNotes),
          effectNotes: trimToNull(form.effectNotes),
          nugStructure: trimToNull(form.nugStructure),
        }),
      })
      // Closing the loop: finishing the harvest completes the grow and moves it to the
      // archive (idempotent server-side — only Planning/Running grows change).
      if (complete) {
        await apiFetch(`/api/grows/${growId}/archive`, { method: 'POST' })
        navigate('/archiv')
      } else {
        navigate(`/grows/${growId}`)
      }
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Ernte konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    void save(false)
  }

  const backTo = growId ? `/grows/${growId}` : '/'
  const yieldSummary = form ? summariseYield(form.wetWeightG, form.dryWeightG) : null

  return (
    <V1Page
      eyebrow="Abschluss"
      title="Ernte erfassen"
      subtitle={harvest?.growName ?? undefined}
      action={<V1LinkButton to={backTo}>Zurück zum Grow</V1LinkButton>}
    >
      {error && <V1Alert title="Fehler" message={error} tone="warn" />}

      {loading || !form ? (
        <V1Skeleton tiles={4} rows={3} label="Lade Ernte" />
      ) : (
        <form onSubmit={handleSubmit}>
          <V1Section title="Gewicht & Trocknung">
            <div className="v1-form-grid">
              <V1Field label="Erntedatum">
                <input type="date" value={form.harvestedAtLocal} onChange={(event) => setForm((current) => current ? { ...current, harvestedAtLocal: event.target.value } : current)} />
              </V1Field>
              <V1Field label="Trocknungsdauer" hint="Tage">
                <input inputMode="numeric" value={form.dryDays} onChange={(event) => setForm((current) => current ? { ...current, dryDays: event.target.value } : current)} />
              </V1Field>
              <V1Field label="Frischgewicht" hint="Gramm">
                <input inputMode="decimal" value={form.wetWeightG} onChange={(event) => setForm((current) => current ? { ...current, wetWeightG: event.target.value } : current)} />
              </V1Field>
              <V1Field label="Trockengewicht" hint="Gramm">
                <input inputMode="decimal" value={form.dryWeightG} onChange={(event) => setForm((current) => current ? { ...current, dryWeightG: event.target.value } : current)} />
              </V1Field>
            </div>

            {/* Das Verhältnis sagt mehr über die Trocknung als jede der beiden
                Zahlen für sich — und es auszurechnen, während man tippt, ist
                genau das, was die App abnehmen kann. */}
            {yieldSummary && (
              <div className="v1-chip-row" data-audit="harvest-ratio">
                <span>{yieldSummary.text}</span>
              </div>
            )}
          </V1Section>

          <V1Section title="Bewertung">
            <div className="v1-form-grid">
              <V1Field label="Bewertung" hint="von 10">
                <input inputMode="numeric" value={form.rating} onChange={(event) => setForm((current) => current ? { ...current, rating: event.target.value } : current)} />
              </V1Field>
              <V1Field label="Blütenstruktur">
                <input value={form.nugStructure} onChange={(event) => setForm((current) => current ? { ...current, nugStructure: event.target.value } : current)} />
              </V1Field>
              <V1Field label="Ertrag-Notizen" wide>
                <textarea rows={3} value={form.yieldNotes} onChange={(event) => setForm((current) => current ? { ...current, yieldNotes: event.target.value } : current)} />
              </V1Field>
              <V1Field label="Geschmack / Aroma" wide>
                <textarea rows={3} value={form.flavorNotes} onChange={(event) => setForm((current) => current ? { ...current, flavorNotes: event.target.value } : current)} />
              </V1Field>
              <V1Field label="Effekt / High" wide>
                <textarea rows={3} value={form.effectNotes} onChange={(event) => setForm((current) => current ? { ...current, effectNotes: event.target.value } : current)} />
              </V1Field>
            </div>
          </V1Section>

          <div className="v1-form-actions">
            <V1LinkButton to={backTo}>Abbrechen</V1LinkButton>
            <V1Button type="submit" disabled={saving !== null}>{saving === 'save' ? 'Speichert…' : 'Ernte speichern'}</V1Button>
            <V1Button type="button" variant="primary" disabled={saving !== null} onClick={() => void save(true)}>{saving === 'complete' ? 'Schließt ab…' : 'Speichern & Grow abschließen'}</V1Button>
          </div>
        </form>
      )}
    </V1Page>
  )
}


function formatDraftNumber(value: number | null | undefined) {
  if (value == null || Number.isNaN(value)) return ''
  return String(value)
}

function parseNullableNumber(value: string) {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number(trimmed.replace(',', '.'))
  return Number.isNaN(parsed) ? null : parsed
}

function parseNullableInteger(value: string) {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number.parseInt(trimmed, 10)
  return Number.isNaN(parsed) ? null : parsed
}

function trimToNull(value: string) {
  const trimmed = value.trim()
  return trimmed ? trimmed : null
}

export default HarvestPage
