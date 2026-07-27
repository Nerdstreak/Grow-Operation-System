import { useState } from 'react'
import type { MetricPayload } from '../../types'
import type { HistoryPoint } from '../../components/SensorChart'
import { MetricTile } from './MetricTile'
import { decimalsForMetric } from './metric-tile-model'
import { SectionHead } from './DashboardEditor'
import {
  moveSection,
  moveTile,
  removeSection,
  removeTile,
  renameSection,
  resolveTile,
  type DashboardLayout,
  type EntityValue,
} from './dashboard-layout'
import { classNames } from '../../utils'

/**
 * Die Messwert-Bereiche aus dem Layout des Zelts.
 *
 * Im Anpassen-Modus lassen sich Kacheln ziehen — innerhalb eines Bereichs und
 * zwischen zweien. Das Rechnen dahinter liegt in `dashboard-layout.ts` und ist
 * dort geprüft; hier steht nur, was der Zeigefinger auslöst.
 */
export function DashboardBands({
  layout, metricsByKey, entityValues, trends, editing, onChange,
}: {
  layout: DashboardLayout
  metricsByKey: Map<string, MetricPayload>
  entityValues: Map<string, EntityValue>
  trends: Map<string, HistoryPoint[]>
  editing: boolean
  onChange: (layout: DashboardLayout) => void
}) {
  const [dragged, setDragged] = useState<{ sectionId: string; index: number } | null>(null)

  function drop(sectionId: string, index: number) {
    if (!dragged) return
    onChange(moveTile(layout, dragged, { sectionId, index }))
    setDragged(null)
  }

  return (
    <>
      {layout.sections.map((section, sectionIndex) => (
        <div key={section.id} className="ls-band" data-audit={`live-section-${section.id}`}>
          <SectionHead
            title={section.title}
            editing={editing}
            canUp={sectionIndex > 0}
            canDown={sectionIndex < layout.sections.length - 1}
            onRename={(value) => onChange(renameSection(layout, section.id, value))}
            onRemove={() => onChange(removeSection(layout, section.id))}
            onUp={() => onChange(moveSection(layout, sectionIndex, sectionIndex - 1))}
            onDown={() => onChange(moveSection(layout, sectionIndex, sectionIndex + 1))}
          />

          <div className={classNames('gos-metric-row', editing && 'is-editing')}>
            {section.tiles.map((tile, index) => {
              const metric = resolveTile(tile, metricsByKey, entityValues)
              const trend = tile.kind === 'Metric' && tile.metricKey ? trends.get(tile.metricKey) : undefined
              return (
                <div
                  key={tile.id}
                  className={classNames('ls-tile-slot', editing && 'is-draggable')}
                  style={{ flex: `${Math.min(Math.max(tile.span, 1), 3)} 1 150px` }}
                  draggable={editing}
                  onDragStart={() => setDragged({ sectionId: section.id, index })}
                  onDragEnd={() => setDragged(null)}
                  onDragOver={(event) => { if (editing && dragged) event.preventDefault() }}
                  onDrop={(event) => { event.preventDefault(); drop(section.id, index) }}
                >
                  <MetricTile
                    label={metric.label}
                    value={metric.numericValue}
                    unit={metric.unit}
                    targetMin={metric.targetMin}
                    targetMax={metric.targetMax}
                    decimals={decimalsForMetric(metric.key)}
                    display={metric.numericValue == null && metric.value !== '–' ? metric.value : undefined}
                    footer={metric.targetMin == null && metric.targetMax == null ? (metric.hint ?? undefined) : undefined}
                    trend={trend}
                  />
                  {editing && (
                    <span className="ls-tile-tools">
                      <span className="dots" aria-hidden="true">⠿</span>
                      <button
                        type="button"
                        className="x"
                        aria-label={`${metric.label} entfernen`}
                        onClick={() => onChange(removeTile(layout, tile.id))}
                      >
                        ×
                      </button>
                    </span>
                  )}
                </div>
              )
            })}

            {editing && (
              // Das Ziel zum Fallenlassen. Ohne es weiss niemand, wohin eine
              // gezogene Kachel ueberhaupt darf — und ein leerer Bereich waere
              // sonst gar nicht zu befuellen.
              <div
                className="ls-dropzone"
                onDragOver={(event) => event.preventDefault()}
                onDrop={(event) => { event.preventDefault(); drop(section.id, section.tiles.length) }}
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
