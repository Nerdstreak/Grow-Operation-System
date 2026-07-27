import { useEffect, useMemo, useState } from 'react'
import { apiFetch } from '../../api'
import {
  KNOWN_METRICS,
  addSection,
  addTile,
  entityTile,
  metricTile,
  type DashboardLayout,
} from './dashboard-layout'
import { classNames } from '../../utils'

type HaEntity = {
  entityId: string
  friendlyName: string | null
  state: string | null
  unitOfMeasurement: string | null
  domain: string
}

/**
 * Die Leiste des Anpassen-Modus: Bereich anlegen, Kachel hinzufügen, speichern,
 * zurücksetzen, fertig.
 *
 * Sie erscheint nur im Anpassen-Modus. Wer nichts anpasst, sieht die Live-Seite
 * unverändert — das ist der Sinn: der Umbau ist ein Angebot, keine Bedingung.
 */
export function DashboardEditorBar({
  layout, onChange, onSave, onReset, onClose, saving, dirty, warning,
}: {
  layout: DashboardLayout
  onChange: (layout: DashboardLayout) => void
  onSave: () => void
  onReset: () => void
  onClose: () => void
  saving: boolean
  dirty: boolean
  warning: string | null
}) {
  const [picking, setPicking] = useState(false)

  return (
    <>
      <div className="ls-editbar" data-audit="dashboard-editbar">
        <span className="ls-label">Anpassen</span>
        <button type="button" className="ls-btn is-small is-ghost" onClick={() => onChange(addSection(layout))}>
          + Bereich
        </button>
        <button type="button" className="ls-btn is-small is-ghost" onClick={() => setPicking(true)}>
          + Kachel
        </button>
        <span className="ls-editbar-spacer" />
        <button type="button" className="ls-btn is-small" onClick={onReset} disabled={saving}>
          Zurücksetzen
        </button>
        <button type="button" className="ls-btn is-small is-primary" onClick={onSave} disabled={saving || !dirty}>
          {saving ? 'Speichert…' : 'Speichern'}
        </button>
        <button type="button" className="ls-btn is-small" onClick={onClose} disabled={saving}>
          Fertig
        </button>
      </div>

      {warning && <p className="ls-editbar-warn" role="status">{warning}</p>}

      {picking && (
        <AddTileDialog
          layout={layout}
          onAdd={(sectionId, tile) => { onChange(addTile(layout, sectionId, tile)); setPicking(false) }}
          onClose={() => setPicking(false)}
        />
      )}
    </>
  )
}

/**
 * Kachel hinzufügen: zwei Quellen in einer Liste.
 *
 * Oben, was Grow OS selbst misst — mit Zielbereich und Ampelfarbe. Darunter
 * jede beliebige Entität aus Home Assistant, auch solche, von denen Grow OS
 * nichts weiß. Genau dafür gibt es das Feature.
 */
function AddTileDialog({
  layout, onAdd, onClose,
}: {
  layout: DashboardLayout
  onAdd: (sectionId: string, tile: ReturnType<typeof metricTile>) => void
  onClose: () => void
}) {
  const [entities, setEntities] = useState<HaEntity[]>([])
  const [query, setQuery] = useState('')
  const [sectionId, setSectionId] = useState(layout.sections[0]?.id ?? '')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const list = await apiFetch<HaEntity[]>('/api/home-assistant/entities', { signal: controller.signal })
        if (!controller.signal.aborted) setEntities(list)
      } catch {
        // Ohne Entitätenliste bleiben die eigenen Messwerte — immer noch nützlich.
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  const suche = query.trim().toLowerCase()
  const metriken = useMemo(
    () => KNOWN_METRICS.filter((metric) => !suche || metric.label.toLowerCase().includes(suche) || metric.key.includes(suche)),
    [suche],
  )
  const sensoren = useMemo(
    () => entities
      // Kameras haben ihre eigene Bühne auf dieser Seite — als Kachel waeren sie
      // ein zweites, kleineres Bild derselben Sache.
      .filter((entity) => entity.domain !== 'camera' && entity.domain !== 'image')
      .filter((entity) => !suche
        || entity.entityId.toLowerCase().includes(suche)
        || (entity.friendlyName ?? '').toLowerCase().includes(suche))
      .slice(0, 40),
    [entities, suche],
  )

  return (
    <div className="ls-dialog-backdrop" role="dialog" aria-modal="true" aria-label="Kachel hinzufügen">
      <div className="ls-dialog" data-audit="dashboard-add-tile">
        <div className="ls-dialog-head">
          <strong>Kachel hinzufügen</strong>
          <button type="button" className="ls-btn is-small" onClick={onClose}>Abbrechen</button>
        </div>

        <div className="ls-dialog-row">
          <label className="ls-dialog-field">
            <span>Bereich</span>
            <select value={sectionId} onChange={(event) => setSectionId(event.target.value)}>
              {layout.sections.map((section) => (
                <option key={section.id} value={section.id}>{section.title}</option>
              ))}
            </select>
          </label>
          <label className="ls-dialog-field is-grow">
            <span>Suchen</span>
            <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="pH, Steckdose, UV …" />
          </label>
        </div>

        <div className="ls-dialog-body">
          {metriken.length > 0 && (
            <>
              <div className="ls-dialog-cap">Grow OS kennt</div>
              {metriken.map((metric) => (
                <button key={metric.key} type="button" className="ls-pick" onClick={() => onAdd(sectionId, metricTile(metric.key))}>
                  <span className="dot" />
                  <span className="ls-pick-text">{metric.label}</span>
                </button>
              ))}
            </>
          )}

          <div className="ls-dialog-cap">Aus Home Assistant</div>
          {loading && <p className="ls-dialog-empty">Lädt Entitäten …</p>}
          {!loading && sensoren.length === 0 && (
            <p className="ls-dialog-empty">
              {entities.length === 0
                ? 'Keine Entitäten erreichbar — ist Home Assistant verbunden?'
                : 'Nichts gefunden.'}
            </p>
          )}
          {sensoren.map((entity) => (
            <button
              key={entity.entityId}
              type="button"
              className="ls-pick"
              onClick={() => onAdd(sectionId, entityTile(entity.entityId, entity.friendlyName, entity.unitOfMeasurement))}
            >
              <span className="dot" />
              <span className="ls-pick-text">{entity.friendlyName ?? entity.entityId}</span>
              <span className="ls-pick-tag">{entity.domain}</span>
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}

/** Der Kopf eines Bereichs: im Anpassen-Modus umbenennbar, sonst nur Beschriftung. */
export function SectionHead({
  title, editing, onRename, onRemove, onUp, onDown, canUp, canDown, tone,
}: {
  title: string
  editing: boolean
  onRename: (value: string) => void
  onRemove: () => void
  onUp: () => void
  onDown: () => void
  canUp: boolean
  canDown: boolean
  tone?: 'new'
}) {
  if (!editing) {
    return (
      <div className="ls-band-label">
        <span>{title}</span>
        <i />
      </div>
    )
  }

  return (
    <div className="ls-band-label is-editing">
      <input
        className={classNames('ls-band-rename', tone === 'new' && 'is-new')}
        value={title}
        onChange={(event) => onRename(event.target.value)}
        aria-label={`Name des Bereichs ${title}`}
      />
      <i />
      {/* Knoepfe zusaetzlich zum Ziehen: auf dem Handy funktioniert HTML5-Drag
          nicht, und das Dashboard wird gerade dort am haeufigsten angeschaut. */}
      <button type="button" className="ls-btn is-small" onClick={onUp} disabled={!canUp} aria-label={`${title} nach oben`}>↑</button>
      <button type="button" className="ls-btn is-small" onClick={onDown} disabled={!canDown} aria-label={`${title} nach unten`}>↓</button>
      <button type="button" className="ls-btn is-small" onClick={onRemove}>Bereich entfernen</button>
    </div>
  )
}
