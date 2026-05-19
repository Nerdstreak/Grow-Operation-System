import type { CSSProperties } from 'react'
import type { HydroSetupLayoutType, ReservoirPosition } from '../types'
import { classNames } from '../utils'

type RdwcPreviewProps = {
  layoutType: HydroSetupLayoutType
  potCount: number
  reservoirPosition: ReservoirPosition
  hydroStyle?: 'DWC' | 'RDWC'
  compact?: boolean
  editable?: boolean
  onLayoutChange?: (value: HydroSetupLayoutType) => void
  onReservoirPositionChange?: (value: ReservoirPosition) => void
  onPotCountChange?: (value: number) => void
}

const layoutOptions: HydroSetupLayoutType[] = ['SingleBucket', 'Row', 'Grid2x2', 'Grid2x3', 'Grid2x4', 'Custom']
const reservoirPositions: ReservoirPosition[] = ['None', 'Left', 'Right', 'Top', 'Bottom', 'External']

export function RdwcPreview({
  layoutType,
  potCount,
  reservoirPosition,
  hydroStyle = 'RDWC',
  compact = false,
  editable = false,
  onLayoutChange,
  onReservoirPositionChange,
  onPotCountChange,
}: RdwcPreviewProps) {
  const safePotCount = Math.max(1, Math.min(12, Math.round(potCount || 1)))
  const isDwc = hydroStyle === 'DWC'
  const effectiveLayout = isDwc ? 'SingleBucket' : layoutType
  const effectiveReservoir = isDwc ? 'None' : reservoirPosition
  const grid = calculateGrid(effectiveLayout, isDwc ? 1 : safePotCount)
  const sites = Array.from({ length: isDwc ? 1 : safePotCount }, (_, index) => index + 1)

  return (
    <div className={classNames('rdwc-preview', compact && 'compact')} data-audit="hydro-preview">
      {editable && (
        <div className="rdwc-preview__controls" data-audit="hydro-layout-controls">
          <label>
            <span>Sites</span>
            <input
              data-audit="hydro-pot-count"
              type="number"
              min={isDwc ? 1 : 2}
              max={12}
              value={safePotCount}
              disabled={isDwc}
              onChange={(event) => onPotCountChange?.(Number.parseInt(event.target.value, 10) || (isDwc ? 1 : 2))}
            />
          </label>
          <label>
            <span>Layout-Typ</span>
            <select data-audit="hydro-layout-select" value={effectiveLayout} disabled={isDwc} onChange={(event) => onLayoutChange?.(event.target.value as HydroSetupLayoutType)}>
              {layoutOptions.map((value) => <option key={value} value={value}>{formatLayout(value)}</option>)}
            </select>
          </label>
          <label>
            <span>Tankposition</span>
            <select data-audit="hydro-reservoir-select" value={effectiveReservoir} disabled={isDwc} onChange={(event) => onReservoirPositionChange?.(event.target.value as ReservoirPosition)}>
              {reservoirPositions.map((value) => <option key={value} value={value}>{formatReservoirPosition(value)}</option>)}
            </select>
          </label>
        </div>
      )}

      <div className={classNames('rdwc-preview__stage', `tank-${effectiveReservoir.toLowerCase()}`)}>
        {hydroStyle === 'RDWC' && effectiveReservoir !== 'None' && <div className="rdwc-preview__tank">Tank</div>}
        <div
          className="rdwc-preview__sites"
          style={{ '--rdwc-cols': String(grid.columns), '--rdwc-rows': String(grid.rows) } as CSSProperties}
        >
          {sites.map((site) => <div key={site} className="rdwc-preview__site">{isDwc ? 'DWC' : site}</div>)}
        </div>
      </div>
      <span className="rdwc-preview__caption">
        {formatLayout(effectiveLayout)} · Tank {formatReservoirPosition(effectiveReservoir)}
      </span>
    </div>
  )
}

function calculateGrid(layout: HydroSetupLayoutType, count: number) {
  if (layout === 'SingleBucket') return { columns: 1, rows: 1 }
  if (layout === 'Grid2x2') return { columns: 2, rows: 2 }
  if (layout === 'Grid2x3') return { columns: 3, rows: 2 }
  if (layout === 'Grid2x4') return { columns: 4, rows: 2 }
  if (layout === 'Row') return { columns: Math.min(4, count), rows: Math.ceil(count / Math.min(4, count)) }
  const columns = count <= 2 ? count : count <= 4 ? 2 : count <= 6 ? 3 : 4
  return { columns, rows: Math.ceil(count / columns) }
}

function formatLayout(value: HydroSetupLayoutType) {
  return value === 'SingleBucket' ? 'Einzeleimer'
    : value === 'Row' ? 'Reihe'
      : value === 'Grid2x2' ? '2×2'
        : value === 'Grid2x3' ? '2×3'
          : value === 'Grid2x4' ? '2×4'
            : 'Flexibel'
}

function formatReservoirPosition(value: ReservoirPosition) {
  return value === 'None' ? 'keiner'
    : value === 'Left' ? 'links'
      : value === 'Right' ? 'rechts'
        : value === 'Top' ? 'oben'
          : value === 'Bottom' ? 'unten'
            : 'extern'
}
