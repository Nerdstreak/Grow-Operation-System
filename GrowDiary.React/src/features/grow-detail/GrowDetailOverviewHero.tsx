import { Link } from 'react-router-dom'
import type { GrowDetail, MeasurementDto } from '../../types'
import { formatNumber } from '../../utils'
import { formatGrowHydroMedium } from './grow-detail-model'

type GrowAction = 'germination' | 'rooting' | 'flip'

type GrowDetailOverviewHeroProps = {
  grow: GrowDetail
  latest: MeasurementDto | null
  measurementCount: number
  openTaskCount: number
  saving: string | null
  canConfirmGermination: boolean
  canConfirmRooting: boolean
  canFlipToFlower: boolean
  onGrowAction: (action: GrowAction) => void
}

export function GrowDetailOverviewHero({
  grow,
  latest,
  measurementCount,
  openTaskCount,
  saving,
  canConfirmGermination,
  canConfirmRooting,
  canFlipToFlower,
  onGrowAction,
}: GrowDetailOverviewHeroProps) {
  return (
    <div className="grow-hero">
      <div className="grow-hero-title">{grow.name}</div>
      <div className="grow-hero-sub">{grow.strain ?? 'Unbekannter Strain'} · {grow.breeder ?? 'kein Breeder'} · {formatGrowHydroMedium(grow)} · {grow.tentName ?? 'ohne Zelt'}</div>
      <div className="grow-kpis">
        <div className="grow-kpi">
          <div className="grow-kpi-val">{formatNumber(latest?.reservoirPh, 2)}</div>
          <div className="grow-kpi-label">Reservoir pH</div>
        </div>
        <div className="grow-kpi">
          <div className="grow-kpi-val">{formatNumber(latest?.reservoirEc, 2)}</div>
          <div className="grow-kpi-label">Reservoir EC</div>
        </div>
        <div className="grow-kpi">
          <div className="grow-kpi-val">{latest ? `${formatNumber(latest.airTemperatureC, 1)}°` : '—'}</div>
          <div className="grow-kpi-label">Lufttemp</div>
        </div>
        <div className="grow-kpi">
          <div className="grow-kpi-val">{latest ? `${formatNumber(latest.humidityPercent, 0)}%` : '—'}</div>
          <div className="grow-kpi-label">Luftfeuchte</div>
        </div>
        <div className="grow-kpi">
          <div className="grow-kpi-val">{measurementCount}</div>
          <div className="grow-kpi-label">Messungen</div>
        </div>
        <div className="grow-kpi">
          <div className="grow-kpi-val">{openTaskCount}</div>
          <div className="grow-kpi-label">Offene Tasks</div>
        </div>
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, marginTop: 14 }}>
        <Link className="btn" to={`/grows/${grow.id}/addback`}>Addback</Link>
        <Link className="btn" to={`/grows/${grow.id}/harvest`}>Harvest</Link>
        <Link className="btn" to={`/analyse?leftGrowId=${grow.id}`}>Vergleichen</Link>
        <a className="btn" href={`/grows/${grow.id}/export`}>Export</a>
        {canConfirmGermination && (
          <button type="button" className="btn" disabled={saving === 'action-germination'} onClick={() => onGrowAction('germination')}>
            {saving === 'action-germination' ? 'Bestätigt...' : 'Keimung bestätigen'}
          </button>
        )}
        {canConfirmRooting && (
          <button type="button" className="btn" disabled={saving === 'action-rooting'} onClick={() => onGrowAction('rooting')}>
            {saving === 'action-rooting' ? 'Bestätigt...' : 'Bewurzelung bestätigen'}
          </button>
        )}
        {canFlipToFlower && (
          <button type="button" className="btn" disabled={saving === 'action-flip'} onClick={() => onGrowAction('flip')}>
            {saving === 'action-flip' ? 'Trägt ein...' : 'Flip zu 12/12'}
          </button>
        )}
      </div>
    </div>
  )
}
