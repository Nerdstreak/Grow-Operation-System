import { useEffect, useMemo, useState } from 'react'
import { apiFetch } from '../api'
import { V1Alert, V1Badge, V1Button, V1Card, V1Field, V1Page, V1Section } from '../components/v1'

const remoteStorageKey = 'grow-os.remote-base-url'

type UrlSet = {
  live: string
  addback: string
  manualMeasurement: string
  homeAssistant: string
}

type NetworkAddressDto = {
  label: string
  host: string
  url: string
  isLoopback: boolean
  isPrivate: boolean
  isCurrent: boolean
}

type NetworkOverviewDto = {
  requestOrigin: string
  recommendedBaseUrl: string
  apiBaseUrl: string
  localAddresses: NetworkAddressDto[]
  warnings: string[]
}

type QrVersion = {
  version: number
  size: number
  dataCodewords: number
  eccCodewords: number
  alignment: number[]
}

const qrVersions: QrVersion[] = [
  { version: 1, size: 21, dataCodewords: 19, eccCodewords: 7, alignment: [] },
  { version: 2, size: 25, dataCodewords: 34, eccCodewords: 10, alignment: [6, 18] },
  { version: 3, size: 29, dataCodewords: 55, eccCodewords: 15, alignment: [6, 22] },
  { version: 4, size: 33, dataCodewords: 80, eccCodewords: 20, alignment: [6, 26] },
  { version: 5, size: 37, dataCodewords: 108, eccCodewords: 26, alignment: [6, 30] },
]

function DeviceConnectPage() {
  const browserOrigin = useMemo(() => window.location.origin.replace(/\/$/, ''), [])
  const [network, setNetwork] = useState<NetworkOverviewDto | null>(null)
  const [networkError, setNetworkError] = useState<string | null>(null)
  const [remoteBaseUrl, setRemoteBaseUrl] = useState(() => window.localStorage.getItem(remoteStorageKey) ?? '')
  const [copied, setCopied] = useState<string | null>(null)
  const [selectedQr, setSelectedQr] = useState<keyof UrlSet>('live')

  useEffect(() => {
    const controller = new AbortController()

    async function loadNetwork() {
      try {
        const overview = await apiFetch<NetworkOverviewDto>(`/api/system/network?frontendOrigin=${encodeURIComponent(browserOrigin)}`, { signal: controller.signal })
        if (!controller.signal.aborted) setNetwork(overview)
      } catch (caught) {
        if (!controller.signal.aborted) setNetworkError(caught instanceof Error ? caught.message : 'Netzwerkdaten konnten nicht geladen werden.')
      }
    }

    void loadNetwork()
    return () => controller.abort()
  }, [browserOrigin])

  const activeBaseUrl = normalizeBaseUrl(remoteBaseUrl) || browserOrigin
  const urls = buildUrls(activeBaseUrl)
  const qrValue = urls[selectedQr]

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
    <V1Page eyebrow="PWA" title="Gerät verbinden" subtitle="Adresse wählen, QR scannen, verbinden." className="device-connect-page">
      {networkError && <V1Alert title="Netzwerk-Erkennung" message={networkError} tone="warn" />}
      {network?.warnings.map((warning) => <div key={warning} className="device-connect-note"><strong>Hinweis</strong><span>{warning}</span></div>)}

      <V1Section title="Adresse">
        <div className="v1-card-grid">
          <V1Card tone={remoteBaseUrl.trim() ? 'ok' : 'neutral'}>
            <span className="v1-card-kicker">Aktive Adresse</span>
            <h2>{shortenUrl(activeBaseUrl)}</h2>
            <p>{activeBaseUrl}</p>
            <div className="v1-action-row">
              <V1Badge tone={remoteBaseUrl.trim() ? 'ok' : 'neutral'}>{remoteBaseUrl.trim() ? 'Remote/VPN' : 'aktueller Browser'}</V1Badge>
              <V1Button onClick={() => copy('Adresse', activeBaseUrl)}>Kopieren</V1Button>
            </div>
          </V1Card>

          <V1Card>
            <span className="v1-card-kicker">Remote / VPN / Tunnel</span>
            <h2>Adresse selbst festlegen</h2>
            <V1Field label="Base URL" hint="Nur nötig, wenn Handy/Tablet nicht dieselbe Browser-Adresse nutzen soll.">
              <input value={remoteBaseUrl} onChange={(event) => setRemoteBaseUrl(event.target.value)} placeholder="https://grow.example.de oder http://192.168.178.50:5000" />
            </V1Field>
            <div className="v1-action-row"><V1Button variant="primary" onClick={saveRemoteUrl}>Speichern</V1Button><V1Button onClick={() => { setRemoteBaseUrl(''); window.localStorage.removeItem(remoteStorageKey) }}>Zurücksetzen</V1Button></div>
          </V1Card>
        </div>
      </V1Section>

      <V1Section title="QR-Code">
        <div className="v1-card-grid">
          <V1Card tone="neutral">
            <span className="v1-card-kicker">Öffnen</span>
            <h2>{selectedQr === 'live' ? 'Live' : selectedQr === 'addback' ? 'Addback' : selectedQr === 'manualMeasurement' ? 'Messung' : 'Home Assistant'}</h2>
            <div className="v1-action-row">
              <V1Button variant={selectedQr === 'live' ? 'primary' : 'secondary'} onClick={() => setSelectedQr('live')}>Live</V1Button>
              <V1Button variant={selectedQr === 'addback' ? 'primary' : 'secondary'} onClick={() => setSelectedQr('addback')}>Addback</V1Button>
              <V1Button variant={selectedQr === 'manualMeasurement' ? 'primary' : 'secondary'} onClick={() => setSelectedQr('manualMeasurement')}>Messung</V1Button>
              <V1Button variant={selectedQr === 'homeAssistant' ? 'primary' : 'secondary'} onClick={() => setSelectedQr('homeAssistant')}>HA</V1Button>
            </div>
            <div style={{ display: 'grid', placeItems: 'center', padding: 16 }}><QrCode value={qrValue} /></div>
            <div className="v1-settings-note">{qrValue}</div>
            <V1Button variant="primary" onClick={() => copy('QR-Link', qrValue)}>Link kopieren</V1Button>
          </V1Card>

          <V1Card>
            <span className="v1-card-kicker">PWA</span>
            <h2>Zum Home-Bildschirm</h2>
            <p>iPhone: Safari öffnen → Teilen → Zum Home-Bildschirm.</p>
            <p>Android: Chrome öffnen → App installieren / Zum Startbildschirm.</p>
          </V1Card>
        </div>
        {copied && <div className="v1-settings-note">{copied} kopiert.</div>}
      </V1Section>
    </V1Page>
  )
}

function QrCode({ value }: { value: string }) {
  const result = useMemo(() => {
    try { return createQrMatrix(value) } catch { return null }
  }, [value])

  if (!result) return <div className="v1-empty"><strong>QR-Code zu lang</strong><span>Bitte kopiere den Link manuell.</span></div>

  const quiet = 4
  const size = result.length + quiet * 2
  const path = result.flatMap((row, y) => row.map((dark, x) => dark ? `M${x + quiet},${y + quiet}h1v1h-1z` : '')).filter(Boolean).join('')
  return <svg role="img" aria-label={`QR-Code für ${value}`} viewBox={`0 0 ${size} ${size}`} width="220" height="220" style={{ maxWidth: '100%', borderRadius: 16, background: '#fff', padding: 12 }}><rect width={size} height={size} fill="#fff" /><path d={path} fill="#000" /></svg>
}

function createQrMatrix(text: string) {
  const bytes = Array.from(new TextEncoder().encode(text))
  const version = qrVersions.find((candidate) => 4 + 8 + bytes.length * 8 <= candidate.dataCodewords * 8)
  if (!version) throw new Error('QR payload too long')
  const data = buildDataCodewords(bytes, version.dataCodewords)
  const ecc = reedSolomonRemainder(data, version.eccCodewords)
  const bits = [...data, ...ecc].flatMap((byte) => byteToBits(byte))
  const matrix = Array.from({ length: version.size }, () => Array<boolean | null>(version.size).fill(null))
  const reserved = Array.from({ length: version.size }, () => Array<boolean>(version.size).fill(false))
  const set = (x: number, y: number, dark: boolean, reserve = true) => { if (x < 0 || y < 0 || x >= version.size || y >= version.size) return; matrix[y][x] = dark; if (reserve) reserved[y][x] = true }
  const reserveOnly = (x: number, y: number) => { if (x < 0 || y < 0 || x >= version.size || y >= version.size) return; reserved[y][x] = true }
  drawFinder(set, 0, 0); drawFinder(set, version.size - 7, 0); drawFinder(set, 0, version.size - 7); drawTiming(set, version.size); drawAlignment(set, version); set(8, 4 * version.version + 9, true); reserveFormatAreas(reserveOnly, version.size)
  let bitIndex = 0
  let upward = true
  for (let right = version.size - 1; right >= 1; right -= 2) {
    if (right === 6) right--
    for (let vertical = 0; vertical < version.size; vertical++) {
      const y = upward ? version.size - 1 - vertical : vertical
      for (let dx = 0; dx < 2; dx++) {
        const x = right - dx
        if (reserved[y][x]) continue
        const raw = bitIndex < bits.length ? bits[bitIndex] : false
        bitIndex++
        const masked = ((x + y) % 2 === 0) ? !raw : raw
        set(x, y, masked, false)
      }
    }
    upward = !upward
  }
  drawFormatBits(set, version.size, 0)
  return matrix.map((row) => row.map(Boolean))
}

function buildDataCodewords(bytes: number[], dataCodewords: number) { const bits = [false, true, false, false, ...byteToBits(bytes.length), ...bytes.flatMap(byteToBits)]; const capacity = dataCodewords * 8; const terminator = Math.min(4, capacity - bits.length); for (let i = 0; i < terminator; i++) bits.push(false); while (bits.length % 8 !== 0) bits.push(false); const codewords: number[] = []; for (let i = 0; i < bits.length; i += 8) codewords.push(bitsToByte(bits.slice(i, i + 8))); const pads = [0xec, 0x11]; let padIndex = 0; while (codewords.length < dataCodewords) { codewords.push(pads[padIndex % 2]); padIndex++ } return codewords }
function byteToBits(byte: number) { return Array.from({ length: 8 }, (_, index) => ((byte >>> (7 - index)) & 1) !== 0) }
function bitsToByte(bits: boolean[]) { return bits.reduce((value, bit) => (value << 1) | (bit ? 1 : 0), 0) }
function drawFinder(set: (x: number, y: number, dark: boolean, reserve?: boolean) => void, x: number, y: number) { for (let dy = -1; dy <= 7; dy++) for (let dx = -1; dx <= 7; dx++) { const inCore = dx >= 0 && dx <= 6 && dy >= 0 && dy <= 6; const dark = inCore && (dx === 0 || dx === 6 || dy === 0 || dy === 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4)); set(x + dx, y + dy, dark) } }
function drawTiming(set: (x: number, y: number, dark: boolean, reserve?: boolean) => void, size: number) { for (let i = 8; i < size - 8; i++) { const dark = i % 2 === 0; set(i, 6, dark); set(6, i, dark) } }
function drawAlignment(set: (x: number, y: number, dark: boolean, reserve?: boolean) => void, version: QrVersion) { for (const y of version.alignment) for (const x of version.alignment) { const nearFinder = (x <= 8 && y <= 8) || (x >= version.size - 9 && y <= 8) || (x <= 8 && y >= version.size - 9); if (nearFinder) continue; for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) set(x + dx, y + dy, Math.max(Math.abs(dx), Math.abs(dy)) !== 1) } }
function reserveFormatAreas(reserve: (x: number, y: number) => void, size: number) { for (let i = 0; i < 9; i++) { reserve(8, i); reserve(i, 8) } for (let i = 0; i < 8; i++) { reserve(size - 1 - i, 8); reserve(8, size - 1 - i) } }
function drawFormatBits(set: (x: number, y: number, dark: boolean, reserve?: boolean) => void, size: number, mask: number) { const bits = makeFormatBits(mask); const first = [[8,0],[8,1],[8,2],[8,3],[8,4],[8,5],[8,7],[8,8],[7,8],[5,8],[4,8],[3,8],[2,8],[1,8],[0,8]]; const second = [[size-1,8],[size-2,8],[size-3,8],[size-4,8],[size-5,8],[size-6,8],[size-7,8],[size-8,8],[8,size-7],[8,size-6],[8,size-5],[8,size-4],[8,size-3],[8,size-2],[8,size-1]]; for (let i = 0; i < 15; i++) { const dark = ((bits >>> i) & 1) !== 0; set(first[i][0], first[i][1], dark); set(second[i][0], second[i][1], dark) } }
function makeFormatBits(mask: number) { const data = (1 << 3) | mask; let bits = data << 10; for (let i = 14; i >= 10; i--) if (((bits >>> i) & 1) !== 0) bits ^= 0x537 << (i - 10); return ((data << 10) | (bits & 0x3ff)) ^ 0x5412 }
function reedSolomonRemainder(data: number[], degree: number) { const generator = reedSolomonGenerator(degree); const result = Array(degree).fill(0); for (const byte of data) { const factor = byte ^ result.shift()!; result.push(0); for (let i = 0; i < degree; i++) result[i] ^= gfMultiply(generator[i + 1], factor) } return result }
function reedSolomonGenerator(degree: number) { let result = [1]; for (let i = 0; i < degree; i++) result = polyMultiply(result, [1, gfExp(i)]); return result }
function polyMultiply(left: number[], right: number[]) { const result = Array(left.length + right.length - 1).fill(0); for (let i = 0; i < left.length; i++) for (let j = 0; j < right.length; j++) result[i + j] ^= gfMultiply(left[i], right[j]); return result }
function gfExp(power: number) { let value = 1; for (let i = 0; i < power; i++) { value <<= 1; if (value & 0x100) value ^= 0x11d } return value }
function gfMultiply(left: number, right: number) { if (left === 0 || right === 0) return 0; let result = 0; let a = left; let b = right; while (b > 0) { if (b & 1) result ^= a; a <<= 1; if (a & 0x100) a ^= 0x11d; b >>>= 1 } return result }
function buildUrls(baseUrl: string): UrlSet { const base = normalizeBaseUrl(baseUrl); return { live: `${base}/`, addback: `${base}/addback`, manualMeasurement: `${base}/messung`, homeAssistant: `${base}/home-assistant` } }
function normalizeBaseUrl(value: string) { return value.trim().replace(/\/$/, '') }
function shortenUrl(value: string) { try { return new URL(value).host } catch { return value } }
export default DeviceConnectPage
