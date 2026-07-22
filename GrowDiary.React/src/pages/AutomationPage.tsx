import { useEffect, useState } from 'react'
import { apiFetch } from '../api'
import type { AutoMeasurementConfigDto, AutoMeasurementFieldMappingUpsertRequest, AutoMeasurementTriggerKind } from '../types'
import { autoMeasurementFields, defaultMetricKeyByField } from '../features/grow-detail/grow-detail-model'
import { GrowScopePicker } from '../features/grow-scope/GrowScopePicker'
import { useSelectedGrow } from '../features/grow-scope/useSelectedGrow'
import { V1Page, V1Card, V1Alert, V1Empty, V1Switch } from '../components/v1'

type Template = {
  triggerKind: Extract<AutoMeasurementTriggerKind, 'LightOnDelay' | 'LightOffDelay'>
  title: string
  description: string
  delayMinutes: number
  windowMinutes: number
}

// Ready-made templates the user just switches on or off. No entities, no metric keys,
// no aggregation — the measurement simply reads whatever sensors are already mapped.
const TEMPLATES: Template[] = [
  {
    triggerKind: 'LightOnDelay',
    title: 'Messung 30 Min nach Licht AN',
    description: 'Erfasst rund 30 Minuten nachdem das Licht angeht automatisch deine Sensorwerte.',
    delayMinutes: 30,
    windowMinutes: 15,
  },
  {
    triggerKind: 'LightOffDelay',
    title: 'Messung 30 Min nach Licht AUS',
    description: 'Erfasst rund 30 Minuten nachdem das Licht ausgeht automatisch deine Sensorwerte.',
    delayMinutes: 30,
    windowMinutes: 15,
  },
]

// Map every standard field to its default live metric key, so the auto-measurement
// captures whatever sensors the user has mapped in Home Assistant — the rest stay empty.
const AUTO_MAPPINGS: AutoMeasurementFieldMappingUpsertRequest[] = autoMeasurementFields.map((field) => ({
  measurementField: field,
  metricKey: defaultMetricKeyByField[field],
  aggregation: 'Average',
  isRequired: false,
}))

function errorMessage(caught: unknown, fallback: string): string {
  return caught instanceof Error ? caught.message : fallback
}

function AutomationPage() {
  const { grows, growId, setGrowId, loading: growsLoading, error: growsError } = useSelectedGrow()
  const [configs, setConfigs] = useState<AutoMeasurementConfigDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState<string | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  const grow = grows.find((candidate) => String(candidate.id) === growId) ?? null
  const reload = () => setReloadKey((key) => key + 1)

  useEffect(() => {
    const controller = new AbortController()
    async function loadConfigs() {
      if (!growId) return
      try {
        const list = await apiFetch<AutoMeasurementConfigDto[]>(`/api/auto-measurements/configs?growId=${growId}`, { signal: controller.signal })
        if (!controller.signal.aborted) setConfigs(list)
      } catch (caught) {
        if (!controller.signal.aborted) setError(errorMessage(caught, 'Automatik konnte nicht geladen werden.'))
      }
    }
    void loadConfigs()
    return () => controller.abort()
  }, [growId, reloadKey])

  const configFor = (template: Template) => configs.find((config) => config.triggerKind === template.triggerKind) ?? null
  const enabledConfigs = configs.filter((config) => config.status === 'Enabled')
  const anyEnabled = enabledConfigs.length > 0
  const snapshotOn = anyEnabled && enabledConfigs.every((config) => config.captureSnapshot)

  async function setTemplateEnabled(template: Template, enabled: boolean) {
    if (!growId) return
    const config = configFor(template)
    setError(null)
    setNotice(null)
    setBusy(template.triggerKind)
    try {
      if (config) {
        await apiFetch(`/api/auto-measurements/configs/${config.id}`, {
          method: 'PUT',
          body: JSON.stringify({
            tentId: config.tentId,
            name: config.name,
            status: enabled ? 'Enabled' : 'Disabled',
            triggerKind: config.triggerKind,
            delayMinutes: config.delayMinutes,
            windowMinutes: config.windowMinutes,
            captureSnapshot: config.captureSnapshot,
          }),
        })
      } else if (enabled) {
        const created = await apiFetch<AutoMeasurementConfigDto>('/api/auto-measurements/configs', {
          method: 'POST',
          body: JSON.stringify({
            growId: Number(growId),
            tentId: grow?.tentId ?? null,
            name: template.title,
            status: 'Enabled',
            triggerKind: template.triggerKind,
            delayMinutes: template.delayMinutes,
            windowMinutes: template.windowMinutes,
            captureSnapshot: false,
          }),
        })
        if (created?.id) {
          await apiFetch(`/api/auto-measurements/configs/${created.id}/mappings`, {
            method: 'PUT',
            body: JSON.stringify({ mappings: AUTO_MAPPINGS }),
          })
        }
      }
      setNotice(enabled ? `„${template.title}" ist aktiv.` : `„${template.title}" ist aus.`)
      reload()
    } catch (caught) {
      setError(errorMessage(caught, 'Änderung konnte nicht gespeichert werden.'))
    } finally {
      setBusy(null)
    }
  }

  // One snapshot setting for the grow's automation — applied to every active template,
  // so the user sees a single switch, not one per template.
  async function setSnapshotAll(capture: boolean) {
    setError(null)
    setNotice(null)
    setBusy('snapshot')
    try {
      for (const config of enabledConfigs) {
        await apiFetch(`/api/auto-measurements/configs/${config.id}`, {
          method: 'PUT',
          body: JSON.stringify({
            tentId: config.tentId,
            name: config.name,
            status: config.status,
            triggerKind: config.triggerKind,
            delayMinutes: config.delayMinutes,
            windowMinutes: config.windowMinutes,
            captureSnapshot: capture,
          }),
        })
      }
      reload()
    } catch (caught) {
      setError(errorMessage(caught, 'Snapshot-Einstellung konnte nicht gespeichert werden.'))
    } finally {
      setBusy(null)
    }
  }

  return (
    <V1Page
      eyebrow="Automatik & Regeln"
      title="Automatik"
      subtitle="Schalte eine Automatik an, fertig. Sie erfasst automatisch die Werte deiner in Home Assistant zugeordneten Sensoren — du musst nichts auswählen. Voraussetzung: dein Licht ist in Home Assistant als Status hinterlegt."
      action={<GrowScopePicker grows={grows} growId={growId} onChange={setGrowId} />}
    >
      {(error || growsError) && <V1Alert message={(error ?? growsError) as string} tone="critical" />}
      {notice && <V1Alert message={notice} tone="ok" />}

      {growsLoading ? (
        <V1Card>Lädt…</V1Card>
      ) : grows.length === 0 ? (
        <V1Empty title="Kein aktiver Grow" text="Lege zuerst einen Grow an, dann kannst du hier Automatiken schalten." />
      ) : (
        <div style={{ display: 'grid', gap: 12 }}>
          {TEMPLATES.map((template) => {
            const config = configFor(template)
            const enabled = config?.status === 'Enabled'
            return (
              <V1Card key={template.triggerKind}>
                <V1Switch
                  label={template.title}
                  checked={enabled}
                  onChange={(checked) => void setTemplateEnabled(template, checked)}
                  hint={template.description}
                />
                {busy === template.triggerKind && <p className="rc2-measurement-note" style={{ margin: '10px 0 0' }}>Speichert…</p>}
              </V1Card>
            )
          })}

          {anyEnabled && (
            <V1Card>
              <V1Switch
                label="Kamera-Snapshot ins Journal"
                checked={snapshotOn}
                onChange={(checked) => void setSnapshotAll(checked)}
                hint="Legt bei jeder automatischen Messung ein Kamerabild im Foto-Tagebuch dieses Grows ab — gilt für alle aktiven Automatiken."
              />
              {busy === 'snapshot' && <p className="rc2-measurement-note" style={{ margin: '10px 0 0' }}>Speichert…</p>}
            </V1Card>
          )}
        </div>
      )}
    </V1Page>
  )
}

export default AutomationPage
