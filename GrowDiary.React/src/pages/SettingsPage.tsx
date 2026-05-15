import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { CreateStrainRequest, StrainDominance, StrainDto } from '../types'

type StrainDraft = {
  name: string
  breeder: string
  dominance: StrainDominance
  flowerWeeksMin: string
  flowerWeeksMax: string
  notes: string
  nutrientDemandFactor: string
  stretchFactor: string
  vpdPreferenceShift: string
}

const strainDominanceOptions: StrainDominance[] = ['Unknown', 'Indica', 'Sativa', 'Hybrid']

function SettingsPage() {
  const [strains, setStrains] = useState<StrainDto[]>([])
  const [strainDraft, setStrainDraft] = useState<StrainDraft>(createStrainDraft())
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [strainError, setStrainError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const strainItems = await apiFetch<StrainDto[]>('/api/strains', { signal: controller.signal })
        setStrains(strainItems)
      } catch (caught) {
        if (!controller.signal.aborted) setError(formatApiError(caught, 'Einstellungen konnten nicht geladen werden.'))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  async function handleCreateStrain(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const request: CreateStrainRequest = {
      name: strainDraft.name.trim(),
      breeder: toNullableString(strainDraft.breeder),
      dominance: strainDraft.dominance,
      flowerWeeksMin: toIntOrNull(strainDraft.flowerWeeksMin),
      flowerWeeksMax: toIntOrNull(strainDraft.flowerWeeksMax),
      notes: toNullableString(strainDraft.notes),
      nutrientDemandFactor: toNumberOrNull(strainDraft.nutrientDemandFactor),
      stretchFactor: toNumberOrNull(strainDraft.stretchFactor),
      vpdPreferenceShift: toNumberOrNull(strainDraft.vpdPreferenceShift),
    }

    if (!request.name) {
      setStrainError('Name darf nicht leer sein.')
      return
    }

    setSaving('strain')
    setStrainError(null)
    try {
      const saved = await apiFetch<StrainDto>('/api/strains', {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setStrains((current) => [...current, saved].sort((a, b) => a.name.localeCompare(b.name)))
      setStrainDraft(createStrainDraft())
    } catch (caught) {
      setStrainError(formatApiError(caught, 'Genetik konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  return (
    <main className="page-scroll app-page settings-page settings-page-clean">
      <header className="control-header">
        <div>
          <span className="control-kicker">App</span>
          <h1>Einstellungen</h1>
        </div>
      </header>

      {error && (
        <div className="alert-bar">
          <div className="alert-dot" />
          <strong>Fehler</strong>
          <span>{error}</span>
        </div>
      )}

      {loading ? (
        <div className="empty-hint">Lade Einstellungen...</div>
      ) : (
        <>
          <section className="admin-section">
            <div className="section-label">Verwaltung</div>
            <div className="settings-link-grid">
              <Link className="admin-card settings-link-card" to="/home-assistant">
                <strong>Home Assistant</strong>
                <span>Verbindung, Kamera und Entitäten zentral verwalten.</span>
              </Link>
              <Link className="admin-card settings-link-card" to="/zelte">
                <strong>Zelte</strong>
                <span>Physische Räume, Größe, Licht und Klima.</span>
              </Link>
              <Link className="admin-card settings-link-card" to="/hydro">
                <strong>Hydro</strong>
                <span>DWC/RDWC-Systeme, Tank, Sites und Layout.</span>
              </Link>
              <Link className="admin-card settings-link-card" to="/hardware">
                <strong>Hardware</strong>
                <span>Sensoren, Pumpen, Wartung und Kalibrierung.</span>
              </Link>
              <Link className="admin-card settings-link-card" to="/wissen">
                <strong>Wissen</strong>
                <span>SOPs, Treatments, Symptome und Setpoints.</span>
              </Link>
            </div>
          </section>

          <section className="admin-section">
            <div className="section-label">Strains</div>
            <form className="admin-card settings-strain-form" onSubmit={(event) => void handleCreateStrain(event)}>
              <label className="field">
                <span>Name</span>
                <input value={strainDraft.name} onChange={(event) => setStrainDraft((current) => ({ ...current, name: event.target.value }))} placeholder="Blue Dream" />
              </label>
              <label className="field">
                <span>Breeder</span>
                <input value={strainDraft.breeder} onChange={(event) => setStrainDraft((current) => ({ ...current, breeder: event.target.value }))} />
              </label>
              <label className="field">
                <span>Dominanz</span>
                <select value={strainDraft.dominance} onChange={(event) => setStrainDraft((current) => ({ ...current, dominance: event.target.value as StrainDominance }))}>
                  {strainDominanceOptions.map((value) => <option key={value} value={value}>{formatDominance(value)}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Blüte min.</span>
                <input type="number" value={strainDraft.flowerWeeksMin} onChange={(event) => setStrainDraft((current) => ({ ...current, flowerWeeksMin: event.target.value }))} />
              </label>
              <label className="field">
                <span>Blüte max.</span>
                <input type="number" value={strainDraft.flowerWeeksMax} onChange={(event) => setStrainDraft((current) => ({ ...current, flowerWeeksMax: event.target.value }))} />
              </label>
              <label className="field settings-form-wide">
                <span>Notizen</span>
                <textarea rows={3} value={strainDraft.notes} onChange={(event) => setStrainDraft((current) => ({ ...current, notes: event.target.value }))} />
              </label>
              {strainError && <div className="systems-form-error settings-form-wide">{strainError}</div>}
              <div className="systems-form-actions settings-form-wide">
                <button className="btn btn-primary" disabled={saving === 'strain'}>{saving === 'strain' ? 'Speichert...' : 'Strain anlegen'}</button>
              </div>
            </form>

            <div className="settings-strain-list">
              {strains.length === 0 ? (
                <div className="empty-hint">Keine Strains angelegt.</div>
              ) : strains.map((strain) => (
                <article key={strain.id} className="admin-card settings-strain-card">
                  <strong>{strain.name}</strong>
                  <span>{strain.breeder ?? 'Ohne Breeder'} · {formatDominance(strain.dominance)}</span>
                  {(strain.flowerWeeksMin || strain.flowerWeeksMax) && <span>Blüte {strain.flowerWeeksMin ?? '?'}–{strain.flowerWeeksMax ?? '?'} Wochen</span>}
                </article>
              ))}
            </div>
          </section>
        </>
      )}
    </main>
  )
}

function createStrainDraft(): StrainDraft {
  return {
    name: '',
    breeder: '',
    dominance: 'Unknown',
    flowerWeeksMin: '',
    flowerWeeksMax: '',
    notes: '',
    nutrientDemandFactor: '',
    stretchFactor: '',
    vpdPreferenceShift: '',
  }
}

function formatDominance(value: StrainDominance): string {
  switch (value) {
    case 'Indica': return 'Indica'
    case 'Sativa': return 'Sativa'
    case 'Hybrid': return 'Hybrid'
    case 'Unknown': return 'Unbekannt'
  }
}

function toNullableString(value: string | null | undefined): string | null {
  const trimmed = (value ?? '').trim()
  return trimmed.length === 0 ? null : trimmed
}

function toIntOrNull(value: string): number | null {
  if (!value.trim()) return null
  const parsed = Number.parseInt(value, 10)
  return Number.isFinite(parsed) ? parsed : null
}

function toNumberOrNull(value: string): number | null {
  if (!value.trim()) return null
  const parsed = Number.parseFloat(value.replace(',', '.'))
  return Number.isFinite(parsed) ? parsed : null
}

function formatApiError(caught: unknown, fallback: string): string {
  if (!(caught instanceof ApiRequestError)) return fallback
  const fieldErrors = caught.payload?.fieldErrors
  if (!fieldErrors) return caught.message
  const messages = Object.values(fieldErrors).flat()
  return messages.length > 0 ? messages.join(' ') : caught.message
}

export default SettingsPage
