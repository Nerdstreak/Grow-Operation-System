import { useEffect, useState } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary, SettingsOverviewDto } from '../types'
import { V1Alert, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section } from '../components/v1'

type ImportPreview = { ok: boolean; title: string; details: string[] }

function SettingsPage() {
  const [settings, setSettings] = useState<SettingsOverviewDto | null>(null)
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [importFileName, setImportFileName] = useState('')
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

  async function createFullBackup() {
    setError(null)
    setMessage(null)
    try {
      const response = await fetch('/api/system/backup', { method: 'POST' })
      if (!response.ok) throw new Error(`Backup fehlgeschlagen (${response.status})`)
      const blob = await response.blob()
      const disposition = response.headers.get('content-disposition') ?? ''
      const match = /filename="?([^"]+)"?/i.exec(disposition)
      downloadBlob(match?.[1] ?? `grow-os-backup-${new Date().toISOString().slice(0, 10)}.zip`, blob)
      setMessage('Vollbackup wurde erstellt.')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Vollbackup konnte nicht erstellt werden.')
    }
  }

  function exportSystemConfig() {
    if (!settings) return
    downloadJson(`grow-os-system-config-${new Date().toISOString().slice(0, 10)}.json`, {
      schema: 'grow-os.system-config.v1',
      exportedAtUtc: new Date().toISOString(),
      homeAssistant: { enabled: settings.homeAssistant.enabled, baseUrl: settings.homeAssistant.baseUrl, tokenStored: Boolean(settings.homeAssistant.accessToken) },
      tents: settings.tents,
    })
    setMessage('System-Konfiguration exportiert.')
  }

  function exportGrowIndex() {
    downloadJson(`grow-os-grows-${new Date().toISOString().slice(0, 10)}.json`, {
      schema: 'grow-os.grow-index.v1',
      exportedAtUtc: new Date().toISOString(),
      grows,
    })
    setMessage('Grow-Index exportiert.')
  }

  function exportHaMapping() {
    if (!settings) return
    downloadJson(`grow-os-ha-mapping-${new Date().toISOString().slice(0, 10)}.json`, {
      schema: 'grow-os.ha-mapping.v1',
      exportedAtUtc: new Date().toISOString(),
      homeAssistant: { enabled: settings.homeAssistant.enabled, baseUrl: settings.homeAssistant.baseUrl },
      tents: settings.tents.map((tent) => ({
        name: tent.name,
        cameraEntityId: tent.cameraEntityId,
        sensors: tent.sensors.filter((sensor) => sensor.isActive || sensor.haEntityId),
      })),
    })
    setMessage('HA-Mapping exportiert.')
  }

  async function handleFile(file: File | null) {
    setPreview(null)
    setImportText('')
    setImportFileName('')
    if (!file) return
    setImportFileName(file.name)
    const text = await file.text()
    setImportText(text)
    inspectImport(text, file.name)
  }

  function inspectImport(text = importText, name = importFileName) {
    try {
      const parsed = JSON.parse(text) as Record<string, unknown>
      const schema = typeof parsed.schema === 'string' ? parsed.schema : 'unbekannt'
      setPreview({
        ok: true,
        title: schema,
        details: [
          `Datei: ${name || 'unbekannt'}`,
          Array.isArray(parsed.tents) ? `${parsed.tents.length} Zelt-/Mapping-Einträge` : 'keine Zeltliste',
          Array.isArray(parsed.grows) ? `${parsed.grows.length} Grow-Einträge` : 'keine Growliste',
          'Schreibender Import wird erst nach expliziter Restore-Bestätigung aktiviert.',
        ],
      })
    } catch {
      setPreview({ ok: false, title: 'Ungültiges JSON', details: [`Datei: ${name || 'unbekannt'}`, 'Syntaxprüfung fehlgeschlagen.'] })
    }
  }

  return (
    <V1Page eyebrow="Admin" title="Einstellungen" subtitle="Backup, Export und Import. Keine Link-Sammlung, keine manuelle JSON-Eingabe.">
      {error && <V1Alert title="Fehler" message={error} tone="warn" />}
      {message && <V1Alert message={message} tone="ok" />}

      {loading ? <V1Empty title="Lade System..." /> : (
        <>
          <section className="v1-kpi-grid rc2-admin-kpis">
            <V1Card><span className="v1-card-kicker">Zelte</span><h2>{settings?.tents.length ?? 0}</h2><p>Systemräume</p></V1Card>
            <V1Card><span className="v1-card-kicker">Grows</span><h2>{grows.length}</h2><p>aktiv/geplant</p></V1Card>
            <V1Card tone={settings?.homeAssistant.enabled ? 'ok' : 'warn'}><span className="v1-card-kicker">HA</span><h2>{settings?.homeAssistant.enabled ? 'aktiv' : 'aus'}</h2><p>{settings?.homeAssistant.baseUrl || 'keine URL'}</p></V1Card>
            <V1Card><span className="v1-card-kicker">Backup</span><h2>ZIP</h2><p>DB + Uploads + Knowledge</p></V1Card>
          </section>

          <V1Section title="Backup & Export">
            <div className="rc2-admin-grid">
              <V1Card className="rc2-admin-card tone-ok">
                <span className="v1-card-kicker">Vollbackup</span>
                <h2>Backup erstellen</h2>
                <p>Erstellt ein ZIP mit SQLite-Datenbank, Uploads und Knowledge-Daten. Das ist die richtige Sicherung für Restore/Umzug.</p>
                <V1Button variant="primary" onClick={() => void createFullBackup()}>Vollbackup herunterladen</V1Button>
              </V1Card>

              <V1Card className="rc2-admin-card">
                <span className="v1-card-kicker">Konfiguration</span>
                <h2>Systemdaten exportieren</h2>
                <p>Zelte, HA-Mapping und Grow-Index als JSON für Kontrolle oder Austausch.</p>
                <div className="v1-action-row">
                  <V1Button onClick={exportSystemConfig}>System JSON</V1Button>
                  <V1Button onClick={exportHaMapping}>HA-Mapping</V1Button>
                  <V1Button onClick={exportGrowIndex}>Grow-Index</V1Button>
                </div>
              </V1Card>
            </div>
          </V1Section>

          <V1Section title="Import prüfen">
            <div className="rc2-admin-grid two">
              <V1Card>
                <span className="v1-card-kicker">Datei auswählen</span>
                <h2>Import-Datei prüfen</h2>
                <V1Field label="JSON-Datei">
                  <input type="file" accept="application/json,.json" onChange={(event) => void handleFile(event.target.files?.[0] ?? null)} />
                </V1Field>
                <p>Kein manuelles JSON-Feld. Erst Datei auswählen, dann Syntax und Schema prüfen.</p>
              </V1Card>

              <V1Card tone={preview?.ok ? 'ok' : preview ? 'warn' : 'neutral'}>
                <span className="v1-card-kicker">Vorschau</span>
                <h2>{preview?.title ?? 'Noch keine Datei geprüft'}</h2>
                {preview ? preview.details.map((detail) => <p key={detail}>{detail}</p>) : <p>Die Vorschau erscheint nach Dateiauswahl.</p>}
              </V1Card>
            </div>
          </V1Section>
        </>
      )}
    </V1Page>
  )
}

function downloadJson(fileName: string, value: unknown) {
  const blob = new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' })
  downloadBlob(fileName, blob)
}

function downloadBlob(fileName: string, blob: Blob) {
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
