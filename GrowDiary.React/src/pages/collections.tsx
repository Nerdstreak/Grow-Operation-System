import AutomationPage from './AutomationPage'
import AlertsPage from './AlertsPage'
import NotificationsPage from './NotificationsPage'
import { AiAssistantPage } from './AiAssistantPage'
import { TabbedCollectionPage } from './TabbedCollectionPage'

/**
 * Regeln & Automatik: EINE Seite mit den vier Bereichen als Tabs, in der
 * Reihenfolge des Entwurfs — Grenzwerte zuerst, denn das ist der Bereich,
 * den man im Alltag anfasst.
 *
 * Sorten und Archiv sind keine Tab-Sammlungen mehr: der Entwurf legt
 * Bibliothek + Pheno-Hunt bzw. Ertragstabelle + Vergleich auf je eine Seite.
 */
export function RulesCollectionPage() {
  return (
    <TabbedCollectionPage
      eyebrow="Anlage / Regeln"
      title="Regeln & Automatik"
      subtitle="Grenzwerte, Auto-Messungen und Benachrichtigungen an einem Ort — vorher drei Seiten plus KI-Assistent."
      tabs={[
        { key: 'grenzwerte', label: 'Grenzwerte', render: () => <AlertsPage /> },
        { key: 'automatik', label: 'Auto-Messungen', render: () => <AutomationPage /> },
        { key: 'push', label: 'Benachrichtigungen', render: () => <NotificationsPage /> },
        { key: 'ki', label: 'KI-Assistent', render: () => <AiAssistantPage /> },
      ]}
    />
  )
}
