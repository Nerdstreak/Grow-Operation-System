import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'

type TrendFinding = {
  code: string
  severity: 'Info' | 'Warning' | 'Critical'
  headline: string
  detail: string
  guidanceId: string | null
}

/**
 * What the guard noticed over days rather than right now.
 *
 * Deliberately its own panel next to the risks: a risk says a value is wrong, this says a
 * value is *heading* somewhere — often while still perfectly inside its band. Mixing the
 * two would bury the slow ones under the loud ones.
 */
export function TrendWatchPanel({ growId }: { growId: number | null }) {
  const [findings, setFindings] = useState<TrendFinding[]>([])

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      if (growId == null) {
        setFindings([])
        return
      }
      try {
        const data = await apiFetch<TrendFinding[]>(`/api/trends/${growId}`, { signal: controller.signal })
        if (!controller.signal.aborted) setFindings(data)
      } catch {
        if (!controller.signal.aborted) setFindings([])
      }
    }
    void load()
    return () => controller.abort()
  }, [growId])

  const actionable = findings.filter((finding) => finding.severity !== 'Info').length

  return (
    <article className="ls-panel" data-audit="live-trend-card">
      <div className="ls-panel-head">
        <span className="ls-label">Beobachtungen · über Tage</span>
        {actionable > 0 && <span className="ls-panel-meta ls-trend-count">{actionable} prüfen</span>}
      </div>
      {findings.length === 0 ? (
        <div className="ls-panel-body"><p>Nichts Auffälliges — keine Drift, kein Verbrauchssprung.</p></div>
      ) : (
        <ul className="ls-trends">
          {findings.slice(0, 5).map((finding) => (
            <li key={finding.code} className={finding.severity === 'Info' ? '' : 'is-warn'}>
              <strong>{finding.headline}</strong>
              <span>{finding.detail}</span>
            </li>
          ))}
        </ul>
      )}
    </article>
  )
}
