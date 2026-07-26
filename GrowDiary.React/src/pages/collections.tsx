import AutomationPage from './AutomationPage'
import AlertsPage from './AlertsPage'
import NotificationsPage from './NotificationsPage'
import { AiAssistantPage } from './AiAssistantPage'
import StrainsPage from './StrainsPage'
import PhenoHuntPage from './PhenoHuntPage'
import ArchivePage from './ArchivePage'
import AnalysisPage from './AnalysisPage'
import { TabbedCollectionPage } from './TabbedCollectionPage'

/**
 * Die drei Sammelseiten aus dem Navigations-Umbau.
 *
 * Bewusst nur eine neue Hülle um die bestehenden Seiten: deren Daten- und
 * API-Logik bleibt unangetastet, nur der Weg dorthin ändert sich. Sensoren
 * fasst der Entwurf zu *einer Tabelle* zusammen statt zu Tabs — das bleibt
 * deshalb HardwarePage und wandert erst mit dem Screen-Redesign.
 */

export function RulesCollectionPage() {
  return (
    <TabbedCollectionPage
      tabs={[
        { key: 'automatik', label: 'Automatik', render: () => <AutomationPage /> },
        { key: 'grenzwerte', label: 'Grenzwerte', render: () => <AlertsPage /> },
        { key: 'push', label: 'Benachrichtigungen', render: () => <NotificationsPage /> },
        { key: 'ki', label: 'KI-Assistent', render: () => <AiAssistantPage /> },
      ]}
    />
  )
}

export function StrainsCollectionPage() {
  return (
    <TabbedCollectionPage
      tabs={[
        { key: 'sorten', label: 'Sorten', render: () => <StrainsPage /> },
        { key: 'pheno', label: 'Pheno Hunt', render: () => <PhenoHuntPage /> },
      ]}
    />
  )
}

/** Ernte bleibt außen vor: HarvestPage arbeitet pro Grow und braucht die
 *  growId aus der Route, in einem Tab hätte sie nichts zu zeigen. */
export function ArchiveCollectionPage() {
  return (
    <TabbedCollectionPage
      tabs={[
        { key: 'archiv', label: 'Archiv', render: () => <ArchivePage /> },
        { key: 'vergleich', label: 'Vergleich', render: () => <AnalysisPage /> },
      ]}
    />
  )
}
