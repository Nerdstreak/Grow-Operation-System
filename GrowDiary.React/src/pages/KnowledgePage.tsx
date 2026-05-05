import { useEffect, useState } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type { KnowledgeOverviewDto } from '../types'

type Selection =
  | { kind: 'program'; key: string }
  | { kind: 'playbook'; key: string }

function KnowledgePage() {
  const [knowledge, setKnowledge] = useState<KnowledgeOverviewDto | null>(null)
  const [selection, setSelection] = useState<Selection | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const data = await apiFetch<KnowledgeOverviewDto>('/api/knowledge', { signal: controller.signal })
        setKnowledge(data)
        if (data.programs[0]) {
          setSelection({ kind: 'program', key: data.programs[0].key })
        } else if (data.playbooks[0]) {
          setSelection({ kind: 'playbook', key: data.playbooks[0].key })
        } else {
          setSelection(null)
        }
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Wissen konnte nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  const selectedProgram = selection?.kind === 'program'
    ? knowledge?.programs.find((program) => program.key === selection.key) ?? null
    : null
  const selectedPlaybook = selection?.kind === 'playbook'
    ? knowledge?.playbooks.find((playbook) => playbook.key === selection.key) ?? null
    : null

  return (
    <>
      <div className="topbar">
        <span className="topbar-title">Wissen</span>
      </div>

      <div className="page-scroll">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14 }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}

        {loading ? (
          <div className="empty-hint">Lade Wissensbasis...</div>
        ) : !knowledge ? (
          <div className="empty-hint">Keine Wissensdaten vorhanden.</div>
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: '280px minmax(0, 1fr)', gap: 18 }}>
            <div style={{ display: 'grid', gap: 14 }}>
              <div>
                <div className="section-label" style={{ marginTop: 0 }}>Naehrstoffprogramme</div>
                <div className="panel-card">
                  {knowledge.programs.map((program) => (
                    <button
                      key={program.key}
                      type="button"
                      className="task-item"
                      style={{
                        width: '100%',
                        textAlign: 'left',
                        background: selection?.kind === 'program' && selection.key === program.key ? 'var(--surface2)' : 'transparent',
                        border: 'none',
                      }}
                      onClick={() => setSelection({ kind: 'program', key: program.key })}
                    >
                      <div>
                        <div className="task-title">{program.name}</div>
                        <div className="task-sub">{program.manufacturer}</div>
                      </div>
                    </button>
                  ))}
                </div>
              </div>

              <div>
                <div className="section-label">Playbooks</div>
                <div className="panel-card">
                  {knowledge.playbooks.map((playbook) => (
                    <button
                      key={playbook.key}
                      type="button"
                      className="task-item"
                      style={{
                        width: '100%',
                        textAlign: 'left',
                        background: selection?.kind === 'playbook' && selection.key === playbook.key ? 'var(--surface2)' : 'transparent',
                        border: 'none',
                      }}
                      onClick={() => setSelection({ kind: 'playbook', key: playbook.key })}
                    >
                      <div className="task-title">{playbook.title}</div>
                    </button>
                  ))}
                </div>
              </div>
            </div>

            <div style={{ display: 'grid', gap: 16 }}>
              {selectedProgram ? (
                <>
                  <div className="grow-hero">
                    <div className="grow-hero-title">{selectedProgram.name}</div>
                    <div className="grow-hero-sub">{selectedProgram.manufacturer} · {selectedProgram.category}</div>
                    {selectedProgram.summary && <p style={{ marginTop: 10 }}>{selectedProgram.summary}</p>}
                  </div>

                  {selectedProgram.stages.length > 0 && (
                    <div>
                      <div className="section-label">Dosierung je Phase</div>
                      <div className="tents-grid">
                        {selectedProgram.stages.map((stage) => (
                          <div key={stage.stage} className="panel-card">
                            <div className="card-pad" style={{ display: 'grid', gap: 8 }}>
                              <div className="task-title">{stage.stage}</div>
                              <div className="row-val">{stage.dose}</div>
                              {stage.target && <div className="task-sub">{stage.target}</div>}
                              {stage.notes && <div className="focus-body">{stage.notes}</div>}
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}

                  <div className="tents-grid">
                    {selectedProgram.phGuidance && <GuidanceCard title="pH" body={selectedProgram.phGuidance} />}
                    {selectedProgram.ecGuidance && <GuidanceCard title="EC" body={selectedProgram.ecGuidance} />}
                    {selectedProgram.waterGuidance && <GuidanceCard title="Wasser" body={selectedProgram.waterGuidance} />}
                    {selectedProgram.bestFor && <GuidanceCard title="Geeignet fuer" body={selectedProgram.bestFor} />}
                  </div>

                  {selectedProgram.tips.length > 0 && (
                    <div className="panel-card">
                      <div className="panel-card-header">
                        <span className="panel-card-title">Tipps</span>
                      </div>
                      {selectedProgram.tips.map((tip) => (
                        <div key={tip} className="task-item">
                          <div className="prio-dot prio-low" />
                          <div className="task-sub" style={{ color: 'var(--text)' }}>{tip}</div>
                        </div>
                      ))}
                    </div>
                  )}
                </>
              ) : selectedPlaybook ? (
                <>
                  <div className="grow-hero">
                    <div className="grow-hero-title">{selectedPlaybook.title}</div>
                    {selectedPlaybook.summary && <p style={{ marginTop: 10 }}>{selectedPlaybook.summary}</p>}
                  </div>

                  <div className="panel-card">
                    <div className="panel-card-header">
                      <span className="panel-card-title">Fokuspunkte</span>
                    </div>
                    {selectedPlaybook.focusPoints.map((point) => (
                      <div key={point} className="task-item">
                        <div className="prio-dot prio-low" />
                        <div className="task-sub" style={{ color: 'var(--text)' }}>{point}</div>
                      </div>
                    ))}
                  </div>

                  <div className="panel-card">
                    <div className="panel-card-header">
                      <span className="panel-card-title">Red Flags</span>
                    </div>
                    {selectedPlaybook.redFlags.map((flag) => (
                      <div key={flag} className="task-item">
                        <div className="prio-dot prio-high" />
                        <div className="task-sub" style={{ color: 'var(--text)' }}>{flag}</div>
                      </div>
                    ))}
                  </div>
                </>
              ) : (
                <div className="empty-hint">Keine Wissenselemente gefunden.</div>
              )}
            </div>
          </div>
        )}
      </div>
    </>
  )
}

function GuidanceCard({ title, body }: { title: string; body: string }) {
  return (
    <div className="panel-card">
      <div className="card-pad" style={{ display: 'grid', gap: 6 }}>
        <div className="focus-label">{title}</div>
        <div className="focus-body">{body}</div>
      </div>
    </div>
  )
}

export default KnowledgePage
