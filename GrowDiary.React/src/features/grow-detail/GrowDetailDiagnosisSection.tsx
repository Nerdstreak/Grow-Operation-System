import type { GrowDeviationDto, GrowTreatmentRecommendationDto, RiskEventDto, TreatmentRecommendationDto } from '../../types'
import { formatSeverityLabel } from '../../utils'
import { RiskActionCard } from '../risks/RiskActionCard'
import { formatDeviationTarget, formatDeviationValue, type GrowDetailSection } from './grow-detail-model'

type GrowDetailDiagnosisSectionProps = {
  activeSection: GrowDetailSection
  deviations: GrowDeviationDto[]
  deviationError: string | null
  treatmentRecommendations: GrowTreatmentRecommendationDto | null
  treatmentRecommendationError: string | null
  riskEvents: RiskEventDto[]
  riskEventError: string | null
  saving: string | null
  onStartRecommendedSop: (recommendation: TreatmentRecommendationDto) => void
  onRiskChanged: (notice: string) => void
}

const severityBadge = (severity: string) =>
  severity === 'Critical' ? 'badge-warn' : severity === 'Warning' ? 'badge-neutral' : 'badge-ok'

// Diagnose in one clear shape: "what needs doing" (risks, with their actions) up top,
// then a quiet, plain-language list of the underlying odd readings and tips. No more
// three parallel cards full of internal jargon (deviations/symptomId/confidence).
export function GrowDetailDiagnosisSection({
  activeSection,
  deviations,
  deviationError,
  treatmentRecommendations,
  treatmentRecommendationError,
  riskEvents,
  riskEventError,
  saving,
  onStartRecommendedSop,
  onRiskChanged,
}: GrowDetailDiagnosisSectionProps) {
  const isVisible = activeSection === 'diagnosis'
  // Only recommendations that actually offer a next step (an SOP) — the rest is noise here.
  const actionableRecommendations = (treatmentRecommendations?.recommendations ?? []).filter((recommendation) => recommendation.sopId)
  const hasDetails = deviations.length > 0 || actionableRecommendations.length > 0

  return (
    <div style={{ display: isVisible ? 'grid' : 'none', gap: 14 }}>
      <div className="section-label">Handlungsbedarf</div>
      <div className="card">
        <div className="card-header">
          <span className="card-title">Was ist los</span>
          <span className="text-muted" style={{ fontSize: 13 }}>{riskEvents.length}</span>
        </div>
        {riskEventError ? (
          <div className="empty-hint" style={{ color: 'var(--red)' }}>{riskEventError}</div>
        ) : riskEvents.length === 0 ? (
          <div className="empty-hint">Alles im grünen Bereich — aktuell kein Handlungsbedarf.</div>
        ) : (
          <div className="rc-risk-action-grid" style={{ padding: 14 }} data-audit="grow-risk-actions">
            {riskEvents.map((risk) => (
              <RiskActionCard key={risk.id} risk={risk} onChanged={onRiskChanged} />
            ))}
          </div>
        )}
      </div>

      {hasDetails && (
        <>
          <div className="section-label">Auffällige Werte &amp; Tipps</div>
          <div className="card">
            {(deviationError || treatmentRecommendationError) && (
              <div className="empty-hint" style={{ color: 'var(--red)' }}>{deviationError ?? treatmentRecommendationError}</div>
            )}
            <div className="grow-deviation-list" data-audit="grow-deviation-list">
              {deviations.map((deviation) => (
                <div key={deviation.stableKey} className={`grow-deviation-card ${deviation.severity.toLowerCase()}`} data-audit="grow-deviation-row">
                  <span className={`badge ${severityBadge(deviation.severity)}`}>{formatSeverityLabel(deviation.severity)}</span>
                  <div className="grow-deviation-main">
                    <div className="tl-title">{deviation.metric}</div>
                    <div className="tl-sub">
                      Ist {formatDeviationValue(deviation.actualValue, deviation.unit)}
                      {formatDeviationTarget(deviation) ? ` · Ziel ${formatDeviationTarget(deviation)}` : ''}
                    </div>
                  </div>
                  <div className="grow-deviation-copy">
                    <p>{deviation.message}</p>
                    {deviation.recommendationHint && <small>Tipp: {deviation.recommendationHint}</small>}
                  </div>
                </div>
              ))}

              {actionableRecommendations.map((recommendation) => (
                <div key={recommendation.stableKey} className="grow-deviation-card" data-audit="grow-recommendation-row" style={{ alignItems: 'center' }}>
                  <span className="badge badge-neutral">Empfehlung</span>
                  <div className="grow-deviation-main">
                    <div className="tl-title">{recommendation.treatmentName ?? recommendation.sopTitle ?? recommendation.metric}</div>
                    <div className="tl-sub">{recommendation.reason}</div>
                  </div>
                  <div className="grow-deviation-copy">
                    <button type="button" className="btn btn-primary" disabled={saving === `start-sop-${recommendation.stableKey}`} onClick={() => onStartRecommendedSop(recommendation)}>
                      {saving === `start-sop-${recommendation.stableKey}` ? 'Startet…' : 'SOP starten'}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  )
}
