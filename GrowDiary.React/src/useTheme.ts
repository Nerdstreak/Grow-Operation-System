/* src/useTheme.ts */
import { useCallback, useEffect, useState } from 'react'
import { applyTheme, initTheme, type Theme } from './theme'

/**
 * Theme-Zustand für Komponenten.
 *
 * Der Umschalter sitzt zweimal in der App (Seitenleiste und Einstellungen).
 * Zwei unabhängige useState-Instanzen liefen auseinander: der eine Knopf
 * schaltete, der andere zeigte weiter den alten Zustand. Deshalb meldet
 * jeder Wechsel ein Event, auf das alle Instanzen hören.
 */
const THEME_EVENT = 'growos-theme-changed'

export function useTheme(): { theme: Theme; toggle: () => void } {
  const [theme, setTheme] = useState<Theme>(() => initTheme())

  useEffect(() => {
    const onChange = (event: Event) => {
      const next = (event as CustomEvent<Theme>).detail
      if (next === 'dark' || next === 'light') setTheme(next)
    }
    window.addEventListener(THEME_EVENT, onChange)
    return () => window.removeEventListener(THEME_EVENT, onChange)
  }, [])

  const toggle = useCallback(() => {
    setTheme((current) => {
      const next: Theme = current === 'dark' ? 'light' : 'dark'
      applyTheme(next)
      window.dispatchEvent(new CustomEvent<Theme>(THEME_EVENT, { detail: next }))
      return next
    })
  }, [])

  return { theme, toggle }
}
