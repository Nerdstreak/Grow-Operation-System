import { Link } from 'react-router-dom'
import type { GrowDetail, MeasurementDto } from '../../types'
import { formatNumber } from '../../utils'
import { formatGrowHydroMedium } from './grow-detail-model'

// Harvest only makes sense once the plant is in bloom or later, so the Ernte action
// on the overview appears only then — otherwise it's hidden to keep the page clean.
const HARVEST_READY_STAGES: ReadonlySet<string> = new Set(['Flower', 'Finish', 'Dry'])

type GrowDetailOverviewHeroProps = {
  grow: GrowDetail
  latest: MeasurementDto | null
  measurementCount: number
  openTaskCount: number
}

export function GrowDetailOverviewHero({
  grow,
  latest,
  measurementCount,
  openTaskCount,
}: GrowDetailOverviewHeroProps) {
  const currentStage: string | null = latest?.stage ?? grow.entryPoint ?? null
  const canHarvest = currentStage != null && HARVEST_READY_STAGES.has(currentStage)
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
        {canHarvest && <Link className="btn" to={`/grows/${grow.id}/harvest`}>Ernte</Link>}
        <Link className="btn" to={`/analyse?leftGrowId=${grow.id}`}>Vergleichen</Link>
        <a className="btn" href={`/grows/${grow.id}/export`}>Export</a>
      </div>
    </div>
  )
}
