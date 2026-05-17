import { useState } from 'react'
import { NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import AddbackHubPage from './pages/AddbackHubPage'
import AddbackPage from './pages/AddbackPage'
import AnalysisPage from './pages/AnalysisPage'
import ArchivePage from './pages/ArchivePage'
import DeviceConnectPage from './pages/DeviceConnectPage'
import GrowDetailPage from './pages/GrowDetailPage'
import GrowSetupPage from './pages/GrowSetupPage'
import HardwarePage from './pages/HardwarePage'
import HarvestPage from './pages/HarvestPage'
import HomeAssistantPage from './pages/HomeAssistantPage'
import HydroPage from './pages/HydroPage'
import KnowledgePage from './pages/KnowledgePage'
import LiveDashboardPage from './pages/LiveDashboardPage'
import MeasurementEditPage from './pages/MeasurementEditPage'
import MobileActionPage from './pages/MobileActionPage'
import ReleasePage from './pages/ReleasePage'
import SettingsPage from './pages/SettingsPage'
import TentDetailPage from './pages/TentDetailPage'
import TentsPage from './pages/TentsPage'
import './rc2-overrides.css'

const coreNav = [
  { to: '/', label: 'Live', end: true },
  { to: '/addback', label: 'Addback', end: true },
  { to: '/zelte', label: 'Zelte', end: false },
  { to: '/hydro', label: 'Hydro', end: true },
]

const moreNav = [
  { to: '/action', label: 'Aufgaben', end: true },
  { to: '/grows/new', label: 'Grow starten', end: true },
  { to: '/home-assistant', label: 'Home Assistant', end: true },
  { to: '/connect', label: 'Gerät verbinden', end: true },
  { to: '/hardware', label: 'Sensoren', end: true },
  { to: '/wissen', label: 'Wissen', end: true },
  { to: '/analyse', label: 'Vergleich', end: true },
  { to: '/archiv', label: 'Archiv', end: true },
  { to: '/settings', label: 'Einstellungen', end: true },
]

function App() {
  const location = useLocation()
  const [mobileMoreOpen, setMobileMoreOpen] = useState(false)
  const title = getCurrentTitle(location.pathname)

  return (
    <div className="v1-app-shell rc2-shell">
      <aside className="v1-desktop-nav" aria-label="Desktop Navigation">
        <div className="v1-brand">
          <div className="v1-brand-mark">●</div>
          <div>
            <strong>Grow OS</strong>
            <span>Local-first RDWC</span>
          </div>
        </div>
        <nav className="v1-nav-group" aria-label="Core">
          <span>Core</span>
          {coreNav.map((item) => <NavItem key={item.to} {...item} />)}
        </nav>
        <nav className="v1-nav-group" aria-label="Mehr">
          <span>Mehr</span>
          {moreNav.map((item) => <NavItem key={item.to} {...item} />)}
        </nav>
        <div className="v1-nav-foot">Selfhost · HA · Offline-first</div>
      </aside>

      <header className="v1-mobile-topbar">
        <div className="v1-brand compact">
          <div className="v1-brand-mark">●</div>
          <div>
            <strong>Grow OS</strong>
            <span>{title}</span>
          </div>
        </div>
        <button type="button" className="v1-mobile-more-button" onClick={() => setMobileMoreOpen((current) => !current)} aria-expanded={mobileMoreOpen}>
          Mehr
        </button>
      </header>

      {mobileMoreOpen && (
        <div className="v1-mobile-more-panel">
          <div className="v1-mobile-more-grid">
            {moreNav.map((item) => (
              <NavLink key={item.to} to={item.to} end={item.end} onClick={() => setMobileMoreOpen(false)} className={({ isActive }) => (isActive ? 'v1-more-tile active' : 'v1-more-tile')}>
                {item.label}
              </NavLink>
            ))}
          </div>
        </div>
      )}

      <main className="v1-route-frame">
        <Routes>
          <Route path="/" element={<LiveDashboardPage />} />
          <Route path="/live" element={<Navigate to="/" replace />} />
          <Route path="/addback" element={<AddbackHubPage />} />
          <Route path="/action" element={<MobileActionPage />} />
          <Route path="/aufgaben" element={<Navigate to="/action" replace />} />
          <Route path="/grows" element={<Navigate to="/" replace />} />
          <Route path="/grows/new" element={<GrowSetupPage />} />
          <Route path="/grows/:growId" element={<GrowDetailPage />} />
          <Route path="/grows/:growId/setup" element={<GrowSetupPage />} />
          <Route path="/grows/:growId/addback" element={<AddbackPage />} />
          <Route path="/grows/:growId/harvest" element={<HarvestPage />} />
          <Route path="/grows/measurements/:measurementId/edit" element={<MeasurementEditPage />} />
          <Route path="/zelte" element={<TentsPage />} />
          <Route path="/zelte/new" element={<TentsPage />} />
          <Route path="/zelte/:tentId" element={<TentDetailPage />} />
          <Route path="/hydro" element={<HydroPage />} />
          <Route path="/hydro/new" element={<HydroPage />} />
          <Route path="/home-assistant" element={<HomeAssistantPage />} />
          <Route path="/connect" element={<DeviceConnectPage />} />
          <Route path="/hardware" element={<HardwarePage />} />
          <Route path="/wissen" element={<KnowledgePage />} />
          <Route path="/release" element={<ReleasePage />} />
          <Route path="/analyse" element={<AnalysisPage />} />
          <Route path="/archiv" element={<ArchivePage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/einstellungen" element={<Navigate to="/settings" replace />} />
        </Routes>
      </main>

      <nav className="v1-bottom-nav" aria-label="Mobile Hauptnavigation">
        {coreNav.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} onClick={() => setMobileMoreOpen(false)} className={({ isActive }) => (isActive ? 'v1-bottom-item active' : 'v1-bottom-item')}>
            {item.label}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}

function NavItem({ to, label, end }: { to: string; label: string; end: boolean }) {
  return <NavLink to={to} end={end} className={({ isActive }) => (isActive ? 'v1-nav-item active' : 'v1-nav-item')}>{label}</NavLink>
}

function getCurrentTitle(pathname: string) {
  if (pathname === '/' || pathname.startsWith('/live')) return 'Live'
  if (pathname.startsWith('/addback') || pathname.includes('/addback')) return 'Addback'
  if (pathname.startsWith('/zelte')) return 'Zelte'
  if (pathname.startsWith('/hydro')) return 'Hydro'
  if (pathname.startsWith('/action') || pathname.startsWith('/aufgaben')) return 'Aufgaben'
  if (pathname.startsWith('/home-assistant')) return 'Home Assistant'
  if (pathname.startsWith('/connect')) return 'Gerät verbinden'
  if (pathname.startsWith('/grows/new')) return 'Grow starten'
  if (pathname.startsWith('/grows')) return 'Grow'
  if (pathname.startsWith('/hardware')) return 'Sensoren'
  if (pathname.startsWith('/wissen')) return 'Wissen'
  if (pathname.startsWith('/release')) return 'Release'
  if (pathname.startsWith('/analyse')) return 'Vergleich'
  if (pathname.startsWith('/archiv')) return 'Archiv'
  if (pathname.startsWith('/settings') || pathname.startsWith('/einstellungen')) return 'Einstellungen'
  return 'Grow OS'
}

export default App
