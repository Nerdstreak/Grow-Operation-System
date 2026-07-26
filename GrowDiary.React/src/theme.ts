/* src/theme.ts — Theme-Umschalter. Das Light-CSS existierte schon,
   es fehlte nur die Umschaltung. */
export type Theme = 'dark' | 'light'

const KEY = 'growos.theme'

export function systemTheme(): Theme {
  return window.matchMedia?.('(prefers-color-scheme: light)').matches ? 'light' : 'dark'
}

export function storedTheme(): Theme | null {
  try {
    const value = localStorage.getItem(KEY)
    return value === 'dark' || value === 'light' ? value : null
  } catch {
    return null
  }
}

export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme
  try { localStorage.setItem(KEY, theme) } catch { /* Privatmodus */ }
}

/** Vor dem ersten Render aufrufen (main.tsx), damit es nicht flackert. */
export function initTheme(): Theme {
  const theme = storedTheme() ?? systemTheme()
  document.documentElement.dataset.theme = theme
  return theme
}
