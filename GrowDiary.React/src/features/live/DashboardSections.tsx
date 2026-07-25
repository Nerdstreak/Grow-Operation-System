import { useState } from 'react'
import type { MetricPayload } from '../../types'
import type { DashboardLayout, DashboardSection, EntityValue } from './useTentDashboard'
import { resolveTile } from './dashboard-tile-model'

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
 * The dashboard's metric sections, driven by the tent's saved layout instead of fixed
 * code. In edit mode tiles can be dragged between and within sections, renamed or removed.
 */
export function DashboardSections({ layout, metricsByKey, entityValues, editing, onChange, renderTile }: Props) {
  const [dragged, setDragged] = useState<{ sectionId: string; index: number } | null>(null)

  const updateSection = (sectionId: string, update: (section: DashboardSection) => DashboardSection) =>
    onChange({ ...layout, sections: layout.sections.map((section) => (section.id === sectionId ? update(section) : section)) })

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
        <div key={section.id} className="ix-panel ix-cluster ix-rise ix-d3" data-audit={`live-section-${section.id}`}>
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
              <button type="button" className="ix-btn" onClick={() => onChange({ ...layout, sections: layout.sections.filter((item) => item.id !== section.id) })}>
                Bereich entfernen
              </button>
            )}
          </div>

          <div className="ix-grid-3">
            {section.tiles.map((tile, index) => {
              const metric = resolveTile(tile, metricsByKey, entityValues)
              return (
                <div
                  key={tile.id}
                  className={editing ? 'ix-tile-drag' : undefined}
                  draggable={editing}
                  onDragStart={() => setDragged({ sectionId: section.id, index })}
                  onDragOver={(event) => { if (editing) event.preventDefault() }}
                  onDrop={(event) => { event.preventDefault(); dropOn(section.id, index) }}
                >
                  {renderTile(metric, tile.kind === 'Metric' ? (tile.metricKey ?? tile.id) : tile.id)}
                  {editing && (
                    <button
                      type="button"
                      className="ix-tile-remove"
                      aria-label={`${metric.label} entfernen`}
                      onClick={() => updateSection(section.id, (current) => ({ ...current, tiles: current.tiles.filter((item) => item.id !== tile.id) }))}
                    >
                      ×
                    </button>
                  )}
                </div>
              )
            })}
            {editing && (
              <div
                className="ix-tile-dropzone"
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => { event.preventDefault(); dropOn(section.id, section.tiles.length) }}
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
