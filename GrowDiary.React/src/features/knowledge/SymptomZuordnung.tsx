import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'
import './symptom-photos.css'

type SymptomOption = { key: string; title: string }

/**
 * Der Auswahlkasten unter einem Foto: „Das zeigt …".
 *
 * <b>Wozu.</b> Zu den Symptomen der Wissensbasis gab es nie ein Bild. Fremde
 * Beispielbilder sind urheberrechtlich nicht zu haben — die eigene Aufnahme ist
 * ohnehin der bessere Vergleich, sie stammt aus demselben Zelt bei demselben
 * Licht. Ein Klick hier, und beim nächsten Verdacht steht das Bild im Wissen.
 *
 * Ohne diese Stelle bliebe das Feld tot: gespeichert, aber nie gefüllt. Genau
 * so liegt <c>IsReferenceShot</c> seit Jahren im Bestand — angekreuzt, aber
 * nirgends ausgewertet.
 */
export function SymptomZuordnung({ photoId, current, onSaved }: {
  photoId: number
  current?: string | null
  onSaved?: (symptomId: string | null) => void
}) {
  const [optionen, setOptionen] = useState<SymptomOption[]>([])
  const [wert, setWert] = useState(current ?? '')
  const [status, setStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  useEffect(() => {
    const controller = new AbortController()
    apiFetch<Array<Record<string, unknown>>>('/api/knowledge/symptoms', { signal: controller.signal })
      .then((liste) => {
        if (controller.signal.aborted) return
        setOptionen(liste
          .map((eintrag) => ({
            key: String(eintrag.id ?? eintrag.key ?? ''),
            title: String(eintrag.name ?? eintrag.title ?? eintrag.id ?? ''),
          }))
          .filter((o) => o.key !== '')
          .sort((a, b) => a.title.localeCompare(b.title)))
      })
      .catch(() => { /* Ohne Liste keine Zuordnung — der Rest des Journals bleibt heil. */ })
    return () => controller.abort()
  }, [])

  async function speichern(neu: string) {
    setWert(neu)
    setStatus('saving')
    try {
      await apiFetch(`/api/photos/${photoId}/symptom`, {
        method: 'PATCH',
        body: JSON.stringify({ symptomId: neu || null }),
      })
      setStatus('saved')
      onSaved?.(neu || null)
    } catch {
      setStatus('error')
    }
  }

  if (optionen.length === 0) return null

  return (
    <div className="sp-assign">
      <span>Zeigt</span>
      <select
        value={wert}
        onChange={(event) => void speichern(event.target.value)}
        aria-label={`Symptom auf Foto ${photoId}`}
      >
        <option value="">— nichts Bestimmtes —</option>
        {optionen.map((o) => <option key={o.key} value={o.key}>{o.title}</option>)}
      </select>
      {status === 'saving' && <span>speichert …</span>}
      {status === 'saved' && <span>steht jetzt im Wissen</span>}
      {status === 'error' && <span>nicht gespeichert</span>}
    </div>
  )
}
