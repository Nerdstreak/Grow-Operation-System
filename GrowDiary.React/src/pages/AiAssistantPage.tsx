import { useCallback, useEffect, useState } from 'react'
import { apiFetch } from '../api'
import { V1Page, V1Section, V1Card, V1Field, V1Switch, V1Button, V1Alert, V1Badge, V1Empty } from '../components/v1'

type AiProvider = 'OpenAiCompatible' | 'Anthropic'

type AiSettings = {
  provider: AiProvider
  baseUrl: string | null
  model: string | null
  enabled: boolean
  allowPhotos: boolean
  hasApiKey: boolean
  isLocalEndpoint: boolean
  isConfigured: boolean
}

/**
 * The three ways people actually connect a model. Picking one fills in the address and a
 * sensible model, so the common case is: choose, paste key, save.
 */
const PRESETS: Array<{
  key: string
  label: string
  provider: AiProvider
  baseUrl: string
  model: string
  note: string
  needsKey: boolean
}> = [
  {
    key: 'anthropic',
    label: 'Claude (Anthropic)',
    provider: 'Anthropic',
    baseUrl: '',
    model: 'claude-sonnet-5',
    note: 'Schlüssel von console.anthropic.com. Die Adresse ist fest, du brauchst sie nicht.',
    needsKey: true,
  },
  {
    key: 'openai',
    label: 'OpenAI',
    provider: 'OpenAiCompatible',
    baseUrl: 'https://api.openai.com/v1',
    model: 'gpt-4o-mini',
    note: 'Schlüssel von platform.openai.com.',
    needsKey: true,
  },
  {
    key: 'local',
    label: 'Lokal (Ollama / LM Studio)',
    provider: 'OpenAiCompatible',
    baseUrl: 'http://localhost:11434/v1',
    model: 'llama3.1',
    note: 'Nichts verlässt dein Netzwerk. Braucht keinen Schlüssel — dafür einen Rechner, der das Modell trägt.',
    needsKey: false,
  },
  {
    key: 'custom',
    label: 'Anderer Dienst',
    provider: 'OpenAiCompatible',
    baseUrl: '',
    model: '',
    note: 'Alles, was die OpenAI-Schnittstelle spricht — OpenRouter, vLLM, ein eigener Server.',
    needsKey: false,
  },
]

type KnowledgeItem = {
  id: string
  kind: string
  title: string
  body: string
  sourceTitle: string | null
  sourceReference: string | null
}

type SendPreview = {
  growId: number
  wouldLeaveTheHouse: boolean
  endpoint: string | null
  growFacts: string[]
  measurements: string[]
  openDeviations: string[]
  knowledge: KnowledgeItem[]
  systemMessage: string
  userMessage: string
}

type GrowOption = { id: number; name: string }

type TestResult = { ok: boolean; errorCode: string | null; message: string | null; reply: string | null }

/** Which preset a stored connection came from, so reopening the page shows the right one. */
function matchPreset(settings: AiSettings): string {
  if (settings.provider === 'Anthropic') return 'anthropic'
  const url = (settings.baseUrl ?? '').toLowerCase()
  if (url.includes('api.openai.com')) return 'openai'
  if (settings.isLocalEndpoint) return 'local'
  return url === '' ? 'anthropic' : 'custom'
}

/**
 * Setting up the assistant, and — the part that matters — showing exactly what would be
 * sent before anything is.
 */
export function AiAssistantPage() {
  const [settings, setSettings] = useState<AiSettings | null>(null)
  const [presetKey, setPresetKey] = useState('anthropic')
  const [baseUrl, setBaseUrl] = useState('')
  const [model, setModel] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [enabled, setEnabled] = useState(false)
  const [allowPhotos, setAllowPhotos] = useState(false)

  const [grows, setGrows] = useState<GrowOption[]>([])
  const [growId, setGrowId] = useState<number | null>(null)
  const [preview, setPreview] = useState<SendPreview | null>(null)
  const [showRaw, setShowRaw] = useState(false)

  const [saving, setSaving] = useState(false)
  const [testing, setTesting] = useState(false)
  const [test, setTest] = useState<TestResult | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const applySettings = useCallback((data: AiSettings) => {
    setSettings(data)
    setBaseUrl(data.baseUrl ?? '')
    setModel(data.model ?? '')
    setEnabled(data.enabled)
    setAllowPhotos(data.allowPhotos)
    setApiKey('')
    setPresetKey(matchPreset(data))
  }, [])

  function choosePreset(key: string) {
    setPresetKey(key)
    const preset = PRESETS.find((item) => item.key === key)
    if (!preset || key === 'custom') return
    setBaseUrl(preset.baseUrl)
    setModel(preset.model)
  }

  const preset = PRESETS.find((item) => item.key === presetKey) ?? PRESETS[0]

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const [ai, growList] = await Promise.all([
          apiFetch<AiSettings>('/api/ai/settings', { signal: controller.signal }),
          apiFetch<GrowOption[]>('/api/grows', { signal: controller.signal }).catch(() => [] as GrowOption[]),
        ])
        if (controller.signal.aborted) return
        applySettings(ai)
        setGrows(growList)
        setGrowId((current) => current ?? growList[0]?.id ?? null)
      } catch {
        if (!controller.signal.aborted) setError('Die Einstellungen konnten nicht geladen werden.')
      }
    }
    void load()
    return () => controller.abort()
  }, [applySettings])

  useEffect(() => {
    if (growId == null) return
    const controller = new AbortController()
    async function load() {
      try {
        const data = await apiFetch<SendPreview>(`/api/ai/preview/${growId}`, { signal: controller.signal })
        if (!controller.signal.aborted) setPreview(data)
      } catch {
        if (!controller.signal.aborted) setPreview(null)
      }
    }
    void load()
    return () => controller.abort()
  }, [growId, settings])

  async function save() {
    setSaving(true)
    setError(null)
    setMessage(null)
    try {
      // An untouched key field means "leave the stored key alone" — the key never comes
      // back from the server, so it cannot be sent in again.
      const data = await apiFetch<AiSettings>('/api/ai/settings', {
        method: 'PUT',
        body: JSON.stringify({
          provider: preset.provider,
          baseUrl,
          model,
          enabled,
          allowPhotos,
          apiKey: apiKey === '' ? null : apiKey,
        }),
      })
      applySettings(data)
      setMessage('Gespeichert.')
    } catch {
      setError('Speichern fehlgeschlagen — stimmt die Adresse?')
    } finally {
      setSaving(false)
    }
  }

  async function runTest() {
    setTesting(true)
    setTest(null)
    try {
      setTest(await apiFetch<TestResult>('/api/ai/test', { method: 'POST' }))
    } catch {
      setTest({ ok: false, errorCode: null, message: 'Der Test konnte nicht ausgeführt werden.', reply: null })
    } finally {
      setTesting(false)
    }
  }

  // Three distinct states, because "nothing leaves your network" would be a false
  // reassurance while no endpoint is set up at all.
  const destination: 'none' | 'local' | 'remote' =
    !settings?.isConfigured ? 'none' : preview?.wouldLeaveTheHouse ? 'remote' : 'local'

  return (
    <V1Page
      eyebrow="Assistent"
      title="KI-Assistent"
      subtitle="Beantwortet Fragen zu deinem Grow anhand deiner Unterlagen — er schlägt vor, du entscheidest."
    >
      {error && <V1Alert tone="critical" message={error} />}
      {message && <V1Alert tone="ok" message={message} />}

      <V1Section title="Modell verbinden">
        <V1Card>
          <p className="v1-muted">
            Ohne Eintrag bleibt der Assistent aus und die App voll nutzbar. Es fallen Kosten beim
            gewählten Anbieter an — für den täglichen Betrieb üblicherweise Cent- bis niedrige
            Eurobeträge im Monat; lokal läuft es kostenlos.
          </p>

          <V1Field label="Anbieter" hint={preset.note}>
            <select value={presetKey} onChange={(event) => choosePreset(event.target.value)}>
              {PRESETS.map((item) => <option key={item.key} value={item.key}>{item.label}</option>)}
            </select>
          </V1Field>

          {/* Anthropic has exactly one address, so asking for it would only invite typos. */}
          {preset.provider !== 'Anthropic' && (
            <V1Field label="Adresse" hint="Endet auf /v1 — den Rest ergänzt Grow OS.">
              <input value={baseUrl} onChange={(event) => setBaseUrl(event.target.value)} placeholder="http://localhost:11434/v1" />
            </V1Field>
          )}

          <V1Field label="Modell" hint={`Vorbelegt: ${preset.model || '—'}. Jedes Modell des Anbieters ist möglich.`}>
            <input value={model} onChange={(event) => setModel(event.target.value)} placeholder={preset.model} />
          </V1Field>

          <V1Field
            label="Schlüssel"
            hint={settings?.hasApiKey
              ? 'Ein Schlüssel ist hinterlegt. Leer lassen behält ihn — er wird nie zurückgegeben.'
              : preset.needsKey
                ? 'Wird gebraucht.'
                : 'Lokale Dienste brauchen meist keinen.'}
          >
            <input
              type="password"
              value={apiKey}
              onChange={(event) => setApiKey(event.target.value)}
              placeholder={settings?.hasApiKey ? '••••••••' : ''}
              autoComplete="off"
            />
          </V1Field>

          <V1Switch
            label="Assistent aktiv"
            checked={enabled}
            onChange={setEnabled}
            hint="Aus heißt: der Assistent taucht nirgends auf."
          />
          <V1Switch
            label="Fotos mitschicken erlauben"
            checked={allowPhotos}
            onChange={setAllowPhotos}
            hint="Ein Bild deines Grow-Raums ist etwas anderes als ein pH-Wert. Bewusst getrennt schaltbar."
          />

          <div className="v1-action-row">
            <V1Button variant="primary" onClick={() => void save()} disabled={saving}>
              {saving ? 'Speichert…' : 'Speichern'}
            </V1Button>
            <V1Button onClick={() => void runTest()} disabled={testing || !settings?.isConfigured}>
              {testing ? 'Testet…' : 'Verbindung testen'}
            </V1Button>
          </div>

          {test && (
            <V1Alert
              tone={test.ok ? 'ok' : 'critical'}
              title={test.ok ? 'Verbindung steht' : 'Verbindung fehlgeschlagen'}
              message={test.ok ? `Antwort des Modells: „${test.reply ?? ''}"` : (test.message ?? 'Unbekannter Fehler.')}
            />
          )}
        </V1Card>
      </V1Section>

      <V1Section
        title="Was gesendet wird"
        action={grows.length > 1 ? (
          <select value={growId ?? ''} onChange={(event) => setGrowId(Number(event.target.value))} aria-label="Grow">
            {grows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name}</option>)}
          </select>
        ) : undefined}
      >
        {!preview ? (
          <V1Empty title="Keine Vorschau" text="Lege einen Grow an, dann steht hier, was den Rechner verlassen würde." />
        ) : (
          <V1Card>
            <V1Alert
              tone={destination === 'remote' ? 'warn' : destination === 'local' ? 'ok' : 'neutral'}
              title={
                destination === 'remote' ? 'Diese Daten gehen an den Anbieter'
                : destination === 'local' ? 'Nichts verlässt dein Netzwerk'
                : 'Noch kein Modell verbunden'
              }
              message={
                destination === 'remote'
                  ? `Ziel: ${preview.endpoint}. Genau das Folgende wird übertragen — nicht mehr.`
                  : destination === 'local'
                    ? `Die Adresse ${preview.endpoint} ist lokal, die Anfrage bleibt im Haus.`
                    : 'Es wird nichts gesendet. So sähe eine Anfrage aus, sobald du oben ein Modell einträgst.'
              }
            />

            <p className="v1-muted">
              Nie enthalten: deine Home-Assistant-Zugangsdaten, deren Konfiguration, Geräte- oder Adressdaten.
              Zusammengestellt wird nur, was hier steht.
            </p>

            <h4>Dein Grow</h4>
            <ul className="v1-list">
              {preview.growFacts.map((fact) => <li key={fact}>{fact}</li>)}
            </ul>

            <h4>Messwerte ({preview.measurements.length})</h4>
            {preview.measurements.length === 0
              ? <p className="v1-muted">Keine.</p>
              : <ul className="v1-list">{preview.measurements.map((row) => <li key={row}>{row}</li>)}</ul>}

            {preview.openDeviations.length > 0 && (
              <>
                <h4>Offene Abweichungen</h4>
                <ul className="v1-list">{preview.openDeviations.map((row) => <li key={row}>{row}</li>)}</ul>
              </>
            )}

            <h4>
              Unterlagen ({preview.knowledge.length}) <V1Badge tone="ok">zitierpflichtig</V1Badge>
            </h4>
            <p className="v1-muted">
              Der Assistent muss seine Aussagen auf diese Einträge stützen. Nennt er etwas anderes als Quelle,
              erkennt Grow OS das und kennzeichnet die Aussage als ungedeckt.
            </p>
            <ul className="v1-list">
              {preview.knowledge.map((item) => (
                <li key={item.id}>
                  <strong>{item.title}</strong> <V1Badge>{item.kind}</V1Badge>
                  {item.sourceTitle && (
                    <span className="v1-muted"> — {item.sourceTitle}{item.sourceReference ? `, ${item.sourceReference}` : ''}</span>
                  )}
                </li>
              ))}
            </ul>

            <V1Button onClick={() => setShowRaw((current) => !current)}>
              {showRaw ? 'Wortlaut ausblenden' : 'Genauen Wortlaut anzeigen'}
            </V1Button>
            {showRaw && (
              <pre className="v1-pre" data-audit="ai-raw-payload">{preview.systemMessage}{'\n\n'}{preview.userMessage}</pre>
            )}
          </V1Card>
        )}
      </V1Section>
    </V1Page>
  )
}

export default AiAssistantPage
