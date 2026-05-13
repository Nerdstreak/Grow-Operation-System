import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowDetail, GrowSummary } from '../types'
import { formatDate, formatNumber } from '../utils'

function AnalysisPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [allGrows, setAllGrows] = useState<GrowSummary[]>([])
  const [leftGrow, setLeftGrow] = useState<GrowDetail | null>(null)
  const [rightGrow, setRightGrow] = useState<GrowDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const leftId = searchParams.get('leftGrowId') ?? ''
  const rightId = searchParams.get('rightGrowId') ?? ''

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const [active, archived] = await Promise.all([
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }),
          apiFetch<GrowSummary[]>('/api/grows?archived=true', { signal: controller.signal }),
        ])
        setAllGrows([...active, ...archived])
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Analyse konnte nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  useEffect(() => {
    void loadGrow(leftId, setLeftGrow)
  }, [leftId])

  useEffect(() => {
    void loadGrow(rightId, setRightGrow)
  }, [rightId])

  async function loadGrow(id: string, assign: (grow: GrowDetail | null) => void) {
    if (!id) {
      assign(null)
      return
    }

    try {
      assign(await apiFetch<GrowDetail>(`/api/grows/${id}`))
    } catch {
      assign(null)
    }
  }

  function updateSelection(key: 'leftGrowId' | 'rightGrowId', value: string) {
    const next = new URLSearchParams(searchParams)
    if (value) {
      next.set(key, value)
    } else {
      next.delete(key)
    }

    setSearchParams(next, { replace: true })
  }

  return (
    <>
      <div className="topbar">
        <span className="topbar-title">Analyse</span>
      </div>

      <div className="page-scroll">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14 }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}

        <div className="tents-grid" style={{ marginBottom: 18 }}>
          <label className="field">
            <span>Grow A</span>
            <select value={leftId} onChange={(event) => updateSelection('leftGrowId', event.target.value)}>
              <option value="">– Grow wählen –</option>
              {allGrows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
            </select>
          </label>
          <label className="field">
            <span>Grow B</span>
            <select value={rightId} onChange={(event) => updateSelection('rightGrowId', event.target.value)}>
              <option value="">– Grow wählen –</option>
              {allGrows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
            </select>
          </label>
        </div>

        {loading ? (
          <div className="empty-hint">Lade Analyse...</div>
        ) : !leftGrow && !rightGrow ? (
          <div className="empty-hint">Wähle zwei Grows zum Vergleichen.</div>
        ) : (
          <div className="data-table">
            <div className="data-table-header" style={{ gridTemplateColumns: '1.2fr 1fr 1fr' }}>
              <span>Kennzahl</span>
              <span>{leftGrow?.name ?? '–'}</span>
              <span>{rightGrow?.name ?? '–'}</span>
            </div>
            {compareRows(leftGrow, rightGrow).map((row) => (
              <div key={row.label} className="data-row" style={{ gridTemplateColumns: '1.2fr 1fr 1fr', cursor: 'default' }}>
                <div className="row-name">{row.label}</div>
                <div className="row-muted">{row.left}</div>
                <div className="row-muted">{row.right}</div>
              </div>
            ))}
          </div>
        )}
      </div>
    </>
  )
}

function compareRows(left: GrowDetail | null, right: GrowDetail | null) {
  return [
    { label: 'Strain', left: left?.strain ?? '–', right: right?.strain ?? '–' },
    { label: 'Hydro-Stil', left: left?.hydroStyle ?? '–', right: right?.hydroStyle ?? '–' },
    { label: 'Nährstoffe', left: left?.nutrients ?? '–', right: right?.nutrients ?? '–' },
    { label: 'Startdatum', left: formatDate(left?.startDate), right: formatDate(right?.startDate) },
    { label: 'Pflanzen', left: left?.plantCount?.toString() ?? '–', right: right?.plantCount?.toString() ?? '–' },
    { label: 'Messungen', left: left?.measurementCount?.toString() ?? '–', right: right?.measurementCount?.toString() ?? '–' },
    { label: 'EC (letzte Messung)', left: formatNumber(left?.latestMeasurement?.reservoirEc, 2), right: formatNumber(right?.latestMeasurement?.reservoirEc, 2) },
    { label: 'pH (letzte Messung)', left: formatNumber(left?.latestMeasurement?.reservoirPh, 2), right: formatNumber(right?.latestMeasurement?.reservoirPh, 2) },
    { label: 'Temperatur (letzte)', left: formatNumber(left?.latestMeasurement?.airTemperatureC, 1), right: formatNumber(right?.latestMeasurement?.airTemperatureC, 1) },
    { label: 'Luftfeuchte (letzte)', left: formatNumber(left?.latestMeasurement?.humidityPercent, 0), right: formatNumber(right?.latestMeasurement?.humidityPercent, 0) },
  ]
}

export default AnalysisPage
