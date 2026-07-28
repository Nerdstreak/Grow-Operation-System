import { useMemo } from 'react'
import { buildMatrix, toPathData, totalSize } from './qr'

/**
 * Der QR-Code als SVG.
 *
 * Gezeichnet statt als Bild geladen: so skaliert er verlustfrei und nimmt die
 * Farben der App an. Die dunklen Module bekommen bewusst KEINE Themenfarbe,
 * sondern den Vordergrund — ein Scanner braucht Kontrast, kein Design.
 * Der helle Grund wird mitgezeichnet, damit der Code im dunklen Modus nicht auf
 * schwarzem Untergrund steht: dunkel auf dunkel liest kein Handy.
 */
export function QrCode({ value, label, moduleSize = 6 }: { value: string; label: string; moduleSize?: number }) {
  const { path, size } = useMemo(() => {
    const matrix = buildMatrix(value)
    return { path: toPathData(matrix), size: totalSize(matrix) }
  }, [value])

  return (
    <svg
      className="mo-qr-svg"
      viewBox={`0 0 ${size} ${size}`}
      width={size * moduleSize}
      height={size * moduleSize}
      role="img"
      aria-label={label}
      shapeRendering="crispEdges">
      <rect x="0" y="0" width={size} height={size} fill="#ffffff" />
      <path d={path} fill="#000000" />
    </svg>
  )
}
