import { useEffect, useState, type FormEvent } from 'react'
import { apiFetch, ApiRequestError } from '../../api'
import type { ChangeoutDto, ChangeoutKind, CreateChangeoutRequest } from '../../types'
import { V1Alert, V1Button, V1Card, V1Empty, V1Field, V1Section } from '../../components/v1'
import { formatDateTime, formatNumber } from '../../utils'

type FormState = {
  kind: ChangeoutKind
  percentChanged: string
  volumeChangedLiters: string
  ecBefore: string
  ecAfter: string
  phBefore: string
  phAfter: string
  notes: string
}

const emptyForm: FormState = {
  kind: 'Partial',
  percentChanged: '',
  volumeChangedLiters: '',
  ecBefore: '',
  ecAfter: '',
  phBefore: '',
  phAfter: '',
  notes: '',
}

function toNumber(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number(trimmed.replace(',', '.'))
  return Number.isFinite(parsed) ? parsed : null
}

function pair(before: number | null, after: number | null): string {
  if (before == null && after == null) return '–'
  return `${formatNumber(before, 2)} → ${formatNumber(after, 2)}`
}

export function ChangeoutsPanel({ growId, growName }: { growId: number; growName: string }) {
  const [items, setItems] = useState<ChangeoutDto[]>([])
  const [loading, setLoading] = useState(true)
  const [open, setOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [refresh, setRefresh] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    async function run() {
      try {
        const data = await apiFetch<ChangeoutDto[]>(`/api/grows/${growId}/changeouts`, { signal: controller.signal })
        if (controller.signal.aborted) return
        setItems([...data].sort((a, b) => b.performedAtUtc.localeCompare(a.performedAtUtc)))
        setError(null)
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Wasserwechsel konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void run()
    return () => controller.abort()
  }, [growId, refresh])

  const patch = (next: Partial<FormState>) => setForm((current) => ({ ...current, ...next }))

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    setError(null)
    setNotice(null)
    try {
      const body: CreateChangeoutRequest = {
        kind: form.kind,
        percentChanged: toNumber(form.percentChanged),
        volumeChangedLiters: toNumber(form.volumeChangedLiters),
        ecBefore: toNumber(form.ecBefore),
        ecAfter: toNumber(form.ecAfter),
        phBefore: toNumber(form.phBefore),
        phAfter: toNumber(form.phAfter),
        notes: form.notes.trim() || null,
      }
      await apiFetch<ChangeoutDto>(`/api/grows/${growId}/changeouts`, { method: 'POST', body: JSON.stringify(body) })
      setForm(emptyForm)
      setOpen(false)
      setNotice('Wasserwechsel gespeichert.')
      setRefresh((value) => value + 1)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Speichern fehlgeschlagen.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <V1Section
      title="Wasserwechsel"
      className="changeouts-section"
      action={<V1Button variant={open ? 'secondary' : 'primary'} onClick={() => { setOpen((value) => !value); setNotice(null) }}>{open ? 'Abbrechen' : 'Wechsel erfassen'}</V1Button>}
    >
      {notice && <V1Alert title="Gespeichert" message={notice} tone="ok" />}
      {error && <V1Alert message={error} tone="warn" />}

      {open && (
        <V1Card className="changeouts-form-card">
          <form onSubmit={submit} className="changeouts-form" data-audit="changeout-form">
            <V1Field label="Art">
              <select value={form.kind} onChange={(event) => {
                const kind = event.target.value as ChangeoutKind
                // A full change is 100% by definition — fix the share and lock the field.
                patch({ kind, percentChanged: kind === 'Full' ? '100' : form.percentChanged })
              }}>
                <option value="Partial">Teilwechsel</option>
                <option value="Full">Komplettwechsel</option>
              </select>
            </V1Field>
            <V1Field label="Anteil (%)"><input inputMode="decimal" value={form.percentChanged} onChange={(event) => patch({ percentChanged: event.target.value })} placeholder="z. B. 50" disabled={form.kind === 'Full'} /></V1Field>
            <V1Field label="Menge (L)"><input inputMode="decimal" value={form.volumeChangedLiters} onChange={(event) => patch({ volumeChangedLiters: event.target.value })} placeholder="z. B. 40" /></V1Field>
            <V1Field label="EC vorher"><input inputMode="decimal" value={form.ecBefore} onChange={(event) => patch({ ecBefore: event.target.value })} placeholder="mS/cm" /></V1Field>
            <V1Field label="EC nachher"><input inputMode="decimal" value={form.ecAfter} onChange={(event) => patch({ ecAfter: event.target.value })} placeholder="mS/cm" /></V1Field>
            <V1Field label="pH vorher"><input inputMode="decimal" value={form.phBefore} onChange={(event) => patch({ phBefore: event.target.value })} placeholder="z. B. 5.8" /></V1Field>
            <V1Field label="pH nachher"><input inputMode="decimal" value={form.phAfter} onChange={(event) => patch({ phAfter: event.target.value })} placeholder="z. B. 5.9" /></V1Field>
            <V1Field label="Notiz" wide><input value={form.notes} onChange={(event) => patch({ notes: event.target.value })} placeholder="Beobachtung, Grund …" /></V1Field>
            <div className="changeouts-form-actions">
              <V1Button type="submit" variant="primary" disabled={saving}>{saving ? 'Speichert…' : 'Wasserwechsel speichern'}</V1Button>
            </div>
          </form>
        </V1Card>
      )}

      {loading ? (
        <V1Empty title="Lade Wasserwechsel …" />
      ) : items.length === 0 ? (
        <V1Empty title="Noch kein Wasserwechsel" text={`Für ${growName} ist noch kein Reservoir-Wechsel erfasst.`} />
      ) : (
        <div className="v1-list" data-audit="changeout-list">
          {items.map((item) => (
            <div key={item.id} className="v1-list-row">
              <strong>{formatDateTime(item.performedAtUtc)}</strong>
              <span>
                {item.kind === 'Full' ? 'Komplettwechsel' : 'Teilwechsel'}
                {item.percentChanged != null ? ` · ${formatNumber(item.percentChanged, 0)}%` : ''}
                {item.volumeChangedLiters != null ? ` · ${formatNumber(item.volumeChangedLiters, 1)} L` : ''}
                {` · EC ${pair(item.ecBefore, item.ecAfter)} · pH ${pair(item.phBefore, item.phAfter)}`}
                {item.notes ? ` · ${item.notes}` : ''}
              </span>
              <em>{item.kind === 'Full' ? 'FULL' : 'PART'}</em>
            </div>
          ))}
        </div>
      )}
    </V1Section>
  )
}
