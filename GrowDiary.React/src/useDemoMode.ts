import { useEffect, useState } from 'react'
import { apiFetch } from './api'

/**
 * Läuft dieser Server auf erfundenen Werten?
 *
 * Wird einmal beim Start gefragt und danach nicht mehr: der Testdatenmodus
 * hängt an einer Umgebungsvariablen und kann sich im Betrieb nicht ändern.
 *
 * Die Antwort trägt einen Streifen quer über die App. Erfundene Messwerte, die
 * jemand für echte hält, wären nicht bloß falsch — an ihnen hängen Alarme und
 * die Dosierung.
 */
export function useDemoMode(): boolean {
  const [enabled, setEnabled] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    async function check() {
      try {
        const result = await apiFetch<{ enabled: boolean }>('/api/system/demo-mode', { signal: controller.signal })
        if (!controller.signal.aborted) setEnabled(Boolean(result.enabled))
      } catch {
        // Kein Streifen, wenn die Frage nicht beantwortet wird — im Zweifel
        // lieber nichts behaupten als „Testdaten" über echte Werte schreiben.
      }
    }
    void check()
    return () => controller.abort()
  }, [])

  return enabled
}
