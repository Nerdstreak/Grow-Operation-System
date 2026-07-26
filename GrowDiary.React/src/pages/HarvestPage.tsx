import type { FormEvent } from 'react'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowDetail, HarvestDto } from '../types'
import { V1Alert, V1Badge, V1Button, V1Field, V1LinkButton, V1Page, V1Section, V1Skeleton } from '../components/v1'
import { summariseYield } from '../features/harvest/harvest-yield'
import { parsePlantWeights, progressLabel, serialisePlantWeights, totals, type PlantWeight } from '../features/harvest/plant-weights-model'
import '../features/harvest/harvest.css'

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
  // Einzelgewichte je Pflanze. Am Trockenregal wiegt man Pflanze fuer Pflanze;
  // die Summe wandert in die Grow-Felder, damit Auswertungen unveraendert
  // weiterrechnen.
  const [plants, setPlants] = useState<PlantWeight[]>([])

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
        const grow = await apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal: controller.signal }).catch(() => null)
        setPlants(parsePlantWeights(nextHarvest.plantWeightsJson, grow?.plantCount ?? 1))
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
          // Sind Einzelgewichte da, gewinnen ihre Summen — sonst bliebe das
          // Grow-Feld auf einem alten Wert stehen, waehrend die Tabelle daneben
          // etwas anderes zeigt.
          wetWeightG: sums.wetG ?? parseNullableNumber(form.wetWeightG),
          dryWeightG: sums.dryG ?? parseNullableNumber(form.dryWeightG),
          plantWeightsJson: serialisePlantWeights(plants),
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

  function patchPlant(index: number, patch: Partial<PlantWeight>) {
    setPlants((current) => current.map((plant, position) => (position === index ? { ...plant, ...patch } : plant)))
  }

  const sums = totals(plants)
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

          <V1Section title="Ernte pro Pflanze" action={<V1Badge tone="neutral">{progressLabel(sums)}</V1Badge>}>
            <div className="hv-table-wrap">
              <table className="hv-table" data-audit="harvest-plants">
                <thead>
                  <tr>
                    <th scope="col">Pflanze</th>
                    <th scope="col">Nass (g)</th>
                    <th scope="col">Trocken (g)</th>
                  </tr>
                </thead>
                <tbody>
                  {plants.map((plant, index) => (
                    <tr key={plant.label}>
                      <th scope="row">
                        <input
                          value={plant.label}
                          aria-label={`Kennung Pflanze ${index + 1}`}
                          onChange={(event) => patchPlant(index, { label: event.target.value })}
                        />
                      </th>
                      <td>
                        <input
                          inputMode="decimal"
                          value={plant.wetG ?? ''}
                          aria-label={`Nassgewicht ${plant.label}`}
                          onChange={(event) => patchPlant(index, { wetG: toGrams(event.target.value) })}
                        />
                      </td>
                      <td>
                        <input
                          inputMode="decimal"
                          value={plant.dryG ?? ''}
                          aria-label={`Trockengewicht ${plant.label}`}
                          onChange={(event) => patchPlant(index, { dryG: toGrams(event.target.value) })}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="hv-sums">
              <div><span>Nass gesamt</span><strong>{sums.wetG ?? '—'}<em>g</em></strong></div>
              <div>
                <span>{sums.dryG != null ? 'Trocken gesamt' : 'Erwartet trocken'}</span>
                <strong>{sums.dryG ?? (sums.expectedDryG != null ? `~${sums.expectedDryG}` : '—')}<em>g</em></strong>
              </div>
            </div>

            <div className="v1-action-row">
              <V1Button type="button" onClick={() => setPlants((current) => [...current, { label: `PL-${String(current.length + 1).padStart(2, '0')}`, wetG: null, dryG: null }])}>
                Pflanze ergänzen
              </V1Button>
            </div>
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

/** Leeres Feld heisst „noch nicht gewogen", nicht „null Gramm". */
function toGrams(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number(trimmed.replace(',', '.'))
  return Number.isFinite(parsed) ? parsed : null
}
