import { useEffect, useMemo, useState } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type { PhenoHuntDto, PhenoPlantDto, PhenoWeightsDto } from '../types/pheno'
import { GrowScopePicker } from '../features/grow-scope/GrowScopePicker'
import { useSelectedGrow } from '../features/grow-scope/useSelectedGrow'
import { PhenoSheetEditor } from '../features/pheno/PhenoSheetEditor'
import type { SheetDraft } from '../features/pheno/pheno-sheet-model'
import { V1Page, V1Section, V1Card, V1Field, V1Button, V1Alert, V1Empty, V1Badge } from '../components/v1'

const BUCKETS = [
  { key: 'yield', label: 'Ertrag' },
  { key: 'quality', label: 'Qualität' },
  { key: 'potency', label: 'Wirkstoff' },
  { key: 'resilience', label: 'Robustheit' },
  { key: 'structure', label: 'Struktur' },
] as const

/** A slim bar so the score breakdown is readable at a glance. */
function Bar({ label, value }: { label: string; value: number | null }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '84px 1fr 42px', gap: 8, alignItems: 'center' }}>
      <span className="rc2-measurement-note">{label}</span>
      <span style={{ height: 6, borderRadius: 3, background: 'rgba(255,255,255,0.07)', overflow: 'hidden' }}>
        <span style={{ display: 'block', height: '100%', width: `${((value ?? 0) / 10) * 100}%`, background: 'var(--v1-green)', opacity: value == null ? 0.15 : 1 }} />
      </span>
      <span className="rc2-measurement-note" style={{ textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{value?.toFixed(1) ?? '—'}</span>
    </div>
  )
}

function PhenoHuntPage() {
  const { grows, growId, setGrowId, error: growsError } = useSelectedGrow()
  const [hunt, setHunt] = useState<PhenoHuntDto | null>(null)
  const [weightDraft, setWeightDraft] = useState<PhenoWeightsDto | null>(null)
  const [weightsOpen, setWeightsOpen] = useState(false)
  const [openPlantId, setOpenPlantId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    if (!growId) return
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      try {
        const data = await apiFetch<PhenoHuntDto>(`/api/pheno/grows/${growId}`, { signal: controller.signal })
        if (controller.signal.aborted) return
        setHunt(data)
        setWeightDraft(data.weights)
      } catch (caught) {
        if (!controller.signal.aborted) setError(caught instanceof ApiRequestError ? caught.message : 'Pheno Hunt konnte nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [growId, reloadKey])

  // Ranked, because the whole point is "which sibling wins?".
  const ranked = useMemo(() => {
    const plants = hunt?.plants ?? []
    return [...plants].sort((a, b) => (b.score.total ?? -1) - (a.score.total ?? -1))
  }, [hunt])

  const weightSum = weightDraft ? BUCKETS.reduce((sum, bucket) => sum + (weightDraft[bucket.key] ?? 0), 0) : 0

  async function saveSheet(plant: PhenoPlantDto, draft: SheetDraft) {
    setError(null)
    setNotice(null)
    try {
      await apiFetch(`/api/pheno/plants/${plant.plantInstanceId}`, {
        method: 'PUT',
        body: JSON.stringify({ ...draft, plantInstanceId: plant.plantInstanceId }),
      })
      setNotice(`Bogen für „${plant.label}" gespeichert.`)
      setOpenPlantId(null)
      setReloadKey((key) => key + 1)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Bogen konnte nicht gespeichert werden.')
    }
  }

  async function saveWeights() {
    if (!weightDraft) return
    try {
      await apiFetch('/api/pheno/weights', { method: 'PUT', body: JSON.stringify(weightDraft) })
      setNotice('Gewichtung gespeichert — die Noten sind neu berechnet.')
      setWeightsOpen(false)
      setReloadKey((key) => key + 1)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Gewichtung konnte nicht gespeichert werden.')
    }
  }

  return (
    <V1Page
      eyebrow="Meine Grows"
      title="Pheno Hunt"
      subtitle="Geschwister aus demselben Saatgut vergleichen und die beste behalten. Ertrag und THC werden dabei gegen die anderen Pflanzen dieses Laufs gewertet — allein sagt eine Zahl nichts."
      action={<GrowScopePicker grows={grows} growId={growId} onChange={setGrowId} />}
    >
      {(error || growsError) && <V1Alert message={(error ?? growsError) as string} tone="critical" />}
      {notice && <V1Alert message={notice} tone="ok" />}

      <V1Section title="Gewichtung" action={<V1Button variant="secondary" onClick={() => setWeightsOpen((open) => !open)}>{weightsOpen ? 'Schließen' : 'Anpassen'}</V1Button>}>
        <V1Card>
          <p className="rc2-measurement-note" style={{ marginTop: 0 }}>
            Was zählt für dich? {BUCKETS.map((bucket, index) => (
              <span key={bucket.key}>{index > 0 ? ' · ' : ''}<strong style={{ color: 'var(--v1-text)' }}>{bucket.label} {weightSum > 0 ? Math.round(((weightDraft?.[bucket.key] ?? 0) / weightSum) * 100) : 0}%</strong></span>
            ))}
          </p>
          {weightsOpen && weightDraft && (
            <>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: 12, marginTop: 12 }}>
                {BUCKETS.map((bucket) => (
                  <V1Field key={bucket.key} label={`${bucket.label} · ${weightDraft[bucket.key]}`}>
                    <input type="range" min={0} max={100} step={5} value={weightDraft[bucket.key]}
                      onChange={(event) => setWeightDraft({ ...weightDraft, [bucket.key]: Number(event.target.value) })} />
                  </V1Field>
                ))}
              </div>
              <div className="v1-action-row" style={{ marginTop: 12 }}>
                <V1Button variant="primary" onClick={() => void saveWeights()}>Gewichtung speichern</V1Button>
              </div>
            </>
          )}
        </V1Card>
      </V1Section>

      <V1Section title="Rangliste">
        {loading ? (
          <V1Card>Lädt…</V1Card>
        ) : grows.length === 0 ? (
          <V1Empty title="Kein aktiver Grow" text="Lege zuerst einen Grow an." />
        ) : ranked.length === 0 ? (
          <V1Empty
            title="Keine Pflanzen in diesem Grow"
            text="Pflanzen werden im Zelt unter Setups angelegt. Sobald sie diesem Grow zugeordnet sind, kannst du sie hier bewerten."
          />
        ) : (
          <div style={{ display: 'grid', gap: 12 }}>
            {ranked.map((plant, index) => {
              const open = openPlantId === plant.plantInstanceId
              const keeper = plant.evaluation?.isKeeper ?? false
              return (
                <V1Card key={plant.plantInstanceId} tone={keeper ? 'ok' : 'neutral'}>
                  <div className="v1-card-title-row">
                    <div>
                      <span className="v1-card-kicker">
                        {plant.score.total != null ? `Platz ${index + 1}` : 'noch nicht bewertet'}
                        {plant.strainName ? ` · ${plant.strainName}` : ''}
                        {plant.phenoLabel ? ` · Phäno ${plant.phenoLabel}` : ''}
                      </span>
                      <h2>{plant.label}</h2>
                    </div>
                    <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                      {keeper && <V1Badge tone="ok">Keeper</V1Badge>}
                      {plant.evaluation?.confirmedInSecondRun && <V1Badge tone="accent">2. Lauf bestätigt</V1Badge>}
                      <V1Badge tone={plant.score.total != null ? 'accent' : 'neutral'}>
                        {plant.score.total != null ? `${plant.score.total.toFixed(1)} / 10${plant.score.isManual ? ' ·手' : ''}` : '—'}
                      </V1Badge>
                    </div>
                  </div>

                  <div style={{ display: 'grid', gap: 5, marginTop: 10 }}>
                    {BUCKETS.map((bucket) => <Bar key={bucket.key} label={bucket.label} value={plant.score[bucket.key]} />)}
                  </div>

                  {plant.evaluation?.stretchFactor != null && (
                    <p className="rc2-measurement-note" style={{ margin: '8px 0 0' }}>Streckung ×{plant.evaluation.stretchFactor}
                      {plant.evaluation.dryYieldG != null ? ` · ${plant.evaluation.dryYieldG} g trocken` : ''}
                      {plant.evaluation.floweringDays != null ? ` · ${plant.evaluation.floweringDays} Blütetage` : ''}
                    </p>
                  )}

                  <div className="v1-action-row" style={{ marginTop: 10 }}>
                    <V1Button variant={open ? 'ghost' : 'secondary'} onClick={() => setOpenPlantId(open ? null : plant.plantInstanceId)}>
                      {open ? 'Bogen schließen' : plant.evaluation ? 'Bogen bearbeiten' : 'Bewerten'}
                    </V1Button>
                  </div>

                  {open && (
                    <div style={{ marginTop: 12, paddingTop: 12, borderTop: '1px solid var(--v1-line)' }}>
                      <PhenoSheetEditor plant={plant} onSave={(draft) => saveSheet(plant, draft)} onCancel={() => setOpenPlantId(null)} />
                    </div>
                  )}
                </V1Card>
              )
            })}
          </div>
        )}
      </V1Section>
    </V1Page>
  )
}

export default PhenoHuntPage
