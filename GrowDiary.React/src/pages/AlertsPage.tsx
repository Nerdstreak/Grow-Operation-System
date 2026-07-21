import { useEffect, useState } from 'react'
import { apiFetch } from '../api'
import type { TentDto } from '../types'
import type { AlertRuleDto, TentAlertRulesDto } from '../types/alert'
import { V1Page, V1Section, V1Card, V1Tabs, V1Field, V1Switch, V1Button, V1Alert, V1Empty } from '../components/v1'

type MetricDef = { key: string; label: string; unit: string; min: string; max: string }

const ALERT_METRICS: MetricDef[] = [
  { key: 'reservoir-ph', label: 'pH', unit: '', min: '5.5', max: '6.5' },
  { key: 'reservoir-ec', label: 'EC', unit: 'mS/cm', min: '1.2', max: '2.4' },
  { key: 'reservoir-temp', label: 'Wassertemperatur', unit: '°C', min: '18', max: '22' },
  { key: 'orp', label: 'ORP', unit: 'mV', min: '200', max: '400' },
  { key: 'dissolved-oxygen', label: 'Sauerstoff (DO)', unit: 'mg/L', min: '6', max: '' },
  { key: 'temperature', label: 'Lufttemperatur', unit: '°C', min: '20', max: '28' },
  { key: 'humidity', label: 'Luftfeuchte', unit: '%', min: '40', max: '65' },
  { key: 'vpd', label: 'VPD', unit: 'kPa', min: '0.8', max: '1.5' },
  { key: 'co2', label: 'CO₂', unit: 'ppm', min: '', max: '1500' },
]

type Row = { min: string; max: string; enabled: boolean }
type Rows = Record<string, Row>

function emptyRows(): Rows {
  return Object.fromEntries(ALERT_METRICS.map((metric) => [metric.key, { min: '', max: '', enabled: false }]))
}

function parseNumber(value: string): number | null {
  const normalized = value.trim().replace(',', '.')
  if (normalized === '') return null
  const parsed = Number(normalized)
  return Number.isFinite(parsed) ? parsed : null
}

function numberToInput(value: number | null): string {
  return value == null ? '' : String(value)
}

function errorMessage(caught: unknown, fallback: string): string {
  return caught instanceof Error ? caught.message : fallback
}

function AlertsPage() {
  const [tents, setTents] = useState<TentDto[]>([])
  const [selectedTentId, setSelectedTentId] = useState<number | null>(null)
  const [notifyOptions, setNotifyOptions] = useState<string[]>([])
  const [notifyService, setNotifyService] = useState('')
  const [cooldown, setCooldown] = useState('30')
  const [rows, setRows] = useState<Rows>(emptyRows())
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [testing, setTesting] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      try {
        const tentList = await apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal })
        if (controller.signal.aborted) return
        const sorted = [...tentList].sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name))
        setTents(sorted)
        setSelectedTentId((current) => current ?? sorted[0]?.id ?? null)
        const services = await apiFetch<string[]>('/api/alerts/notify-services', { signal: controller.signal }).catch(() => [])
        if (!controller.signal.aborted) setNotifyOptions(services)
      } catch (caught) {
        if (!controller.signal.aborted) setError(errorMessage(caught, 'Zelte konnten nicht geladen werden.'))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (selectedTentId == null) return
    const controller = new AbortController()
    async function loadRules(tentId: number) {
      setMessage(null)
      try {
        const dto = await apiFetch<TentAlertRulesDto>(`/api/alerts/tents/${tentId}`, { signal: controller.signal })
        if (controller.signal.aborted) return
        const next = emptyRows()
        for (const rule of dto.rules) {
          if (next[rule.metricKey]) {
            next[rule.metricKey] = { min: numberToInput(rule.minValue), max: numberToInput(rule.maxValue), enabled: rule.enabled }
          }
        }
        setRows(next)
        const first = dto.rules[0]
        if (first) {
          setNotifyService(first.notifyService)
          setCooldown(String(first.cooldownMinutes))
        }
      } catch (caught) {
        if (!controller.signal.aborted) setError(errorMessage(caught, 'Alarme konnten nicht geladen werden.'))
      }
    }
    void loadRules(selectedTentId)
    return () => controller.abort()
  }, [selectedTentId])

  function setRow(key: string, patch: Partial<Row>) {
    setRows((current) => ({ ...current, [key]: { ...current[key], ...patch } }))
  }

  const activeRules = ALERT_METRICS.filter((metric) => {
    const row = rows[metric.key]
    return row.enabled && (parseNumber(row.min) != null || parseNumber(row.max) != null)
  })

  async function save() {
    if (selectedTentId == null) return
    if (activeRules.length > 0 && notifyService.trim() === '') {
      setError('Bitte zuerst einen Push-Dienst wählen (z. B. deine Home-Assistant-App).')
      return
    }
    setSaving(true)
    setError(null)
    setMessage(null)
    const cooldownMinutes = Math.max(1, parseNumber(cooldown) ?? 30)
    const payload: { rules: AlertRuleDto[] } = {
      rules: activeRules.map((metric) => ({
        metricKey: metric.key,
        minValue: parseNumber(rows[metric.key].min),
        maxValue: parseNumber(rows[metric.key].max),
        notifyService: notifyService.trim(),
        enabled: true,
        cooldownMinutes,
      })),
    }
    try {
      await apiFetch<TentAlertRulesDto>(`/api/alerts/tents/${selectedTentId}`, {
        method: 'PUT',
        body: JSON.stringify(payload),
      })
      setMessage(activeRules.length === 0 ? 'Alle Alarme für dieses Zelt entfernt.' : `${activeRules.length} Alarm(e) gespeichert.`)
    } catch (caught) {
      setError(errorMessage(caught, 'Speichern fehlgeschlagen.'))
    } finally {
      setSaving(false)
    }
  }

  async function sendTest() {
    if (notifyService.trim() === '') {
      setError('Bitte zuerst einen Push-Dienst wählen.')
      return
    }
    setTesting(true)
    setError(null)
    setMessage(null)
    try {
      const result = await apiFetch<{ ok: boolean }>('/api/alerts/test', {
        method: 'POST',
        body: JSON.stringify({ notifyService: notifyService.trim() }),
      })
      setMessage(result.ok ? 'Test-Benachrichtigung gesendet — schau auf dein Handy.' : 'Home Assistant hat die Test-Nachricht nicht angenommen. Stimmt der Dienstname?')
    } catch (caught) {
      setError(errorMessage(caught, 'Test fehlgeschlagen.'))
    } finally {
      setTesting(false)
    }
  }

  if (loading) {
    return <V1Page eyebrow="Integration" title="Alarme & Push"><V1Card>Lädt…</V1Card></V1Page>
  }

  if (tents.length === 0) {
    return (
      <V1Page eyebrow="Integration" title="Alarme & Push">
        <V1Empty title="Noch kein Zelt" text="Lege zuerst ein Zelt an und mappe deine Sensoren, dann kannst du hier Grenzwerte setzen." />
      </V1Page>
    )
  }

  return (
    <V1Page
      eyebrow="Integration"
      title="Alarme & Push"
      subtitle="Setze Grenzwerte pro Sensor. Grow OS schickt dir über Home Assistant eine Push-Nachricht aufs Handy, sobald ein Wert über- oder unterschritten wird."
    >
      {tents.length > 1 && (
        <V1Tabs
          label="Zelt"
          active={selectedTentId ?? tents[0].id}
          onChange={(id) => setSelectedTentId(id)}
          items={tents.map((tent) => ({ value: tent.id, label: tent.name }))}
        />
      )}

      {error && <V1Alert message={error} tone="critical" />}
      {message && <V1Alert message={message} tone="ok" />}

      <V1Section title="Benachrichtigung">
        <V1Card>
          <V1Field label="Push-Dienst" hint="Deine Home-Assistant-App auf dem Handy, z. B. notify.mobile_app_pixel. Erscheint automatisch, wenn die App in HA angemeldet ist.">
            <input
              list="notify-services"
              value={notifyService}
              onChange={(event) => setNotifyService(event.target.value)}
              placeholder="notify.mobile_app_dein_handy"
            />
            <datalist id="notify-services">
              {notifyOptions.map((service) => <option key={service} value={service} />)}
            </datalist>
          </V1Field>
          <V1Field label="Ruhepause (Minuten)" hint="Mindestabstand zwischen wiederholten Meldungen desselben Alarms.">
            <input inputMode="numeric" value={cooldown} onChange={(event) => setCooldown(event.target.value)} placeholder="30" />
          </V1Field>
          <div style={{ marginTop: 12 }}>
            <V1Button variant="secondary" onClick={() => void sendTest()} disabled={testing}>
              {testing ? 'Sende…' : 'Test-Push senden'}
            </V1Button>
          </div>
        </V1Card>
      </V1Section>

      <V1Section
        title="Grenzwerte"
        action={<V1Button variant="primary" onClick={() => void save()} disabled={saving}>{saving ? 'Speichert…' : 'Speichern'}</V1Button>}
      >
        <div style={{ display: 'grid', gap: 12 }}>
          {ALERT_METRICS.map((metric) => {
            const row = rows[metric.key]
            return (
              <V1Card key={metric.key} tone={row.enabled ? 'neutral' : 'neutral'}>
                <V1Switch
                  label={`${metric.label}${metric.unit ? ` · ${metric.unit}` : ''}`}
                  checked={row.enabled}
                  onChange={(checked) => setRow(metric.key, { enabled: checked })}
                />
                {row.enabled && (
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginTop: 12 }}>
                    <V1Field label="Warnen unter" hint={metric.min ? `z. B. ${metric.min}` : 'optional'}>
                      <input inputMode="decimal" value={row.min} placeholder={metric.min} onChange={(event) => setRow(metric.key, { min: event.target.value })} />
                    </V1Field>
                    <V1Field label="Warnen über" hint={metric.max ? `z. B. ${metric.max}` : 'optional'}>
                      <input inputMode="decimal" value={row.max} placeholder={metric.max} onChange={(event) => setRow(metric.key, { max: event.target.value })} />
                    </V1Field>
                  </div>
                )}
              </V1Card>
            )
          })}
        </div>
      </V1Section>
    </V1Page>
  )
}

export default AlertsPage
