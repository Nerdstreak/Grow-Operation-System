import { useMemo, useState } from 'react'
import { V1Button, V1Card, V1Field, V1LinkButton, V1Page, V1Section } from '../components/v1'

const remoteStorageKey = 'grow-os.remote-base-url'

function DeviceConnectPage() {
  const detectedBaseUrl = useMemo(() => window.location.origin.replace(/\/$/, ''), [])
  const [remoteBaseUrl, setRemoteBaseUrl] = useState(() => window.localStorage.getItem(remoteStorageKey) ?? '')
  const [copied, setCopied] = useState<string | null>(null)

  const localUrls = buildUrls(detectedBaseUrl)
  const remoteUrls = remoteBaseUrl.trim() ? buildUrls(normalizeBaseUrl(remoteBaseUrl)) : null

  const saveRemoteUrl = () => {
    const normalized = normalizeBaseUrl(remoteBaseUrl)
    setRemoteBaseUrl(normalized)
    if (normalized) window.localStorage.setItem(remoteStorageKey, normalized)
    else window.localStorage.removeItem(remoteStorageKey)
  }

  const copy = async (label: string, value: string) => {
    await navigator.clipboard?.writeText(value)
    setCopied(label)
    window.setTimeout(() => setCopied(null), 1800)
  }

  return (
    <V1Page eyebrow="Selfhost" title="Gerät verbinden" subtitle="Lokale Nutzung zuerst. Remote nur über VPN, Tailscale, Cloudflare Access oder Reverse Proxy mit Schutz.">
      <V1Section title="Lokale Adresse">
        <div className="v1-card-grid">
          <ConnectCard label="Live" url={localUrls.live} onCopy={() => copy('Live lokal', localUrls.live)} />
          <ConnectCard label="Addback" url={localUrls.addback} onCopy={() => copy('Addback lokal', localUrls.addback)} />
          <ConnectCard label="Home Assistant" url={localUrls.homeAssistant} onCopy={() => copy('HA lokal', localUrls.homeAssistant)} />
        </div>
        {copied && <div className="v1-settings-note">{copied} kopiert.</div>}
      </V1Section>

      <V1Section title="Remote-Adresse">
        <div className="v1-form-grid">
          <V1Field label="Remote Base URL" hint="Beispiel: https://grow.example.de oder Tailscale/VPN-Adresse">
            <input value={remoteBaseUrl} onChange={(event) => setRemoteBaseUrl(event.target.value)} placeholder="https://grow.meinedomain.de" />
          </V1Field>
          <div className="v1-form-actions">
            <V1Button variant="primary" onClick={saveRemoteUrl}>Speichern</V1Button>
          </div>
        </div>
        {remoteUrls && (
          <div className="v1-card-grid">
            <ConnectCard label="Remote Live" url={remoteUrls.live} onCopy={() => copy('Remote Live', remoteUrls.live)} />
            <ConnectCard label="Remote Addback" url={remoteUrls.addback} onCopy={() => copy('Remote Addback', remoteUrls.addback)} />
          </div>
        )}
      </V1Section>

      <V1Section title="PWA installieren">
        <div className="v1-card-grid">
          <V1Card>
            <span className="v1-card-kicker">iPhone / Safari</span>
            <h2>Teilen → Zum Home-Bildschirm</h2>
            <p>Öffne die lokale oder Remote-Adresse in Safari, tippe auf Teilen und füge Grow OS zum Home-Bildschirm hinzu.</p>
          </V1Card>
          <V1Card>
            <span className="v1-card-kicker">Android / Chrome</span>
            <h2>Installieren</h2>
            <p>Öffne Grow OS in Chrome und nutze „App installieren“ oder „Zum Startbildschirm hinzufügen“.</p>
          </V1Card>
          <V1Card>
            <span className="v1-card-kicker">Sicherheit</span>
            <h2>Remote nur geschützt</h2>
            <p>Kein ungeschützter Port ins Internet. Nutze VPN/Tailscale/Cloudflare Access oder Auth am Reverse Proxy.</p>
          </V1Card>
        </div>
      </V1Section>

      <V1Section title="Schnellstart">
        <div className="v1-action-row">
          <V1LinkButton to="/" variant="primary">Live öffnen</V1LinkButton>
          <V1LinkButton to="/addback">Addback</V1LinkButton>
          <V1LinkButton to="/home-assistant">HA einrichten</V1LinkButton>
        </div>
      </V1Section>
    </V1Page>
  )
}

function ConnectCard({ label, url, onCopy }: { label: string; url: string; onCopy: () => void }) {
  return (
    <V1Card>
      <span className="v1-card-kicker">{label}</span>
      <h2>{shortenUrl(url)}</h2>
      <p>{url}</p>
      <V1Button variant="primary" onClick={onCopy}>Kopieren</V1Button>
    </V1Card>
  )
}

function buildUrls(baseUrl: string) {
  const base = normalizeBaseUrl(baseUrl)
  return {
    live: `${base}/`,
    addback: `${base}/addback`,
    homeAssistant: `${base}/home-assistant`,
  }
}

function normalizeBaseUrl(value: string) {
  return value.trim().replace(/\/$/, '')
}

function shortenUrl(value: string) {
  try {
    const parsed = new URL(value)
    return parsed.host
  } catch {
    return value
  }
}

export default DeviceConnectPage
