import { useMemo, useState } from 'react'
import type { HistoryPoint } from '../../components/SensorChart'

export type HistoryLine = { key: string; label: string; unit: string | null; points: HistoryPoint[] }

/**
 * Mehrere Messwerte über 24 Stunden in einem Bild.
 *
 * Warum eigenes SVG und keine Bibliothek: dieselbe Entscheidung wie beim
 * `SensorChart` daneben — kleineres Bündel, und die Kurven tragen die Farben
 * der App statt der eines fremden Widgets.
 *
 * **Zwei Achsen, weil eine nicht reicht.** Temperatur (25), Luftfeuchte (69),
 * VPD (0,64) und CO₂ (478) auf eine gemeinsame Skala zu legen macht das VPD zu
 * einer flachen Linie am Boden. Jede Linie wird deshalb auf ihren EIGENEN
 * Wertebereich normiert; die Beschriftung nennt links und rechts nur die zwei
 * gröbsten Bereiche als Orientierung. Was zählt, ist der Verlauf und die Zahl
 * unter dem Zeiger — nicht das Ablesen am Raster.
 */
const W = 900
const H = 260
const PAD = { top: 14, right: 46, bottom: 26, left: 46 }

const FARBEN: Record<string, string> = {
  temperature: '#ef4444',
  humidity: '#6366f1',
  vpd: '#22c55e',
  co2: '#ec4899',
  ppfd: '#f59e0b',
  'reservoir-ph': '#0ea5e9',
  'reservoir-ec': '#a855f7',
  'reservoir-temp': '#ef4444',
  orp: '#14b8a6',
  'dissolved-oxygen': '#64748b',
}

const RESERVE = ['#8b5cf6', '#0891b2', '#d97706', '#be123c', '#4d7c0f']

function farbe(key: string, index: number) {
  return FARBEN[key] ?? RESERVE[index % RESERVE.length]
}

export function HistoryChart({ lines, height = 260 }: { lines: HistoryLine[]; height?: number }) {
  const [zeiger, setZeiger] = useState<number | null>(null)

  const daten = useMemo(() => {
    const brauchbar = lines.filter((line) => line.points.length > 1)
    if (brauchbar.length === 0) return null

    const zeiten = brauchbar.flatMap((line) => line.points.map((p) => new Date(p.t).getTime()))
    const von = Math.min(...zeiten)
    const bis = Math.max(...zeiten)
    const spanne = Math.max(1, bis - von)
    const x = (t: number) => PAD.left + ((t - von) / spanne) * (W - PAD.left - PAD.right)

    const kurven = brauchbar.map((line, index) => {
      const werte = line.points.map((p) => p.v)
      const min = Math.min(...werte)
      const max = Math.max(...werte)
      // Eine völlig flache Linie darf nicht durch null geteilt werden — sie
      // gehört in die Mitte, nicht an den Rand.
      const hub = max - min < 1e-9 ? 1 : max - min
      const y = (v: number) => PAD.top + (1 - (v - min) / hub) * (H - PAD.top - PAD.bottom) * 0.92 + (H - PAD.top - PAD.bottom) * 0.04

      const d = line.points
        .map((p, i) => `${i === 0 ? 'M' : 'L'} ${x(new Date(p.t).getTime()).toFixed(1)} ${y(p.v).toFixed(1)}`)
        .join(' ')

      return { ...line, d, min, max, farbe: farbe(line.key, index), x, y }
    })

    return { von, bis, spanne, x, kurven }
  }, [lines])

  if (!daten) {
    return <p className="hc-empty">Noch kein Verlauf — sobald Messwerte ankommen, steht die Kurve hier.</p>
  }

  const zeitpunkt = zeiger != null ? daten.von + (zeiger / 100) * daten.spanne : null

  /** Der Punkt jeder Kurve, der dem Zeiger am nächsten liegt. */
  const amZeiger = zeitpunkt == null ? [] : daten.kurven.map((kurve) => {
    let naechster = kurve.points[0]
    let abstand = Infinity
    for (const punkt of kurve.points) {
      const d = Math.abs(new Date(punkt.t).getTime() - zeitpunkt)
      if (d < abstand) { abstand = d; naechster = punkt }
    }
    return { kurve, punkt: naechster }
  })

  const stunde = (t: number) => new Date(t).toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })

  return (
    <div className="hc" data-audit="history-chart">
      <svg
        viewBox={`0 0 ${W} ${H}`}
        // Nicht einpassen, sondern fuellen: ein Diagramm darf sich in der
        // Breite strecken, sonst bleibt neben der Kurve die halbe Kachel leer.
        preserveAspectRatio="none"
        style={{ width: '100%', height }}
        role="img"
        aria-label={`Verlauf der letzten 24 Stunden: ${daten.kurven.map((k) => k.label).join(', ')}`}
        onMouseMove={(event) => {
          const box = event.currentTarget.getBoundingClientRect()
          const anteil = ((event.clientX - box.left) / box.width) * 100
          const links = (PAD.left / W) * 100
          const rechts = ((W - PAD.right) / W) * 100
          setZeiger(Math.min(100, Math.max(0, ((anteil - links) / (rechts - links)) * 100)))
        }}
        onMouseLeave={() => setZeiger(null)}
      >
        {[0, 0.25, 0.5, 0.75, 1].map((anteil) => {
          const y = PAD.top + anteil * (H - PAD.top - PAD.bottom)
          return <line key={anteil} x1={PAD.left} x2={W - PAD.right} y1={y} y2={y} className="hc-grid" />
        })}

        {[0, 0.5, 1].map((anteil) => (
          <text key={anteil} x={PAD.left + anteil * (W - PAD.left - PAD.right)} y={H - 6} className="hc-axis" textAnchor="middle">
            {stunde(daten.von + anteil * daten.spanne)}
          </text>
        ))}

        {daten.kurven.map((kurve) => (
          <path key={kurve.key} d={kurve.d} fill="none" stroke={kurve.farbe} strokeWidth={1.8} strokeLinejoin="round" strokeLinecap="round" />
        ))}

        {zeiger != null && zeitpunkt != null && (
          <>
            <line
              x1={daten.x(zeitpunkt)} x2={daten.x(zeitpunkt)}
              y1={PAD.top} y2={H - PAD.bottom}
              className="hc-cursor"
            />
            {amZeiger.map(({ kurve, punkt }) => (
              <circle
                key={kurve.key}
                cx={daten.x(new Date(punkt.t).getTime())}
                cy={kurve.y(punkt.v)}
                r={3.5}
                fill={kurve.farbe}
              />
            ))}
          </>
        )}
      </svg>

      {/* Die Werte unter dem Zeiger. Ohne Zeiger der letzte Stand — so ist die
          Legende nie leer und erklaert die Farben von selbst. */}
      <div className="hc-readout">
        <span className="hc-readout-time">
          {zeitpunkt != null ? stunde(zeitpunkt) : `jetzt · ${stunde(daten.bis)}`}
        </span>
        {daten.kurven.map((kurve) => {
          const treffer = amZeiger.find((eintrag) => eintrag.kurve.key === kurve.key)?.punkt
            ?? kurve.points[kurve.points.length - 1]
          return (
            <span key={kurve.key} className="hc-readout-item">
              <span className="hc-dot" style={{ background: kurve.farbe }} />
              {kurve.label}: <strong>{treffer.v.toLocaleString('de-DE', { maximumFractionDigits: 2 })}{kurve.unit ? ` ${kurve.unit}` : ''}</strong>
            </span>
          )
        })}
      </div>
    </div>
  )
}
