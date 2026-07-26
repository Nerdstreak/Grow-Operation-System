import { checkDraft, summariseOk } from './live-check-model'
import type { MetricPayload } from '../../types'
import { V1Section } from '../../components/v1'
import { classNames } from '../../utils'

/**
 * Die Abweichungen zu den gerade eingetippten Werten, neben dem Formular.
 *
 * Vorher stand das Ergebnis erst nach dem Speichern in der Diagnose. Wer am
 * Reservoir steht, erfährt es jetzt beim Tippen — und kann direkt handeln, statt
 * später zurückzukommen.
 */
export function LiveCheckPanel({ draft, metrics }: { draft: Record<string, string>; metrics: MetricPayload[] }) {
  const findings = checkDraft(draft, metrics)
  const problems = findings.filter((finding) => finding.severity !== 'ok')
  const okLine = summariseOk(findings)

  if (findings.length === 0) {
    return (
      <V1Section title="Abweichungen">
        <p className="chk-idle">
          Sobald Werte drinstehen, wird hier gegen die Sollwerte der aktuellen Phase geprüft.
        </p>
      </V1Section>
    )
  }

  return (
    <V1Section title="Abweichungen — live geprüft">
      <ul className="chk-list" data-audit="live-check">
        {problems.map((finding) => (
          <li key={finding.field} className={classNames('chk-row', `is-${finding.severity}`)}>
            <span className="chk-delta">
              {finding.delta != null ? `${finding.delta > 0 ? '+' : '−'}${String(Math.abs(finding.delta)).replace('.', ',')}` : ''}
            </span>
            <span className="chk-text">
              <strong>{finding.text}</strong>
              {finding.hint && <em>{finding.hint}</em>}
            </span>
          </li>
        ))}
        {okLine && (
          <li className="chk-row is-ok">
            <span className="chk-delta">OK</span>
            <span className="chk-text"><strong>{okLine}</strong></span>
          </li>
        )}
      </ul>
    </V1Section>
  )
}
