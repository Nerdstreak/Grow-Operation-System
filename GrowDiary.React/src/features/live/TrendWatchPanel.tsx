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
    <div className="ix-panel ix-alerts ix-rise ix-d5" data-audit="live-trend-card">
      <div className="ix-alerts-head">
        <h3>Beobachtungen · über Tage</h3>
        {actionable > 0 && <span className="ix-badge ix-b-warn">{actionable} prüfen</span>}
      </div>

      {findings.length === 0 ? (
        <div className="ix-empty-line">Nichts Auffälliges — keine Drift, kein Verbrauchssprung.</div>
      ) : (
        findings.slice(0, 5).map((finding) => (
          <div key={finding.code} className={`ix-alert ${finding.severity === 'Info' ? '' : 'warn'}`}>
            <div className="sev" />
            <div>
              <div className="ttl">{finding.headline}</div>
              <div className="meta">{finding.detail}</div>
            </div>
          </div>
        ))
      )}
    </div>
  )
}
