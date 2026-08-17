import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../../api'
import './symptom-photos.css'

type SymptomPhoto = {
  photoId: number
  growId: number
  growName: string
  relativePath: string
  caption: string | null
  tag: string
  takenAtUtc: string
}

/**
 * Die eigenen Aufnahmen zu einem Symptom.
 *
 * <b>Warum ausschließlich eigene Bilder.</b> Zu den Symptomen und Erregern der
 * Wissensbasis gab es nie ein Bild, und fremde Beispielbilder sind nicht zu
 * haben, ohne fremde Rechte zu verletzen. Der eigene Bestand ist ohnehin der
 * bessere Vergleich: gleiches Licht, gleiche Kamera, gleiche Anlage. Beim
 * dritten Mal Wurzelfäule sieht man, wie die ersten beiden aussahen.
 *
 * Gibt es keine Bilder, zeigt die Komponente nichts — außer einem kurzen Hinweis,
 * wie man welche dazutut. Ein leerer Bereich, der sich aufklappen lässt, wäre
 * schlimmer als keiner.
 */
export function SymptomPhotos({ symptomId, symptomName }: { symptomId: string; symptomName?: string }) {
  const [photos, setPhotos] = useState<SymptomPhoto[] | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    apiFetch<SymptomPhoto[]>(`/api/knowledge/symptoms/${encodeURIComponent(symptomId)}/photos`, { signal: controller.signal })
      .then((geladen) => { if (!controller.signal.aborted) setPhotos(geladen) })
      .catch(() => { if (!controller.signal.aborted) setPhotos([]) })
    return () => controller.abort()
  }, [symptomId])

  if (photos === null) return null

  if (photos.length === 0) {
    return (
      <p className="sp-empty">
        Noch kein eigenes Bild dazu. Wenn du das nächste Mal {symptomName ? `„${symptomName}"` : 'so etwas'} vor
        dir hast: fotografieren, im <Link to="/journal">Journal</Link> dem Symptom zuordnen — dann steht es hier
        beim nächsten Mal.
      </p>
    )
  }

  return (
    <div className="sp-wrap" data-audit="symptom-photos">
      <div className="sp-head">
        <span>Deine Aufnahmen · {photos.length}</span>
        <small>aus deinen eigenen Läufen</small>
      </div>
      <div className="sp-grid">
        {photos.map((foto) => (
          <figure key={foto.photoId} className="sp-item">
            <img src={foto.relativePath} alt={foto.caption ?? `${symptomName ?? symptomId} in ${foto.growName}`} loading="lazy" />
            <figcaption>
              <Link to={`/grows/${foto.growId}`}>{foto.growName}</Link>
              {' · '}
              {new Date(foto.takenAtUtc).toLocaleDateString('de-DE', { day: '2-digit', month: '2-digit', year: '2-digit' })}
              {foto.caption && <span className="sp-caption">{foto.caption}</span>}
            </figcaption>
          </figure>
        ))}
      </div>
    </div>
  )
}
