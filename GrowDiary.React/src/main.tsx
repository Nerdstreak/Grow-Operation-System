import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
import '@fontsource/jetbrains-mono/400.css'
import '@fontsource/jetbrains-mono/500.css'
import '@fontsource/jetbrains-mono/600.css'
import './index.css'
import App from './App'
import { ROUTER_BASENAME } from './base'

function updateAppViewportHeight() {
  const height = window.visualViewport?.height ?? window.innerHeight
  document.documentElement.style.setProperty('--app-viewport-height', `${Math.round(height)}px`)
}

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
