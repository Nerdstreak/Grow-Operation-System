import { useState } from 'react'
import { NavLink, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import AddbackHubPage from './pages/AddbackHubPage'
import AddbackPage from './pages/AddbackPage'
import AlertsPage from './pages/AlertsPage'
import NotificationsPage from './pages/NotificationsPage'
import { GrowScopedSectionPage } from './pages/GrowScopedSectionPage'
import AutomationPage from './pages/AutomationPage'
import AnalysisPage from './pages/AnalysisPage'
import ArchivePage from './pages/ArchivePage'
import GettingStartedPage from './pages/GettingStartedPage'
import GrowDetailPage from './pages/GrowDetailPage'
import GrowsPage from './pages/GrowsPage'
import GrowSetupPage from './pages/GrowSetupPage'
import HardwarePage from './pages/HardwarePage'
import HarvestPage from './pages/HarvestPage'
import HomeAssistantPage from './pages/HomeAssistantPage'
import HydroDetailPage from './pages/HydroDetailPage'
import HydroPage from './pages/HydroPage'
import KnowledgePage from './pages/KnowledgePage'
import LiveDashboardPage from './pages/LiveDashboardPage'
import ManualMeasurementPage from './pages/ManualMeasurementPage'
import MeasurementEditPage from './pages/MeasurementEditPage'
import MobileActionPage from './pages/MobileActionPage'
import ReleasePage from './pages/ReleasePage'
import SettingsPage from './pages/SettingsPage'
import TentDetailPage from './pages/TentDetailPage'
import TentsPage from './pages/TentsPage'
import './rc2-overrides.css'
import './styles/redesign-shell.css'
import './styles/redesign-primitives.css'

type NavLeaf = { to: string; label: string; end: boolean }
type NavGroup = { id: string; label: string; defaultOpen: boolean; items: NavLeaf[] }

// Single source for both the desktop sidebar and the mobile "Mehr" panel, grouped by
// what the user wants to do rather than by feature. The first group is the daily
// core loop (also the mobile bottom nav); the rest are collapsible.
const navGroups: NavGroup[] = [
  { id: 'daily', label: 'Täglich', defaultOpen: true, items: [
    { to: '/', label: 'Live', end: true },
    { to: '/messung', label: 'Messung', end: true },
    { to: '/addback', label: 'Addback', end: true },
    { to: '/aufgaben', label: 'Aufgaben', end: true },
  ] },
  { id: 'history', label: 'Verlauf & Daten', defaultOpen: true, items: [
    { to: '/diagnose', label: 'Diagnose', end: true },
    { to: '/journal', label: 'Journal & Fotos', end: true },
  ] },
  { id: 'rules', label: 'Automatik & Regeln', defaultOpen: true, items: [
    { to: '/automatik', label: 'Automatik', end: true },
    { to: '/alarme', label: 'Grenzwerte', end: true },
    { to: '/benachrichtigungen', label: 'Benachrichtigungen', end: true },
  ] },
  { id: 'grows', label: 'Meine Grows', defaultOpen: true, items: [
    { to: '/grows', label: 'Grows', end: false },
    { to: '/analyse', label: 'Vergleich', end: true },
    { to: '/archiv', label: 'Archiv', end: true },
  ] },
  { id: 'setup', label: 'Einrichten', defaultOpen: false, items: [
    { to: '/zelte', label: 'Zelte', end: false },
    { to: '/hydro', label: 'Hydro', end: true },
    { to: '/hardware', label: 'Sensoren', end: true },
    { to: '/home-assistant', label: 'Home Assistant', end: true },
  ] },
  { id: 'system', label: 'Wissen', defaultOpen: false, items: [
    { to: '/sops', label: 'SOPs', end: true },
    { to: '/wissen', label: 'Wissen', end: true },
    { to: '/start', label: 'Erste Schritte', end: true },
    { to: '/settings', label: 'Einstellungen', end: true },
  ] },
]

const mobilePrimaryNav = navGroups[0].items
const NAV_STORAGE_KEY = 'growos.navGroups'

function defaultOpenGroups(): Record<string, boolean> {
  return Object.fromEntries(navGroups.map((group) => [group.id, group.defaultOpen]))
}

function isNavLeafActive(item: NavLeaf, pathname: string): boolean {
  return item.end ? pathname === item.to : pathname === item.to || pathname.startsWith(`${item.to}/`)
}

function App() {
  const location = useLocation()
  const [mobileMoreOpen, setMobileMoreOpen] = useState(false)
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>(() => {
    try {
      const stored = localStorage.getItem(NAV_STORAGE_KEY)
      if (stored) return { ...defaultOpenGroups(), ...(JSON.parse(stored) as Record<string, boolean>) }
    } catch { /* ignore corrupt/absent storage */ }
    return defaultOpenGroups()
  })
  const title = getCurrentTitle(location.pathname)
  // The group containing the current page stays open regardless of collapse state,
  // so the active page never hides.
  const activeGroupId = navGroups.find((group) => group.items.some((item) => isNavLeafActive(item, location.pathname)))?.id

  function toggleGroup(id: string) {
    setOpenGroups((current) => {
      const next = { ...current, [id]: !(current[id] ?? true) }
      try { localStorage.setItem(NAV_STORAGE_KEY, JSON.stringify(next)) } catch { /* ignore */ }
      return next
    })
  }

  return (
    <div className="v1-app-shell rc2-shell" data-audit="mobile-shell">
      <aside className="v1-desktop-nav" aria-label="Desktop Navigation">
        <div className="v1-brand">
          <div className="v1-brand-mark">●</div>
          <div>
            <strong>Grow OS</strong>
            <span>RDWC/DWC · Home Assistant</span>
          </div>
        </div>
        {navGroups.map((group) => {
          const open = group.id === activeGroupId || (openGroups[group.id] ?? group.defaultOpen)
          return (
            <nav key={group.id} className={open ? 'v1-nav-group' : 'v1-nav-group collapsed'} aria-label={group.label}>
              <button type="button" className="v1-nav-group-head" onClick={() => toggleGroup(group.id)} aria-expanded={open}>
                <span className="v1-nav-group-label">{group.label}</span>
                <span className="v1-nav-group-count">{group.items.length}</span>
                <span className="v1-nav-group-chev" aria-hidden="true" />
              </button>
              {open && group.items.map((item) => <NavItem key={item.to} {...item} />)}
            </nav>
          )
        })}
      </aside>

      <header className="v1-mobile-topbar" data-audit="mobile-header">
        <div className="v1-brand compact">
          <div className="v1-brand-mark">●</div>
          <div>
            <strong>Grow OS</strong>
            <span>{title}</span>
          </div>
        </div>
        <button type="button" className="v1-mobile-more-button" data-audit="mobile-more-button" onClick={() => setMobileMoreOpen((current) => !current)} aria-expanded={mobileMoreOpen}>
          Mehr
        </button>
      </header>

      {mobileMoreOpen && (
        <div className="v1-mobile-more-panel" data-audit="mobile-more-menu">
          {navGroups.slice(1).map((group) => (
            <section key={group.id} className="v1-mobile-more-group" data-audit={`mobile-more-group-${group.id}`}>
              <h2>{group.label}</h2>
              <div className="v1-mobile-more-grid">
                {group.items.map((item) => (
                  <NavLink key={item.to} to={item.to} end={item.end} onClick={() => setMobileMoreOpen(false)} className={({ isActive }) => (isActive ? 'v1-more-tile active' : 'v1-more-tile')}>
                    {item.label}
                  </NavLink>
                ))}
              </div>
            </section>
          ))}
        </div>
      )}

      <main className="v1-route-frame">
        <Routes>
          <Route path="/" element={<LiveDashboardPage />} />
          <Route path="/live" element={<Navigate to="/" replace />} />
          <Route path="/addback" element={<AddbackHubPage />} />
          <Route path="/aufgaben" element={<MobileActionPage />} />
          <Route path="/action" element={<Navigate to="/aufgaben" replace />} />
          <Route path="/grows" element={<GrowsPage />} />
          <Route path="/grows/new" element={<GrowSetupPage />} />
          <Route path="/messung" element={<ManualMeasurementPage />} />
          <Route path="/messungen/new" element={<Navigate to="/messung" replace />} />
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
          <Route path="/hydro/:setupId" element={<HydroDetailPage />} />
          <Route path="/home-assistant" element={<HomeAssistantPage />} />
          <Route path="/alarme" element={<AlertsPage />} />
          <Route path="/benachrichtigungen" element={<NotificationsPage />} />
          <Route path="/automatik" element={<AutomationPage />} />
          <Route path="/messungen" element={<GrowScopedSectionPage title="Messungen" section="measurements" />} />
          <Route path="/diagnose" element={<GrowScopedSectionPage title="Diagnose" section="diagnosis" />} />
          <Route path="/journal" element={<GrowScopedSectionPage title="Journal & Fotos" section="journal" />} />
          <Route path="/sops" element={<GrowScopedSectionPage title="SOPs" section="sops" />} />
          <Route path="/hardware" element={<HardwarePage />} />
          <Route path="/wissen" element={<KnowledgePage />} />
          <Route path="/release" element={<ReleasePage />} />
          <Route path="/analyse" element={<AnalysisPage />} />
          <Route path="/archiv" element={<ArchivePage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/start" element={<GettingStartedPage />} />
          <Route path="/einstellungen" element={<Navigate to="/settings" replace />} />
        </Routes>
      </main>

      <nav className="v1-bottom-nav" aria-label="Mobile Hauptnavigation" data-audit="mobile-bottom-nav">
        {mobilePrimaryNav.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} data-audit="mobile-bottom-nav-item" onClick={() => setMobileMoreOpen(false)} className={({ isActive }) => (isActive ? 'v1-bottom-item active' : 'v1-bottom-item')}>
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
  if (pathname.startsWith('/benachrichtigungen')) return 'Benachrichtigungen'
  if (pathname.startsWith('/automatik')) return 'Automatik'
  if (pathname.startsWith('/messungen')) return 'Messungen'
  if (pathname.startsWith('/diagnose')) return 'Diagnose'
  if (pathname.startsWith('/journal')) return 'Journal & Fotos'
  if (pathname.startsWith('/sops')) return 'SOPs'
  if (pathname.startsWith('/alarme')) return 'Grenzwerte'
  if (pathname.startsWith('/grows/new')) return 'Grow starten'
  if (pathname.startsWith('/messung') || pathname.startsWith('/messungen')) return 'Messung'
  if (pathname.startsWith('/grows')) return 'Grows'
  if (pathname.startsWith('/hardware')) return 'Sensoren'
  if (pathname.startsWith('/wissen')) return 'Wissen'
  if (pathname.startsWith('/release')) return 'Release'
  if (pathname.startsWith('/analyse')) return 'Vergleich'
  if (pathname.startsWith('/archiv')) return 'Archiv'
  if (pathname.startsWith('/settings') || pathname.startsWith('/einstellungen')) return 'Einstellungen'
  if (pathname.startsWith('/start')) return 'Erste Schritte'
  return 'Grow OS'
}

export default App
