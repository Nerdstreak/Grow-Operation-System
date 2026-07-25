import { useEffect, useState } from 'react'
import { resolveUrl } from '../../base'
import { cameraTileLabel } from './dashboard-tile-model'

/**
 * A camera as a dashboard tile: the snapshot endpoint returns a still image, so the tile
 * re-requests it on a timer rather than holding a stream open. Several tiles can point at
 * different cameras, which is the point — a tent with three of them shows all three.
 */
export function DashboardCameraTile({
  tentId,
  entityId,
  label,
  refreshSeconds = 30,
}: {
  tentId: number
  entityId: string
  label: string | null
  refreshSeconds?: number
}) {
  const [stamp, setStamp] = useState(() => Date.now())
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    const timer = window.setInterval(() => setStamp(Date.now()), refreshSeconds * 1000)
    return () => window.clearInterval(timer)
  }, [refreshSeconds])

  const caption = label ?? cameraTileLabel(entityId)
  const src = resolveUrl(`/api/live/tents/${tentId}/camera?entity=${encodeURIComponent(entityId)}&t=${stamp}`)

  return (
    <figure className="ix-cam-tile" data-audit={`cam-tile-${entityId}`}>
      {failed ? (
        <div className="ix-cam-tile-fallback">Kein Bild — in Home Assistant erreichbar?</div>
      ) : (
        <img
          src={src}
          alt={caption}
          loading="lazy"
          onError={() => setFailed(true)}
          onLoad={() => setFailed(false)}
        />
      )}
      <figcaption>{caption}</figcaption>
    </figure>
  )
}
