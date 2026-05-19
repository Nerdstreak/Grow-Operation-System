import type { FormEvent } from 'react'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { HarvestDto } from '../types'

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
  const [saving, setSaving] = useState(false)
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

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!growId || !form) return

    setSaving(true)
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
      navigate(`/grows/${growId}`)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Ernte konnte nicht gespeichert werden.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to={growId ? `/grows/${growId}` : '/'}>â† Zurück</Link>
          <span className="topbar-title">{harvest?.growName ?? 'Ernte'}</span>
        </div>
      </div>

      <div className="page-scroll">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)' }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}

        {loading || !form ? (
          <div className="empty-hint">Lade Ernte-Dokumentation...</div>
        ) : (
          <div className="card" style={{ maxWidth: 920 }}>
            <div className="card-header">
              <span className="card-title">Ernte-Dokumentation</span>
            </div>
            <form onSubmit={handleSubmit} style={{ padding: '18px 20px', display: 'grid', gap: 14 }}>
              <div className="meas-fields">
                <label className="meas-field">
                  <span>Erntedatum</span>
                  <input className="meas-input" type="date" value={form.harvestedAtLocal} onChange={(event) => setForm((current) => current ? { ...current, harvestedAtLocal: event.target.value } : current)} />
                </label>
                <label className="meas-field">
                  <span>Trocknungsdauer</span>
                  <div className="meas-field-inner">
                    <input className="meas-input" value={form.dryDays} onChange={(event) => setForm((current) => current ? { ...current, dryDays: event.target.value } : current)} />
                    <span className="meas-unit">Tage</span>
                  </div>
                </label>
                <label className="meas-field">
                  <span>Frischgewicht</span>
                  <div className="meas-field-inner">
                    <input className="meas-input" value={form.wetWeightG} onChange={(event) => setForm((current) => current ? { ...current, wetWeightG: event.target.value } : current)} />
                    <span className="meas-unit">g</span>
                  </div>
                </label>
                <label className="meas-field">
                  <span>Trockengewicht</span>
                  <div className="meas-field-inner">
                    <input className="meas-input" value={form.dryWeightG} onChange={(event) => setForm((current) => current ? { ...current, dryWeightG: event.target.value } : current)} />
                    <span className="meas-unit">g</span>
                  </div>
                </label>
                <label className="meas-field">
                  <span>Bewertung</span>
                  <div className="meas-field-inner">
                    <input className="meas-input" value={form.rating} onChange={(event) => setForm((current) => current ? { ...current, rating: event.target.value } : current)} />
                    <span className="meas-unit">/10</span>
                  </div>
                </label>
                <label className="meas-field">
                  <span>Blue­tenstruktur</span>
                  <input className="meas-input" value={form.nugStructure} onChange={(event) => setForm((current) => current ? { ...current, nugStructure: event.target.value } : current)} />
                </label>
              </div>

              <label className="field">
                <span>Ertrag-Notizen</span>
                <textarea rows={3} value={form.yieldNotes} onChange={(event) => setForm((current) => current ? { ...current, yieldNotes: event.target.value } : current)} />
              </label>
              <label className="field">
                <span>Geschmack / Aroma</span>
                <textarea rows={3} value={form.flavorNotes} onChange={(event) => setForm((current) => current ? { ...current, flavorNotes: event.target.value } : current)} />
              </label>
              <label className="field">
                <span>Effekt / High</span>
                <textarea rows={3} value={form.effectNotes} onChange={(event) => setForm((current) => current ? { ...current, effectNotes: event.target.value } : current)} />
              </label>

              <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
                <Link className="btn" to={growId ? `/grows/${growId}` : '/'}>Abbrechen</Link>
                <button className="btn btn-primary" disabled={saving}>{saving ? 'Speichertâ€¦' : 'Ernte speichern'}</button>
              </div>
            </form>
          </div>
        )}
      </div>
    </>
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
