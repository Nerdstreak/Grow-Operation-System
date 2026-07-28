import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'
import { V1Field } from '../../components/v1'

type Option = { id: string; name: string; isShipped: boolean }

/**
 * Die Auswahl eines Sollwert-Profils — an zwei Stellen dieselbe.
 *
 * Beim Hydro-System als Vorgabe (DWC oder RDWC ist eine Eigenschaft der
 * Hardware, einmal einstellen), beim Grow als Abweichung. Steht am Grow nichts
 * Eigenes, sagt der Hinweis, was geerbt wird — statt leer zu bleiben und den
 * Nutzer raten zu lassen.
 */
export function ProfileSelect({ value, onChange, inheritedLabel, hint }: {
  value: string | null
  onChange: (value: string | null) => void
  /** Was gälte, wenn hier nichts gewählt ist. */
  inheritedLabel: string
  hint?: string
}) {
  const [options, setOptions] = useState<Option[]>([])

  useEffect(() => {
    const controller = new AbortController()
    async function laden() {
      try {
        const data = await apiFetch<Option[]>('/api/setpoint-profiles', { signal: controller.signal })
        if (!controller.signal.aborted) setOptions(data)
      } catch {
        // Ohne Liste bleibt „geerbt" — das ist der sichere Zustand.
      }
    }
    void laden()
    return () => controller.abort()
  }, [])

  return (
    <V1Field label="Sollwert-Profil" hint={hint}>
      <select
        value={value ?? ''}
        onChange={(event) => onChange(event.target.value === '' ? null : event.target.value)}
        data-audit="setpoint-profile-select"
      >
        <option value="">Geerbt — {inheritedLabel}</option>
        {options.map((option) => (
          <option key={option.id} value={option.id}>
            {option.name}{option.isShipped ? '' : ' (meins)'}
          </option>
        ))}
      </select>
    </V1Field>
  )
}
