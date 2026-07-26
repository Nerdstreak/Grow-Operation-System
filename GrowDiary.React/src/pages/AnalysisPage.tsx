import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowDetail, GrowSummary } from '../types'
import { V1Alert, V1Empty, V1Field, V1Page, V1Section, V1Skeleton } from '../components/v1'
import { classNames, formatDate, formatNumber } from '../utils'
import '../features/analysis/compare.css'

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

  const loadGrow = useCallback(async (id: string, assign: (grow: GrowDetail | null) => void) => {
    if (!id) {
      assign(null)
      return
    }

    try {
      assign(await apiFetch<GrowDetail>(`/api/grows/${id}`))
    } catch {
      assign(null)
    }
  }, [])

  useEffect(() => {
    void loadGrow(leftId, setLeftGrow)
  }, [leftId, loadGrow])

  useEffect(() => {
    void loadGrow(rightId, setRightGrow)
  }, [rightId, loadGrow])

  function updateSelection(key: 'leftGrowId' | 'rightGrowId', value: string) {
    const next = new URLSearchParams(searchParams)
    if (value) {
      next.set(key, value)
    } else {
      next.delete(key)
    }

    setSearchParams(next, { replace: true })
  }

  const rows = compareRows(leftGrow, rightGrow)

  return (
    <V1Page eyebrow="Auswertung" title="Vergleich" subtitle="Zwei Grows nebeneinander — was war anders, und was ist dabei herausgekommen.">
      {error && <V1Alert title="Fehler" message={error} tone="warn" />}

      <V1Section title="Grows wählen">
        <div className="v1-form-grid">
          <V1Field label="Grow A">
            <select value={leftId} onChange={(event) => updateSelection('leftGrowId', event.target.value)}>
              <option value="">– Grow wählen –</option>
              {allGrows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
            </select>
          </V1Field>
          <V1Field label="Grow B">
            <select value={rightId} onChange={(event) => updateSelection('rightGrowId', event.target.value)}>
              <option value="">– Grow wählen –</option>
              {allGrows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
            </select>
          </V1Field>
        </div>
      </V1Section>

      {loading ? (
        <V1Skeleton rows={4} label="Lade Grows" />
      ) : !leftGrow && !rightGrow ? (
        <V1Empty title="Noch nichts zu vergleichen" text="Wähle oben zwei Grows aus. Es geht auch mit einem — dann siehst du dessen Kennzahlen allein." />
      ) : (
        <V1Section title="Kennzahlen">
          <div className="cmp-table-wrap">
            <table className="cmp-table">
              <thead>
                <tr>
                  <th scope="col">Kennzahl</th>
                  <th scope="col">{leftGrow?.name ?? '–'}</th>
                  <th scope="col">{rightGrow?.name ?? '–'}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.label} className={classNames(row.differs && 'differs')}>
                    <th scope="row">{row.label}</th>
                    <td>{row.left}</td>
                    <td>{row.right}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {/* Ohne Hervorhebung liest man zehn Zeilen und sucht die Unterschiede
              selbst — die sind aber der einzige Grund, zwei Grows nebeneinander
              zu legen. */}
          <p className="cmp-note">Hervorgehoben ist, worin sich die beiden unterscheiden.</p>
        </V1Section>
      )}
    </V1Page>
  )
}

type CompareRow = { label: string; left: string; right: string; differs: boolean }

function compareRows(left: GrowDetail | null, right: GrowDetail | null): CompareRow[] {
  const raw = [
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

  // Nur markieren, wenn beide Seiten überhaupt einen Wert haben — sonst wäre
  // jede Zeile hervorgehoben, solange erst ein Grow gewählt ist.
  const both = left != null && right != null
  return raw.map((row) => ({
    ...row,
    differs: both && row.left !== row.right && row.left !== '–' && row.right !== '–',
  }))
}

export default AnalysisPage
