import { useCallback, useMemo, useState } from 'react'
import type { Dispatch, FormEvent, SetStateAction } from 'react'
import { apiFetch, ApiRequestError } from '../../api'
import type {
  AutoMeasurementConfigDto,
  AutoMeasurementFieldMappingDto,
  AutoMeasurementFieldMappingUpsertRequest,
  AutoMeasurementGrowStatusDto,
  AutoMeasurementRunDto,
  AutoMeasurementTriggerKind,
  GrowDetail,
} from '../../types'
import {
  defaultMetricKeyByField,
  emptyAutoConfigForm,
  emptyMappingDraft,
  toNullableInteger,
} from './grow-detail-model'

type UseGrowDetailAutomationArgs = {
  growId: string | undefined
  grow: GrowDetail | null
  setError: Dispatch<SetStateAction<string | null>>
  setNotice: Dispatch<SetStateAction<string | null>>
  setSaving: Dispatch<SetStateAction<string | null>>
}

export function useGrowDetailAutomation({
  growId,
  grow,
  setError,
  setNotice,
  setSaving,
}: UseGrowDetailAutomationArgs) {
  const [autoConfigs, setAutoConfigs] = useState<AutoMeasurementConfigDto[]>([])
  const [autoMappingsByConfigId, setAutoMappingsByConfigId] = useState<Record<number, AutoMeasurementFieldMappingDto[]>>({})
  const [autoRunsByConfigId, setAutoRunsByConfigId] = useState<Record<number, AutoMeasurementRunDto[]>>({})
  const [autoStatus, setAutoStatus] = useState<AutoMeasurementGrowStatusDto | null>(null)
  const [autoStatusError, setAutoStatusError] = useState<string | null>(null)
  const [mappingDraftsByConfigId, setMappingDraftsByConfigId] = useState<Record<number, AutoMeasurementFieldMappingUpsertRequest[]>>({})
  const [autoConfigForm, setAutoConfigForm] = useState(emptyAutoConfigForm)
  const [autoLoading, setAutoLoading] = useState(false)

  const autoStatusByConfigId = useMemo(
    () => Object.fromEntries((autoStatus?.configs ?? []).map((configStatus) => [configStatus.configId, configStatus] as const)),
    [autoStatus],
  )

  const loadAutoMeasurements = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    setAutoLoading(true)
    try {
      const configs = await apiFetch<AutoMeasurementConfigDto[]>(`/api/auto-measurements/configs?growId=${growId}`, { signal })
      try {
        const status = await apiFetch<AutoMeasurementGrowStatusDto>(`/api/auto-measurements/grows/${growId}/status`, { signal })
        setAutoStatus(status)
        setAutoStatusError(null)
      } catch (caught) {
        if (signal?.aborted) return
        setAutoStatus(null)
        setAutoStatusError(caught instanceof ApiRequestError ? caught.message : 'AutoMeasurement-Status konnte nicht geladen werden.')
      }

      const detailEntries = await Promise.all(configs.map(async (config) => {
        const [mappings, runs] = await Promise.all([
          apiFetch<AutoMeasurementFieldMappingDto[]>(`/api/auto-measurements/configs/${config.id}/mappings`, { signal }),
          apiFetch<AutoMeasurementRunDto[]>(`/api/auto-measurements/configs/${config.id}/runs`, { signal }),
        ])
        return [config.id, mappings, runs] as const
      }))

      const mappingEntries = detailEntries.map(([configId, mappings]) => [configId, mappings] as const)
      const runEntries = detailEntries.map(([configId, , runs]) => [configId, runs] as const)
      const nextMappings = Object.fromEntries(mappingEntries)
      setAutoConfigs(configs)
      setAutoMappingsByConfigId(nextMappings)
      setAutoRunsByConfigId(Object.fromEntries(runEntries))
      setMappingDraftsByConfigId(Object.fromEntries(mappingEntries.map(([configId, mappings]) => [
        configId,
        mappings.map((mapping) => ({
          measurementField: mapping.measurementField,
          metricKey: mapping.metricKey,
          aggregation: mapping.aggregation,
          isRequired: mapping.isRequired,
        })),
      ])))
    } catch (caught) {
      if (signal?.aborted) return
      setError(caught instanceof ApiRequestError ? caught.message : 'AutoMeasurement-Konfigurationen konnten nicht geladen werden.')
    } finally {
      if (!signal?.aborted) setAutoLoading(false)
    }
  }, [growId, setError])

  async function handleAutoConfigSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!grow) return

    const windowMinutes = toNullableInteger(autoConfigForm.windowMinutes)
    if (!autoConfigForm.name.trim() || !windowMinutes) {
      setError('Name und gültiges Zeitfenster sind erforderlich.')
      return
    }

    setSaving('auto-config')
    try {
      await apiFetch('/api/auto-measurements/configs', {
        method: 'POST',
        body: JSON.stringify({
          growId: grow.id,
          tentId: grow.tentId,
          name: autoConfigForm.name.trim(),
          status: autoConfigForm.status,
          triggerKind: autoConfigForm.triggerKind,
          delayMinutes: toNullableInteger(autoConfigForm.delayMinutes),
          windowMinutes,
          captureSnapshot: autoConfigForm.captureSnapshot,
        }),
      })
      setAutoConfigForm(emptyAutoConfigForm())
      setNotice('AutoMeasurement-Konfiguration gespeichert.')
      await loadAutoMeasurements()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'AutoMeasurement-Konfiguration konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function createLightPreset() {
    if (!grow) return

    setSaving('auto-preset')
    try {
      const existing = new Set(autoConfigs.map((config) => config.triggerKind))
      const presets: Array<{ name: string; triggerKind: AutoMeasurementTriggerKind }> = [
        { name: 'Licht AN → Messung (30 min)', triggerKind: 'LightOnDelay' },
        { name: 'Licht AUS → Messung (30 min)', triggerKind: 'LightOffDelay' },
      ]
      const presetMappings: AutoMeasurementFieldMappingUpsertRequest[] = [
        { measurementField: 'ReservoirPh', metricKey: defaultMetricKeyByField.ReservoirPh, aggregation: 'Average', isRequired: false },
        { measurementField: 'ReservoirEc', metricKey: defaultMetricKeyByField.ReservoirEc, aggregation: 'Average', isRequired: false },
        { measurementField: 'ReservoirWaterTempC', metricKey: defaultMetricKeyByField.ReservoirWaterTempC, aggregation: 'Average', isRequired: false },
        { measurementField: 'AirTemperatureC', metricKey: defaultMetricKeyByField.AirTemperatureC, aggregation: 'Average', isRequired: false },
        { measurementField: 'HumidityPercent', metricKey: defaultMetricKeyByField.HumidityPercent, aggregation: 'Average', isRequired: false },
      ]

      let created = 0
      for (const preset of presets) {
        if (existing.has(preset.triggerKind)) continue
        const config = await apiFetch<AutoMeasurementConfigDto>('/api/auto-measurements/configs', {
          method: 'POST',
          body: JSON.stringify({
            growId: grow.id,
            tentId: grow.tentId,
            name: preset.name,
            status: 'Enabled',
            triggerKind: preset.triggerKind,
            delayMinutes: 30,
            windowMinutes: 15,
          }),
        })
        if (config?.id) {
          await apiFetch(`/api/auto-measurements/configs/${config.id}/mappings`, {
            method: 'PUT',
            body: JSON.stringify({ mappings: presetMappings }),
          })
        }
        created += 1
      }

      setNotice(
        created > 0
          ? `Preset angelegt (${created}). Greift automatisch, sobald deine HA-Entitäten zugeordnet sind (Licht als LightStatus + Sensoren).`
          : 'Beide Licht-Auto-Messungen sind bereits angelegt.',
      )
      await loadAutoMeasurements()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Preset konnte nicht angelegt werden.')
    } finally {
      setSaving(null)
    }
  }

  function addMappingDraft(configId: number) {
    setMappingDraftsByConfigId((current) => ({
      ...current,
      [configId]: [...(current[configId] ?? []), emptyMappingDraft()],
    }))
  }

  function updateMappingDraft(configId: number, index: number, patch: Partial<AutoMeasurementFieldMappingUpsertRequest>) {
    setMappingDraftsByConfigId((current) => ({
      ...current,
      [configId]: (current[configId] ?? []).map((mapping, currentIndex) => {
        if (currentIndex !== index) return mapping
        const next = { ...mapping, ...patch }
        if (patch.measurementField && patch.metricKey === undefined) {
          next.metricKey = defaultMetricKeyByField[patch.measurementField]
        }
        return next
      }),
    }))
  }

  function removeMappingDraft(configId: number, index: number) {
    setMappingDraftsByConfigId((current) => ({
      ...current,
      [configId]: (current[configId] ?? []).filter((_, currentIndex) => currentIndex !== index),
    }))
  }

  async function saveMappingDrafts(configId: number) {
    const mappings = mappingDraftsByConfigId[configId] ?? []
    if (mappings.some((mapping) => !mapping.metricKey.trim())) {
      setError('MetricKey darf nicht leer sein.')
      return
    }

    setSaving(`auto-mappings-${configId}`)
    try {
      await apiFetch(`/api/auto-measurements/configs/${configId}/mappings`, {
        method: 'PUT',
        body: JSON.stringify({
          mappings: mappings.map((mapping) => ({
            ...mapping,
            metricKey: mapping.metricKey.trim(),
          })),
        }),
      })
      setNotice('Mappings gespeichert.')
      await loadAutoMeasurements()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Mappings konnten nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  return {
    autoConfigForm,
    autoConfigs,
    autoLoading,
    autoMappingsByConfigId,
    autoRunsByConfigId,
    autoStatusByConfigId,
    autoStatusError,
    mappingDraftsByConfigId,
    addMappingDraft,
    createLightPreset,
    handleAutoConfigSubmit,
    loadAutoMeasurements,
    removeMappingDraft,
    saveMappingDrafts,
    setAutoConfigForm,
    updateMappingDraft,
  }
}
