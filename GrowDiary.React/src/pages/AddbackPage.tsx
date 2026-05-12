import type { FormEvent } from 'react'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { AddbackDefaultsDto, AddbackResultDto } from '../types'
import { formatNumber } from '../utils'

interface AddbackFormState {
  reservoirLiters: string
  ecIst: string
  ecZiel: string
  ecStock: string
}

function AddbackPage() {
  const { growId } = useParams()
  const [defaults, setDefaults] = useState<AddbackDefaultsDto | null>(null)
  const [form, setForm] = useState<AddbackFormState>({ reservoirLiters: '', ecIst: '', ecZiel: '', ecStock: '3' })
  const [result, setResult] = useState<AddbackResultDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!growId) return
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      try {
        const nextDefaults = await apiFetch<AddbackDefaultsDto>(`/api/grows/${growId}/addback`, { signal: controller.signal })
        setDefaults(nextDefaults)
        setForm({
          reservoirLiters: formatDraftNumber(nextDefaults.reservoirLiters),
          ecIst: formatDraftNumber(nextDefaults.ecIst),
          ecZiel: formatDraftNumber(nextDefaults.ecZiel),
          ecStock: formatDraftNumber(nextDefaults.ecStock),
        })
        setError(null)
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Addback-Daten konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [growId])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!growId) return

    setSaving(true)
    try {
      const nextResult = await apiFetch<AddbackResultDto>(`/api/grows/${growId}/addback/calculate`, {
        method: 'POST',
        body: JSON.stringify({
          reservoirLiters: parseNullableNumber(form.reservoirLiters),
          ecIst: parseNullableNumber(form.ecIst),
          ecZiel: parseNullableNumber(form.ecZiel),
          ecStock: parseNullableNumber(form.ecStock),
        }),
      })
      setResult(nextResult)
      setError(null)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Addback konnte nicht berechnet werden.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to={growId ? `/grows/${growId}` : '/'}>← Zurück</Link>
          <span className="topbar-title">{defaults?.growName ?? 'Addback'}</span>
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

        {loading ? (
          <div className="empty-hint">Lade Addback-Rechner...</div>
        ) : (
          <>
            <div className="card" style={{ marginBottom: 18 }}>
              <div className="card-header">
                <span className="card-title">Addback-Rechner</span>
              </div>
              <form onSubmit={handleSubmit} style={{ padding: '18px 20px', display: 'grid', gap: 16 }}>
                <div className="meas-fields">
                  <NumericField label="Reservoir" unit="L" value={form.reservoirLiters} onChange={(value) => setForm((current) => ({ ...current, reservoirLiters: value }))} hint={defaults?.suggestedReservoirLiters == null ? null : `Vorschlag: ${formatNumber(defaults.suggestedReservoirLiters, 1)} L`} />
                  <NumericField label="EC aktuell" unit="mS/cm" value={form.ecIst} onChange={(value) => setForm((current) => ({ ...current, ecIst: value }))} hint={defaults?.suggestedEcIst == null ? null : `Letzte Messung: ${formatNumber(defaults.suggestedEcIst, 2)}`} />
                  <NumericField label="Ziel-EC" unit="mS/cm" value={form.ecZiel} onChange={(value) => setForm((current) => ({ ...current, ecZiel: value }))} hint={defaults?.suggestedEcZiel == null ? null : `Sollwert: ${formatNumber(defaults.suggestedEcZiel, 2)}`} />
                  <NumericField label="Addback-EC" unit="mS/cm" value={form.ecStock} onChange={(value) => setForm((current) => ({ ...current, ecStock: value }))} hint="Vorgemischte Stammlösung" />
                </div>
                <div style={{ display: 'flex', gap: 10 }}>
                  <button className="btn btn-primary" disabled={saving}>{saving ? 'Berechnet…' : 'Berechnen'}</button>
                  <button type="button" className="btn" onClick={() => setResult(null)}>Ergebnis löschen</button>
                </div>
              </form>
            </div>

            {result && (
              <div className="card">
                <div className="card-header">
                  <span className="card-title">Ergebnis</span>
                </div>
                <div style={{ padding: '20px 22px', display: 'grid', gap: 10 }}>
                  {result.errorMessage ? (
                    <div style={{ color: 'var(--red)', fontWeight: 600 }}>{result.errorMessage}</div>
                  ) : !result.needsAddback ? (
                    <>
                      <div style={{ fontSize: 18, fontWeight: 600 }}>Kein Addback nötig</div>
                      <div className="text-muted">EC liegt bereits im Zielbereich oder darüber.</div>
                    </>
                  ) : (
                    <>
                      <div style={{ fontSize: 34, fontFamily: 'var(--mono)', letterSpacing: '-0.04em' }}>{formatNumber(result.litersToAdd, 2)} L</div>
                      <div style={{ fontWeight: 600 }}>Addback hinzufügen</div>
                      <div className="text-muted">Reservoir danach: {formatNumber(result.newReservoirVolume, 1)} L</div>
                    </>
                  )}
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </>
  )
}

function NumericField(props: {
  label: string
  unit: string
  value: string
  hint: string | null
  onChange: (value: string) => void
}) {
  return (
    <label className="meas-field">
      <span>{props.label}</span>
      <div className="meas-field-inner">
        <input className="meas-input" value={props.value} onChange={(event) => props.onChange(event.target.value)} />
        <span className="meas-unit">{props.unit}</span>
      </div>
      {props.hint && <span className="hint-text">{props.hint}</span>}
    </label>
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

export default AddbackPage
