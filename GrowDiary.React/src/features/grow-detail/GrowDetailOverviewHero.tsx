import type { GrowDetail, MeasurementDto } from '../../types'
import { formatNumber } from '../../utils'
import { V1LinkButton, V1Stat } from '../../components/v1'
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

  // Der Grow-Name steht schon in der Seitenüberschrift darüber; hier stünde er
  // zum zweiten Mal. Die Zeile darunter trägt das, was er nicht sagt.
  return (
    <div className="grow-hero">
      <p className="grow-hero-sub">
        {grow.strain ?? 'Unbekannter Strain'} · {grow.breeder ?? 'kein Breeder'} · {formatGrowHydroMedium(grow)} · {grow.tentName ?? 'ohne Zelt'}
      </p>

      <div className="v1-kpi-grid">
        <V1Stat label="Reservoir pH" value={formatNumber(latest?.reservoirPh, 2)} />
        <V1Stat label="Reservoir EC" value={formatNumber(latest?.reservoirEc, 2)} unit="mS/cm" />
        <V1Stat label="Lufttemperatur" value={latest ? formatNumber(latest.airTemperatureC, 1) : '—'} unit={latest ? '°C' : undefined} />
        <V1Stat label="Luftfeuchte" value={latest ? formatNumber(latest.humidityPercent, 0) : '—'} unit={latest ? '%' : undefined} />
        <V1Stat label="Messungen" value={measurementCount} />
        <V1Stat label="Offene Aufgaben" value={openTaskCount} />
      </div>

      <div className="v1-action-row">
        <V1LinkButton to={`/grows/${grow.id}/addback`}>Addback</V1LinkButton>
        {canHarvest && <V1LinkButton to={`/grows/${grow.id}/harvest`} variant="primary">Ernte erfassen</V1LinkButton>}
        {/* /analyse gibt es nur noch als Weiterleitung — direkt auf den Tab zeigen. */}
        <V1LinkButton to={`/archiv?tab=vergleich&leftGrowId=${grow.id}`}>Vergleichen</V1LinkButton>
        <a className="v1-button" href={`/grows/${grow.id}/export`}>Export</a>
      </div>
    </div>
  )
}
