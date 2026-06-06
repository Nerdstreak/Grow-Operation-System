import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import type { MeasurementDto } from '../../types'
import { formatDateTime, formatNumber } from '../../utils'
import type { GrowDetailSection, MeasurementFormState } from './grow-detail-model'

type GrowDetailMeasurementsSectionProps = {
  activeSection: GrowDetailSection
  measurements: MeasurementDto[]
  selectedMeasurementId: number | null
  measurementForm: MeasurementFormState
  saving: string | null
  onSelectMeasurement: (measurementId: number | null) => void
  onMeasurementFormChange: (patch: Partial<MeasurementFormState>) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}

export function GrowDetailMeasurementsSection({
  activeSection,
  measurements,
  selectedMeasurementId,
  measurementForm,
  saving,
  onSelectMeasurement,
  onMeasurementFormChange,
  onSubmit,
}: GrowDetailMeasurementsSectionProps) {
  const isVisible = activeSection === 'measurements'

  return (
    <>
      <div className="section-label" style={{ display: isVisible ? undefined : 'none' }}>Messungen</div>
      <div className="card" style={{ marginBottom: 14, display: isVisible ? undefined : 'none' }}>
        <div className="card-header">
          <span className="card-title">Verlauf</span>
          <span className="text-muted" style={{ fontSize: 13 }}>{measurements.length} gesamt</span>
        </div>
        {measurements.length === 0 ? (
          <div className="empty-hint">Noch keine Messungen vorhanden.</div>
        ) : (
          measurements.slice(0, 15).map((measurement) => (
            <div
              key={measurement.id}
              className="timeline-item"
              style={{ cursor: 'pointer', padding: '12px 16px', background: selectedMeasurementId === measurement.id ? 'var(--surface2)' : undefined }}
              onClick={() => onSelectMeasurement(measurement.id)}
            >
              <div className="tl-dot-col">
                <div className="tl-dot measurement" />
                <div className="tl-line" />
              </div>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div className="tl-title">{measurement.stage} · pH {formatNumber(measurement.reservoirPh, 2)} · EC {formatNumber(measurement.reservoirEc, 2)}</div>
                <div className="tl-sub">{formatNumber(measurement.airTemperatureC, 1)}°C · {formatNumber(measurement.humidityPercent, 0)}% rF</div>
              </div>
              <div style={{ display: 'grid', gap: 6, justifyItems: 'end' }}>
                <div className="tl-time">{formatDateTime(measurement.takenAt)}</div>
                <Link className="btn" to={`/grows/measurements/${measurement.id}/edit`} onClick={(event) => event.stopPropagation()}>Bearbeiten</Link>
              </div>
            </div>
          ))
        )}
      </div>

      <div className="section-label" style={{ display: isVisible ? undefined : 'none' }}>Neue Messung</div>
      <div className="card" style={{ marginBottom: 14, display: isVisible ? undefined : 'none' }}>
        <div className="card-header"><span className="card-title">Messung eintragen</span></div>
        <form onSubmit={onSubmit} style={{ padding: '16px 20px' }}>
          <div className="meas-fields" style={{ marginBottom: 16 }}>
            <div className="meas-field">
              <label>Zeitpunkt</label>
              <input className="meas-input" style={{ fontSize: 15 }} type="datetime-local" value={measurementForm.takenAtLocal} onChange={(event) => onMeasurementFormChange({ takenAtLocal: event.target.value })} />
            </div>
            <div className="meas-field">
              <label>Phase</label>
              <select className="meas-input" style={{ fontSize: 15 }} value={measurementForm.stage} onChange={(event) => onMeasurementFormChange({ stage: event.target.value })}>
                <option>Seedling</option><option>Clone</option><option>Veg</option><option>Transition</option><option>Flower</option><option>Finish</option><option>Dry</option><option>Cure</option>
              </select>
            </div>
            <div className="meas-field">
              <label>pH</label>
              <div className="meas-field-inner">
                <input className="meas-input" value={measurementForm.reservoirPh} onChange={(event) => onMeasurementFormChange({ reservoirPh: event.target.value })} placeholder="5.8" />
                <span className="meas-unit">pH</span>
              </div>
            </div>
            <div className="meas-field">
              <label>EC</label>
              <div className="meas-field-inner">
                <input className="meas-input" value={measurementForm.reservoirEc} onChange={(event) => onMeasurementFormChange({ reservoirEc: event.target.value })} placeholder="1.6" />
                <span className="meas-unit">mS/cm</span>
              </div>
            </div>
            <div className="meas-field">
              <label>Wassertemp</label>
              <div className="meas-field-inner">
                <input className="meas-input" value={measurementForm.reservoirWaterTempC} onChange={(event) => onMeasurementFormChange({ reservoirWaterTempC: event.target.value })} placeholder="19.0" />
                <span className="meas-unit">°C</span>
              </div>
            </div>
            <div className="meas-field">
              <label>Lufttemp</label>
              <div className="meas-field-inner">
                <input className="meas-input" value={measurementForm.airTemperatureC} onChange={(event) => onMeasurementFormChange({ airTemperatureC: event.target.value })} placeholder="24.0" />
                <span className="meas-unit">°C</span>
              </div>
            </div>
            <div className="meas-field">
              <label>Luftfeuchte</label>
              <div className="meas-field-inner">
                <input className="meas-input" value={measurementForm.humidityPercent} onChange={(event) => onMeasurementFormChange({ humidityPercent: event.target.value })} placeholder="60" />
                <span className="meas-unit">%</span>
              </div>
            </div>
          </div>
          <div className="field" style={{ marginBottom: 14 }}>
            <label>Notiz</label>
            <textarea value={measurementForm.notes} onChange={(event) => onMeasurementFormChange({ notes: event.target.value })} rows={2} placeholder="Zustand, Auffälligkeiten, Korrekturen..." />
          </div>
          <button className="btn btn-primary" disabled={saving === 'measurement'}>{saving === 'measurement' ? 'Speichert...' : 'Messung speichern'}</button>
        </form>
      </div>
    </>
  )
}
