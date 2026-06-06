import type { FormEvent } from 'react'
import type {
  AutoMeasurementAggregation,
  AutoMeasurementConfigDto,
  AutoMeasurementConfigStatusDto,
  AutoMeasurementField,
  AutoMeasurementFieldMappingDto,
  AutoMeasurementFieldMappingUpsertRequest,
  AutoMeasurementRunDto,
  AutoMeasurementStatus,
  AutoMeasurementTriggerKind,
} from '../../types'
import { formatDateTime } from '../../utils'
import {
  autoMeasurementAggregations,
  autoMeasurementFields,
  autoMeasurementStatuses,
  autoMeasurementTriggerKinds,
  type AutoConfigFormState,
  type GrowDetailSection,
} from './grow-detail-model'

type GrowDetailAutomationSectionProps = {
  activeSection: GrowDetailSection
  autoConfigs: AutoMeasurementConfigDto[]
  autoConfigForm: AutoConfigFormState
  autoStatusByConfigId: Record<number, AutoMeasurementConfigStatusDto>
  autoMappingsByConfigId: Record<number, AutoMeasurementFieldMappingDto[]>
  autoRunsByConfigId: Record<number, AutoMeasurementRunDto[]>
  mappingDraftsByConfigId: Record<number, AutoMeasurementFieldMappingUpsertRequest[]>
  autoStatusError: string | null
  autoLoading: boolean
  saving: string | null
  onAutoConfigFormChange: (patch: Partial<AutoConfigFormState>) => void
  onAutoConfigSubmit: (event: FormEvent<HTMLFormElement>) => void
  onAddMappingDraft: (configId: number) => void
  onUpdateMappingDraft: (configId: number, index: number, patch: Partial<AutoMeasurementFieldMappingUpsertRequest>) => void
  onRemoveMappingDraft: (configId: number, index: number) => void
  onSaveMappingDrafts: (configId: number) => void
  onCreateLightPreset: () => void
}

const triggerKindLabels: Record<AutoMeasurementTriggerKind, string> = {
  Manual: 'Manuell',
  LightOnDelay: 'Nach Licht AN',
  LightOffDelay: 'Nach Licht AUS',
}

export function GrowDetailAutomationSection({
  activeSection,
  autoConfigs,
  autoConfigForm,
  autoStatusByConfigId,
  autoMappingsByConfigId,
  autoRunsByConfigId,
  mappingDraftsByConfigId,
  autoStatusError,
  autoLoading,
  saving,
  onAutoConfigFormChange,
  onAutoConfigSubmit,
  onAddMappingDraft,
  onUpdateMappingDraft,
  onRemoveMappingDraft,
  onSaveMappingDrafts,
  onCreateLightPreset,
}: GrowDetailAutomationSectionProps) {
  const isVisible = activeSection === 'automation'

  return (
    <>
      <div className="section-label" style={{ display: isVisible ? undefined : 'none' }}>AutoMeasurement</div>
      <div className="card" style={{ marginBottom: 14, display: isVisible ? undefined : 'none' }}>
        <div className="card-header">
          <span className="card-title">Konfigurationen</span>
          <span className="text-muted" style={{ fontSize: 13 }}>{autoConfigs.length} aktiv</span>
        </div>
        <div className="auto-preset">
          <h3>⚡ Schnellstart: Messung nach Licht an/aus</h3>
          <p>
            Legt zwei Auto-Messungen an — jeweils 30 Min nach „Licht AN" und „Licht AUS" (15-Min-Sensorfenster,
            pH/EC/Wasser-Temp/Klima vorbelegt). Greift automatisch, sobald deine HA-Entitäten zugeordnet sind
            (Licht als LightStatus + die Sensoren).
          </p>
          <div>
            <button type="button" className="btn btn-primary" disabled={saving === 'auto-preset'} onClick={onCreateLightPreset}>
              {saving === 'auto-preset' ? 'Legt an…' : '30-Min-Preset anlegen'}
            </button>
          </div>
        </div>
        <form onSubmit={onAutoConfigSubmit} style={{ padding: '16px 20px', borderBottom: '1px solid var(--border)' }}>
          <div className="meas-fields" style={{ marginBottom: 14 }}>
            <div className="meas-field">
              <label>Name</label>
              <input className="meas-input" value={autoConfigForm.name} onChange={(event) => onAutoConfigFormChange({ name: event.target.value })} placeholder="z. B. Licht an" />
            </div>
            <div className="meas-field">
              <label>Status</label>
              <select className="meas-input" value={autoConfigForm.status} onChange={(event) => onAutoConfigFormChange({ status: event.target.value as AutoMeasurementStatus })}>
                {autoMeasurementStatuses.map((status) => <option key={status} value={status}>{status}</option>)}
              </select>
            </div>
            <div className="meas-field">
              <label>Trigger</label>
              <select className="meas-input" value={autoConfigForm.triggerKind} onChange={(event) => onAutoConfigFormChange({ triggerKind: event.target.value as AutoMeasurementTriggerKind })}>
                {autoMeasurementTriggerKinds.map((trigger) => <option key={trigger} value={trigger}>{triggerKindLabels[trigger]}</option>)}
              </select>
            </div>
            <div className="meas-field">
              <label>Fenster</label>
              <div className="meas-field-inner">
                <input className="meas-input" value={autoConfigForm.windowMinutes} onChange={(event) => onAutoConfigFormChange({ windowMinutes: event.target.value })} />
                <span className="meas-unit">min</span>
              </div>
            </div>
            <div className="meas-field">
              <label>Delay</label>
              <div className="meas-field-inner">
                <input className="meas-input" value={autoConfigForm.delayMinutes} onChange={(event) => onAutoConfigFormChange({ delayMinutes: event.target.value })} placeholder="optional" />
                <span className="meas-unit">min</span>
              </div>
            </div>
          </div>
          <button className="btn btn-primary" disabled={saving === 'auto-config'}>{saving === 'auto-config' ? 'Speichert...' : 'Config anlegen'}</button>
        </form>
        {autoStatusError && (
          <div className="empty-hint" style={{ borderBottom: '1px solid var(--border)' }}>{autoStatusError}</div>
        )}

        {autoLoading ? (
          <div className="empty-hint">Lade AutoMeasurement-Konfigurationen...</div>
        ) : autoConfigs.length === 0 ? (
          <div className="empty-hint">Noch keine AutoMeasurement-Konfigurationen.</div>
        ) : (
          autoConfigs.map((config) => {
            const drafts = mappingDraftsByConfigId[config.id] ?? []
            const savedMappingCount = autoMappingsByConfigId[config.id]?.length ?? 0
            const runs = autoRunsByConfigId[config.id] ?? []
            const status = autoStatusByConfigId[config.id]
            const mappingCount = status?.mappingCount ?? savedMappingCount
            const requiredMappingCount = status?.requiredMappingCount ?? (autoMappingsByConfigId[config.id]?.filter((mapping) => mapping.isRequired).length ?? 0)
            return (
              <div key={config.id} style={{ padding: '14px 20px', borderTop: '1px solid var(--border)', display: 'grid', gap: 12 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
                  <div>
                    <div className="tl-title">{config.name}</div>
                    <div className="tl-sub">{triggerKindLabels[config.triggerKind] ?? config.triggerKind} · {config.windowMinutes} min Fenster{config.delayMinutes != null ? ` · ${config.delayMinutes} min Delay` : ''}</div>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span className={`badge ${config.status === 'Enabled' ? 'badge-ok' : 'badge-neutral'}`}>{config.status}</span>
                    <span className="text-muted" style={{ fontSize: 13 }}>{mappingCount} Mappings / {requiredMappingCount} Pflicht</span>
                  </div>
                </div>

                <div style={{ display: 'grid', gap: 6, fontSize: 13, color: 'var(--muted)' }}>
                  <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                    <span>Runs: Created {status?.createdRunCount ?? 0}</span>
                    <span>Skipped {status?.skippedRunCount ?? 0}</span>
                    <span>Failed {status?.failedRunCount ?? 0}</span>
                  </div>
                  <div>
                    Letzter Run:{' '}
                    {status?.lastRunStatus ? (
                      <>
                        <span className={`badge ${status.lastRunStatus === 'Created' ? 'badge-ok' : status.lastRunStatus === 'Failed' ? 'badge-warn' : 'badge-neutral'}`}>{status.lastRunStatus}</span>
                        <span> {status.lastRunScheduledForUtc ? formatDateTime(status.lastRunScheduledForUtc) : '-'} </span>
                        <span>{status.lastRunMeasurementId ? `M#${status.lastRunMeasurementId}` : '-'}</span>
                      </>
                    ) : (
                      <span>noch keiner</span>
                    )}
                  </div>
                  {status?.lastRunErrorMessage && <div>{status.lastRunErrorMessage}</div>}
                  <div>
                    Letzte relevante LightTransition:{' '}
                    {status?.latestRelevantLightTransitionKind && status.latestRelevantLightTransitionAtUtc
                      ? `${status.latestRelevantLightTransitionKind} ${formatDateTime(status.latestRelevantLightTransitionAtUtc)}`
                      : '-'}
                  </div>
                </div>

                <div style={{ display: 'grid', gap: 8 }}>
                  {drafts.length === 0 ? (
                    <div className="empty-hint" style={{ padding: 0 }}>Keine Mappings.</div>
                  ) : (
                    drafts.map((mapping, index) => (
                      <div key={`${config.id}-${index}`} className="meas-fields" style={{ alignItems: 'end' }}>
                        <div className="meas-field">
                          <label>Feld</label>
                          <select className="meas-input" value={mapping.measurementField} onChange={(event) => onUpdateMappingDraft(config.id, index, { measurementField: event.target.value as AutoMeasurementField })}>
                            {autoMeasurementFields.map((field) => <option key={field} value={field}>{field}</option>)}
                          </select>
                        </div>
                        <div className="meas-field">
                          <label>MetricKey</label>
                          <input className="meas-input" value={mapping.metricKey} onChange={(event) => onUpdateMappingDraft(config.id, index, { metricKey: event.target.value })} />
                        </div>
                        <div className="meas-field">
                          <label>Aggregation</label>
                          <select className="meas-input" value={mapping.aggregation} onChange={(event) => onUpdateMappingDraft(config.id, index, { aggregation: event.target.value as AutoMeasurementAggregation })}>
                            {autoMeasurementAggregations.map((aggregation) => <option key={aggregation} value={aggregation}>{aggregation}</option>)}
                          </select>
                        </div>
                        <label className="checkbox-row" style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, color: 'var(--muted)', minHeight: 40 }}>
                          <input type="checkbox" checked={mapping.isRequired} onChange={(event) => onUpdateMappingDraft(config.id, index, { isRequired: event.target.checked })} />
                          Pflicht
                        </label>
                        <button type="button" className="btn" onClick={() => onRemoveMappingDraft(config.id, index)}>Entfernen</button>
                      </div>
                    ))
                  )}
                </div>

                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  <button type="button" className="btn" onClick={() => onAddMappingDraft(config.id)}>Mapping hinzufügen</button>
                  <button type="button" className="btn btn-primary" disabled={saving === `auto-mappings-${config.id}`} onClick={() => onSaveMappingDrafts(config.id)}>
                    {saving === `auto-mappings-${config.id}` ? 'Speichert...' : 'Mappings speichern'}
                  </button>
                </div>

                <div style={{ display: 'grid', gap: 6 }}>
                  <div className="tl-sub">Letzte Runs</div>
                  {runs.length === 0 ? (
                    <div style={{ fontSize: 13, color: 'var(--faint)' }}>Noch keine Runs.</div>
                  ) : (
                    runs.slice(0, 5).map((run) => (
                      <div key={run.id} style={{ display: 'grid', gridTemplateColumns: '110px minmax(160px, 1fr) 110px minmax(0, 1.3fr)', gap: 8, fontSize: 13, alignItems: 'center' }}>
                        <span className={`badge ${run.status === 'Created' ? 'badge-ok' : run.status === 'Failed' ? 'badge-warn' : 'badge-neutral'}`}>{run.status}</span>
                        <span className="text-muted">{formatDateTime(run.scheduledForUtc)}</span>
                        <span className="text-muted">{run.measurementId ? `M#${run.measurementId}` : '-'}</span>
                        <span className="text-muted" style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{run.errorMessage ?? ''}</span>
                      </div>
                    ))
                  )}
                </div>
              </div>
            )
          })
        )}
      </div>
    </>
  )
}
