import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
// Archivo is what the design tokens ask for. Without it the app fell back to
// system-ui and the whole redesign looked subtly wrong for no visible reason.
import '@fontsource/archivo/400.css'
import '@fontsource/archivo/500.css'
import '@fontsource/archivo/600.css'
import '@fontsource/archivo/700.css'
import '@fontsource/jetbrains-mono/400.css'
import '@fontsource/jetbrains-mono/500.css'
import '@fontsource/jetbrains-mono/600.css'
import { initTheme } from './theme'
import './index.css'
import App from './App'
import { ROUTER_BASENAME } from './base'

function updateAppViewportHeight() {
  const height = window.visualViewport?.height ?? window.innerHeight
  document.documentElement.style.setProperty('--app-viewport-height', `${Math.round(height)}px`)
}

// Before the first render, so the app never flashes dark then light.
initTheme()

updateAppViewportHeight()
window.addEventListener('load', updateAppViewportHeight, { passive: true })
window.addEventListener('resize', updateAppViewportHeight, { passive: true })
window.addEventListener('orientationchange', updateAppViewportHeight, { passive: true })
window.visualViewport?.addEventListener('resize', updateAppViewportHeight, { passive: true })
window.visualViewport?.addEventListener('scroll', updateAppViewportHeight, { passive: true })

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter basename={ROUTER_BASENAME}>
      <App />
    </BrowserRouter>
  </StrictMode>,
)
