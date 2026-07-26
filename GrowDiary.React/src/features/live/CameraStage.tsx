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
            {/* Das Vorschaubild ist derselbe Proxy-Aufruf — der Server liefert
                den zuletzt gültigen Frame, auch wenn die Kamera gerade hustet. */}
            <img
              src={`/api/live/tents/${tentId}/camera?entity=${encodeURIComponent(camera.entityId)}&thumb=1`}
              alt=""
              loading="lazy"
              onError={(event) => { (event.currentTarget as HTMLImageElement).style.visibility = 'hidden' }}
            />
            <span>{camera.label}</span>
          </button>
        ))}
      </div>
    </div>
  )
}
