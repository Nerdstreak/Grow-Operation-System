import { useCallback, useState } from 'react'
import type { Dispatch, SetStateAction } from 'react'
import { apiFetch, ApiRequestError } from '../../api'
import type { GrowDetail, GrowTaskDto, JournalEntryDto, MeasurementDto, PhotoAssetDto } from '../../types'

export interface DetailBundle {
  grow: GrowDetail | null
  measurements: MeasurementDto[]
  tasks: GrowTaskDto[]
  journal: JournalEntryDto[]
}

type UseGrowDetailBundleArgs = {
  growId: string | undefined
  setError: Dispatch<SetStateAction<string | null>>
}

export function useGrowDetailBundle({ growId, setError }: UseGrowDetailBundleArgs) {
  const [bundle, setBundle] = useState<DetailBundle>({ grow: null, measurements: [], tasks: [], journal: [] })
  const [photos, setPhotos] = useState<PhotoAssetDto[]>([])
  const [selectedMeasurementId, setSelectedMeasurementId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [photoLoading, setPhotoLoading] = useState(false)

  const loadPhotos = useCallback(async (measurementId: number, signal?: AbortSignal) => {
    setPhotoLoading(true)
    try {
      const nextPhotos = await apiFetch<PhotoAssetDto[]>(`/api/measurements/${measurementId}/photos`, { signal })
      setPhotos(nextPhotos)
    } catch (caught) {
      if (signal?.aborted) return
      setError(caught instanceof ApiRequestError ? caught.message : 'Fotos konnten nicht geladen werden.')
    } finally {
      if (!signal?.aborted) setPhotoLoading(false)
    }
  }, [setError])

  const loadBundle = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    try {
      const [grow, measurements, tasks, journal] = await Promise.all([
        apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal }),
        apiFetch<MeasurementDto[]>(`/api/grows/${growId}/measurements`, { signal }),
        apiFetch<GrowTaskDto[]>(`/api/grows/${growId}/tasks`, { signal }),
        apiFetch<JournalEntryDto[]>(`/api/grows/${growId}/journal`, { signal }),
      ])
      const nextMeasurementId = measurements.find((measurement) => measurement.id === selectedMeasurementId)?.id ?? measurements[0]?.id ?? null
      setBundle({ grow, measurements, tasks, journal })
      setSelectedMeasurementId(nextMeasurementId)
      setError(null)
      if (nextMeasurementId) {
        await loadPhotos(nextMeasurementId, signal)
      } else {
        setPhotos([])
      }
    } catch (caught) {
      if (signal?.aborted) return
      setError(caught instanceof ApiRequestError ? caught.message : 'Grow-Details konnten nicht geladen werden.')
    } finally {
      if (!signal?.aborted) setLoading(false)
    }
  }, [growId, loadPhotos, selectedMeasurementId, setError])

  async function handleMeasurementSelection(nextId: number | null) {
    setSelectedMeasurementId(nextId)
    if (!nextId) {
      setPhotos([])
      return
    }

    await loadPhotos(nextId)
  }

  return {
    bundle,
    loading,
    photoLoading,
    photos,
    selectedMeasurementId,
    handleMeasurementSelection,
    loadBundle,
    loadPhotos,
  }
}
