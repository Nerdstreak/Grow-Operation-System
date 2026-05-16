import { Link } from 'react-router-dom'
import { V1Page, V1Section } from '../components/v1'

function SettingsPage() {
  return (
    <V1Page eyebrow="System" title="Settings">
      <V1Section title="Verwaltung">
        <div className="v1-card-grid">
          <SettingsLink to="/home-assistant" title="Home Assistant" text="Verbindung und Entitäten" />
          <SettingsLink to="/wissen" title="Wissen" text="SOPs, Programme und Setpoints" />
          <SettingsLink to="/hardware" title="Sensoren" text="Kalibrierung, Wartung, Sensorvertrauen" />
          <SettingsLink to="/analyse" title="Analyse" text="Trends und Auswertung" />
        </div>
      </V1Section>
      <V1Section title="Release">
        <div className="v1-settings-note">Backup, Restore, Import und Export sind backendseitig abgesichert. Die UI dafür bleibt eine eigene Systemseite und wird nicht mit operativen Workflows vermischt.</div>
      </V1Section>
    </V1Page>
  )
}

function SettingsLink({ to, title, text }: { to: string; title: string; text: string }) {
  return <Link to={to} className="v1-settings-link"><strong>{title}</strong><span>{text}</span></Link>
}

export default SettingsPage
