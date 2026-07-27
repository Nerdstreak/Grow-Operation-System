import { Link } from 'react-router-dom'
import type { GrowSummary, MetricPayload, RiskEventDto, TentDto } from '../../types'
import type { HistoryPoint } from '../../components/SensorChart'
import { MetricTile } from './MetricTile'
import { decimalsForMetric } from './metric-tile-model'
import { CameraPanel } from './CameraPanel'
import { TrendWatchPanel } from './TrendWatchPanel'
import { DashboardBands } from './DashboardBands'
import { DashboardEditorBar } from './DashboardEditor'
import type { DashboardLayout, EntityValue } from './dashboard-layout'
import { buildScore } from './live-model'
import { classNames } from '../../utils'
import { flipLabel, type Phase } from '../grows/phase-timeline'

/**
 * Der Live-Bildschirm, gebaut nach dem Entwurf des Designers.
 *
 * Aufbau von oben: Kopfzeile mit Score und den zwei Handlungen, dann die
 * Messwerte in zwei beschrifteten Reihen (Klima, Nährlösung), dann Kamera neben
 * Risiko und heute fälligen Aufgaben, unten der Grow mit seiner Phasen-Timeline.
 *
 * Die Reihenfolge ist fest. Wer morgens auf diese Seite schaut, sucht denselben
 * Wert an derselben Stelle — eine Sortierung nach Dringlichkeit würde das jeden
 * Tag verschieben. Die Statusfarbe macht das Hervorheben.
 */

export type LiveTask = {
  id: string
  when: string
  title: string
  action: string
  to: string
  due?: boolean
}

export type LiveScreenProps = {
  tent: TentDto | null
  grow: GrowSummary | null
  score: ReturnType<typeof buildScore>
  scoreParts: string
  climate: MetricPayload[]
  hydro: MetricPayload[]
  sensorsLive: number
  lastMeasurement: string | null
  stageLine: string | null
  risks: RiskEventDto[]
  tasks: LiveTask[]
  /** Der geteilte Phasentyp — die Inline-Fassung kannte den Fortschritt nicht. */
  timeline: Phase[]
  timelineDates: { start: string; flip: string; harvest: string }
  /** Flip steht nur im Plan; die Beschriftung sagt das dann auch. */
  flipIsPlanned: boolean
  daysToFlip: number | null
  plantLine: string | null
  /** Alle Zelte zur Auswahl; erst ab zwei erscheint der Umschalter. */
  tents: TentDto[]
  /** Nur gesetzt, wenn die Überwachung SELBST ein Problem meldet (Watchdog). */
  systemWarning: { headline: string; detail: string } | null
  /** Die 24-h-Kurve je Messwert; fehlt sie, zeigt die Kachel ihr Zielband. */
  trends: Map<string, HistoryPoint[]>
  /** Gesetzt, sobald der Nutzer eine eigene Anordnung hat oder gerade anpasst. */
  dashboard: DashboardPanel | null
  onTent: (tentId: number) => void
  onRefresh: () => void
}

/** Alles, was der Anpassen-Modus braucht — gebündelt, damit LiveScreen nicht zerfasert. */
export type DashboardPanel = {
  layout: DashboardLayout
  entityValues: Map<string, EntityValue>
  editing: boolean
  saving: boolean
  dirty: boolean
  warning: string | null
  onChange: (layout: DashboardLayout) => void
  onSave: () => void
  onReset: () => void
  /** Schaltet den Anpassen-Modus an und wieder aus — „Anpassen" oben, „Fertig" in der Leiste. */
  onToggleEditing: () => void
}

export function LiveScreen({
  tent, grow, score, scoreParts, climate, hydro, sensorsLive,
  lastMeasurement, stageLine, risks, tasks, timeline, timelineDates, plantLine,
  flipIsPlanned, daysToFlip, tents, systemWarning, trends, dashboard, onTent, onRefresh,
}: LiveScreenProps) {
  const topRisk = risks[0] ?? null
  // Die eigene Anordnung zeichnet nur, wer eine hat oder gerade eine baut.
  // Sonst bleibt es bei den festen Reihen — Buchstabe fuer Buchstabe wie bisher.
  const eigeneAnordnung = dashboard && (dashboard.editing || dashboard.layout.isCustom)
  const metricsByKey = new Map([...climate, ...hydro].map((metric) => [metric.key, metric]))

  return (
    <main className="ls" data-audit="live-screen">
      {/* ---------- Kopfzeile ---------- */}
      <header className="ls-head">
        <ScoreRing value={score.value} tone={score.tone} />

        <div className="ls-head-title">
          <div className="ls-eyebrow">Jetzt / Live · Grow-Score</div>
          <h1 className={`ls-score-label is-${score.tone}`}>{score.label}</h1>
          <div className="ls-head-parts">{scoreParts}</div>
        </div>

        <span className="ls-pill">
          <i />{sensorsLive} Sensoren live
        </span>

        <span className="ls-head-meta">
          {[lastMeasurement && `Letzte Messung ${lastMeasurement}`, stageLine, grow?.name]
            .filter(Boolean).join(' · ')}
        </span>

        <div className="ls-head-actions">
          {dashboard && !dashboard.editing && (
            <button type="button" className="ls-btn" onClick={dashboard.onToggleEditing} data-audit="dashboard-customise">
              ▦ Anpassen
            </button>
          )}
          {tents.length > 1 && (
            <select
              className="ls-tent-select"
              aria-label="Zelt"
              value={tent?.id ?? ''}
              onChange={(event) => onTent(Number(event.target.value))}
            >
              {tents.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
          )}
          <Link className="ls-btn is-primary" to="/messung">Messung erfassen</Link>
          <Link className="ls-btn" to="/addback">Addback starten</Link>
        </div>
      </header>

      {/* ---------- Systemwarnung ---------- */}
      {/* Wenn die Überwachung selbst schweigt, sind alle Kacheln darunter
          womöglich alte Zahlen — das muss ÜBER den Messwerten stehen, nicht
          in einer Benachrichtigung, die erst abends jemand liest. */}
      {systemWarning && (
        <Link className="ls-syswarn" to="/regeln?tab=push" data-audit="live-system-warning">
          <span className="ls-label">Systemüberwachung</span>
          <span className="ls-syswarn-text"><strong>{systemWarning.headline}</strong> — {systemWarning.detail}</span>
        </Link>
      )}

      {/* ---------- Messwerte ---------- */}
      {dashboard?.editing && (
        <DashboardEditorBar
          layout={dashboard.layout}
          onChange={dashboard.onChange}
          onSave={dashboard.onSave}
          onReset={dashboard.onReset}
          onClose={dashboard.onToggleEditing}
          saving={dashboard.saving}
          dirty={dashboard.dirty}
          warning={dashboard.warning}
        />
      )}

      <section className="ls-metrics">
        {eigeneAnordnung && dashboard ? (
          <DashboardBands
            layout={dashboard.layout}
            metricsByKey={metricsByKey}
            entityValues={dashboard.entityValues}
            trends={trends}
            editing={dashboard.editing}
            onChange={dashboard.onChange}
          />
        ) : (
          <>
            <MetricBand title="Klima" metrics={climate} trends={trends} />
            <MetricBand title="Hydroponik · Nährlösung" metrics={hydro} trends={trends} />
          </>
        )}
      </section>

      {/* ---------- Kamera · Risiko · Aufgaben ---------- */}
      <section className="ls-lower">
        <CameraPanel tent={tent} onReload={onRefresh} />

        <div className="ls-lower-right">
          {topRisk ? (
            <article className={classNames('ls-panel', 'ls-risk', `is-${topRisk.severity.toLowerCase()}`)} data-audit="live-risk">
              <div className="ls-panel-head">
                <span className="ls-label">Risiko · {topRisk.severity === 'Critical' ? 'kritisch' : topRisk.severity === 'Warning' ? 'Warnung' : 'Hinweis'}</span>
                <span className="ls-panel-meta">{topRisk.startedAtUtc ? `seit ${sinceLabel(topRisk.startedAtUtc)}` : ''}</span>
              </div>
              <div className="ls-panel-body">
                <strong>{topRisk.title}</strong>
                {topRisk.description && <p>{topRisk.description}</p>}
                <div className="ls-panel-actions">
                  {/* Ein laufender SOP hat eine Instanz — dann fuehrt der Knopf
                      dorthin, sonst in die Diagnose, wo die Prozedur ausgewaehlt
                      wird. Kein erfundener Link auf eine SOP-Id, die es im DTO
                      nicht gibt. */}
                  {topRisk.sopInstanceId
                    ? <Link className="ls-btn is-primary" to={`/sops?instance=${topRisk.sopInstanceId}`}>SOP fortsetzen</Link>
                    : <Link className="ls-btn is-primary" to="/diagnose">Maßnahme wählen</Link>}
                  <Link className="ls-btn" to="/aufgaben">Aufgaben</Link>
                </div>
              </div>
            </article>
          ) : (
            <article className="ls-panel" data-audit="live-risk">
              <div className="ls-panel-head"><span className="ls-label">Risiken</span></div>
              <div className="ls-panel-body"><strong>Nichts Offenes</strong><p>Keine kritischen Abweichungen im gewählten Zelt.</p></div>
            </article>
          )}

          <article className="ls-panel" data-audit="live-tasks">
            <div className="ls-panel-head">
              <span className="ls-label">Heute fällig</span>
              <span className="ls-panel-meta">{tasks.length} {tasks.length === 1 ? 'Aufgabe' : 'Aufgaben'}</span>
              <Link className="ls-btn is-small" to="/aufgaben">Alle</Link>
            </div>
            {tasks.length === 0 ? (
              <div className="ls-panel-body"><p>Für heute steht nichts an.</p></div>
            ) : (
              <ul className="ls-tasks">
                {tasks.slice(0, 3).map((task) => (
                  <li key={task.id}>
                    <span className={classNames('ls-task-when', task.due && 'is-due')}>{task.when}</span>
                    <span className="ls-task-title">{task.title}</span>
                    <Link className="ls-btn is-small" to={task.to}>{task.action}</Link>
                  </li>
                ))}
              </ul>
            )}
          </article>

          {/* Die Watchdog-Beobachtungen (Drift, Verbrauch) blieben beim Umbau
              zunaechst auf der Strecke — sie sind ein bestehendes Feature und
              gehoeren zu „heute fällig“ dazu: was sich ueber Tage anbahnt. */}
          <TrendWatchPanel growId={grow?.id ?? null} />
        </div>
      </section>

      {/* ---------- Grow im Zelt ---------- */}
      {grow && (
        <section className="ls-panel ls-grow" data-audit="live-grow">
          <div className="ls-panel-head">
            <span className="ls-label">Grow im Zelt</span>
            <span className="ls-panel-meta">{[grow.name, plantLine, tent?.name].filter(Boolean).join(' · ')}</span>
            <Link className="ls-btn is-small" to={`/grows/${grow.id}`}>Grow öffnen</Link>
          </div>
          <div className="ls-panel-body">
            <div className="ls-timeline">
              {timeline.map((phase) => (
                <div
                  key={phase.label}
                  className={classNames('ls-phase', `is-${phase.state}`, phase.days === 0 && 'is-unknown')}
                  style={{ flexGrow: Math.max(1, phase.days) }}
                >
                  {/* Der Fuellstand zeigt, wo im Plan man heute steht — die
                      Balkenbreite allein sagt nur, wie lang die Phase ist. */}
                  {phase.progress != null && (
                    <i className="ls-phase-fill" style={{ width: `${Math.round(phase.progress * 100)}%` }} aria-hidden="true" />
                  )}
                  <span>{phase.label}</span>
                </div>
              ))}
            </div>
            <div className="ls-timeline-dates">
              <span>Start {timelineDates.start}</span>
              <span className={daysToFlip != null && daysToFlip < 0 ? 'is-due' : undefined}>{flipLabel(flipIsPlanned, daysToFlip, timelineDates.flip)}</span>
              <span>{timelineDates.harvest === '\u2014' ? 'Ernte offen' : `Ernte ~${timelineDates.harvest}`}</span>
            </div>
          </div>
        </section>
      )}
    </main>
  )
}

/** Eine beschriftete Reihe Messwerte mit Haarlinie bis zum Rand. */
function MetricBand({ title, metrics, trends }: { title: string; metrics: MetricPayload[]; trends: Map<string, HistoryPoint[]> }) {
  if (metrics.length === 0) return null
  return (
    <>
      <div className="ls-band-label">
        <span>{title}</span>
        <i />
      </div>
      <div className="gos-metric-row">
        {metrics.map((metric) => (
          <MetricTile
            key={metric.key}
            label={metric.label}
            value={metric.numericValue}
            unit={metric.unit}
            targetMin={metric.targetMin}
            targetMax={metric.targetMax}
            decimals={decimalsForMetric(metric.key)}
            display={metric.numericValue == null && metric.value !== '–' ? metric.value : undefined}
            footer={metric.targetMin == null && metric.targetMax == null ? (metric.hint ?? undefined) : undefined}
            trend={trends.get(metric.key)}
          />
        ))}
      </div>
    </>
  )
}

/**
 * Der Score als Ring. Bewusst klein: er ist die Zusammenfassung, nicht die
 * Nachricht — die steht als Wort daneben.
 */
/**
 * Der Score-Ring — in der Farbe seiner Bewertung.
 *
 * Der Ring war fest auf Akzentgrün gesetzt: bei 40 Punkten stand daneben
 * „Kritisch“ und der Kreis leuchtete trotzdem grün. Die Bewertung kommt schon
 * immer aus `buildScore`, sie wurde hier nur nicht benutzt.
 */
function ScoreRing({ value, tone }: { value: number; tone: 'ok' | 'warn' | 'critical' | 'neutral' }) {
  const clamped = Math.max(0, Math.min(100, value))
  const farbe = tone === 'critical' ? 'var(--danger)'
    : tone === 'warn' ? 'var(--warn)'
      : tone === 'ok' ? 'var(--accent)'
        : 'var(--hair-2)'
  return (
    <div
      className={classNames('ls-ring', `is-${tone}`)}
      style={{ background: `conic-gradient(${farbe} 0 ${clamped}%, var(--sunk) ${clamped}% 100%)` }}
      role="img"
      aria-label={`Grow-Score ${clamped} von 100`}
    >
      <div className="ls-ring-inner">
        <span>{clamped}</span>
        <span className="ls-ring-max">/100</span>
      </div>
    </div>
  )
}

function sinceLabel(iso: string): string {
  const started = new Date(iso).getTime()
  if (Number.isNaN(started)) return ''
  const minutes = Math.max(0, Math.round((Date.now() - started) / 60000))
  if (minutes < 60) return `${minutes} min`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} h ${minutes % 60} min`
  return `${Math.floor(hours / 24)} Tagen`
}

