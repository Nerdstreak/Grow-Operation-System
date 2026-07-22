import { useEffect, useState } from 'react'
import '../features/grow-detail/growdetail-instrument.css'
import { apiFetch } from '../api'
import type { AutoMeasurementConfigDto, AutoMeasurementFieldMappingUpsertRequest, AutoMeasurementTriggerKind } from '../types'
import { autoMeasurementFields, defaultMetricKeyByField } from '../features/grow-detail/grow-detail-model'
import { GrowScopeHeader } from '../features/grow-scope/GrowScopeHeader'
import { useSelectedGrow } from '../features/grow-scope/useSelectedGrow'
import { V1Switch } from '../components/v1'

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

  async function setSnapshot(config: AutoMeasurementConfigDto, capture: boolean) {
    setError(null)
    setNotice(null)
    setBusy(`snapshot-${config.id}`)
    try {
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
      reload()
    } catch (caught) {
      setError(errorMessage(caught, 'Snapshot-Einstellung konnte nicht gespeichert werden.'))
    } finally {
      setBusy(null)
    }
  }

  return (
    <>
      <GrowScopeHeader title="Automatik" grows={grows} growId={growId} onChange={setGrowId} />
      <div className="page-scroll">
        {(error || growsError) && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)' }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error ?? growsError}</span>
          </div>
        )}
        {notice && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)', background: 'var(--green-bg)', borderColor: 'var(--green)' }}>
            <div className="alert-dot" style={{ background: 'var(--green)' }} />
            <strong style={{ color: 'var(--green)' }}>Info</strong>
            <span>{notice}</span>
          </div>
        )}

        <p className="text-muted" style={{ margin: '0 0 14px', fontSize: 13 }}>
          Schalte eine Automatik an, fertig. Sie erfasst automatisch die Werte deiner in Home Assistant zugeordneten
          Sensoren — du musst nichts auswählen. Voraussetzung: dein Licht ist in Home Assistant als Status hinterlegt.
        </p>

        {growsLoading ? (
          <div className="empty-hint">Lade Grows…</div>
        ) : grows.length === 0 ? (
          <div className="empty-hint">Kein aktiver Grow. Lege zuerst einen Grow an, dann kannst du hier Automatiken schalten.</div>
        ) : (
          <div style={{ display: 'grid', gap: 12 }}>
            {TEMPLATES.map((template) => {
              const config = configFor(template)
              const enabled = config?.status === 'Enabled'
              return (
                <div key={template.triggerKind} className="card" style={{ padding: '16px 20px' }}>
                  <V1Switch
                    label={template.title}
                    checked={enabled}
                    onChange={(checked) => void setTemplateEnabled(template, checked)}
                    hint={template.description}
                  />
                  {enabled && config && (
                    <div style={{ marginTop: 14, paddingTop: 14, borderTop: '1px solid var(--border)' }}>
                      <V1Switch
                        label="Kamera-Snapshot ins Journal"
                        checked={config.captureSnapshot}
                        onChange={(checked) => void setSnapshot(config, checked)}
                        hint="Legt bei jeder automatischen Messung ein Kamerabild im Foto-Tagebuch dieses Grows ab."
                      />
                    </div>
                  )}
                  {busy === template.triggerKind && <p className="text-muted" style={{ margin: '10px 0 0', fontSize: 13 }}>Speichert…</p>}
                </div>
              )
            })}
          </div>
        )}
      </div>
    </>
  )
}

export default AutomationPage
