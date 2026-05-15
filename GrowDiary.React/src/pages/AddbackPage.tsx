import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { AddbackDefaultsDto, AddbackResultDto } from '../types'
import { V1Alert, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section, V1Stat, V1Wizard, draftNumber, toNullableFloat } from '../components/v1'
import { formatNumber } from '../utils'

type AddbackForm = { reservoirLiters: string; ecIst: string; ecZiel: string; ecStock: string; phBefore: string; ecAfter: string; phAfter: string; notes: string }
type AddbackLogDto = { id: number }

const steps = ['Istwerte', 'Ziel', 'Dosierung', 'Nachmessung']

function AddbackPage() {
  const { growId } = useParams()
  const [defaults, setDefaults] = useState<AddbackDefaultsDto | null>(null)
  const [form, setForm] = useState<AddbackForm>({ reservoirLiters: '', ecIst: '', ecZiel: '', ecStock: '3', phBefore: '', ecAfter: '', phAfter: '', notes: '' })
  const [result, setResult] = useState<AddbackResultDto | null>(null)
  const [step, setStep] = useState(1)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!growId) return
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const data = await apiFetch<AddbackDefaultsDto>(`/api/grows/${growId}/addback`, { signal: controller.signal })
        setDefaults(data)
        setForm((current) => ({ ...current, reservoirLiters: draftNumber(data.reservoirLiters), ecIst: draftNumber(data.ecIst), ecZiel: draftNumber(data.ecZiel), ecStock: draftNumber(data.ecStock || 3) }))
      } catch (caught) {
        if (!controller.signal.aborted) setError(caught instanceof ApiRequestError ? caught.message : 'Addback-Daten konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [growId])

  const liters = result?.litersToAdd ?? null
  const reservoir = toNullableFloat(form.reservoirLiters)

  async function calculate() {
    if (!growId) return
    setSaving(true)
    setError(null)
    setMessage(null)
    try {
      const data = await apiFetch<AddbackResultDto>(`/api/grows/${growId}/addback/calculate`, { method: 'POST', body: JSON.stringify({ reservoirLiters: reservoir, ecIst: toNullableFloat(form.ecIst), ecZiel: toNullableFloat(form.ecZiel), ecStock: toNullableFloat(form.ecStock) }) })
      setResult(data)
      setStep(3)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Addback konnte nicht berechnet werden.')
    } finally {
      setSaving(false)
    }
  }

  async function saveLog() {
    if (!growId || !result) return
    setSaving(true)
    setError(null)
    setMessage(null)
    try {
      await apiFetch<AddbackLogDto>(`/api/grows/${growId}/addback/logs`, { method: 'POST', body: JSON.stringify({ reservoirLiters: reservoir, ecBefore: toNullableFloat(form.ecIst), ecTarget: toNullableFloat(form.ecZiel), ecStock: toNullableFloat(form.ecStock), ecAfter: toNullableFloat(form.ecAfter), phBefore: toNullableFloat(form.phBefore), phAfter: toNullableFloat(form.phAfter), litersAdded: result.litersToAdd, newReservoirVolumeLiters: result.newReservoirVolume, notes: form.notes || null }) })
      setMessage('Addback gespeichert.')
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Addback konnte nicht gespeichert werden.')
    } finally {
      setSaving(false)
    }
  }

  const canCalculate = useMemo(() => Boolean(form.ecIst && form.ecZiel && form.ecStock), [form.ecIst, form.ecStock, form.ecZiel])

  return (
    <V1Page eyebrow="Addback" title={defaults?.growName ?? 'Addback'} action={<Link to="/addback" className="v1-button is-ghost">Zurück</Link>}>
      {error && <V1Alert message={error} tone="warn" />}
      {message && <V1Alert message={message} tone="ok" />}
      {loading ? <V1Empty title="Lade Rechner..." /> : (
        <>
          <V1Wizard steps={steps} currentStep={step} onStep={setStep} />
          <section className="v1-addback-grid">
            <V1Section title={steps[step - 1]}>
              {(step === 1 || step === 2) && <div className="v1-form-grid"><V1Field label="Reservoir"><input inputMode="decimal" value={form.reservoirLiters} onChange={(event) => setForm((current) => ({ ...current, reservoirLiters: event.target.value }))} /></V1Field><V1Field label="EC aktuell"><input inputMode="decimal" value={form.ecIst} onChange={(event) => setForm((current) => ({ ...current, ecIst: event.target.value }))} /></V1Field><V1Field label="Ziel-EC"><input inputMode="decimal" value={form.ecZiel} onChange={(event) => setForm((current) => ({ ...current, ecZiel: event.target.value }))} /></V1Field><V1Field label="Stammlösung EC"><input inputMode="decimal" value={form.ecStock} onChange={(event) => setForm((current) => ({ ...current, ecStock: event.target.value }))} /></V1Field><V1Field label="pH vorher"><input inputMode="decimal" value={form.phBefore} onChange={(event) => setForm((current) => ({ ...current, phBefore: event.target.value }))} /></V1Field></div>}
              {step === 3 && <V1Card className="v1-addback-result"><span className="v1-card-kicker">Dosierung</span>{result?.errorMessage ? <strong>{result.errorMessage}</strong> : !result?.needsAddback ? <strong>Kein Addback nötig</strong> : <><h2>{formatNumber(liters, 2)} L</h2><p>Reservoir danach: {formatNumber(result?.newReservoirVolume, 1)} L</p></>}<V1Button variant="primary" disabled={!canCalculate || saving} onClick={() => void calculate()}>{saving ? 'Berechnet...' : 'Neu berechnen'}</V1Button></V1Card>}
              {step === 4 && <div className="v1-form-grid"><V1Field label="EC nachher"><input inputMode="decimal" value={form.ecAfter} onChange={(event) => setForm((current) => ({ ...current, ecAfter: event.target.value }))} /></V1Field><V1Field label="pH nachher"><input inputMode="decimal" value={form.phAfter} onChange={(event) => setForm((current) => ({ ...current, phAfter: event.target.value }))} /></V1Field><V1Field label="Notizen" wide><textarea rows={4} value={form.notes} onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))} /></V1Field></div>}
              <div className="v1-form-actions">{step > 1 && <V1Button variant="ghost" onClick={() => setStep((current) => Math.max(1, current - 1))}>Zurück</V1Button>}{step < 3 && <V1Button variant="primary" disabled={!canCalculate || saving} onClick={() => void calculate()}>{saving ? 'Berechnet...' : 'Berechnen'}</V1Button>}{step === 3 && <V1Button variant="primary" onClick={() => setStep(4)}>Nachmessen</V1Button>}{step === 4 && <V1Button variant="primary" disabled={saving || !result} onClick={() => void saveLog()}>{saving ? 'Speichert...' : 'Speichern'}</V1Button>}</div>
            </V1Section>
            <V1Section title="Kontext"><div className="v1-kpi-grid one-col"><V1Stat label="Reservoir" value={form.reservoirLiters || '–'} unit="L" /><V1Stat label="EC ist" value={form.ecIst || '–'} unit="mS/cm" /><V1Stat label="EC Ziel" value={form.ecZiel || '–'} unit="mS/cm" /><V1Stat label="Addback" value={formatNumber(liters, 2)} unit="L" /></div></V1Section>
          </section>
        </>
      )}
    </V1Page>
  )
}

export default AddbackPage
