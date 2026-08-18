import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'
import { ablaufArt } from './sop-woerter'
import { choiceLabel, optionLabel, subjectPlural } from './sop-choice-labels'
import { V1Button } from '../../components/v1'

// The knowledge base's SOP definitions, so a user can start a routine proactively —
// not only when a risk happens to recommend one.
type SopCatalogEntry = {
  id: string
  name?: string
  type?: string
  intervalDays?: number | null
  estimatedDurationMinutes?: number | null
  steps?: unknown[]
}

type SopChoice = { key: string; prompt: string | null; options: string[]; suggested?: string | null }
type SopPlanQuestions = { sopId: string; choices: SopChoice[]; repeatSubjects: string[] }

function meta(entry: SopCatalogEntry): string {
  const parts: string[] = []
  const stepCount = Array.isArray(entry.steps) ? entry.steps.length : 0
  if (entry.type) parts.push(ablaufArt(entry.type))
  if (stepCount > 0) parts.push(`${stepCount} Schritte`)
  if (entry.estimatedDurationMinutes) parts.push(`~${entry.estimatedDurationMinutes} Min`)
  if (entry.intervalDays) parts.push(`alle ${entry.intervalDays} Tage`)
  return parts.join(' · ')
}

export function SopCatalog({
  growId,
  activeSopIds,
  onStarted,
}: {
  growId: string
  activeSopIds: Set<string>
  onStarted: (notice: string) => void
}) {
  const [catalog, setCatalog] = useState<SopCatalogEntry[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState<string | null>(null)

  // The SOP currently being set up, with the answers collected so far.
  const [pending, setPending] = useState<SopPlanQuestions | null>(null)
  const [answers, setAnswers] = useState<Record<string, string>>({})
  const [counts, setCounts] = useState<Record<string, number>>({})

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const list = await apiFetch<SopCatalogEntry[]>('/api/knowledge/sops', { signal: controller.signal })
        if (!controller.signal.aborted) setCatalog(list)
      } catch (caught) {
        if (!controller.signal.aborted) setError(caught instanceof Error ? caught.message : 'SOP-Katalog konnte nicht geladen werden.')
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  /**
   * Branching SOPs ask first. Finding out half-way through a root-rot treatment that a
   * different path applied is exactly what a written procedure exists to prevent.
   */
  async function begin(sopId: string) {
    setBusy(sopId)
    setError(null)
    try {
      // Mit growId, damit die Wasserfrage aus dem Grow vorbeantwortet kommt —
      // beim Anlegen wurde sie schon einmal gestellt.
      const questions = await apiFetch<SopPlanQuestions>(
        `/api/sop-instances/plan-questions/${encodeURIComponent(sopId)}?growId=${encodeURIComponent(growId)}`,
      )
      if (questions.choices.length === 0 && questions.repeatSubjects.length === 0) {
        await send(sopId, {}, {})
        return
      }
      setPending(questions)
      setAnswers(Object.fromEntries(questions.choices.map((choice) => [choice.key, choice.suggested ?? choice.options[0] ?? ''])))
      setCounts(Object.fromEntries(questions.repeatSubjects.map((subject) => [subject, 1])))
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Routine konnte nicht vorbereitet werden.')
    } finally {
      setBusy(null)
    }
  }

  async function send(sopId: string, chosen: Record<string, string>, repeatCounts: Record<string, number>) {
    setBusy(sopId)
    setError(null)
    try {
      await apiFetch('/api/sop-instances/start', {
        method: 'POST',
        body: JSON.stringify({
          growId: Number(growId),
          sopId,
          source: 'Manual',
          answers: chosen,
          repeatCounts,
        }),
      })
      setPending(null)
      onStarted('Routine gestartet.')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Routine konnte nicht gestartet werden.')
    } finally {
      setBusy(null)
    }
  }

  return (
    <>
      <div className="section-label">Routine starten</div>
      <div className="card" style={{ marginBottom: 14 }}>
        <div className="card-header">
          <span className="card-title">SOP-Katalog</span>
          <span className="text-muted" style={{ fontSize: 13 }}>{catalog.length}</span>
        </div>
        {error && <div className="empty-hint" style={{ color: 'var(--red)' }}>{error}</div>}
        {catalog.length === 0 && !error ? (
          <div className="empty-hint">Keine Routinen im Katalog.</div>
        ) : (
          catalog.map((entry) => {
            const active = activeSopIds.has(entry.id)
            const setup = pending?.sopId === entry.id ? pending : null
            return (
              <div key={entry.id} style={{ borderTop: '1px solid var(--border)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, padding: '12px 16px', flexWrap: 'wrap' }}>
                  <div style={{ minWidth: 0 }}>
                    <div className="tl-title">{entry.name || entry.id}</div>
                    <div className="tl-sub">{meta(entry) || '—'}</div>
                  </div>
                  <V1Button type="button" variant="primary" disabled={active || busy === entry.id} onClick={() => void begin(entry.id)}>
                    {active ? 'Läuft' : busy === entry.id ? 'Startet…' : 'Starten'}
                  </V1Button>
                </div>

                {setup && (
                  <div className="sop-setup" data-audit={`sop-setup-${entry.id}`}>
                    <p className="tl-sub" style={{ margin: '0 0 10px' }}>
                      Diese Routine verläuft je nach Situation unterschiedlich. Beantworte das kurz,
                      dann stehen nur die Schritte in der Liste, die wirklich gelten.
                    </p>

                    {setup.choices.map((choice) => (
                      <label key={choice.key} className="sop-setup-field">
                        <span>{choice.prompt || choiceLabel(choice.key)}</span>
                        <select
                          value={answers[choice.key] ?? ''}
                          onChange={(event) => setAnswers((current) => ({ ...current, [choice.key]: event.target.value }))}
                        >
                          {choice.options.map((option) => (
                            <option key={option} value={option}>{optionLabel(option)}</option>
                          ))}
                        </select>
                      </label>
                    ))}

                    {setup.repeatSubjects.map((subject) => (
                      <label key={subject} className="sop-setup-field">
                        <span>Wie viele {subjectPlural(subject)} sind betroffen?</span>
                        <input
                          type="number"
                          min={1}
                          max={200}
                          value={counts[subject] ?? 1}
                          onChange={(event) => setCounts((current) => ({
                            ...current,
                            [subject]: Math.max(1, Number(event.target.value) || 1),
                          }))}
                        />
                      </label>
                    ))}

                    <div className="sop-setup-actions">
                      <V1Button
                        type="button"
                        variant="primary"
                        disabled={busy === entry.id}
                        onClick={() => void send(entry.id, answers, counts)}
                      >
                        {busy === entry.id ? 'Startet…' : 'Routine starten'}
                      </V1Button>
                      <V1Button type="button"  onClick={() => setPending(null)}>Abbrechen</V1Button>
                    </div>
                  </div>
                )}
              </div>
            )
          })
        )}
      </div>
    </>
  )
}
