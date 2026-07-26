/* src/useTheme.ts */
import { useCallback, useState } from 'react'
import { applyTheme, initTheme, type Theme } from './theme'

export function useTheme(): { theme: Theme; toggle: () => void } {
  const [theme, setTheme] = useState<Theme>(() => initTheme())
  const toggle = useCallback(() => {
    setTheme((current) => {
      const next: Theme = current === 'dark' ? 'light' : 'dark'
      applyTheme(next)
      return next
    })
  }, [])
  return { theme, toggle }
}
