import { metricScale, metricStatus, statusLabel, targetLabel, type MetricStatus } from './metric-tile-model'
import { Sparkline, type HistoryPoint } from '../../components/SensorChart'
import { classNames } from '../../utils'

export type MetricTileProps = {
  label: string
  value: number | null
  unit?: string | null
  targetMin?: number | null
  targetMax?: number | null
  /** Weitere Grenze: innerhalb auffällig, ausserhalb kritisch. */
  critical?: { min: number | null; max: number | null }
  /** Nachkommastellen für Wert und Zielbereich — gehört zum Messwert, nicht zur Anzeige. */
  decimals?: number
  /** Ersetzt die Zielzeile, wo es keinen Bereich gibt (Licht: „Aus in 4 h 20 min"). */
  footer?: string
  /** Ersetzt den Zahlenwert, wo der Messwert keiner ist (Licht: „18/6"). */
  display?: string
  /** Zeitpunkt des Werts, falls er nicht mehr frisch ist. */
  stale?: string
  /** Die letzten 24 Stunden. Vorhanden = Kurve statt Zielband. */
  trend?: HistoryPoint[]
  /** Woran ein zurueckgerechnetes Ziel haengt — „bei 46 % RLF". */
  targetNote?: string | null
}

/**
 * Ein Messwert mit seinem Zielbereich.
 *
 * Die Skala darunter ist der eigentliche Punkt: „6,02" sagt nur dann etwas, wenn
 * man den Zielbereich auswendig kennt. Mit Band und Marker sieht man auch, ob der
 * Wert mittig sitzt oder am Rand hängt — und das ist der Unterschied zwischen
 * „passt" und „kippt gleich".
 *
 * Die Kacheln tragen ihre Trennlinien selbst (`border-left`/`border-top` plus
 * negativer Rand), statt dass der Container eine Rasterfarbe durchscheinen lässt.
 * Sonst färbt sich eine leere Rasterzelle wie ein leeres Panel ein, sobald die
 * Kachelzahl nicht zur Spaltenzahl passt.
 */
export function MetricTile({
  label, value, unit, targetMin = null, targetMax = null, critical, decimals, footer, display, stale, trend, targetNote,
}: MetricTileProps) {
  const status: MetricStatus = display != null && targetMin == null && targetMax == null
    ? 'unknown'
    : metricStatus(value, targetMin, targetMax, critical)
  const scale = metricScale(value, targetMin, targetMax)
  const target = footer ?? targetLabel(targetMin, targetMax, unit, decimals)

  const shown = display ?? (value == null || Number.isNaN(value)
    ? '—'
    : (decimals == null ? String(value) : value.toFixed(decimals)).replace('.', ','))

  return (
    <div className={classNames('gos-metric', `is-${status}`)} data-audit={`metric-${label.toLowerCase()}`}>
      <div className="gos-metric-head">
        <span className="gos-metric-label">{label}</span>
        {status !== 'unknown' && <span className="gos-metric-status">{statusLabel(status)}</span>}
      </div>

      <div className="gos-metric-value">
        {shown}
        {unit && display == null && <span className="unit">{unit}</span>}
      </div>

      {trend && trend.length > 1 ? (
        // Die Kurve ERSETZT das Zielband, sie kommt nicht dazu: sonst wächst jede
        // Kachel und die Seite mit ihr. Der Zielbereich steht als Text darunter
        // weiter da, und die Farbe kommt vom Status der Kachel.
        <div className="gos-metric-spark" aria-hidden="true">
          <Sparkline points={trend} height={22} />
        </div>
      ) : scale ? (
        <div className="gos-metric-scale" aria-hidden="true">
          <span className="band" style={{ left: `${scale.bandLeft}%`, width: `${scale.bandWidth}%` }} />
          <span className={classNames('mark', scale.clamped && 'clamped')} style={{ left: `${scale.marker}%` }} />
        </div>
      ) : (
        // Ohne Skala bleibt die Höhe trotzdem stehen, sonst stehen Kacheln mit
        // und ohne Zielbereich unterschiedlich hoch nebeneinander.
        <div className="gos-metric-scale is-empty" aria-hidden="true" />
      )}

      {/* Der Zusatz nennt, woran ein zurueckgerechnetes Ziel haengt. „Ziel
          15,8–19,6 °C" allein liest sich als „kuehl runter", obwohl in
          Wahrheit die Feuchte zu niedrig ist. */}
      {target && <div className="gos-metric-target">{target}{targetNote ? ` · ${targetNote}` : ''}</div>}
      {stale && <div className="gos-metric-stale">{stale}</div>}
    </div>
  )
}

/** Die Reihe, in der die Kacheln liegen — umbrechend, mit Hairline-Raster. */
export function MetricRow({ title, children }: { title?: string; children: React.ReactNode }) {
  return (
    <div className="gos-metric-block">
      {title && (
        <div className="gos-metric-title">
          <span>{title}</span>
          <span className="rule" />
        </div>
      )}
      <div className="gos-metric-row">{children}</div>
    </div>
  )
}
