import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import type { MeasurementDto } from '../../types'
import { formatDateTime, formatNumber } from '../../utils'
import type { GrowDetailSection, MeasurementFormState } from './grow-detail-model'
import { V1Button } from '../../components/v1'
import { useAbBreite } from '../../breite'
import { bilanzKurz, bilanzSatz, herkunftWort, urteilFuer, urteilKlasse, urteilSatz, urteilZeichen, wertUrteil } from './mess-urteil'
import type { MeasurementAssessmentReportDto } from '../../types'
import './grow-detail-legacy.css'

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
  beurteilung,
}: GrowDetailMeasurementsSectionProps & { beurteilung?: MeasurementAssessmentReportDto | null }) {
  /**
   * Die Liste war auf 15 gedeckelt, der Zaehler daneben zeigte trotzdem die
   * volle Zahl: „19 gesamt" ueber genau 15 Zeilen, ohne Hinweis darauf, dass
   * mehr da ist, und ohne Weg dorthin. Das ist keine Anzeigefrage, das ist
   * verlorene Auskunft. Jetzt sagt der Zaehler, was er zeigt, und der Rest ist
   * einen Klick entfernt.
   */
  /**
   * Ab 1000 px eine Tabelle statt einer Zeitachse.
   *
   * In der Zeitachse steckten pH, EC, Lufttemperatur und Feuchte in zwei
   * Saetzen — 70 bis 80 Prozent der Zeile blieben leer, und die Liste
   * brauchte trotzdem anderthalb Bildschirme. Als Tabelle stehen die Werte
   * untereinander und sind vergleichbar.
   *
   * Warum nicht per CSS: das sind zwei verschiedene Auszeichnungen. Beide in
   * die Seite zu schreiben und eine auszublenden hiesse, jede Messung doppelt
   * in den Baum zu setzen.
   */
  const alsTabelle = useAbBreite(1000)
  const [alleZeigen, setAlleZeigen] = useState(false)
  const GEZEIGT = 15
  const sichtbare = alleZeigen ? measurements : measurements.slice(0, GEZEIGT)
  const versteckt = measurements.length - sichtbare.length
  const isVisible = activeSection === 'measurements'

  return (
    <>
      {/* Die Ueberschrift der SEITE heisst seit dem Menue-Eintrag schon
          „Messungen“. Hier nochmal dasselbe Wort waere doppelt — der
          Abschnitt sagt jetzt, was er zeigt. */}
      <div className="section-label" style={{ display: isVisible ? undefined : 'none' }}>Verlauf</div>
      <div className="card" style={{ marginBottom: 14, display: isVisible ? undefined : 'none' }}>
        <div className="card-header">
          <span className="card-title">Verlauf</span>
          <span className="text-muted" style={{ fontSize: 'var(--fs-text)' }}>{versteckt > 0 ? `${sichtbare.length} von ${measurements.length}` : `${measurements.length} gesamt`}</span>
        </div>
        {beurteilung && beurteilung.checkedValueCount > 0 && (
          <p className="gd-mess-bilanz">
            {bilanzSatz(beurteilung)}
            <em>geprüft gegen {beurteilung.profileLabel}, Phase je Messung — Stand heute</em>
          </p>
        )}
        {measurements.length === 0 ? (
          <div className="empty-hint">Noch keine Messungen vorhanden.</div>
        ) : alsTabelle ? (
          <div className="co-table-wrap">
            <div className="co-table" style={{ gridTemplateColumns: '1.2fr .8fr .5fr .5fr .6fr .6fr .5fr .8fr' }}>
              <div className="co-th">Zeitpunkt</div>
              <div className="co-th">Phase</div>
              <div className="co-th">pH</div>
              <div className="co-th">EC</div>
              <div className="co-th">Wasser</div>
              <div className="co-th">Luft</div>
              <div className="co-th">rF</div>
              <div className="co-th" />
              {sichtbare.map((measurement) => {
                const zeile = urteilFuer(beurteilung ?? null, measurement.id)
                // Eine Zelle mit Urteil: Farbe UND Zeichen, dazu der
                // ausgeschriebene Satz fuer Vorlesegeraete. Ohne Urteil
                // bleibt sie schlicht — nicht gemessen darf nie aussehen
                // wie in Ordnung.
                const zelle = (metrik: string, wert: number | null, stellen: number) => {
                  const u = wertUrteil(zeile, metrik)
                  if (!u || wert == null) return <div className="co-td">{formatNumber(wert, stellen)}</div>
                  return (
                    <div className={urteilKlasse(u.verdict)} title={urteilSatz(u)}>
                      {formatNumber(wert, stellen)}
                      {urteilZeichen(u.verdict) && <span aria-hidden="true"> {urteilZeichen(u.verdict)}</span>}
                      <span className="sr-only">{urteilSatz(u)}</span>
                    </div>
                  )
                }
                return (
                <div
                  key={measurement.id}
                  /* `display: contents`: die Zellen muessen direkte Kinder
                     des Rasters bleiben, sonst fallen sie aus den Spalten. */
                  className={selectedMeasurementId === measurement.id ? 'gd-mess-zeile is-gewaehlt' : 'gd-mess-zeile'}
                  onClick={() => onSelectMeasurement(measurement.id)}
                >
                  {/* Die Herkunft steht IN der Namenszelle, nicht in einer
                      neunten Spalte: die Tabelle hat acht feste Spuren, und
                      unter 1000 px gibt es sie gar nicht. */}
                  <div className="co-td is-name">
                    {formatDateTime(measurement.takenAt)}
                    <em className="gd-mess-herkunft">{herkunftWort(measurement.source)}</em>
                  </div>
                  <div className="co-td is-muted">
                    {measurement.stage}
                    {/* Weicht die gerechnete Phase ab, stehen beide da. Die
                        gespeicherte still zu ueberschreiben waere schlimmer:
                        im Bestand laeuft sie stellenweise rueckwaerts. */}
                    {zeile?.computedStage && zeile.computedStage !== measurement.stage && (
                      <em className="gd-mess-herkunft" title={`Der Lauf war an dem Tag in Phase ${zeile.computedStage}`}>≠ {zeile.computedStage}</em>
                    )}
                  </div>
                  {zelle('ph', measurement.reservoirPh, 2)}
                  {zelle('ec', measurement.reservoirEc, 2)}
                  {zelle('water-temp', measurement.reservoirWaterTempC, 1)}
                  {zelle('air-temp', measurement.airTemperatureC, 1)}
                  {zelle('humidity', measurement.humidityPercent, 0)}
                  <div className="co-td">
                    <Link className="ls-btn is-small" to={`/grows/measurements/${measurement.id}/edit`} onClick={(event) => event.stopPropagation()}>Bearbeiten</Link>
                  </div>
                </div>
                )
              })}
            </div>
          </div>
        ) : (
          sichtbare.map((measurement) => {
            const kurz = bilanzKurz(urteilFuer(beurteilung ?? null, measurement.id))
            return (
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
                <div className="tl-sub">{formatNumber(measurement.airTemperatureC, 1)}°C · {formatNumber(measurement.humidityPercent, 0)}% rF · {herkunftWort(measurement.source)}{kurz ? ' · ' + kurz : ''}</div>
              </div>
              <div style={{ display: 'grid', gap: 6, justifyItems: 'end' }}>
                <div className="tl-time">{formatDateTime(measurement.takenAt)}</div>
                <Link className="btn" to={`/grows/measurements/${measurement.id}/edit`} onClick={(event) => event.stopPropagation()}>Bearbeiten</Link>
              </div>
            </div>
            )
          })
        )}
        {versteckt > 0 && (
          <button type="button" className="ls-btn is-small" style={{ margin: 12 }} onClick={() => setAlleZeigen(true)}>
            weitere {versteckt} anzeigen
          </button>
        )}
      </div>

      <div className="section-label" style={{ display: isVisible ? undefined : 'none' }}>Neue Messung</div>
      <div className="card" style={{ marginBottom: 14, display: isVisible ? undefined : 'none' }}>
        <div className="card-header"><span className="card-title">Messung eintragen</span></div>
        <form onSubmit={onSubmit} style={{ padding: '16px 20px' }}>
          <div className="meas-fields" style={{ marginBottom: 16 }}>
            <div className="meas-field">
              <label>Zeitpunkt</label>
              <input className="meas-input" style={{ fontSize: 'var(--fs-text)', fontFamily: 'var(--font-sans)', padding: '0 10px' }} type="datetime-local" value={measurementForm.takenAtLocal} onChange={(event) => onMeasurementFormChange({ takenAtLocal: event.target.value })} />
            </div>
            <div className="meas-field">
              <label>Phase</label>
              <select className="meas-input" style={{ fontSize: 'var(--fs-gross)' }} value={measurementForm.stage} onChange={(event) => onMeasurementFormChange({ stage: event.target.value })}>
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
          <div className="meas-field" style={{ marginBottom: 14, gridColumn: '1 / -1' }}>
            <label>Notiz</label>
            <textarea value={measurementForm.notes} onChange={(event) => onMeasurementFormChange({ notes: event.target.value })} rows={2} placeholder="Zustand, Auffälligkeiten, Korrekturen..." />
          </div>
          {/* type="submit" ist hier nicht optional: V1Button setzt sonst
              type="button" (v1.tsx), und dann loest der Knopf das Formular nie
              aus — er sah aus wie ein Speichern-Knopf und tat nichts. */}
          <V1Button type="submit" variant="primary" disabled={saving === 'measurement'}>{saving === 'measurement' ? 'Speichert...' : 'Messung speichern'}</V1Button>
        </form>
      </div>
    </>
  )
}
