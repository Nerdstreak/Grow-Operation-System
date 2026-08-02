import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowDetail, GrowSummary, HarvestDto } from '../types'

type GrowKosten = {
  stromEur: number | null
  stromHerkunft: string | null
  duengerEur: number | null
  duengerHerkunft: string | null
  pumpenOhnePreis: string[]
  summeEur: number | null
  eurProGramm: number | null
}
import { formatNumber } from '../utils'
import { V1Alert, V1Empty, V1Page, V1Skeleton } from '../components/v1'

/**
 * Ernte & Archiv nach dem Entwurf: abgeschlossene Grows als Ertragstabelle,
 * darunter der direkte Vergleich zweier Läufe — statt Archiv und Vergleich
 * auf zwei Seiten.
 *
 * VERGLEICHEN wählt einen Lauf in den Vergleich; der zweite Klick füllt die
 * zweite Spalte. Ein dritter tauscht den älteren der beiden aus.
 */
function ArchivePage() {
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [harvestByGrow, setHarvestByGrow] = useState<Map<number, HarvestDto>>(new Map())
  const [kostenByGrow, setKostenByGrow] = useState<Map<number, GrowKosten>>(new Map())
  const [compareIds, setCompareIds] = useState<number[]>([])
  const [compareDetails, setCompareDetails] = useState<GrowDetail[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const data = await apiFetch<GrowSummary[]>('/api/grows?archived=true', { signal: controller.signal })
        if (controller.signal.aborted) return
        setGrows(sortByHarvest(data))
        const harvests = await Promise.all(data.map((grow) =>
          apiFetch<HarvestDto>(`/api/grows/${grow.id}/harvest`, { signal: controller.signal }).catch(() => null),
        ))
        if (controller.signal.aborted) return
        const map = new Map<number, HarvestDto>()
        data.forEach((grow, index) => { const harvest = harvests[index]; if (harvest) map.set(grow.id, harvest) })
        setHarvestByGrow(map)
        const kosten = await Promise.all(data.map((grow) =>
          apiFetch<GrowKosten>(`/api/grows/${grow.id}/costs`, { signal: controller.signal }).catch(() => null),
        ))
        if (controller.signal.aborted) return
        const kostenMap = new Map<number, GrowKosten>()
        data.forEach((grow, index) => { const k = kosten[index]; if (k) kostenMap.set(grow.id, k) })
        setKostenByGrow(kostenMap)
      } catch (caught) {
        if (!controller.signal.aborted) setError(caught instanceof ApiRequestError ? caught.message : 'Archiv konnte nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    async function loadDetails() {
      const details = compareIds.length === 0 ? [] : await Promise.all(compareIds.map((id) =>
        apiFetch<GrowDetail>(`/api/grows/${id}`, { signal: controller.signal }).catch(() => null),
      ))
      if (!controller.signal.aborted) setCompareDetails(details.filter((detail): detail is GrowDetail => detail != null))
    }
    void loadDetails()
    return () => controller.abort()
  }, [compareIds])

  function toggleCompare(id: number) {
    setCompareIds((current) => {
      if (current.includes(id)) return current.filter((existing) => existing !== id)
      if (current.length < 2) return [...current, id]
      return [current[1], id]
    })
  }

  const compareCells = useMemo(() => buildCompareCells(compareDetails, harvestByGrow), [compareDetails, harvestByGrow])

  return (
    <V1Page
      eyebrow="Grow / Ernte & Archiv"
      title="Ernte & Archiv"
      subtitle="Abgeschlossene Grows mit Ertrag — direkt vergleichbar, statt Archiv und Vergleich auf zwei Seiten."
    >
      {error && <V1Alert message={error} tone="warn" />}

      {loading ? (
        <V1Skeleton rows={4} label="Lade Archiv" />
      ) : grows.length === 0 ? (
        <V1Empty title="Noch keine archivierten Grows" text="Abgeschlossene Grows erscheinen hier — samt Ertrag." />
      ) : (
        <>
          <section className="ls-panel co-table-wrap" data-audit="grows-archive">
            <div className="co-table" style={{ gridTemplateColumns: '1.2fr .9fr .7fr .7fr .7fr .9fr 1fr' }}>
              <div className="co-th">Grow</div>
              <div className="co-th">Geerntet</div>
              <div className="co-th">Dauer</div>
              <div className="co-th">Trocken</div>
              <div className="co-th">g/Pflanze</div>
              {/* Berechnete Kosten — Strom aus Watt x Lichtstunden, Duenger aus dem
                  Protokoll. Der Titel sagt es, die Zelle bleibt eine Zahl. */}
              <div className="co-th" title="berechnet: Licht-Strom + Dünger aus dem Dosier-Protokoll">Kosten ~</div>
              <div className="co-th">Aktion</div>
              {grows.map((grow) => {
                const harvest = harvestByGrow.get(grow.id)
                const perPlant = gramsPerPlant(grow, harvest)
                const selected = compareIds.includes(grow.id)
                return (
                  <RowCells key={grow.id}>
                    <div className="co-td is-name"><Link to={`/grows/${grow.id}`}>{grow.name}</Link></div>
                    <div className="co-td is-muted">{harvestDate(grow, harvest)}</div>
                    <div className="co-td">{durationDays(grow) ?? '—'}</div>
                    <div className="co-td">{harvest?.dryWeightG != null ? `${formatNumber(harvest.dryWeightG, 0)} g` : '—'}</div>
                    <div className={perPlant != null ? 'co-td is-good' : 'co-td'}>{perPlant != null ? formatNumber(perPlant, 0) : '—'}</div>
                    <div className="co-td" title={kostenTitel(kostenByGrow.get(grow.id))}>{kostenZelle(kostenByGrow.get(grow.id))}</div>
                    <div className="co-td">
                      <button type="button" className={`ls-btn is-small${selected ? ' is-primary' : ''}`} style={{ marginLeft: 0 }} onClick={() => toggleCompare(grow.id)}>
                        {selected ? 'Gewählt' : 'Vergleichen'}
                      </button>
                    </div>
                  </RowCells>
                )
              })}
            </div>
          </section>

          {compareDetails.length === 2 && (
            <section className="ls-panel" data-audit="archive-compare">
              <div className="ls-panel-head">
                <span className="ls-label">Vergleich · {compareDetails[0].name} vs {compareDetails[1].name}</span>
              </div>
              <div className="co-cells">
                {compareCells.map((cell) => (
                  <div key={cell.label} className="co-cand">
                    <div className="co-cell-label">{cell.label}</div>
                    <div className={`co-cell-value${cell.highlight ? ' is-good' : ''}`}>{cell.value}</div>
                  </div>
                ))}
              </div>
            </section>
          )}
          {compareDetails.length === 1 && (
            <p className="gc-facts">Einen zweiten Lauf mit VERGLEICHEN wählen — dann erscheint der Vergleich hier.</p>
          )}
        </>
      )}
    </V1Page>
  )
}

/** Nur ein Fragment — die Zellen müssen direkte Grid-Kinder bleiben. */
function RowCells({ children }: { children: React.ReactNode }) {
  return <>{children}</>
}

/** „128 € · 0,42 €/g" — oder ein ehrliches Minus, wenn Preise fehlen. */
function kostenZelle(kosten: GrowKosten | undefined) {
  if (!kosten || kosten.summeEur == null) return '—'
  const summe = `${formatNumber(kosten.summeEur, 0)} €`
  return kosten.eurProGramm != null ? `${summe} · ${formatNumber(kosten.eurProGramm, 2)} €/g` : summe
}

function kostenTitel(kosten: GrowKosten | undefined) {
  if (!kosten) return undefined
  return [kosten.stromHerkunft, kosten.duengerHerkunft].filter(Boolean).join(' · ') || undefined
}

function sortByHarvest(items: GrowSummary[]) {
  return [...items].sort((a, b) => (b.endDate ?? '').localeCompare(a.endDate ?? '') || a.name.localeCompare(b.name))
}

function harvestDate(grow: GrowSummary, harvest: HarvestDto | undefined): string {
  const value = harvest?.harvestedAtLocal ?? grow.endDate
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(date)
}

function durationDays(grow: GrowSummary): string | null {
  if (!grow.startDate || !grow.endDate) return null
  const start = new Date(grow.startDate).getTime()
  const end = new Date(grow.endDate).getTime()
  if (Number.isNaN(start) || Number.isNaN(end) || end <= start) return null
  return `${Math.round((end - start) / 86_400_000)} T`
}

function gramsPerPlant(grow: GrowSummary, harvest: HarvestDto | undefined): number | null {
  if (harvest?.dryWeightG == null || !grow.plantCount) return null
  return harvest.dryWeightG / grow.plantCount
}

type CompareCell = { label: string; value: string; highlight: boolean }

/**
 * Die Vergleichsleiste: je Kennzahl „A vs B". Hervorgehoben ist g/Pflanze,
 * weil das die Zahl ist, für die man überhaupt vergleicht. Ø-Werte über die
 * Blüte gibt es (noch) nicht als Aggregat — gezeigt wird, was beide Läufe
 * wirklich haben: letzte Messwerte, Dauer, Ertrag.
 */
function buildCompareCells(details: GrowDetail[], harvests: Map<number, HarvestDto>): CompareCell[] {
  if (details.length !== 2) return []
  const [a, b] = details
  const pair = (left: string, right: string) => `${left} vs ${right}`
  const num = (value: number | null | undefined, decimals: number) => (value != null ? formatNumber(value, decimals) : '—')
  const perPlant = (detail: GrowDetail) => {
    const harvest = harvests.get(detail.id)
    return harvest?.dryWeightG != null && detail.plantCount ? formatNumber(harvest.dryWeightG / detail.plantCount, 0) : '—'
  }
  const duration = (detail: GrowDetail) => {
    if (!detail.startDate || !detail.endDate) return '—'
    const days = Math.round((new Date(detail.endDate).getTime() - new Date(detail.startDate).getTime()) / 86_400_000)
    return Number.isFinite(days) && days > 0 ? `${days} T` : '—'
  }
  return [
    { label: 'EC (letzte)', value: pair(num(a.latestMeasurement?.reservoirEc, 2), num(b.latestMeasurement?.reservoirEc, 2)), highlight: false },
    { label: 'pH (letzte)', value: pair(num(a.latestMeasurement?.reservoirPh, 2), num(b.latestMeasurement?.reservoirPh, 2)), highlight: false },
    { label: 'Dauer', value: pair(duration(a), duration(b)), highlight: false },
    { label: 'g/Pflanze', value: pair(perPlant(a), perPlant(b)), highlight: true },
  ]
}

export default ArchivePage
