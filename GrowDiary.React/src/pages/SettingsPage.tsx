import { Link } from 'react-router-dom'
import { V1Page, V1Section } from '../components/v1'

function SettingsPage() {
  return (
    <V1Page eyebrow="System" title="Einstellungen" subtitle="Zentrale Übersicht für Verbindung, Wissen, Release und Systempflege. Operative Workflows bleiben bewusst in eigenen Seiten.">
      <V1Section title="V1 Systembereiche">
        <div className="v1-card-grid">
          <SettingsLink to="/connect" title="Gerät verbinden" text="Handy, PWA, lokale Adresse, Remote-Adresse und QR-Code" />
          <SettingsLink to="/home-assistant" title="Home Assistant" text="Geführtes Setup für Verbindung, Kamera und Sensor-Entitäten" />
          <SettingsLink to="/wissen" title="Wissen & SOPs" text="Programme, SOPs, Symptome, Setpoints und Empfehlungen" />
          <SettingsLink to="/release" title="Release & Daten" text="Grow-Export, Import-Plan, Import und Backup-Hinweise" />
          <SettingsLink to="/hardware" title="Sensoren" text="Kalibrierung, Wartung, Sensorvertrauen" />
          <SettingsLink to="/analyse" title="Analyse" text="Trends und Auswertung" />
        </div>
      </V1Section>
      <V1Section title="V1-Abnahme">
        <div className="v1-card-grid">
          <div className="v1-settings-link"><strong>Local-first</strong><span>Die App bleibt selfhosted. Remote-Zugriff ist optional und muss geschützt werden.</span></div>
          <div className="v1-settings-link"><strong>RDWC/DWC getrennt</strong><span>Zelte, Hydro-Systeme, Grows, Addback und Sensoren sind getrennte Workflows.</span></div>
          <div className="v1-settings-link"><strong>Auditfähig</strong><span>Visual Audit prüft Live, Addback Deep Flow, Connect, Release und die Kernseiten.</span></div>
        </div>
      </V1Section>
    </V1Page>
  )
}

function SettingsLink({ to, title, text }: { to: string; title: string; text: string }) {
  return <Link to={to} className="v1-settings-link"><strong>{title}</strong><span>{text}</span></Link>
}

export default SettingsPage
