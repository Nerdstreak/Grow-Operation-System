import { useState } from 'react'
import { NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import AddbackPage from './pages/AddbackPage'
import AnalysisPage from './pages/AnalysisPage'
import ArchivePage from './pages/ArchivePage'
import GrowDetailPage from './pages/GrowDetailPage'
import GrowSetupPage from './pages/GrowSetupPage'
import HardwarePage from './pages/HardwarePage'
import HarvestPage from './pages/HarvestPage'
import KnowledgePage from './pages/KnowledgePage'
import LiveDashboardPage from './pages/LiveDashboardPage'
import MeasurementEditPage from './pages/MeasurementEditPage'
import MobileActionPage from './pages/MobileActionPage'
import SettingsPage from './pages/SettingsPage'
import TentDetailPage from './pages/TentDetailPage'
import TentsPage from './pages/TentsPage'

const navItems = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/action', label: 'Aktion', end: true },
  { to: '/live', label: 'Live', end: true },
  { to: '/zelte', label: 'Zelte & Systeme', end: true },
  { to: '/hardware', label: 'Hardware', end: true },
  { to: '/archiv', label: 'Archiv', end: true },
  { to: '/grows/new', label: 'Neuer Grow', end: true },
  { to: '/wissen', label: 'Wissen', end: true },
  { to: '/analyse', label: 'Analyse', end: true },
  { to: '/settings', label: 'Einstellungen', end: true },
]

const mobilePrimaryItems = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/action', label: 'Aktion', end: true },
  { to: '/zelte', label: 'Zelte', end: true },
  { to: '/grows/new', label: 'Grow', end: true },
]

const mobileMoreItems = [
  { to: '/live', label: 'Live', end: true },
  { to: '/hardware', label: 'Hardware', end: true },
  { to: '/wissen', label: 'Wissen', end: true },
  { to: '/analyse', label: 'Analyse', end: true },
  { to: '/settings', label: 'Einstellungen', end: true },
  { to: '/archiv', label: 'Archiv', end: true },
]

function App() {
  const location = useLocation()
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const currentTitle = getCurrentTitle(location.pathname)

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="sidebar-logo">
          <div className="logo-mark">
            <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
              <path d="M7 1C7 1 2 4 2 8a5 5 0 0010 0c0-4-5-7-5-7z" fill="currentColor" opacity="0.9" />
            </svg>
          </div>
          <div className="logo-name">Grow OS</div>
          <div className="logo-sub">Cultivation Management</div>
        </div>

        <nav className="sidebar-nav" aria-label="Hauptnavigation">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => (isActive ? 'nav-item active' : 'nav-item')}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>

        <div className="sidebar-footer">
          <span className="logo-sub">Self-hosted · Lokal · HA-zentriert</span>
        </div>
      </aside>

      <header className="mobile-shell" aria-label="Mobile Navigation">
        <div className="mobile-shell-bar">
          <div className="mobile-brand">
            <div className="logo-mark">
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                <path d="M7 1C7 1 2 4 2 8a5 5 0 0010 0c0-4-5-7-5-7z" fill="currentColor" opacity="0.9" />
              </svg>
            </div>
            <div>
              <div className="logo-name">Grow OS</div>
              <div className="mobile-current-page">{currentTitle}</div>
            </div>
          </div>
          <button
            type="button"
            className="mobile-menu-button"
            aria-expanded={mobileMenuOpen}
            aria-controls="mobile-more-navigation"
            onClick={() => setMobileMenuOpen((current) => !current)}
          >
            Menü
          </button>
        </div>

        <nav className="mobile-primary-nav" aria-label="Mobile Schnellnavigation">
          {mobilePrimaryItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => (isActive ? 'mobile-nav-item active' : 'mobile-nav-item')}
              onClick={() => setMobileMenuOpen(false)}
            >
              {item.label}
            </NavLink>
          ))}
        </nav>

        {mobileMenuOpen && (
          <nav id="mobile-more-navigation" className="mobile-more-nav" aria-label="Weitere Navigation">
            {mobileMoreItems.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) => (isActive ? 'mobile-more-item active' : 'mobile-more-item')}
                onClick={() => setMobileMenuOpen(false)}
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
        )}
      </header>

      <div className="content">
        <Routes>
          <Route path="/" element={<LiveDashboardPage />} />
          <Route path="/action" element={<MobileActionPage />} />
          <Route path="/live" element={<LiveDashboardPage />} />
          <Route path="/grows" element={<Navigate to="/" replace />} />
          <Route path="/grows/new" element={<GrowSetupPage />} />
          <Route path="/grows/:growId" element={<GrowDetailPage />} />
          <Route path="/grows/measurements/:measurementId/edit" element={<MeasurementEditPage />} />
          <Route path="/grows/:growId/addback" element={<AddbackPage />} />
          <Route path="/grows/:growId/harvest" element={<HarvestPage />} />
          <Route path="/grows/:growId/setup" element={<GrowSetupPage />} />
          <Route path="/zelte" element={<TentsPage />} />
          <Route path="/zelte/:tentId" element={<TentDetailPage />} />
          <Route path="/hardware" element={<HardwarePage />} />
          <Route path="/archiv" element={<ArchivePage />} />
          <Route path="/wissen" element={<KnowledgePage />} />
          <Route path="/analyse" element={<AnalysisPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/einstellungen" element={<Navigate to="/settings" replace />} />
        </Routes>
      </div>
    </div>
  )
}

function getCurrentTitle(pathname: string): string {
  if (pathname === '/') return 'Dashboard'
  if (pathname.startsWith('/action')) return 'Aktion'
  if (pathname.startsWith('/live')) return 'Live'
  if (pathname.startsWith('/zelte')) return 'Zelte & Systeme'
  if (pathname.startsWith('/hardware')) return 'Hardware'
  if (pathname.startsWith('/grows/new')) return 'Neuer Grow'
  if (pathname.startsWith('/grows')) return 'Grow'
  if (pathname.startsWith('/wissen')) return 'Wissen'
  if (pathname.startsWith('/analyse')) return 'Analyse'
  if (pathname.startsWith('/settings') || pathname.startsWith('/einstellungen')) return 'Einstellungen'
  if (pathname.startsWith('/archiv')) return 'Archiv'
  return 'Grow OS'
}

export default App
