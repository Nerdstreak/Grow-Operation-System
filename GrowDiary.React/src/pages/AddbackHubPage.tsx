import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { AddbackLogDto, GrowDetail, GrowSummary, HydroSetupDto } from '../types'
import { V1Alert, V1Card, V1Empty, V1LinkButton, V1Page, V1Section, V1Stat } from '../components/v1'
import { formatDateTime, formatNumber } from '../utils'

type GrowWithLogs = {
  detail: GrowDetail
  logs: AddbackLogDto[]
}

type ProtocolGroup = {
  hydroSetupId: number | null
  name: string
  tentName: string | null
  growNames: string[]
  logs: AddbackLogDto[]
}

function AddbackHubPage() {
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [protocolGroups, setProtocolGroups] = useState<ProtocolGroup[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const [data, hydroSetups] = await Promise.all([
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }),
          apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true', { signal: controller.signal }).catch(() => []),
        ])
        if (controller.signal.aborted) return

        setGrows(data)
        const hydroGrows = data
          .filter((grow) => grow.status === 'Running' || grow.status === 'Planning')
          .filter((grow) => grow.hydroStyle === 'DWC' || grow.hydroStyle === 'RDWC')

        const detailsAndLogs = await Promise.all(hydroGrows.map(async (grow) => {
          try {
            const [detail, logs] = await Promise.all([
              apiFetch<GrowDetail>(`/api/grows/${grow.id}`, { signal: controller.signal }),
              apiFetch<AddbackLogDto[]>(`/api/grows/${grow.id}/addback/logs`, { signal: controller.signal }).catch(() => []),
            ])
            return { detail, logs } satisfies GrowWithLogs
          } catch {
            return null
          }
        }))

        if (controller.signal.aborted) return
        setProtocolGroups(buildProtocolGroups(detailsAndLogs.filter((item): item is GrowWithLogs => item !== null), hydroSetups))
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
  const totalLogs = useMemo(() => protocolGroups.reduce((sum, group) => sum + group.logs.length, 0), [protocolGroups])

  return (
    <V1Page eyebrow="Reservoir" title="Addback" action={<V1LinkButton to="/grows/new" variant="primary">Grow starten</V1LinkButton>}>
      {error && <V1Alert message={error} tone="warn" />}

      <section className="v1-addback-command" data-audit="addback-hub">
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

        <V1Card className="v1-addback-hub-summary">
          <span className="v1-card-kicker">Hub</span>
          <h2>{hydroGrows.length} Hydro-Grows verfügbar</h2>
          <p>Wähle einen Grow, starte einen Addback oder öffne den Verlauf nach Hydro-Setup.</p>
        </V1Card>
      </section>

      <section className="v1-kpi-grid v1-kpi-grid-compact"><V1Stat label="Aktive Grows" value={activeGrows.length} /><V1Stat label="Hydro" value={hydroGrows.length} /><V1Stat label="Hydro-Verläufe" value={protocolGroups.length} /><V1Stat label="Logs" value={totalLogs} /></section>

      <V1Section title="Addback-Verlauf nach Hydro-Setup">
        {loading ? <V1Empty title="Lade Addback-Verlauf..." /> : protocolGroups.length === 0 ? <V1Empty title="Noch kein Addback-Verlauf" text="Der Verlauf entsteht aus echten Addback-Logs und wird nach Hydro-Setup gruppiert, nicht aus der letzten Grow-Messung." /> : (
          <div className="v1-card-grid v1-card-grid-compact">
            {protocolGroups.map((group) => <ProtocolGroupCard key={group.hydroSetupId ?? `legacy-${group.name}`} group={group} />)}
          </div>
        )}
      </V1Section>

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

function ProtocolGroupCard({ group }: { group: ProtocolGroup }) {
  const latest = group.logs[0] ?? null
  return (
    <V1Card>
      <span className="v1-card-kicker">Hydro-Setup</span>
      <h2>{group.name}</h2>
      <p>{group.tentName ?? 'ohne Zelt'} · {group.growNames.join(', ')}</p>
      <div className="v1-info-grid compact">
        <Info label="Logs" value={String(group.logs.length)} />
        <Info label="Letzter" value={formatShortDateTime(latest?.performedAtUtc)} />
        <Info label="EC" value={latest ? `${formatNumber(latest.ecBefore, 2)} → ${formatNumber(latest.ecAfter ?? latest.ecTarget, 2)}` : '–'} />
        <Info label="Menge" value={latest ? `${formatNumber(latest.litersAdded, 2)} L` : '–'} />
      </div>
      <div className="v1-list">
        {group.logs.slice(0, 5).map((log) => (
          <Link key={log.id} className="v1-list-row" to={`/grows/${log.growId}/addback`}>
            <strong>{formatDateTime(log.performedAtUtc)}</strong>
            <span>EC {formatNumber(log.ecBefore, 2)} → {formatNumber(log.ecAfter ?? log.ecTarget, 2)} · pH {formatNumber(log.phBefore, 2)} → {formatNumber(log.phAfter, 2)}</span>
            <em>{formatNumber(log.litersAdded, 2)} L</em>
          </Link>
        ))}
      </div>
    </V1Card>
  )
}

function buildProtocolGroups(items: GrowWithLogs[], hydroSetups: HydroSetupDto[]): ProtocolGroup[] {
  const setupNames = new Map(hydroSetups.map((setup) => [setup.id, setup.name]))
  const setupTentNames = new Map(hydroSetups.map((setup) => [setup.id, setup.tentName ?? null]))
  const groups = new Map<string, ProtocolGroup>()

  for (const item of items) {
    for (const log of item.logs) {
      const hydroSetupId = log.hydroSetupId ?? item.detail.systemId ?? null
      const key = hydroSetupId == null ? `legacy-${item.detail.id}` : String(hydroSetupId)
      const existing = groups.get(key)
      if (existing) {
        existing.logs.push(log)
        if (!existing.growNames.includes(item.detail.name)) existing.growNames.push(item.detail.name)
        if (!existing.tentName) existing.tentName = item.detail.tentName
        continue
      }

      groups.set(key, {
        hydroSetupId,
        name: hydroSetupId == null ? 'Legacy / ohne HydroSetup' : setupNames.get(hydroSetupId) ?? `HydroSetup #${hydroSetupId}`,
        tentName: hydroSetupId == null ? item.detail.tentName : setupTentNames.get(hydroSetupId) ?? item.detail.tentName,
        growNames: [item.detail.name],
        logs: [log],
      })
    }
  }

  return Array.from(groups.values())
    .map((group) => ({ ...group, logs: [...group.logs].sort((a, b) => b.performedAtUtc.localeCompare(a.performedAtUtc)) }))
    .sort((a, b) => (b.logs[0]?.performedAtUtc ?? '').localeCompare(a.logs[0]?.performedAtUtc ?? ''))
}

function Info({ label, value }: { label: string; value: string }) { return <div className="v1-info"><span>{label}</span><strong>{value}</strong></div> }

function formatShortDateTime(value: string | null | undefined) {
  if (!value) return '–'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '–'
  return new Intl.DateTimeFormat('de-DE', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
}

export default AddbackHubPage
