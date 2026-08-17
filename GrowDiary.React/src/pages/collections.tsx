import AutomationPage from './AutomationPage'
import AlertsPage from './AlertsPage'
import NotificationsPage from './NotificationsPage'
import { TabbedCollectionPage } from './TabbedCollectionPage'

/**
 * Regeln & Automatik: EINE Seite mit den vier Bereichen als Tabs, in der
 * Reihenfolge des Entwurfs — Grenzwerte zuerst, denn das ist der Bereich,
 * den man im Alltag anfasst.
 *
 * Der Untertitel nennt ausdruecklich die beiden Automatiken, die NICHT hier
 * sitzen. Wer Automatik sucht, kommt auf die Seite, die so heisst — und schloss
 * bisher aus ihrem Inhalt, dass es die anderen nicht gibt.
 *
 * Sorten und Archiv sind keine Tab-Sammlungen mehr: der Entwurf legt
 * Bibliothek + Pheno-Hunt bzw. Ertragstabelle + Vergleich auf je eine Seite.
 */
export function RulesCollectionPage() {
  return (
    <TabbedCollectionPage
      eyebrow="Betrieb / Regeln"
      title="Regeln & Automatik"
      subtitle="Grenzwerte, Auto-Messungen und Benachrichtigungen an einem Ort. Zwei Automatiken sitzen dort, wo sie wirken: die Dosierung bei den Pumpen und die Nachtabsenkung beim jeweiligen Grow."
      tabs={[
        { key: 'grenzwerte', label: 'Grenzwerte', render: () => <AlertsPage /> },
        { key: 'automatik', label: 'Auto-Messungen', render: () => <AutomationPage /> },
        { key: 'push', label: 'Benachrichtigungen', render: () => <NotificationsPage /> },
      ]}
    />
  )
}
