import { Navigate, Route, Routes } from 'react-router-dom'
import AddbackHubPage from './pages/AddbackHubPage'
import AddbackPage from './pages/AddbackPage'
import { GrowScopedSectionPage } from './pages/GrowScopedSectionPage'
import GettingStartedPage from './pages/GettingStartedPage'
import GrowDetailPage from './pages/GrowDetailPage'
import GrowsPage from './pages/GrowsPage'
import GrowSetupPage from './pages/GrowSetupPage'
import HardwarePage from './pages/HardwarePage'
import HarvestPage from './pages/HarvestPage'
import HomeAssistantPage from './pages/HomeAssistantPage'
import HydroDetailPage from './pages/HydroDetailPage'
import HydroPage from './pages/HydroPage'
import HydroEditorPage from './features/hydro/HydroEditorPage'
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

import { AppShell } from './AppShell'
import { legacyRedirects } from './navigation'
import { useAppScope } from './useAppScope'
import { ArchiveCollectionPage, RulesCollectionPage, StrainsCollectionPage } from './pages/collections'

function App() {
  const { scope, counts } = useAppScope()

  return (
    <AppShell scope={scope} counts={counts}>
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
          {/* Anlegen und Bearbeiten laufen ueber eine Seite statt fuenf Wizard-Schritte. */}
          <Route path="/hydro/new" element={<HydroEditorPage />} />
          <Route path="/hydro/:id/edit" element={<HydroEditorPage />} />
          <Route path="/hydro/:setupId" element={<HydroDetailPage />} />
          <Route path="/home-assistant" element={<HomeAssistantPage />} />
          <Route path="/messungen" element={<GrowScopedSectionPage title="Messungen" section="measurements" />} />
          <Route path="/diagnose" element={<GrowScopedSectionPage title="Diagnose" section="diagnosis" />} />
          <Route path="/journal" element={<GrowScopedSectionPage title="Journal & Fotos" section="journal" />} />
          <Route path="/sops" element={<GrowScopedSectionPage title="SOPs" section="sops" />} />
          <Route path="/wissen" element={<KnowledgePage />} />
          <Route path="/release" element={<ReleasePage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/start" element={<GettingStartedPage />} />
          <Route path="/einstellungen" element={<Navigate to="/settings" replace />} />
        
          <Route path="/sensoren" element={<HardwarePage />} />

          {/* Sammelseiten: verwandte Bereiche unter Tabs statt als eigene Menuepunkte. */}
          <Route path="/regeln" element={<RulesCollectionPage />} />
          <Route path="/sorten" element={<StrainsCollectionPage />} />
          <Route path="/archiv" element={<ArchiveCollectionPage />} />

          {/* Alte Pfade bleiben gueltig — Lesezeichen und Links aus HA-Dashboards. */}
          {Object.entries(legacyRedirects).map(([from, to]) => (
            <Route key={from} path={from} element={<Navigate to={to} replace />} />
          ))}
        </Routes>
    </AppShell>
  )
}

export default App
