import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'
import type { HistoryPoint, TentHistory } from '../../components/SensorChart'

// The metrics the live tiles can show a curve for.
const SPARK_METRICS = [
  'temperature', 'humidity', 'vpd', 'co2', 'ppfd',
  'reservoir-ph', 'reservoir-ec', 'reservoir-temp', 'reservoir-level', 'orp', 'dissolved-oxygen',
].join(',')

/**
 * Last 24 hours per metric for the live tiles, in one request. Refreshed sparingly — the
 * tiles' own values update every 30 s, the trend behind them changes far slower.
 */
export function useTentSparklines(tentId: number | null): Map<string, HistoryPoint[]> {
  const [byMetric, setByMetric] = useState<Map<string, HistoryPoint[]>>(new Map())

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      if (tentId == null) {
        setByMetric(new Map())
        return
      }
      try {
        const history = await apiFetch<TentHistory>(
          `/api/tents/${tentId}/history?metrics=${SPARK_METRICS}&days=1&resolution=raw`,
          { signal: controller.signal },
        )
        if (controller.signal.aborted) return
        setByMetric(new Map(history.series.filter((s) => s.points.length > 1).map((s) => [s.metricKey, s.points])))
      } catch {
        // History is a nice-to-have on this screen — leave the tiles as they are.
      }
    }

    void load()
    const timer = window.setInterval(() => void load(), 5 * 60 * 1000)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [tentId])

  return byMetric
}
