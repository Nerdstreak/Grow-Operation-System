import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowSummary } from '../types'
import { V1Alert, V1Card, V1Empty, V1LinkButton, V1Page, V1Section, V1Stat } from '../components/v1'
import { formatDateTime, formatNumber } from '../utils'

const addbackSteps = ['Grow', 'Istwerte', 'Ziel', 'Dosierung', 'Nachmessung']

function AddbackHubPage() {
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const data = await apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal })
        setGrows(data)
      } catch (caught) {
        if (!controller.signal.aborted) setError(caught instanceof ApiRequestError ? caught.message : 'Grows konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  const activeGrows = useMemo(() => grows.filter((grow) => grow.status === 'Running' || grow.status === 'Planning'), [grows])
  const hydroGrows = useMemo(() => activeGrows.filter((grow) => grow.hydroStyle === 'DWC' || grow.hydroStyle === 'RDWC'), [activeGrows])
  const primaryGrow = hydroGrows[0] ?? null

  return (
    <V1Page eyebrow="Reservoir" title="Addback" action={<V1LinkButton to="/grows/new" variant="primary">Grow starten</V1LinkButton>}>
      {error && <V1Alert message={error} tone="warn" />}

      <section className="v1-addback-command">
        <V1Card className="v1-addback-command-main">
          <span className="v1-card-kicker">Nächster Addback</span>
          <h2>{primaryGrow?.name ?? 'Kein Hydro-Grow aktiv'}</h2>
          <p>{primaryGrow ? `${primaryGrow.strain ?? 'Sorte offen'} · ${primaryGrow.tentName ?? 'ohne Zelt'} · ${primaryGrow.hydroStyle}` : 'Addback braucht einen aktiven DWC/RDWC-Grow.'}</p>
          <div className="v1-addback-current-values">
            <Info label="pH" value={formatNumber(primaryGrow?.latestReservoirPh, 2)} />
            <Info label="EC" value={formatNumber(primaryGrow?.latestReservoirEc, 2)} />
            <Info label="Messung" value={formatDateTime(primaryGrow?.latestMeasurementAt)} />
          </div>
          {primaryGrow ? <V1LinkButton to={`/grows/${primaryGrow.id}/addback`} variant="primary">Addback starten</V1LinkButton> : <V1LinkButton to="/grows/new" variant="primary">Grow starten</V1LinkButton>}
        </V1Card>

        <div className="v1-addback-flow-strip rc2" aria-label="Addback Ablauf">
          {addbackSteps.map((item, index) => <div key={item}><span>{index + 1}</span><strong>{item}</strong></div>)}
        </div>
      </section>

      <section className="v1-kpi-grid v1-kpi-grid-compact"><V1Stat label="Aktive Grows" value={activeGrows.length} /><V1Stat label="Hydro" value={hydroGrows.length} /><V1Stat label="Bereit" value={hydroGrows.length} /></section>

      <V1Section title="Grow wählen">
        {loading ? <V1Empty title="Lade Grows..." /> : hydroGrows.length === 0 ? <V1Empty title="Kein DWC/RDWC-Grow" text="Addback braucht einen aktiven Grow mit Hydro-Setup." action={<V1LinkButton to="/grows/new" variant="primary">Grow starten</V1LinkButton>} /> : (
          <div className="v1-card-grid v1-card-grid-compact">
            {hydroGrows.map((grow) => (
              <Link key={grow.id} to={`/grows/${grow.id}/addback`} className="v1-grow-card-link">
                <V1Card className="v1-grow-card v1-grow-card-compact">
                  <span className="v1-card-kicker">{grow.hydroStyle}</span>
                  <h2>{grow.name}</h2>
                  <p>{grow.strain ?? 'Sorte offen'} · {grow.tentName ?? 'ohne Zelt'}</p>
                  <div className="v1-info-grid compact">
                    <Info label="pH" value={formatNumber(grow.latestReservoirPh, 2)} />
                    <Info label="EC" value={formatNumber(grow.latestReservoirEc, 2)} />
                    <Info label="Messung" value={formatDateTime(grow.latestMeasurementAt)} />
                  </div>
                  <div className="v1-button is-primary full">Addback</div>
                </V1Card>
              </Link>
            ))}
          </div>
        )}
      </V1Section>
    </V1Page>
  )
}

function Info({ label, value }: { label: string; value: string }) { return <div className="v1-info"><span>{label}</span><strong>{value}</strong></div> }

export default AddbackHubPage
