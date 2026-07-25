import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'

type Verdict = 'Unknown' | 'Normal' | 'Instability'

type Signal = { key: string; label: string; verdict: Verdict; observation: string }

type Assessment = {
  overall: Verdict
  headline: string
  detail: string
  signals: Signal[]
  visualChecks: string[]
}

const MARK: Record<Verdict, string> = { Normal: '✓', Instability: '!', Unknown: '·' }

/**
 * SOP-N1 §2.1 on screen: the whole diagnostic table at once.
 *
 * The point is that no single row decides. A falling pH with stable EC and good oxygen is
 * a plant feeding; the same falling pH with rising EC and low oxygen is biofilm. Showing
 * the rows next to each other is what makes that readable — and the two checks no sensor
 * can make are listed rather than dropped.
 */
export function SolutionStabilityPanel({ growId }: { growId: string }) {
  const [assessment, setAssessment] = useState<Assessment | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const data = await apiFetch<Assessment>(`/api/trends/${growId}/stability`, { signal: controller.signal })
        if (!controller.signal.aborted) setAssessment(data)
      } catch {
        if (!controller.signal.aborted) setAssessment(null)
      }
    }
    void load()
    return () => controller.abort()
  }, [growId])

  if (!assessment) return null

  return (
    <>
      <div className="section-label">Zustand der Nährlösung</div>
      <div className="card" style={{ marginBottom: 14 }} data-audit="solution-stability">
        <div className={`stability-head ${assessment.overall.toLowerCase()}`}>
          <strong>{assessment.headline}</strong>
          <p>{assessment.detail}</p>
        </div>

        <ul className="stability-signals">
          {assessment.signals.map((signal) => (
            <li key={signal.key} className={signal.verdict.toLowerCase()}>
              <span className="mark" aria-hidden="true">{MARK[signal.verdict]}</span>
              <span className="lab">{signal.label}</span>
              <span className="obs">{signal.observation}</span>
            </li>
          ))}
        </ul>

        <div className="stability-visual">
          <strong>Selbst prüfen — dafür gibt es keinen Sensor:</strong>
          <ul>
            {assessment.visualChecks.map((check) => <li key={check}>{check}</li>)}
          </ul>
        </div>
      </div>
    </>
  )
}
