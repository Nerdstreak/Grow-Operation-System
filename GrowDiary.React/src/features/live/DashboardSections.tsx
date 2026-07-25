import { useState } from 'react'
import type { MetricPayload } from '../../types'
import type { DashboardLayout, DashboardSection, EntityValue } from './useTentDashboard'
import { resolveTile } from './dashboard-tile-model'
import { DashboardCameraTile } from './DashboardCameraTile'

type Props = {
  layout: DashboardLayout
  metricsByKey: Map<string, MetricPayload>
  entityValues: Map<string, EntityValue>
  trends: Map<string, { t: string; v: number }[]>
  editing: boolean
  onChange: (layout: DashboardLayout) => void
  renderTile: (metric: MetricPayload, trendKey: string) => React.ReactNode
}

function move<T>(items: T[], from: number, to: number): T[] {
  if (to < 0 || to >= items.length) return items
  const next = [...items]
  const [item] = next.splice(from, 1)
  next.splice(to, 0, item)
  return next
}

/**
 * The dashboard's sections, driven by the tent's saved layout instead of fixed code. In
 * edit mode both the sections and the tiles inside them can be rearranged, resized,
 * renamed or removed.
 *
 * Ordering is offered as buttons as well as drag-and-drop: HTML5 dragging does nothing on
 * a touchscreen, and the dashboard is read on phones more than anywhere else.
 */
export function DashboardSections({ layout, metricsByKey, entityValues, editing, onChange, renderTile }: Props) {
  const [dragged, setDragged] = useState<{ sectionId: string; index: number } | null>(null)
  const [draggedSection, setDraggedSection] = useState<number | null>(null)

  const updateSection = (sectionId: string, update: (section: DashboardSection) => DashboardSection) =>
    onChange({ ...layout, sections: layout.sections.map((section) => (section.id === sectionId ? update(section) : section)) })

  const moveSection = (from: number, to: number) =>
    onChange({ ...layout, sections: move(layout.sections, from, to) })

  function dropOn(sectionId: string, index: number) {
    if (!dragged) return
    const source = layout.sections.find((section) => section.id === dragged.sectionId)
    const tile = source?.tiles[dragged.index]
    if (!source || !tile) return

    const sections = layout.sections.map((section) => {
      if (section.id === dragged.sectionId && section.id === sectionId) {
        return { ...section, tiles: move(section.tiles, dragged.index, index) }
      }
      if (section.id === dragged.sectionId) {
        return { ...section, tiles: section.tiles.filter((_, i) => i !== dragged.index) }
      }
      if (section.id === sectionId) {
        const tiles = [...section.tiles]
        tiles.splice(index, 0, tile)
        return { ...section, tiles }
      }
      return section
    })

    onChange({ ...layout, sections })
    setDragged(null)
  }

  return (
    <>
      {layout.sections.map((section, sectionIndex) => (
        <div
          key={section.id}
          className={`ix-panel ix-cluster ix-rise ix-d3${editing ? ' ix-section-editing' : ''}`}
          data-audit={`live-section-${section.id}`}
          draggable={editing}
          onDragStart={(event) => {
            if (!editing) return
            // Only the section header starts a section drag; tiles have their own handler.
            if ((event.target as HTMLElement).closest('.ix-grid-3')) return
            setDraggedSection(sectionIndex)
          }}
          onDragOver={(event) => { if (editing && draggedSection != null) event.preventDefault() }}
          onDrop={(event) => {
            if (draggedSection == null) return
            event.preventDefault()
            moveSection(draggedSection, sectionIndex)
            setDraggedSection(null)
          }}
        >
          <div className="ix-cluster-head">
            <div className="t">
              <span className="ix-kick">Sektion {String(sectionIndex + 1).padStart(2, '0')}</span>
              {editing ? (
                <input
                  className="ix-section-title-input"
                  value={section.title}
                  onChange={(event) => updateSection(section.id, (current) => ({ ...current, title: event.target.value }))}
                  aria-label="Bereichsname"
                />
              ) : (
                <h3>{section.title}</h3>
              )}
            </div>
            {editing && (
              <div className="ix-section-tools">
                <button
                  type="button"
                  className="ix-btn"
                  aria-label={`${section.title} nach oben`}
                  disabled={sectionIndex === 0}
                  onClick={() => moveSection(sectionIndex, sectionIndex - 1)}
                >
                  ↑
                </button>
                <button
                  type="button"
                  className="ix-btn"
                  aria-label={`${section.title} nach unten`}
                  disabled={sectionIndex === layout.sections.length - 1}
                  onClick={() => moveSection(sectionIndex, sectionIndex + 1)}
                >
                  ↓
                </button>
                <button
                  type="button"
                  className="ix-btn"
                  onClick={() => onChange({ ...layout, sections: layout.sections.filter((item) => item.id !== section.id) })}
                >
                  Bereich entfernen
                </button>
              </div>
            )}
          </div>

          <div className="ix-grid-3">
            {section.tiles.map((tile, index) => {
              const span = Math.min(Math.max(tile.span ?? 1, 1), 3)
              const isCamera = tile.kind === 'Camera' && tile.entityId
              const metric = isCamera ? null : resolveTile(tile, metricsByKey, entityValues)
              return (
                <div
                  key={tile.id}
                  className={`ix-tile-slot${editing ? ' ix-tile-drag' : ''}`}
                  style={{ gridColumn: `span ${span}` }}
                  draggable={editing}
                  onDragStart={(event) => { event.stopPropagation(); setDragged({ sectionId: section.id, index }) }}
                  onDragOver={(event) => { if (editing && dragged) event.preventDefault() }}
                  onDrop={(event) => { event.preventDefault(); event.stopPropagation(); dropOn(section.id, index) }}
                >
                  {isCamera ? (
                    <DashboardCameraTile tentId={layout.tentId} entityId={tile.entityId!} label={tile.label} />
                  ) : (
                    renderTile(metric!, tile.kind === 'Metric' ? (tile.metricKey ?? tile.id) : tile.id)
                  )}
                  {editing && (
                    <div className="ix-tile-tools">
                      <button
                        type="button"
                        aria-label="Schmaler"
                        disabled={span === 1}
                        onClick={() => updateSection(section.id, (current) => ({
                          ...current,
                          tiles: current.tiles.map((item) => (item.id === tile.id ? { ...item, span: span - 1 } : item)),
                        }))}
                      >
                        −
                      </button>
                      <button
                        type="button"
                        aria-label="Breiter"
                        disabled={span === 3}
                        onClick={() => updateSection(section.id, (current) => ({
                          ...current,
                          tiles: current.tiles.map((item) => (item.id === tile.id ? { ...item, span: span + 1 } : item)),
                        }))}
                      >
                        +
                      </button>
                      <button
                        type="button"
                        className="ix-tile-remove"
                        aria-label={`${tile.label ?? metric?.label ?? 'Kachel'} entfernen`}
                        onClick={() => updateSection(section.id, (current) => ({ ...current, tiles: current.tiles.filter((item) => item.id !== tile.id) }))}
                      >
                        ×
                      </button>
                    </div>
                  )}
                </div>
              )
            })}
            {editing && (
              <div
                className="ix-tile-dropzone"
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => { event.preventDefault(); event.stopPropagation(); dropOn(section.id, section.tiles.length) }}
              >
                hierher ziehen
              </div>
            )}
          </div>
        </div>
      ))}
    </>
  )
}
