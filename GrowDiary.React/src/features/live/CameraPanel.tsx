import { useEffect, useRef, useState } from 'react'
import { resolveUrl } from '../../base'
import type { TentDto } from '../../types'
import { classNames } from '../../utils'

/**
 * Die Kamera als Bühne, wie im Entwurf: Kopfzeile mit Entity und Alter des
 * Bildes, darunter das Bild in voller Breite.
 *
 * Mehrere Kameras liegen als Umschaltleiste darunter — drei gleich große Karten
 * wären drei kleine Bilder, und bei einer Kamera schaut man auf Details.
 *
 * Das zuletzt gültige Bild bleibt stehen, wenn ein Abruf scheitert. Der Server
 * hält es ohnehin vor; eine leere Fläche wäre die schlechtere Auskunft, weil das
 * Zelt ja weiterläuft.
 */
export function CameraPanel({ tent, onReload }: { tent: TentDto | null; onReload?: () => void }) {
  const cameras = tent?.cameras?.length ? tent.cameras : (tent?.cameraEntityId ? [tent.cameraEntityId] : [])
  const [active, setActive] = useState(0)
  const [src, setSrc] = useState<string | null>(null)
  const [meta, setMeta] = useState<{ capturedAt: string | null; live: boolean } | null>(null)
  const [failed, setFailed] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)
  const urlRef = useRef<string | null>(null)
  const current = cameras[Math.min(active, cameras.length - 1)]

  useEffect(() => {
    if (!tent || !current) return
    let alive = true
    let timer: number | undefined

    async function loop() {
      try {
        const response = await fetch(resolveUrl(`/api/live/tents/${tent!.id}/camera?entity=${encodeURIComponent(current!)}&t=${Date.now()}`))
        if (!alive) return
        if (response.ok) {
          const blob = await response.blob()
          if (!alive) return
          const next = URL.createObjectURL(blob)
          if (urlRef.current) URL.revokeObjectURL(urlRef.current)
          urlRef.current = next
          setSrc(next)
          setMeta({ capturedAt: response.headers.get('X-Camera-Captured-At'), live: response.headers.get('X-Camera-Live') !== 'false' })
          setFailed(false)
        } else if (!urlRef.current) {
          setFailed(true)
        }
      } catch {
        if (alive && !urlRef.current) setFailed(true)
      } finally {
        // Der nächste Abruf startet erst, wenn der vorige durch ist — eine
        // langsame Kamera wird dadurch seltener aktualisiert statt abgebrochen.
        if (alive) timer = window.setTimeout(loop, 1000)
      }
    }

    void loop()
    return () => { alive = false; if (timer !== undefined) window.clearTimeout(timer) }
  }, [tent, current, reloadKey])

  useEffect(() => () => { if (urlRef.current) URL.revokeObjectURL(urlRef.current) }, [])

  return (
    <article className="ls-panel ls-cam" data-audit="live-camera">
      <div className="ls-panel-head">
        <span className="ls-label">Kamera</span>
        <span className="ls-panel-meta">
          {current ? `${current}${meta?.capturedAt ? ` · ${ageLabel(meta.capturedAt)}` : ''}` : 'keine gemappt'}
        </span>
        {current && (
          <button
            type="button"
            className="ls-btn is-small"
            onClick={() => { setReloadKey((key) => key + 1); onReload?.() }}
          >
            Neu laden
          </button>
        )}
      </div>

      <div className="ls-cam-stage">
        {src ? (
          <img src={src} alt={`Kamerabild ${current}`} />
        ) : (
          <div className="ls-cam-empty">
            {cameras.length === 0
              ? 'Keine Kamera zugeordnet — im Home-Assistant-Setup verbinden.'
              : failed ? 'Kein Bild — in Home Assistant erreichbar?' : 'Lädt …'}
          </div>
        )}
        {meta && !meta.live && src && <span className="ls-cam-stale">veraltet</span>}
      </div>

      {cameras.length > 1 && (
        <div className="ls-cam-strip" role="tablist" aria-label="Kamera wählen">
          {cameras.map((camera, index) => (
            <button
              key={camera}
              type="button"
              role="tab"
              aria-selected={index === active}
              className={classNames('ls-cam-tab', index === active && 'active')}
              onClick={() => setActive(index)}
            >
              <span className="n">{index + 1}</span>
              {shortName(camera, index)}
            </button>
          ))}
        </div>
      )}
    </article>
  )
}

/**
 * „camera.hauptzelt" → „Hauptzelt", „camera.pflanze_2" → „Pflanze 2".
 *
 * Erst nahm ich nur das letzte Wort — bei `camera.pflanze_2` blieb davon „2"
 * uebrig, und im Streifen standen Ziffern statt Namen. Der ganze Rest hinter dem
 * Praefix ist der Name; er ist kurz genug.
 */
function shortName(entityId: string, index: number): string {
  const raw = entityId.replace(/^(camera|image)\./i, '').replace(/[_-]+/g, ' ').trim()
  if (!raw) return `Kamera ${index + 1}`
  // Jedes Wort gross anfangen: „pflanze 2“ ist ein Bezeichner, „Pflanze 2“ ein Name.
  return raw
    .split(' ')
    .map((wort) => (wort ? wort.charAt(0).toUpperCase() + wort.slice(1) : wort))
    .join(' ')
}

function ageLabel(iso: string): string {
  const captured = new Date(iso).getTime()
  if (Number.isNaN(captured)) return ''
  const seconds = Math.max(0, Math.round((Date.now() - captured) / 1000))
  if (seconds < 90) return `vor ${seconds} s`
  const minutes = Math.round(seconds / 60)
  return minutes < 90 ? `vor ${minutes} min` : `vor ${Math.round(minutes / 60)} h`
}
