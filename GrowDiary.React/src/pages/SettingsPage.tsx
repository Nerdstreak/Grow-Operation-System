import { Link } from 'react-router-dom'
import { V1Page, V1Section } from '../components/v1'

function SettingsPage() {
  return (
    <V1Page eyebrow="System" title="Einstellungen">
      <V1Section title="Verwaltung">
        <div className="v1-card-grid">
          <SettingsLink to="/connect" title="Gerät verbinden" text="Handy, PWA, lokale Adresse, Remote-Adresse und QR-Code" />
          <SettingsLink to="/home-assistant" title="Home Assistant" text="Verbindung und Entitäten" />
          <SettingsLink to="/wissen" title="Wissen" text="SOPs, Programme und Setpoints" />
          <SettingsLink to="/hardware" title="Sensoren" text="Kalibrierung, Wartung, Sensorvertrauen" />
          <SettingsLink to="/analyse" title="Analyse" text="Trends und Auswertung" />
        </div>
      </V1Section>
      <V1Section title="Release">
        <div className="v1-settings-note">Backup, Restore, Import, Export, Addback, Grow-Wizard, Sensoren und Visual Audit sind V1-seitig angebunden. Operative Workflows bleiben getrennt von dieser Systemübersicht.</div>
      </V1Section>
    </V1Page>
  )
}

function SettingsLink({ to, title, text }: { to: string; title: string; text: string }) {
  return <Link to={to} className="v1-settings-link"><strong>{title}</strong><span>{text}</span></Link>
}

export default SettingsPage
