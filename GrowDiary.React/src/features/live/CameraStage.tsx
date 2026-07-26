import { useState } from 'react'
import { DashboardCameraTile } from './DashboardCameraTile'
import { classNames } from '../../utils'

export type StageCamera = { entityId: string; label: string }

/**
 * Mehrere Kameras: eine große Bühne, darunter eine Reihe zum Umschalten.
 *
 * Drei gleich große Karten nebeneinander bedeuten drei kleine Bilder — und bei
 * einer Kamera schaut man auf Details: hängen die Blätter, steht Wasser, sieht
 * das Blatt anders aus als gestern. Also eine groß, die anderen als Streifen.
 *
 * Nur wenn wirklich mehr als eine da ist. Bei einer einzelnen wäre eine
 * Thumbnail-Zeile mit genau einem Eintrag nur Beiwerk.
 */
export function CameraStage({ tentId, cameras }: { tentId: number; cameras: StageCamera[] }) {
  const [active, setActive] = useState(0)
  const current = cameras[Math.min(active, cameras.length - 1)]
  if (!current) return null

  if (cameras.length === 1) {
    return <DashboardCameraTile tentId={tentId} entityId={current.entityId} label={current.label} />
  }

  return (
    <div className="cam-stage" data-audit="camera-stage">
      <DashboardCameraTile key={current.entityId} tentId={tentId} entityId={current.entityId} label={current.label} />

      <div className="cam-strip" role="tablist" aria-label="Kamera wählen">
        {cameras.map((camera, index) => (
          <button
            key={camera.entityId}
            type="button"
            role="tab"
            aria-selected={index === active}
            className={classNames('cam-strip-item', index === active && 'active')}
            onClick={() => setActive(index)}
          >
            {/* Bewusst ohne Vorschaubild: der Kamera-Proxy kennt keine kleine
                Größe, ein Miniaturbild wäre also ein zweites Vollbild — bei drei
                Kameras vier Vollbilder je Aktualisierung auf einem Pi. Sobald der
                Proxy skaliert liefert, gehört das Bild hierher. */}
            <span className="cam-strip-index">{index + 1}</span>
            <span className="cam-strip-label">{camera.label}</span>
          </button>
        ))}
      </div>
    </div>
  )
}
