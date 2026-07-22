import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../api'
import type { AutoMeasurementConfigDto, AutoMeasurementTriggerKind, GrowSummary, HardwareItemDto } from '../types'
import { V1Page, V1Section, V1Card, V1Badge, V1Alert, V1Empty, V1LinkButton } from '../components/v1'

const TRIGGER_LABEL: Record<AutoMeasurementTriggerKind, string> = {
  Manual: 'Manuell',
  LightOnDelay: 'Nach Licht AN',
  LightOffDelay: 'Nach Licht AUS',
}

function triggerText(config: AutoMeasurementConfigDto): string {
  const base = TRIGGER_LABEL[config.triggerKind] ?? config.triggerKind
  return config.triggerKind !== 'Manual' && config.delayMinutes != null ? `${base} · ${config.delayMinutes} Min` : base
}

function errorMessage(caught: unknown, fallback: string): string {
  return caught instanceof Error ? caught.message : fallback
}

function AutomationOverviewPage() {
  const [configs, setConfigs] = useState<AutoMeasurementConfigDto[]>([])
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [hardware, setHardware] = useState<HardwareItemDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      try {
        const [configList, growList, hardwareList] = await Promise.all([
          apiFetch<AutoMeasurementConfigDto[]>('/api/auto-measurements/configs', { signal: controller.signal }),
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }),
          apiFetch<HardwareItemDto[]>('/api/hardware-items', { signal: controller.signal }),
        ])
        if (controller.signal.aborted) return
        setConfigs(configList)
        setGrows(growList)
        setHardware(hardwareList)
      } catch (caught) {
        if (!controller.signal.aborted) setError(errorMessage(caught, 'Automatik konnte nicht geladen werden.'))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  const growName = (id: number) => grows.find((grow) => grow.id === id)?.name ?? `Grow #${id}`
  const calibrated = hardware.filter((item) => item.calibrationIntervalDays != null)

  if (loading) {
    return <V1Page eyebrow="Integration" title="Automatik"><V1Card>Lädt…</V1Card></V1Page>
  }

  return (
    <V1Page
      eyebrow="Integration"
      title="Automatik"
      subtitle="Alles, was Grow OS automatisch für dich tut — an einem Ort. Zum Einrichten geht es jeweils zur passenden Stelle."
    >
      {error && <V1Alert message={error} tone="critical" />}

      <section className="v1-kpi-grid">
        <V1Card><span className="v1-card-kicker">Grenzwerte &amp; Alarme</span><h2>Push bei Über-/Unterschreitung</h2><p><Link to="/alarme">→ Grenzwerte einstellen</Link></p></V1Card>
        <V1Card><span className="v1-card-kicker">Benachrichtigungen</span><h2>Handy, Ruhezeiten, Digest</h2><p><Link to="/benachrichtigungen">→ Benachrichtigungen</Link></p></V1Card>
      </section>

      <V1Section title="Auto-Messungen">
        {configs.length === 0 ? (
          <V1Empty title="Noch keine Auto-Messung" text="Auto-Messungen erfassen Sensorwerte automatisch per Trigger (z. B. 30 Min nach Licht AN). Du legst sie im jeweiligen Grow im Tab Automation an." action={grows[0] ? <V1LinkButton to={`/grows/${grows[0].id}`} variant="primary">Zum Grow</V1LinkButton> : undefined} />
        ) : (
          <div style={{ display: 'grid', gap: 10 }}>
            {configs.map((config) => (
              <V1Card key={config.id}>
                <div className="v1-card-title-row">
                  <div><span className="v1-card-kicker">{growName(config.growId)}</span><h2>{config.name}</h2></div>
                  <V1Badge tone={config.status === 'Enabled' ? 'ok' : 'neutral'}>{config.status === 'Enabled' ? 'aktiv' : 'aus'}</V1Badge>
                </div>
                <p>{triggerText(config)}{config.captureSnapshot ? ' · mit Kamera-Snapshot' : ''}</p>
                <div className="v1-action-row"><V1LinkButton to={`/grows/${config.growId}`}>Im Grow bearbeiten</V1LinkButton></div>
              </V1Card>
            ))}
          </div>
        )}
      </V1Section>

      <V1Section title="Kalibrierung" action={<V1LinkButton to="/hardware">Sensoren</V1LinkButton>}>
        {calibrated.length === 0 ? (
          <V1Empty title="Keine Kalibrier-Intervalle" text="Setze bei einem Sensor unter Sensoren ein Kalibrier-Intervall — Grow OS erinnert dich dann rechtzeitig per Push." />
        ) : (
          <div style={{ display: 'grid', gap: 10 }}>
            {calibrated.map((item) => (
              <V1Card key={item.id}>
                <div className="v1-card-title-row">
                  <div><span className="v1-card-kicker">Kalibrierung</span><h2>{item.name}</h2></div>
                  <V1Badge tone="neutral">alle {item.calibrationIntervalDays} Tage</V1Badge>
                </div>
                <div className="v1-action-row"><V1LinkButton to="/hardware">Bei Sensoren bearbeiten</V1LinkButton></div>
              </V1Card>
            ))}
          </div>
        )}
      </V1Section>
    </V1Page>
  )
}

export default AutomationOverviewPage
