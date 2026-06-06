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

  return (
    <>
      <div className="section-label" style={{ display: isVisible ? undefined : 'none' }}>Risiken</div>
      <div className="card" style={{ marginBottom: 14, display: isVisible ? undefined : 'none' }}>
        <div className="card-header">
          <span className="card-title">Offene Risiken</span>
          <span className="text-muted" style={{ fontSize: 13 }}>{riskEvents.length}</span>
        </div>
        {riskEventError ? (
          <div className="empty-hint" style={{ color: 'var(--red)' }}>{riskEventError}</div>
        ) : riskEvents.length === 0 ? (
          <div className="empty-hint">Keine offenen Risiken für diesen Grow.</div>
        ) : (
          <div className="rc-risk-action-grid" style={{ padding: 14 }} data-audit="grow-risk-actions">
            {riskEvents.map((risk) => (
              <RiskActionCard key={risk.id} risk={risk} onChanged={onRiskChanged} />
            ))}
          </div>
        )}
      </div>

      <div className="section-label" style={{ display: isVisible ? undefined : 'none' }}>Deviations</div>
      <div className="card" style={{ marginBottom: 14, display: isVisible ? undefined : 'none' }}>
        <div className="card-header">
          <span className="card-title">Hydro-Abweichungen</span>
          <span className="text-muted" style={{ fontSize: 13 }}>{deviations.length}</span>
        </div>
        {deviationError ? (
          <div className="empty-hint" style={{ color: 'var(--red)' }}>{deviationError}</div>
        ) : deviations.length === 0 ? (
          <div className="empty-hint">Keine strukturierten Hydro-Deviations erkannt.</div>
        ) : (
          <div className="grow-deviation-list" data-audit="grow-deviation-list">
            {deviations.map((deviation) => (
              <div key={deviation.stableKey} className={`grow-deviation-card ${deviation.severity.toLowerCase()}`} data-audit="grow-deviation-row">
                <span className={`badge ${deviation.severity === 'Critical' ? 'badge-warn' : deviation.severity === 'Warning' ? 'badge-neutral' : 'badge-ok'}`}>{formatSeverityLabel(deviation.severity)}</span>
                <div className="grow-deviation-main">
                  <div className="tl-title">{deviation.metric}</div>
                  <div className="tl-sub">{deviation.source} · Folge {deviation.consecutiveCount}</div>
                </div>
                <div className="grow-deviation-values">
                  <span>Ist {formatDeviationValue(deviation.actualValue, deviation.unit)}</span>
                  {formatDeviationTarget(deviation) && <span>Ziel {formatDeviationTarget(deviation)}</span>}
                </div>
                <div className="grow-deviation-copy">
                  <p>{deviation.message}</p>
                  {deviation.recommendationHint && <small>{deviation.recommendationHint}</small>}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="section-label" style={{ display: isVisible ? undefined : 'none' }}>Treatment-Empfehlungen</div>
      <div className="card" style={{ marginBottom: 14, display: isVisible ? undefined : 'none' }}>
        <div className="card-header">
          <span className="card-title">Knowledge-Vorschläge</span>
          <span className="text-muted" style={{ fontSize: 13 }}>{treatmentRecommendations?.recommendations.length ?? 0}</span>
        </div>
        {treatmentRecommendationError ? (
          <div className="empty-hint" style={{ color: 'var(--red)' }}>{treatmentRecommendationError}</div>
        ) : !treatmentRecommendations || treatmentRecommendations.recommendations.length === 0 ? (
          <div className="empty-hint">Keine Treatment- oder SOP-Empfehlungen für die aktuellen Deviations.</div>
        ) : (
          <div style={{ display: 'grid' }}>
            {treatmentRecommendations.recommendations.map((recommendation) => (
              <div key={recommendation.stableKey} style={{ display: 'grid', gridTemplateColumns: '120px minmax(180px, 1fr) minmax(0, 2fr)', gap: 10, alignItems: 'start', padding: '12px 16px', borderTop: '1px solid var(--border)' }}>
                <div style={{ display: 'grid', gap: 6 }}>
                  <span className={`badge ${recommendation.confidence === 'High' ? 'badge-warn' : recommendation.confidence === 'Medium' ? 'badge-neutral' : 'badge-ok'}`}>{formatSeverityLabel(recommendation.confidence)}</span>
                  <span className="tl-sub">{formatSeverityLabel(recommendation.severity)}</span>
                </div>
                <div>
                  <div className="tl-title">{recommendation.treatmentName ?? recommendation.sopTitle ?? recommendation.metric}</div>
                  <div className="tl-sub">
                    {recommendation.treatmentId ?? recommendation.sopId ?? recommendation.symptomId ?? 'Diagnosehinweis'}
                  </div>
                </div>
                <div className="tl-sub" style={{ display: 'grid', gap: 5 }}>
                  <span>{recommendation.reason}</span>
                  {recommendation.safetyNotes.length > 0 && <span>Hinweise: {recommendation.safetyNotes.join(' | ')}</span>}
                  {recommendation.conflictTreatmentIds.length > 0 && <span>Konflikte: {recommendation.conflictTreatmentIds.join(', ')}</span>}
                  {recommendation.hardwareRequirements.length > 0 && <span>Hardware: {recommendation.hardwareRequirements.join(', ')}</span>}
                  {recommendation.sopId && (
                    <div>
                      <button type="button" className="btn" disabled={saving === `start-sop-${recommendation.stableKey}`} onClick={() => onStartRecommendedSop(recommendation)}>
                        {saving === `start-sop-${recommendation.stableKey}` ? 'Startet...' : 'SOP starten'}
                      </button>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </>
  )
}
