import { useCallback, useState } from 'react'
import { apiFetch, ApiRequestError } from '../../api'
import type {
  GrowDeviationDto,
  GrowTreatmentRecommendationDto,
  RiskEventDto,
  SopInstanceDto,
  SopStepInstanceDto,
} from '../../types'

type UseGrowDetailResourcesArgs = {
  growId: string | undefined
}

export function useGrowDetailResources({ growId }: UseGrowDetailResourcesArgs) {
  const [deviations, setDeviations] = useState<GrowDeviationDto[]>([])
  const [deviationError, setDeviationError] = useState<string | null>(null)
  const [treatmentRecommendations, setTreatmentRecommendations] = useState<GrowTreatmentRecommendationDto | null>(null)
  const [treatmentRecommendationError, setTreatmentRecommendationError] = useState<string | null>(null)
  const [sopInstances, setSopInstances] = useState<SopInstanceDto[]>([])
  const [sopStepsByInstanceId, setSopStepsByInstanceId] = useState<Record<number, SopStepInstanceDto[]>>({})
  const [sopStepNotesById, setSopStepNotesById] = useState<Record<number, string>>({})
  const [sopInstanceError, setSopInstanceError] = useState<string | null>(null)
  const [riskEvents, setRiskEvents] = useState<RiskEventDto[]>([])
  const [riskEventError, setRiskEventError] = useState<string | null>(null)

  const loadDeviations = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    try {
      const nextDeviations = await apiFetch<GrowDeviationDto[]>(`/api/grows/${growId}/deviations`, { signal })
      setDeviations(nextDeviations)
      setDeviationError(null)
    } catch (caught) {
      if (signal?.aborted) return
      setDeviations([])
      setDeviationError(caught instanceof ApiRequestError ? caught.message : 'Deviations konnten nicht geladen werden.')
    }
  }, [growId])

  const loadTreatmentRecommendations = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    try {
      const nextRecommendations = await apiFetch<GrowTreatmentRecommendationDto>(`/api/grows/${growId}/treatment-recommendations`, { signal })
      setTreatmentRecommendations(nextRecommendations)
      setTreatmentRecommendationError(null)
    } catch (caught) {
      if (signal?.aborted) return
      setTreatmentRecommendations(null)
      setTreatmentRecommendationError(caught instanceof ApiRequestError ? caught.message : 'Treatment-Empfehlungen konnten nicht geladen werden.')
    }
  }, [growId])

  const loadSopInstances = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    try {
      const nextInstances = await apiFetch<SopInstanceDto[]>(`/api/sop-instances?growId=${growId}`, { signal })
      const visibleInstances = nextInstances.filter((instance) => instance.status !== 'Cancelled')
      const stepEntries = await Promise.all(visibleInstances.map(async (instance) => {
        const steps = await apiFetch<SopStepInstanceDto[]>(`/api/sop-instances/${instance.id}/steps`, { signal })
        return [instance.id, steps] as const
      }))
      setSopInstances(visibleInstances)
      setSopStepsByInstanceId(Object.fromEntries(stepEntries))
      setSopStepNotesById(Object.fromEntries(stepEntries.flatMap(([, steps]) => steps.map((step) => [step.id, step.notes ?? ''] as const))))
      setSopInstanceError(null)
    } catch (caught) {
      if (signal?.aborted) return
      setSopInstances([])
      setSopStepsByInstanceId({})
      setSopStepNotesById({})
      setSopInstanceError(caught instanceof ApiRequestError ? caught.message : 'SOP-Instanzen konnten nicht geladen werden.')
    }
  }, [growId])

  const loadRiskEvents = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    try {
      const next = await apiFetch<RiskEventDto[]>(`/api/risk-events?growId=${growId}`, { signal })
      setRiskEvents(next.filter((risk) => risk.status === 'Open' || risk.status === 'Acknowledged'))
      setRiskEventError(null)
    } catch (caught) {
      if (signal?.aborted) return
      setRiskEvents([])
      setRiskEventError(caught instanceof ApiRequestError ? caught.message : 'Risiken konnten nicht geladen werden.')
    }
  }, [growId])

  return {
    deviationError,
    deviations,
    loadDeviations,
    loadRiskEvents,
    loadSopInstances,
    loadTreatmentRecommendations,
    riskEventError,
    riskEvents,
    setSopStepNotesById,
    sopInstanceError,
    sopInstances,
    sopStepNotesById,
    sopStepsByInstanceId,
    treatmentRecommendationError,
    treatmentRecommendations,
  }
}
