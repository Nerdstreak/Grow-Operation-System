import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, SettingsOverviewDto } from '../types'
import { V1Alert, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section } from '../components/v1'

type ImportPreview = {
  ok: boolean
  title: string
  details: string[]
}

function SettingsPage() {
  const [settings, setSettings] = useState<SettingsOverviewDto | null>(null)
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [importText, setImportText] = useState('')
  const [preview, setPreview] = useState<ImportPreview | null>(null)
  const [loading, setLoading] = useState(true)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)
      try {
        const [overview, activeGrows] = await Promise.all([
          apiFetch<SettingsOverviewDto>('/api/settings', { signal: controller.signal }),
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }),
        ])
        if (controller.signal.aborted) return
        setSettings(overview)
        setGrows(activeGrows)
      } catch (caught) {
        if (!controller.signal.aborted) setError(formatApiError(caught, 'Einstellungen konnten nicht geladen werden.'))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  function exportSystemConfig() {
    if (!settings) return
    const payload = {
      schema: 'grow-os.system-config.v1',
      exportedAtUtc: new Date().toISOString(),
      homeAssistant: {
        enabled: settings.homeAssistant.enabled,
        baseUrl: settings.homeAssistant.baseUrl,
        tokenStored: Boolean(settings.homeAssistant.accessToken),
      },
      tents: settings.tents.map((tent) => ({
        id: tent.id,
        name: tent.name,
        kind: tent.kind,
        tentType: tent.tentType,
        size: { widthCm: tent.widthCm, depthCm: tent.depthCm, heightCm: tent.tentHeightCm },
        cameraEntityId: tent.cameraEntityId,
        lightControllerEntityId: tent.lightControllerEntityId,
        hvacControllerEntityId: tent.hvacControllerEntityId,
        sensors: tent.sensors.map((sensor) => ({
          metricType: sensor.metricType,
          haEntityId: sensor.haEntityId,
          displayLabel: sensor.displayLabel,
          isActive: sensor.isActive,
        })),
      })),
      note: 'Dieser Export ist für Dokumentation und späteren Import gedacht. Tokens werden nicht im Klartext exportiert.',
    }
    downloadJson(`grow-os-system-config-${new Date().toISOString().slice(0, 10)}.json`, payload)
    setMessage('System-Konfiguration als JSON exportiert.')
  }

  function exportHaMapping() {
    if (!settings) return
    const payload = {
      schema: 'grow-os.ha-mapping.v1',
      exportedAtUtc: new Date().toISOString(),
      homeAssistant: {
        enabled: settings.homeAssistant.enabled,
        baseUrl: settings.homeAssistant.baseUrl,
      },
      tents: settings.tents.map((tent) => ({
        name: tent.name,
        cameraEntityId: tent.cameraEntityId,
        lightControllerEntityId: tent.lightControllerEntityId,
        hvacControllerEntityId: tent.hvacControllerEntityId,
        sensors: tent.sensors
          .filter((sensor) => sensor.isActive || sensor.haEntityId)
          .map((sensor) => ({
            metricType: sensor.metricType,
            haEntityId: sensor.haEntityId,
            displayLabel: sensor.displayLabel,
            isActive: sensor.isActive,
          })),
      })),
    }
    downloadJson(`grow-os-ha-mapping-${new Date().toISOString().slice(0, 10)}.json`, payload)
    setMessage('Home-Assistant-Mapping als JSON exportiert.')
  }

  async function handleFile(file: File | null) {
    if (!file) return
    setImportText(await file.text())
    setPreview(null)
  }

  function inspectImport() {
    try {
      const parsed = JSON.parse(importText) as Record<string, unknown>
      const schema = typeof parsed.schema === 'string' ? parsed.schema : 'unbekannt'
      const tents = Array.isArray(parsed.tents) ? parsed.tents.length : 0
      setPreview({
        ok: true,
        title: schema,
        details: [
          `${tents} Zelt-/Mapping-Einträge erkannt`,
          schema.includes('ha-mapping') ? 'HA-Mapping-Export erkannt' : 'System-/Grow-Export erkannt',
          'Import schreibt in V1 noch nicht automatisch. Erst prüfen, dann gezielt übernehmen.',
        ],
      })
      setMessage('JSON gelesen. Vorschau erstellt.')
    } catch {
      setPreview({ ok: false, title: 'Ungültiges JSON', details: ['Datei konnte nicht gelesen werden.'] })
    }
  }

  return (
    <V1Page eyebrow="Admin" title="Einstellungen" subtitle="Administrative Zentrale: Backup, Export, Import, Verbindung und Systempflege. Keine operative Grow-Bedienung.">
      {error && <V1Alert title="Fehler" message={error} tone="warn" />}
      {message && <V1Alert message={message} tone="ok" />}

      {loading ? <V1Empty title="Lade System..." /> : (
        <>
          <section className="v1-kpi-grid rc2-admin-kpis">
            <V1Card><span className="v1-card-kicker">Zelte</span><h2>{settings?.tents.length ?? 0}</h2><p>inkl. HA-Mapping</p></V1Card>
            <V1Card><span className="v1-card-kicker">Grows</span><h2>{grows.length}</h2><p>aktiv oder geplant</p></V1Card>
            <V1Card tone={settings?.homeAssistant.enabled ? 'ok' : 'warn'}><span className="v1-card-kicker">Home Assistant</span><h2>{settings?.homeAssistant.enabled ? 'aktiv' : 'aus'}</h2><p>{settings?.homeAssistant.baseUrl || 'keine URL'}</p></V1Card>
            <V1Card><span className="v1-card-kicker">Backup</span><h2>JSON</h2><p>System- und Grow-Daten getrennt</p></V1Card>
          </section>

          <V1Section title="Daten & Backup">
            <div className="rc2-admin-grid">
              <V1Card className="rc2-admin-card">
                <span className="v1-card-kicker">System-Konfiguration</span>
                <h2>Zelte + HA-Mapping exportieren</h2>
                <p>Exportiert Zeltstruktur, Kamera-IDs, Sensor-Entity-IDs und HA-Basisdaten. Tokens werden nicht im Klartext exportiert.</p>
                <div className="v1-action-row">
                  <V1Button variant="primary" onClick={exportSystemConfig}>System JSON exportieren</V1Button>
                  <V1Button onClick={exportHaMapping}>Nur HA-Mapping</V1Button>
                </div>
              </V1Card>

              <V1Card className="rc2-admin-card">
                <span className="v1-card-kicker">Grow-Export / Import</span>
                <h2>Run-Daten sichern oder übertragen</h2>
                <p>Grow-Exports, Import-Plan und Import bleiben ein heikler Admin-Workflow. Deshalb ist der Bereich hier eingeordnet.</p>
                <Link to="/release" className="v1-button is-primary">Grow Import/Export öffnen</Link>
              </V1Card>

              <V1Card className="rc2-admin-card tone-warn">
                <span className="v1-card-kicker">Vollbackup</span>
                <h2>Datenbank + App_Data</h2>
                <p>Für ein vollständiges Restore reicht ein Grow-Export nicht. SQLite-Datenbank und App_Data müssen zusammen gesichert werden.</p>
                <small>Restore/Werkseinstellungen werden erst mit isoliertem Sicherheitsdialog schreibend aktiviert.</small>
              </V1Card>
            </div>
          </V1Section>

          <V1Section title="Import vorbereiten">
            <div className="rc2-admin-grid two">
              <V1Card>
                <span className="v1-card-kicker">JSON prüfen</span>
                <h2>Import-Datei kontrollieren</h2>
                <V1Field label="Datei">
                  <input type="file" accept="application/json,.json" onChange={(event) => void handleFile(event.target.files?.[0] ?? null)} />
                </V1Field>
                <V1Field label="Oder JSON einfügen">
                  <textarea rows={8} value={importText} onChange={(event) => setImportText(event.target.value)} placeholder="{ ... }" />
                </V1Field>
                <V1Button variant="primary" disabled={!importText.trim()} onClick={inspectImport}>JSON prüfen</V1Button>
              </V1Card>

              <V1Card tone={preview?.ok ? 'ok' : preview ? 'warn' : 'neutral'}>
                <span className="v1-card-kicker">Vorschau</span>
                <h2>{preview?.title ?? 'Noch keine Datei geprüft'}</h2>
                {preview ? preview.details.map((detail) => <p key={detail}>{detail}</p>) : <p>Hier siehst du vor dem Import, was in der Datei steckt.</p>}
              </V1Card>
            </div>
          </V1Section>

          <V1Section title="Systembereiche">
            <div className="v1-card-grid">
              <SettingsLink to="/connect" title="Gerät verbinden" text="Handy, PWA, lokale/Remote-Adresse und QR-Code" />
              <SettingsLink to="/home-assistant" title="Home Assistant" text="Verbindung, Kamera und Sensor-Entity-Mapping" />
              <SettingsLink to="/wissen" title="Wissen & SOPs" text="Wiki-artige Wissensbasis für RDWC, Addback und Fehlerbilder" />
              <SettingsLink to="/hardware" title="Sensoren" text="Sensorvertrauen, Kalibrierung und Wartung" />
            </div>
          </V1Section>
        </>
      )}
    </V1Page>
  )
}

function SettingsLink({ to, title, text }: { to: string; title: string; text: string }) {
  return <Link to={to} className="v1-settings-link"><strong>{title}</strong><span>{text}</span></Link>
}

function downloadJson(fileName: string, value: unknown) {
  const blob = new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

function formatApiError(caught: unknown, fallback: string) {
  return caught instanceof ApiRequestError ? caught.message : caught instanceof Error ? caught.message : fallback
}

export default SettingsPage
