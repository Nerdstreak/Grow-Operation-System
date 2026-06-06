import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { AddbackLogDto, GrowDetail, GrowSummary, HydroSetupDto } from '../types'
import { formatNumber } from '../utils'
import { ChangeoutsPanel } from '../features/changeouts/ChangeoutsPanel'
import '../features/addback/addback-instrument.css'

type GrowWithLogs = { detail: GrowDetail; logs: AddbackLogDto[] }
type ProtocolGroup = { hydroSetupId: number | null; name: string; tentName: string | null; growNames: string[]; logs: AddbackLogDto[] }

function AddbackHubPage() {
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [protocolGroups, setProtocolGroups] = useState<ProtocolGroup[]>([])
  const [selectedGrowId, setSelectedGrowId] = useState<number | null>(null)
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
        const hydro = data.filter((grow) => (grow.status === 'Running' || grow.status === 'Planning') && (grow.hydroStyle === 'DWC' || grow.hydroStyle === 'RDWC'))
        const detailsAndLogs = await Promise.all(hydro.map(async (grow) => {
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
  const allLogs = useMemo(() => protocolGroups.flatMap((group) => group.logs), [protocolGroups])
  const latestByGrowId = useMemo(() => {
    const map = new Map<number, AddbackLogDto>()
    for (const log of allLogs) {
      const existing = map.get(log.growId)
      if (!existing || log.performedAtUtc.localeCompare(existing.performedAtUtc) > 0) map.set(log.growId, log)
    }
    return map
  }, [allLogs])

  const selectedGrow = hydroGrows.find((grow) => grow.id === selectedGrowId) ?? hydroGrows[0] ?? null
  const selectedLogs = useMemo(
    () => (selectedGrow ? allLogs.filter((log) => log.growId === selectedGrow.id).sort((a, b) => b.performedAtUtc.localeCompare(a.performedAtUtc)) : []),
    [allLogs, selectedGrow],
  )
  const lastAddback = selectedGrow ? latestByGrowId.get(selectedGrow.id) ?? null : null

  const topBar = (
    <div className="ix-top">
      <div className="ix-brand"><span className="dot" /><b>RESERVOIR</b></div>
      {hydroGrows.length > 0 && (
        <div className="ix-tents">
          {hydroGrows.map((grow) => (
            <button key={grow.id} type="button" className={`ix-tent ${grow.id === selectedGrow?.id ? 'on' : ''}`} onClick={() => setSelectedGrowId(grow.id)}>
              {grow.name} · {grow.hydroStyle}
            </button>
          ))}
        </div>
      )}
      <Link className="ix-btn" to="/grows/new" style={{ marginLeft: 'auto' }}>Grow anlegen</Link>
    </div>
  )

  if (loading) {
    return <div className="ix-addback" data-audit="addback-hub">{topBar}<div className="ix-panel ix-addback-empty"><h2>Lade Addback …</h2></div></div>
  }

  if (!selectedGrow) {
    return (
      <div className="ix-addback" data-audit="addback-hub">
        {topBar}
        {error && <div className="ix-empty-line" style={{ color: 'var(--ix-red)' }}>{error}</div>}
        <div className="ix-panel ix-addback-empty ix-rise ix-d1">
          <span className="ix-corner ix-tl" /><span className="ix-corner ix-br" />
          <h2>Kein aktiver Hydro-Grow</h2>
          <p>Addback braucht einen aktiven DWC/RDWC-Grow mit Hydro-Setup.</p>
          <div className="ix-addback-cta" style={{ justifyContent: 'center' }}>
            <Link className="ix-btn pri" to="/grows/new">Grow anlegen</Link>
            <Link className="ix-btn" to="/hydro">Hydro öffnen</Link>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="ix-addback" data-audit="addback-hub">
      {topBar}
      {error && <div className="ix-empty-line" style={{ color: 'var(--ix-red)' }}>{error}</div>}

      <section className="ix-addback-top-grid">
        <div className="ix-panel ix-addback-hero ix-rise ix-d1">
          <span className="ix-corner ix-tl" /><span className="ix-corner ix-tr" /><span className="ix-corner ix-bl" /><span className="ix-corner ix-br" />
          <div className="ix-addback-res">
            <div className="ix-res-cell ph"><div className="lab">pH</div><div className="val">{formatNumber(selectedGrow.latestReservoirPh, 2)}</div></div>
            <div className="ix-res-cell ec"><div className="lab">EC</div><div className="val">{formatNumber(selectedGrow.latestReservoirEc, 2)}<u>mS</u></div></div>
          </div>
          <div>
            <div className="ix-kick">Nächster Addback · {selectedGrow.tentName ?? 'ohne Zelt'}</div>
            <h1>{selectedGrow.name}</h1>
            <div className="sub">{selectedGrow.strain ?? 'Sorte offen'} · {selectedGrow.hydroStyle}</div>
            <div className="ix-facts">
              <div><span>Hydro</span><strong>{selectedGrow.hydroStyle}</strong></div>
              <div><span>Letzter Addback</span><strong>{formatShortDateTime(lastAddback?.performedAtUtc)}</strong></div>
            </div>
            <div className="ix-addback-cta">
              <Link className="ix-btn pri" to={`/grows/${selectedGrow.id}/addback`}>Addback starten</Link>
              <Link className="ix-btn" to={`/grows/${selectedGrow.id}`}>Grow öffnen</Link>
            </div>
          </div>
        </div>

        <div className="ix-panel ix-addback-side ix-rise ix-d2">
          <span className="ix-corner ix-tl" /><span className="ix-corner ix-br" />
          <div className="ix-kick">Hub</div>
          <h2>{hydroGrows.length} Hydro-Grow{hydroGrows.length === 1 ? '' : 's'}</h2>
          <p>Reservoir-Pflege & Addback-Verlauf je Grow. Oben den Grow wählen, Werte prüfen und Addback starten.</p>
        </div>
      </section>

      <section className="ix-addback-kpis ix-rise ix-d3">
        <div className="ix-addback-kpi"><span>Aktive Grows</span><strong>{activeGrows.length}</strong></div>
        <div className="ix-addback-kpi"><span>Hydro</span><strong>{hydroGrows.length}</strong></div>
        <div className="ix-addback-kpi"><span>Verläufe</span><strong>{protocolGroups.length}</strong></div>
        <div className="ix-addback-kpi"><span>Logs</span><strong>{allLogs.length}</strong></div>
      </section>

      <ChangeoutsPanel growId={selectedGrow.id} growName={selectedGrow.name} />

      <div className="ix-panel ix-cluster ix-rise ix-d4" data-audit="addback-log-list" style={{ marginTop: 16 }}>
        <div className="ix-cluster-head">
          <div className="t"><span className="ix-kick">Verlauf</span><h3>Addback-Protokoll · {selectedGrow.name}</h3></div>
          {selectedLogs.length > 0 && <span className="ix-badge ix-b-ok">{selectedLogs.length}</span>}
        </div>
        {selectedLogs.length === 0 ? (
          <div className="ix-empty-line">Noch kein Addback für diesen Grow erfasst.</div>
        ) : (
          selectedLogs.slice(0, 8).map((log) => (
            <Link key={log.id} className="ix-grow-row" to={`/grows/${log.growId}/addback`}>
              <strong>{formatShortDateTime(log.performedAtUtc)}</strong>
              <span>EC {formatNumber(log.ecBefore, 2)} → {formatNumber(log.ecAfter ?? log.ecTarget, 2)} · pH {formatNumber(log.phBefore, 2)} → {formatNumber(log.phAfter, 2)}</span>
              <em>{formatNumber(log.litersAdded, 2)} L</em>
            </Link>
          ))
        )}
      </div>
    </div>
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

  return Array.from(groups.values()).map((group) => ({ ...group, logs: [...group.logs] }))
}

function formatShortDateTime(value: string | null | undefined) {
  if (!value) return '–'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '–'
  return new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit', year: '2-digit', hour: '2-digit', minute: '2-digit' }).format(date)
}

export default AddbackHubPage
