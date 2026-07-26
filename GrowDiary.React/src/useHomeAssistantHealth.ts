import { useEffect, useState } from 'react'
import { apiFetch } from './api'

export type HomeAssistantHealth = {
  configured: boolean
  reachable: boolean
  retryAtUtc: string | null
}

/**
 * Ob Home Assistant antwortet — einmal für die ganze App.
 *
 * Der Entwurf verlangt bei einem Ausfall genau eine Meldung unter der
 * Kontextleiste statt leerer Felder auf jeder Karte. Das ist auch fachlich die
 * richtige Aussage: ein RDWC läuft weiter, wenn Home Assistant aussetzt — die
 * Werte sind dann alt, nicht weg. Und Messen und Addback funktionieren ohnehin
 * ohne HA.
 *
 * Alle 60 Sekunden, weil der Ausfall selbst nicht dringend ist: die Meldung soll
 * erscheinen und wieder verschwinden, ohne dass jemand neu lädt.
 */
export function useHomeAssistantHealth(): HomeAssistantHealth | null {
  const [health, setHealth] = useState<HomeAssistantHealth | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function check() {
      try {
        const next = await apiFetch<HomeAssistantHealth>('/api/home-assistant/health', { signal: controller.signal })
        if (!controller.signal.aborted) setHealth(next)
      } catch {
        // Antwortet nicht mal die eigene API, ist das kein HA-Problem — dann
        // schweigt das Banner, statt die falsche Ursache zu nennen.
      }
    }

    void check()
    const timer = window.setInterval(() => void check(), 60_000)
    return () => {
      controller.abort()
      window.clearInterval(timer)
    }
  }, [])

  return health
}
