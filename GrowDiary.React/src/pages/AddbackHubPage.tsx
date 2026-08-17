import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { AddbackLogDto, GrowDetail, GrowSummary, HydroSetupDto } from '../types'
import { formatNumber } from '../utils'
import { ChangeoutsPanel } from '../features/changeouts/ChangeoutsPanel'
import { V1Alert, V1Empty, V1Page, V1Skeleton } from '../components/v1'

type GrowWithLogs = { detail: GrowDetail; logs: AddbackLogDto[] }
type ProtocolGroup = { hydroSetupId: number | null; name: string; tentName: string | null; growNames: string[]; logs: AddbackLogDto[] }

/**
 * Addback-Übersicht: welcher Grow ist dran, was war zuletzt, und der Verlauf.
 *
 * Die Seite war zuletzt die einzige in der alten Instrument-Optik (cyanfarbene
 * Werte, Eckklammern) und fiel dadurch aus der App heraus. Sie benutzt jetzt
 * dieselben Muster wie alle anderen — ihren eigenen Grow-Umschalter behält sie,
 * weil jede Seite ihre Auswahl selbst trägt.
 */
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

  return (
    <V1Page
      eyebrow="Jetzt / Addback"
      title="Addback"
      subtitle="Was das Reservoir gerade braucht — nachfüllen, aufdüngen, oder wechseln."
      action={hydroGrows.length > 1 ? (
        <select
          className="ls-tent-select"
          aria-label="Grow"
          value={selectedGrow?.id ?? ''}
          onChange={(event) => setSelectedGrowId(Number(event.target.value))}
        >
          {hydroGrows.map((grow) => <option key={grow.id} value={grow.id}>{grow.name} · {grow.hydroStyle}</option>)}
        </select>
      ) : undefined}
    >
      {error && <V1Alert message={error} tone="warn" />}

      {loading ? (
        <V1Skeleton tiles={4} rows={3} label="Lade Addback" />
      ) : !selectedGrow ? (
        <V1Empty
          title="Kein aktiver Hydro-Grow"
          text="Addback braucht einen laufenden DWC- oder RDWC-Grow mit Hydro-System."
          action={(
            <div className="co-actions">
              <Link className="ls-btn is-primary" to="/grows/new">Grow anlegen</Link>
              <Link className="ls-btn" to="/hydro">Hydro öffnen</Link>
            </div>
          )}
        />
      ) : (
        <>
          <div className="co-strip" data-audit="addback-status">
            <div className="co-cell">
              <div className="co-cell-label">pH</div>
              <div className="co-cell-value is-lg">{formatNumber(selectedGrow.latestReservoirPh, 2)}</div>
            </div>
            <div className="co-cell">
              <div className="co-cell-label">EC</div>
              <div className="co-cell-value is-lg">{formatNumber(selectedGrow.latestReservoirEc, 2)}<span className="co-unit">mS/cm</span></div>
            </div>
            <div className="co-cell">
              <div className="co-cell-label">Letzter Addback</div>
              <div className="co-cell-value is-md">{formatShortDateTime(lastAddback?.performedAtUtc)}</div>
            </div>
            <div className="co-cell">
              <div className="co-cell-label">Erfasst</div>
              <div className="co-cell-value is-md">{selectedLogs.length} {selectedLogs.length === 1 ? 'Eintrag' : 'Einträge'}</div>
            </div>
          </div>

          <section className="ls-panel" data-audit="addback-next">
            <div className="ls-panel-head">
              <span className="ls-label">Nächster Addback</span>
              <span className="ls-panel-meta">{[selectedGrow.tentName ?? 'ohne Zelt', selectedGrow.strain, selectedGrow.hydroStyle].filter(Boolean).join(' · ')}</span>
            </div>
            <div className="ls-panel-body">
              <strong>{selectedGrow.name}</strong>
              <p>Werte prüfen, Menge berechnen lassen und protokollieren — der Assistent rechnet mit dem Reservoirvolumen dieses Systems.</p>
              <div className="ls-panel-actions">
                <Link className="ls-btn is-primary" to={`/grows/${selectedGrow.id}/addback`}>Addback starten</Link>
                <Link className="ls-btn" to={`/grows/${selectedGrow.id}`}>Grow öffnen</Link>
              </div>
            </div>
          </section>

          <ChangeoutsPanel growId={selectedGrow.id} growName={selectedGrow.name} />

          <section className="ls-panel" data-audit="addback-log-list">
            <div className="ls-panel-head">
              <span className="ls-label">Verlauf · {selectedGrow.name}</span>
              {selectedLogs.length > 0 && <span className="ls-panel-meta">{selectedLogs.length} erfasst</span>}
            </div>
            {selectedLogs.length === 0 ? (
              <div className="ls-panel-body"><p>Noch kein Addback für diesen Grow erfasst.</p></div>
            ) : (
              selectedLogs.slice(0, 8).map((log) => (
                <Link key={log.id} className="co-row" to={`/grows/${log.growId}/addback`}>
                  <span className="co-row-title">{formatShortDateTime(log.performedAtUtc)}</span>
                  <span className="co-row-sub">
                    EC {formatNumber(log.ecBefore, 2)} → {formatNumber(log.ecAfter ?? log.ecTarget, 2)} · pH {formatNumber(log.phBefore, 2)} → {formatNumber(log.phAfter, 2)}
                  </span>
                  <span className="co-row-value">{formatNumber(log.litersAdded, 2)} L</span>
                </Link>
              ))
            )}
          </section>
        </>
      )}
    </V1Page>
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
