import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'
import type { DashboardLayout, DashboardTile } from './useTentDashboard'
import { cameraTileLabel } from './dashboard-tile-model'

type HaEntity = { entityId: string; friendlyName: string | null; state: string | null; unitOfMeasurement: string | null; domain: string }

// The values Grow OS knows itself — offered by name so a user doesn't need to know keys.
const KNOWN_METRICS: Array<{ key: string; label: string }> = [
  { key: 'temperature', label: 'Temperatur' },
  { key: 'humidity', label: 'Luftfeuchte' },
  { key: 'vpd', label: 'VPD' },
  { key: 'co2', label: 'CO₂' },
  { key: 'ppfd', label: 'PPFD' },
  { key: 'light-cycle', label: 'Licht' },
  { key: 'reservoir-ph', label: 'pH' },
  { key: 'reservoir-ec', label: 'EC' },
  { key: 'reservoir-temp', label: 'Wassertemperatur' },
  { key: 'reservoir-level', label: 'Wasserstand (L)' },
  { key: 'reservoir-level-cm', label: 'Wasserstand (cm)' },
  { key: 'orp', label: 'ORP' },
  { key: 'dissolved-oxygen', label: 'Sauerstoff' },
]

function newId() {
  return Math.random().toString(36).slice(2, 10)
}

/**
 * The controls for arranging a dashboard: add sections, add a known value or ANY Home
 * Assistant entity as a tile, save or reset.
 */
export function DashboardEditorBar({
  layout,
  onChange,
  onSave,
  onReset,
  onClose,
  saving,
  tentCameras,
}: {
  layout: DashboardLayout
  onChange: (layout: DashboardLayout) => void
  onSave: () => void
  onReset: () => void
  onClose: () => void
  saving: boolean
  /** The camera entities assigned to this tent — the only ones a tile can show. */
  tentCameras: string[]
}) {
  const [entities, setEntities] = useState<HaEntity[]>([])
  const [pick, setPick] = useState('')
  const [targetSection, setTargetSection] = useState(layout.sections[0]?.id ?? '')

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const list = await apiFetch<HaEntity[]>('/api/home-assistant/entities', { signal: controller.signal })
        if (!controller.signal.aborted) setEntities(list)
      } catch {
        // Without the entity list only the known metrics can be added — still useful.
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  // Only the tent's own cameras: the snapshot proxy refuses entities the tent doesn't
  // own, so offering every camera in the house would just produce dead tiles. The tent
  // is the source of truth here; Home Assistant only supplies nicer names.
  const cameras = tentCameras.map((entityId) => ({
    entityId,
    friendlyName: entities.find((entity) => entity.entityId.toLowerCase() === entityId.toLowerCase())?.friendlyName ?? null,
  }))
  const sensors = entities.filter((entity) => entity.domain !== 'camera' && entity.domain !== 'image')

  function addTile(tile: DashboardTile) {
    const sectionId = targetSection || layout.sections[0]?.id
    if (!sectionId) return
    onChange({
      ...layout,
      sections: layout.sections.map((section) =>
        section.id === sectionId ? { ...section, tiles: [...section.tiles, tile] } : section),
    })
    setPick('')
  }

  function addFromPick() {
    if (!pick) return
    if (pick.startsWith('metric:')) {
      const key = pick.slice('metric:'.length)
      addTile({ id: newId(), kind: 'Metric', metricKey: key, entityId: null, label: null, unit: null, span: 1 })
    } else if (pick.startsWith('camera:')) {
      const entityId = pick.slice('camera:'.length)
      const entity = entities.find((item) => item.entityId === entityId)
      // A camera in a third of a row is unreadable, so it starts two columns wide.
      addTile({ id: newId(), kind: 'Camera', metricKey: null, entityId, label: entity?.friendlyName ?? cameraTileLabel(entityId), unit: null, span: 2 })
    } else {
      const entityId = pick.slice('entity:'.length)
      const entity = entities.find((item) => item.entityId === entityId)
      addTile({ id: newId(), kind: 'Entity', metricKey: null, entityId, label: entity?.friendlyName ?? null, unit: entity?.unitOfMeasurement ?? null, span: 1 })
    }
  }

  return (
    <div className="ix-panel ix-dash-editor" data-audit="dashboard-editor">
      <div className="ix-dash-editor-row">
        <span className="ix-kick">Dashboard anpassen</span>
        <span className="ix-dash-editor-hint">
          Kacheln und Bereiche ziehen zum Umsortieren · ↑ ↓ verschiebt Bereiche auch per Touch · − + ändert die Breite · × entfernt
        </span>
      </div>

      <div className="ix-dash-editor-row">
        <select value={targetSection} onChange={(event) => setTargetSection(event.target.value)} aria-label="Bereich">
          {layout.sections.map((section) => <option key={section.id} value={section.id}>{section.title}</option>)}
        </select>

        <select value={pick} onChange={(event) => setPick(event.target.value)} aria-label="Kachel hinzufügen" style={{ minWidth: 220 }}>
          <option value="">Kachel hinzufügen …</option>
          <optgroup label="Grow OS kennt">
            {KNOWN_METRICS.map((metric) => <option key={metric.key} value={`metric:${metric.key}`}>{metric.label}</option>)}
          </optgroup>
          {cameras.length > 0 && (
            <optgroup label="Kameras">
              {cameras.map((entity) => (
                <option key={entity.entityId} value={`camera:${entity.entityId}`}>
                  {entity.friendlyName ?? cameraTileLabel(entity.entityId)}
                </option>
              ))}
            </optgroup>
          )}
          {sensors.length > 0 && (
            <optgroup label="Eigene Home-Assistant-Sensoren">
              {sensors.map((entity) => (
                <option key={entity.entityId} value={`entity:${entity.entityId}`}>
                  {entity.friendlyName ?? entity.entityId}
                </option>
              ))}
            </optgroup>
          )}
        </select>
        <button type="button" className="ix-btn" disabled={!pick} onClick={addFromPick}>Hinzufügen</button>
        {entities.length === 0 && (
          <span className="ix-dash-editor-hint">Eigene Sensoren erscheinen hier, sobald Home Assistant verbunden ist.</span>
        )}

        <button
          type="button"
          className="ix-btn"
          onClick={() => onChange({ ...layout, sections: [...layout.sections, { id: newId(), title: 'Neuer Bereich', tiles: [] }] })}
        >
          Bereich anlegen
        </button>
      </div>

      <div className="ix-dash-editor-row">
        <button type="button" className="ix-btn pri" disabled={saving} onClick={onSave}>{saving ? 'Speichert…' : 'Layout speichern'}</button>
        <button type="button" className="ix-btn" onClick={onClose}>Abbrechen</button>
        <button type="button" className="ix-btn" onClick={onReset}>Auf Standard zurücksetzen</button>
      </div>
    </div>
  )
}
