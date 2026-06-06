import { useState } from 'react'
import { apiFetch, ApiRequestError } from '../../api'
import type {
  AcknowledgeRiskEventRequest,
  ResolveRiskEventRequest,
  RiskEventDto,
  RiskEventSopRecommendationDto,
  SopInstanceDto,
  StartRiskEventSopRequest,
} from '../../types'
import { V1Button, V1Card } from '../../components/v1'
import { formatSeverityLabel } from '../../utils'

type Props = {
  risk: RiskEventDto
  context?: string
  onChanged: (notice: string) => void
}

export function RiskActionCard({ risk, context, onChanged }: Props) {
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [recommendations, setRecommendations] = useState<RiskEventSopRecommendationDto[] | null>(null)

  async function run(key: string, fn: () => Promise<void>) {
    setBusy(key)
    setError(null)
    try {
      await fn()
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Aktion fehlgeschlagen.')
    } finally {
      setBusy(null)
    }
  }

  const acknowledge = () =>
    run('ack', async () => {
      const body: AcknowledgeRiskEventRequest = {}
      await apiFetch<RiskEventDto>(`/api/risk-events/${risk.id}/acknowledge`, { method: 'POST', body: JSON.stringify(body) })
      onChanged(`„${risk.title}" bestätigt.`)
    })

  const resolve = () =>
    run('resolve', async () => {
      const body: ResolveRiskEventRequest = {}
      await apiFetch<RiskEventDto>(`/api/risk-events/${risk.id}/resolve`, { method: 'POST', body: JSON.stringify(body) })
      onChanged(`„${risk.title}" als erledigt markiert.`)
    })

  const loadRecommendations = () =>
    run('recommend', async () => {
      const recs = await apiFetch<RiskEventSopRecommendationDto[]>(`/api/risk-events/${risk.id}/sop-recommendations`)
      setRecommendations(recs)
    })

  const startSop = (sopId: string) =>
    run(`sop-${sopId}`, async () => {
      const body: StartRiskEventSopRequest = { sopId }
      await apiFetch<SopInstanceDto>(`/api/risk-events/${risk.id}/start-sop`, { method: 'POST', body: JSON.stringify(body) })
      onChanged('SOP gestartet.')
    })

  const isAcknowledged = risk.status === 'Acknowledged'
  const tone = risk.severity === 'Critical' ? 'critical' : 'warn'

  return (
    <V1Card className="rc-risk-action-card" tone={tone}>
      <div className="rc-risk-action-head">
        <div>
          <strong>{risk.title}</strong>
          {(context || isAcknowledged) && (
            <span>{[context, isAcknowledged ? 'bestätigt' : null].filter(Boolean).join(' · ')}</span>
          )}
        </div>
        <em>{formatSeverityLabel(risk.severity)}</em>
      </div>
      {risk.description && <p className="rc-risk-action-desc">{risk.description}</p>}
      <div className="rc-risk-action-buttons">
        {!isAcknowledged && (
          <V1Button variant="secondary" disabled={busy !== null} onClick={() => void acknowledge()} audit="risk-acknowledge">
            {busy === 'ack' ? 'Speichert…' : 'Bestätigen'}
          </V1Button>
        )}
        <V1Button variant="primary" disabled={busy !== null} onClick={() => void resolve()} audit="risk-resolve">
          {busy === 'resolve' ? 'Speichert…' : 'Erledigt'}
        </V1Button>
        {risk.growId !== null && recommendations === null && (
          <V1Button variant="ghost" disabled={busy !== null} onClick={() => void loadRecommendations()} audit="risk-sop-recommend">
            {busy === 'recommend' ? 'Lädt…' : 'SOP vorschlagen'}
          </V1Button>
        )}
      </div>
      {recommendations !== null && (
        <div className="rc-risk-sop-list">
          {recommendations.length === 0 ? (
            <p className="rc-risk-action-desc">Keine SOP-Empfehlung für dieses Risiko.</p>
          ) : (
            recommendations.map((rec) => (
              <div key={rec.sopId} className="rc-risk-sop-row">
                <div>
                  <strong>{rec.sopName}</strong>
                  <span>{rec.reason} · {rec.confidence}</span>
                </div>
                <V1Button
                  variant="secondary"
                  disabled={busy !== null || rec.alreadyActive}
                  onClick={() => void startSop(rec.sopId)}
                  audit="risk-sop-start"
                >
                  {rec.alreadyActive ? 'Aktiv' : busy === `sop-${rec.sopId}` ? 'Startet…' : 'Starten'}
                </V1Button>
              </div>
            ))
          )}
        </div>
      )}
      {error && <p className="rc-risk-action-error">{error}</p>}
    </V1Card>
  )
}
