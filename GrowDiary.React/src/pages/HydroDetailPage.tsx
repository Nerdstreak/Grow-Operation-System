import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch } from '../api'
import type { GrowSummary, HydroSetupDto } from '../types'
import { V1Alert, V1Badge, V1Card, V1Empty, V1LinkButton, V1Page, V1Section } from '../components/v1'
import { formatNumber } from '../utils'

function HydroDetailPage() {
  const { setupId } = useParams()
  const [setup, setSetup] = useState<HydroSetupDto | null>(null)
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      if (!setupId) return
      setLoading(true)
      setError(null)
      try {
        const [loadedSetup, loadedGrows] = await Promise.all([
          apiFetch<HydroSetupDto>(`/api/hydro-setups/${setupId}`, { signal: controller.signal }),
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }).catch(() => []),
        ])
        if (controller.signal.aborted) return
        setSetup(loadedSetup)
        setGrows(loadedGrows)
      } catch (caught) {
        if (!controller.signal.aborted) setError(caught instanceof Error ? caught.message : 'Hydro-Setup konnte nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [setupId])

  const linkedGrows = useMemo(() => setup ? grows.filter((grow) => grow.setupId === setup.id) : [], [grows, setup])

  if (loading) return <V1Page eyebrow="Hydro" title="Lade Setup..."><V1Empty title="Hydro-Setup wird geladen." /></V1Page>
  if (!setup) return <V1Page eyebrow="Hydro" title="Nicht gefunden" action={<V1LinkButton to="/hydro">Zurück</V1LinkButton>}><V1Alert message={error ?? 'Hydro-Setup nicht gefunden.'} tone="warn" /></V1Page>

  return (
    <V1Page eyebrow={setup.hydroStyle} title={setup.name} action={<div className="v1-action-row"><V1LinkButton to="/hydro" variant="ghost">Hydro</V1LinkButton><V1LinkButton to="/hydro/new">Neu</V1LinkButton></div>}>
      {error && <V1Alert message={error} tone="warn" />}
      <section className="v1-kpi-grid">
        <V1Card><span className="v1-card-kicker">Zelt</span><h2>{setup.tentName ?? 'offen'}</h2><p>{setup.status}</p></V1Card>
        <V1Card><span className="v1-card-kicker">Volumen</span><h2>{formatNumber(setup.totalVolumeLiters, 0)} L</h2><p>Gesamt</p></V1Card>
        <V1Card><span className="v1-card-kicker">Sites</span><h2>{setup.potCount ?? '–'}</h2><p>{setup.layoutType}</p></V1Card>
        <V1Card tone={setup.status === 'Active' ? 'ok' : 'neutral'}><span className="v1-card-kicker">Status</span><h2>{setup.status === 'Active' ? 'aktiv' : 'Archiv'}</h2><p>{setup.reservoirPosition}</p></V1Card>
      </section>

      <V1Section title="Details">
        <V1Card>
          <div className="v1-info-grid compact">
            <Info label="Topf" value={formatLiters(setup.potSizeLiters)} />
            <Info label="Tank" value={formatLiters(setup.reservoirLiters)} />
            <Info label="Umwälzung" value={setup.hasCirculationPump ? 'ja' : 'nein'} />
            <Info label="Luft" value={setup.hasAirPump ? `${setup.airStoneCount ?? '–'} Steine` : 'nein'} />
            <Info label="Chiller" value={setup.hasChiller ? 'ja' : 'nein'} />
            <Info label="UV-C" value={setup.hasUvSterilizer ? 'ja' : 'nein'} />
          </div>
          {setup.notes && <p>{setup.notes}</p>}
        </V1Card>
      </V1Section>

      <V1Section title="Verknüpfte Grows">
        {linkedGrows.length === 0 ? <V1Empty title="Keine aktiven Grows verknüpft" /> : <div className="v1-list">{linkedGrows.map((grow) => <Link key={grow.id} to={`/grows/${grow.id}`} className="v1-list-row"><strong>{grow.name}</strong><span>{grow.tentName ?? 'ohne Zelt'} · {grow.status}</span><V1Badge>{grow.latestStage ?? grow.status}</V1Badge></Link>)}</div>}
      </V1Section>
    </V1Page>
  )
}

function Info({ label, value }: { label: string; value: string }) { return <div className="v1-info"><span>{label}</span><strong>{value}</strong></div> }
function formatLiters(value: number | null | undefined) { return value == null ? '–' : `${value.toLocaleString('de-DE', { maximumFractionDigits: 1 })} L` }

export default HydroDetailPage
