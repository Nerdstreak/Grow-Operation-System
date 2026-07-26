import type { ReactNode } from 'react'
import { formatShort, type PlanFinding, type PlanTimeline } from './grow-plan-model'
import { V1Section } from '../../components/v1'
import { classNames } from '../../utils'

/**
 * Timeline und Prüfung neben dem Formular „Grow anlegen".
 *
 * Die beiden Fragen, die man beim Anlegen wirklich hat: *wann bin ich fertig* und
 * *passt das überhaupt zusammen*. Vorher stand die Antwort im letzten von sechs
 * Schritten — also erst, wenn alles schon eingetragen war.
 */
export function GrowPlanPanel({
  timeline,
  findings,
  summary,
}: {
  timeline: PlanTimeline | null
  findings: PlanFinding[]
  summary: ReactNode
}) {
  return (
    <>
      <V1Section title="Geplante Timeline">
        {timeline ? (
          <div data-audit="grow-timeline">
            <div className="gp-bar">
              {timeline.vegDays != null && (
                <div className="gp-phase is-veg" style={{ flexGrow: Math.max(1, timeline.vegDays) }}>
                  VEG {timeline.vegDays} T
                </div>
              )}
              {timeline.flowerDays != null && (
                <div className="gp-phase is-flower" style={{ flexGrow: Math.max(1, timeline.flowerDays) }}>
                  BLÜTE {timeline.flowerDays} T
                </div>
              )}
              {timeline.vegDays == null && timeline.flowerDays == null && (
                <div className="gp-phase is-open">Dauer offen</div>
              )}
            </div>
            <div className="gp-dates">
              <span>Start {formatShort(timeline.startDate)}</span>
              <span>Flip {formatShort(timeline.flipDate)}</span>
              <span>Ernte ~{formatShort(timeline.harvestDate)}</span>
            </div>
          </div>
        ) : (
          <p className="gp-idle">Sobald ein Startdatum drinsteht, erscheint hier der Verlauf.</p>
        )}
      </V1Section>

      <V1Section title="Prüfung">
        {findings.length === 0 ? (
          <p className="gp-idle">Wird geprüft, sobald Zelt, System und Pflanzenzahl feststehen.</p>
        ) : (
          <ul className="gp-checks" data-audit="grow-plan-checks">
            {findings.map((finding) => (
              <li key={finding.key} className={classNames('gp-check', `is-${finding.severity}`)}>
                <span className="gp-mark">{finding.severity === 'ok' ? 'OK' : finding.severity === 'warn' ? '!' : '×'}</span>
                <span>{finding.text}</span>
              </li>
            ))}
          </ul>
        )}
      </V1Section>

      {summary}
    </>
  )
}
