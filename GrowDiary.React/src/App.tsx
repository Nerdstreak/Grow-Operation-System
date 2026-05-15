import { useState } from 'react'
import { NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import AddbackHubPage from './pages/AddbackHubPage'
import AddbackPage from './pages/AddbackPage'
import AnalysisPage from './pages/AnalysisPage'
import ArchivePage from './pages/ArchivePage'
import GrowDetailPage from './pages/GrowDetailPage'
import GrowSetupPage from './pages/GrowSetupPage'
import HardwarePage from './pages/HardwarePage'
import HomeAssistantPage from './pages/HomeAssistantPage'
import HydroPage from './pages/HydroPage'
import HarvestPage from './pages/HarvestPage'
import KnowledgePage from './pages/KnowledgePage'
import LiveDashboardPage from './pages/LiveDashboardPage'
import MeasurementEditPage from './pages/MeasurementEditPage'
import MobileActionPage from './pages/MobileActionPage'
import SettingsPage from './pages/SettingsPage'
import TentDetailPage from './pages/TentDetailPage'
import TentsPage from './pages/TentsPage'

const primaryNav = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/addback', label: 'Addback', end: true },
  { to: '/zelte', label: 'Zelte', end: false },
  { to: '/hydro', label: 'Hydro', end: true },
  { to: '/home-assistant', label: 'Home Assistant', end: true },
]

const secondaryNav = [
  { to: '/action', label: 'Aktion', end: true },
  { to: '/hardware', label: 'Hardware', end: true },
  { to: '/grows/new', label: 'Grow starten', end: true },
  { to: '/wissen', label: 'Wissen', end: true },
  { to: '/archiv', label: 'Archiv', end: true },
  { to: '/analyse', label: 'Analyse', end: true },
  { to: '/settings', label: 'Einstellungen', end: true },
]

const bottomNav = [
  { to: '/', label: 'Live', end: true },
  { to: '/addback', label: 'Addback', end: true },
  { to: '/zelte', label: 'Zelte', end: false },
  { to: '/hydro', label: 'Hydro', end: true },
]

const mobileMoreItems = [
  { to: '/action', label: 'Aktion', end: true },
  { to: '/home-assistant', label: 'Home Assistant', end: true },
  { to: '/hardware', label: 'Hardware', end: true },
  { to: '/grows/new', label: 'Grow starten', end: true },
  { to: '/wissen', label: 'Wissen', end: true },
  { to: '/archiv', label: 'Archiv', end: true },
  { to: '/analyse', label: 'Analyse', end: true },
  { to: '/settings', label: 'Einstellungen', end: true },
]

function App() {
  const location = useLocation()
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const currentTitle = getCurrentTitle(location.pathname)

  return (
    <div className="app app-v2">
      <aside className="sidebar shell-sidebar">
        <div className="shell-brand">
          <div className="logo-mark" aria-hidden="true">
            <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
              <path d="M7 1C7 1 2 4 2 8a5 5 0 0010 0c0-4-5-7-5-7z" fill="currentColor" opacity="0.9" />
            </svg>
          </div>
          <div>
            <div className="logo-name">Grow OS</div>
            <div className="logo-sub">Local Grow Control</div>
          </div>
        </div>

        <nav className="sidebar-nav shell-nav" aria-label="Hauptnavigation">
          <div className="nav-section">Core</div>
          {primaryNav.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => (isActive ? 'nav-item active' : 'nav-item')}>
              {item.label}
            </NavLink>
          ))}
          <div className="nav-section">Mehr</div>
          {secondaryNav.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => (isActive ? 'nav-item active' : 'nav-item')}>
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div className="sidebar-footer">
          <span className="logo-sub">Self-hosted · Lokal · HA</span>
        </div>
      </aside>

      <header className="mobile-shell compact-mobile-shell" aria-label="Mobile Navigation">
        <div className="mobile-shell-bar">
          <div className="mobile-brand">
            <div className="logo-mark" aria-hidden="true">
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                <path d="M7 1C7 1 2 4 2 8a5 5 0 0010 0c0-4-5-7-5-7z" fill="currentColor" opacity="0.9" />
              </svg>
            </div>
            <div>
              <div className="logo-name">Grow OS</div>
              <div className="mobile-current-page">{currentTitle}</div>
            </div>
          </div>
          <button type="button" className="mobile-menu-button" aria-expanded={mobileMenuOpen} aria-controls="mobile-more-navigation" onClick={() => setMobileMenuOpen((current) => !current)}>
            Menü
          </button>
        </div>

        {mobileMenuOpen && (
          <nav id="mobile-more-navigation" className="mobile-more-nav" aria-label="Weitere Navigation">
            {mobileMoreItems.map((item) => (
              <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => (isActive ? 'mobile-more-item active' : 'mobile-more-item')} onClick={() => setMobileMenuOpen(false)}>
                {item.label}
              </NavLink>
            ))}
          </nav>
        )}
      </header>

      <div className="content shell-content">
        <Routes>
          <Route path="/" element={<LiveDashboardPage />} />
          <Route path="/live" element={<Navigate to="/" replace />} />
          <Route path="/addback" element={<AddbackHubPage />} />
          <Route path="/action" element={<MobileActionPage />} />
          <Route path="/grows" element={<Navigate to="/" replace />} />
          <Route path="/grows/new" element={<GrowSetupPage />} />
          <Route path="/grows/:growId" element={<GrowDetailPage />} />
          <Route path="/grows/measurements/:measurementId/edit" element={<MeasurementEditPage />} />
          <Route path="/grows/:growId/addback" element={<AddbackPage />} />
          <Route path="/grows/:growId/harvest" element={<HarvestPage />} />
          <Route path="/grows/:growId/setup" element={<GrowSetupPage />} />
          <Route path="/zelte" element={<TentsPage />} />
          <Route path="/zelte/:tentId" element={<TentDetailPage />} />
          <Route path="/hydro" element={<HydroPage />} />
          <Route path="/home-assistant" element={<HomeAssistantPage />} />
          <Route path="/hardware" element={<HardwarePage />} />
          <Route path="/archiv" element={<ArchivePage />} />
          <Route path="/wissen" element={<KnowledgePage />} />
          <Route path="/analyse" element={<AnalysisPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/einstellungen" element={<Navigate to="/settings" replace />} />
        </Routes>
      </div>

      <nav className="mobile-bottom-nav" aria-label="Mobile Hauptnavigation">
        {bottomNav.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => (isActive ? 'bottom-nav-item active' : 'bottom-nav-item')} onClick={() => setMobileMenuOpen(false)}>
            {item.label}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}

function getCurrentTitle(pathname: string): string {
  if (pathname === '/' || pathname.startsWith('/live')) return 'Dashboard'
  if (pathname.startsWith('/addback') || pathname.includes('/addback')) return 'Addback'
  if (pathname.startsWith('/action')) return 'Aktion'
  if (pathname.startsWith('/zelte')) return 'Zelte'
  if (pathname.startsWith('/hydro')) return 'Hydro'
  if (pathname.startsWith('/home-assistant')) return 'Home Assistant'
  if (pathname.startsWith('/hardware')) return 'Hardware'
  if (pathname.startsWith('/grows/new')) return 'Grow starten'
  if (pathname.startsWith('/grows')) return 'Grow'
  if (pathname.startsWith('/wissen')) return 'Wissen'
  if (pathname.startsWith('/analyse')) return 'Analyse'
  if (pathname.startsWith('/settings') || pathname.startsWith('/einstellungen')) return 'Einstellungen'
  if (pathname.startsWith('/archiv')) return 'Archiv'
  return 'Grow OS'
}

export default App
