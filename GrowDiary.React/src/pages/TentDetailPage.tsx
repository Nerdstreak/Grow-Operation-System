import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { CreateCloneFromMotherRequest, DecideQuarantinePlantRequest, GrowSummary, PlantInstanceDto, QuarantineDecision, SetupDto, TentDto, TentLivePayload } from '../types'

type CloneDraft = {
  label: string
  phenoLabel: string
  notes: string
  targetSetupId: string
}

const emptyCloneDraft: CloneDraft = {
  label: '',
  phenoLabel: '',
  notes: '',
  targetSetupId: '',
}

type QuarantineDecisionDraft = {
  targetSetupId: string
  targetGrowId: string
  notes: string
}

const emptyDecisionDraft: QuarantineDecisionDraft = {
  targetSetupId: '',
  targetGrowId: '',
  notes: '',
}

function TentDetailPage() {
  const { tentId } = useParams()
  const [tent, setTent] = useState<TentDto | null>(null)
  const [live, setLive] = useState<TentLivePayload | null>(null)
  const [grows, setGrows] = useState<GrowSummary[]>([])
  const [allActiveGrows, setAllActiveGrows] = useState<GrowSummary[]>([])
  const [setups, setSetups] = useState<SetupDto[]>([])
  const [allSetups, setAllSetups] = useState<SetupDto[]>([])
  const [plantsBySetupId, setPlantsBySetupId] = useState<Record<number, PlantInstanceDto[]>>({})
  const [cloneDrafts, setCloneDrafts] = useState<Record<number, CloneDraft>>({})
  const [cloneErrors, setCloneErrors] = useState<Record<number, string>>({})
  const [savingClonePlantId, setSavingClonePlantId] = useState<number | null>(null)
  const [decisionDrafts, setDecisionDrafts] = useState<Record<number, QuarantineDecisionDraft>>({})
  const [decisionErrors, setDecisionErrors] = useState<Record<number, string>>({})
  const [savingDecisionPlantId, setSavingDecisionPlantId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const quarantineSetups = useMemo(
    () => allSetups.filter((setup) => setup.setupType === 'Quarantine' && isActiveSetup(setup)),
    [allSetups],
  )
  const productionSetups = useMemo(
    () => allSetups.filter((setup) => setup.setupType === 'Production' && isActiveSetup(setup)),
    [allSetups],
  )

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      if (!tentId) return

      setLoading(true)
      setError(null)

      try {
        const [tents, livePayload, activeGrows, setupList] = await Promise.all([
          apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal }),
          apiFetch<TentLivePayload>(`/api/live/tents/${tentId}`, { signal: controller.signal }),
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }),
          apiFetch<SetupDto[]>('/api/setups', { signal: controller.signal }),
        ])

        const tentIdNumber = Number(tentId)
        const selectedTent = tents.find((item) => item.id === Number(tentId)) ?? null
        const activeSetups = setupList.filter((setup) => setup.tentId === tentIdNumber && isActiveSetup(setup))
        const plantEntries = await fetchPlantsForSetups(activeSetups, controller.signal)

        setTent(selectedTent)
        setLive(livePayload)
        setGrows(activeGrows.filter((grow) => grow.tentId === tentIdNumber))
        setAllActiveGrows(activeGrows)
        setAllSetups(setupList)
        setSetups(activeSetups)
        setPlantsBySetupId(Object.fromEntries(plantEntries))
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Zelt-Details konnten nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [tentId])

  const hasCritical = useMemo(() => live?.stateTone === 'critical', [live])
  const hasActiveContent = grows.length > 0 || setups.length > 0

  function updateCloneDraft(plantId: number, patch: Partial<CloneDraft>) {
    setCloneDrafts((current) => ({
      ...current,
      [plantId]: { ...emptyCloneDraft, ...current[plantId], ...patch },
    }))
  }

  function updateDecisionDraft(plantId: number, patch: Partial<QuarantineDecisionDraft>) {
    setDecisionDrafts((current) => ({
      ...current,
      [plantId]: { ...emptyDecisionDraft, ...current[plantId], ...patch },
    }))
  }

  async function refreshSetupsAndPlants() {
    if (!tentId) return

    const setupList = await apiFetch<SetupDto[]>('/api/setups')
    const tentIdNumber = Number(tentId)
    const activeSetups = setupList.filter((setup) => setup.tentId === tentIdNumber && isActiveSetup(setup))
    const plantEntries = await fetchPlantsForSetups(activeSetups)
    setAllSetups(setupList)
    setSetups(activeSetups)
    setPlantsBySetupId(Object.fromEntries(plantEntries))
  }

  async function handleCreateClone(mother: PlantInstanceDto) {
    const draft = { ...emptyCloneDraft, ...cloneDrafts[mother.id] }
    if (!draft.label.trim()) {
      setCloneErrors((current) => ({ ...current, [mother.id]: 'Label ist erforderlich.' }))
      return
    }

    const request: CreateCloneFromMotherRequest = {
      motherPlantId: mother.id,
      targetSetupId: draft.targetSetupId ? Number(draft.targetSetupId) : null,
      label: draft.label.trim(),
      phenoLabel: normalizeDraftText(draft.phenoLabel),
      notes: normalizeDraftText(draft.notes),
      strainId: null,
      cutAt: null,
    }

    setSavingClonePlantId(mother.id)
    setCloneErrors((current) => ({ ...current, [mother.id]: '' }))

    try {
      await apiFetch<PlantInstanceDto>('/api/plants/clone-from-mother', {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setCloneDrafts((current) => {
        const next = { ...current }
        delete next[mother.id]
        return next
      })
      await refreshSetupsAndPlants()
    } catch (caught) {
      setCloneErrors((current) => ({
        ...current,
        [mother.id]: caught instanceof ApiRequestError ? caught.message : 'Clone konnte nicht erstellt werden.',
      }))
    } finally {
      setSavingClonePlantId(null)
    }
  }

  async function handleDecideQuarantine(plant: PlantInstanceDto, decision: QuarantineDecision) {
    const draft = { ...emptyDecisionDraft, ...decisionDrafts[plant.id] }
    const request: DecideQuarantinePlantRequest = {
      plantId: plant.id,
      decision,
      targetSetupId: decision === 'Cleared' && draft.targetSetupId ? Number(draft.targetSetupId) : null,
      targetGrowId: decision === 'Cleared' && draft.targetGrowId ? Number(draft.targetGrowId) : null,
      decidedAt: null,
      notes: normalizeDraftText(draft.notes),
    }

    setSavingDecisionPlantId(plant.id)
    setDecisionErrors((current) => ({ ...current, [plant.id]: '' }))

    try {
      await apiFetch<PlantInstanceDto>('/api/plants/decide-quarantine', {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setDecisionDrafts((current) => {
        const next = { ...current }
        delete next[plant.id]
        return next
      })
      await refreshSetupsAndPlants()
    } catch (caught) {
      setDecisionErrors((current) => ({
        ...current,
        [plant.id]: caught instanceof ApiRequestError ? caught.message : 'Quarantäne-Entscheidung konnte nicht gespeichert werden.',
      }))
    } finally {
      setSavingDecisionPlantId(null)
    }
  }

  function renderQuarantineDecisionForm(plant: PlantInstanceDto) {
    const draft = { ...emptyDecisionDraft, ...decisionDrafts[plant.id] }
    const selectedSetupId = draft.targetSetupId ? Number(draft.targetSetupId) : null
    const growOptions = getCompatibleGrowOptions(allActiveGrows, productionSetups, selectedSetupId)

    return (
      <form
        className="setup-action-form"
        onSubmit={(event) => event.preventDefault()}
        style={{ display: 'grid', gap: 6, maxWidth: 560 }}
      >
        <div className="setup-action-grid" style={{ display: 'grid', gridTemplateColumns: 'minmax(150px, 1fr) minmax(150px, 1fr)', gap: 6 }}>
          <select
            value={draft.targetSetupId}
            onChange={(event) => updateDecisionDraft(plant.id, { targetSetupId: event.target.value, targetGrowId: '' })}
          >
            <option value="">Ohne Production-Setup</option>
            {productionSetups.map((target) => (
              <option key={target.id} value={target.id}>{target.name}</option>
            ))}
          </select>
          <select
            value={draft.targetGrowId}
            onChange={(event) => updateDecisionDraft(plant.id, { targetGrowId: event.target.value })}
          >
            <option value="">Ohne Grow</option>
            {growOptions.map((grow) => (
              <option key={grow.id} value={grow.id}>{grow.name}</option>
            ))}
          </select>
        </div>
        <textarea
          rows={2}
          value={draft.notes}
          onChange={(event) => updateDecisionDraft(plant.id, { notes: event.target.value })}
          placeholder="Entscheidungsnotiz optional"
        />
        <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
          <button
            className="btn btn-primary"
            type="button"
            disabled={savingDecisionPlantId === plant.id}
            onClick={() => void handleDecideQuarantine(plant, 'Cleared')}
          >
            Freigeben
          </button>
          <button
            className="btn"
            type="button"
            disabled={savingDecisionPlantId === plant.id}
            onClick={() => void handleDecideQuarantine(plant, 'Rejected')}
          >
            Verwerfen
          </button>
        </div>
        {productionSetups.length === 0 && <div className="row-muted">Kein aktives Production-Setup vorhanden.</div>}
        {decisionErrors[plant.id] && <div className="row-muted" style={{ color: '#b42318' }}>{decisionErrors[plant.id]}</div>}
      </form>
    )
  }

  return (
    <>
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to="/zelte">← Zelte</Link>
          <span className="topbar-title">{tent?.name ?? 'Zelt-Detail'}</span>
        </div>
        <div className="topbar-right">
          <Link className="btn btn-primary" to="/settings">Konfiguration</Link>
        </div>
      </div>

      <div className="page-scroll">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14 }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}

        {loading ? (
          <div className="empty-hint">Lade Zelt-Daten...</div>
        ) : !tent ? (
          <div className="empty-hint">Zelt nicht gefunden.</div>
        ) : (
          <div className="detail-layout">
            <div>
              <div className="metric-row">
                {(live?.metrics ?? []).map((metric) => (
                  <div key={metric.key} className="metric-block">
                    <div className="metric-block-label">{metric.label}</div>
                    <div className={`metric-block-val ${metric.tone === 'danger' ? 'crit' : metric.tone === 'warning' ? 'warn' : metric.tone === 'success' ? 'ok' : 'neutral'}`}>{metric.value}</div>
                    <div className="metric-block-unit">{metric.unit ?? ' '}</div>
                  </div>
                ))}
              </div>

              {setups.length > 0 && (
                <>
                  <div className="section-label">Aktive Setups</div>
                  <div className="data-table">
                    {setups.map((setup) => (
                      <div key={setup.id} className="data-row" style={{ gridTemplateColumns: '2fr 1fr 1fr', textDecoration: 'none' }}>
                        <div>
                          <div className="row-name">{setup.name}</div>
                          <div className="row-sub">{formatSetupDetails(setup).join(' | ') || setup.notes || 'Keine Basisdaten'}</div>
                          {(plantsBySetupId[setup.id] ?? []).length > 0 && (
                            <div style={{ display: 'grid', gap: 3, marginTop: 6 }}>
                              {(plantsBySetupId[setup.id] ?? []).map((plant) => (
                                <div key={plant.id} style={{ display: 'grid', gap: 6 }}>
                                  <div className="row-sub">{formatPlantLine(plant)}</div>
                                  {setup.setupType === 'Mother' && plant.plantRole === 'Mother' && (
                                    <form
                                      className="setup-action-form"
                                      onSubmit={(event) => {
                                        event.preventDefault()
                                        void handleCreateClone(plant)
                                      }}
                                      style={{ display: 'grid', gap: 6, maxWidth: 520 }}
                                    >
                                      <div className="setup-action-grid" style={{ display: 'grid', gridTemplateColumns: 'minmax(120px, 1fr) minmax(120px, 1fr)', gap: 6 }}>
                                        <input
                                          value={(cloneDrafts[plant.id] ?? emptyCloneDraft).label}
                                          onChange={(event) => updateCloneDraft(plant.id, { label: event.target.value })}
                                          placeholder="Clone-Label"
                                        />
                                        <input
                                          value={(cloneDrafts[plant.id] ?? emptyCloneDraft).phenoLabel}
                                          onChange={(event) => updateCloneDraft(plant.id, { phenoLabel: event.target.value })}
                                          placeholder="Pheno optional"
                                        />
                                      </div>
                                      <div className="setup-action-grid" style={{ display: 'grid', gridTemplateColumns: 'minmax(150px, 1fr) auto', gap: 6 }}>
                                        <select
                                          value={(cloneDrafts[plant.id] ?? emptyCloneDraft).targetSetupId}
                                          onChange={(event) => updateCloneDraft(plant.id, { targetSetupId: event.target.value })}
                                        >
                                          <option value="">Ohne Quarantäne-Ziel</option>
                                          {quarantineSetups.map((target) => (
                                            <option key={target.id} value={target.id}>{target.name}</option>
                                          ))}
                                        </select>
                                        <button className="btn btn-primary" type="submit" disabled={savingClonePlantId === plant.id}>
                                          {savingClonePlantId === plant.id ? 'Erstelle...' : 'Clone erstellen'}
                                        </button>
                                      </div>
                                      <textarea
                                        rows={2}
                                        value={(cloneDrafts[plant.id] ?? emptyCloneDraft).notes}
                                        onChange={(event) => updateCloneDraft(plant.id, { notes: event.target.value })}
                                        placeholder="Notiz optional"
                                      />
                                      {quarantineSetups.length === 0 && <div className="row-muted">Kein aktives Quarantäne-Setup vorhanden.</div>}
                                      {cloneErrors[plant.id] && <div className="row-muted" style={{ color: '#b42318' }}>{cloneErrors[plant.id]}</div>}
                                    </form>
                                  )}
                                  {setup.setupType === 'Quarantine' && isDecidablePlant(plant) && renderQuarantineDecisionForm(plant)}
                                </div>
                              ))}
                            </div>
                          )}
                        </div>
                        <div><span className="badge badge-neutral">{setup.setupType}</span></div>
                        <div><span className={`badge ${setup.status === 'Active' ? 'badge-ok' : 'badge-neutral'}`}>{setup.status}</span></div>
                      </div>
                    ))}
                  </div>
                </>
              )}

              {grows.length > 0 && (
                <>
                <div className="section-label">Aktive Grows</div>
                <div className="data-table">
                  {grows.map((grow) => (
                    <Link key={grow.id} to={`/grows/${grow.id}`} className="data-row" style={{ gridTemplateColumns: '2fr 1fr 1fr 60px', textDecoration: 'none' }}>
                      <div>
                        <div className="row-name">{grow.name}</div>
                        <div className="row-sub">{grow.strain ?? '–'}{grow.breeder ? ` · ${grow.breeder}` : ''}</div>
                      </div>
                      <div><span className="badge badge-neutral">{grow.latestStage ?? '–'}</span></div>
                      <div><span className={`badge ${hasCritical ? 'badge-crit' : 'badge-ok'}`}>{live?.stateLabel ?? 'stabil'}</span></div>
                      <div className="row-muted">→</div>
                    </Link>
                  ))}
                </div>
                </>
              )}

              {!hasActiveContent && <div className="empty-hint" style={{ padding: '30px 0' }}>Keine aktiven Grows oder Setups in diesem Zelt.</div>}
            </div>

            <div className="side-panel">
              <div className="panel-card">
                <div className="panel-card-header">
                  <span className="panel-card-title">Info</span>
                </div>
                <div style={{ padding: '12px 14px', display: 'grid', gap: 8, fontSize: 13 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Typ</span><span>{tent.kind}</span></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Tent-Typ</span><span>{tent.tentType}</span></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Aktive Runs</span><span>{tent.activeGrowCount}</span></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Archivierte Runs</span><span>{tent.archivedGrowCount}</span></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Aktive Setups</span><span>{tent.activeSetupCount}</span></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Archivierte Setups</span><span>{tent.archivedSetupCount}</span></div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="row-muted">Sensoren</span><span>{tent.sensors.filter((sensor) => sensor.isActive).length}</span></div>
                </div>
              </div>

              {live?.cameraUrl && (
                <div className="panel-card">
                  <div className="panel-card-header">
                    <span className="panel-card-title">Kamera</span>
                  </div>
                  <div style={{ padding: 12 }}>
                    <img src={live.cameraUrl} alt={`Livebild ${tent.name}`} style={{ width: '100%', borderRadius: 8, display: 'block' }} />
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </>
  )
}

function formatSetupDetails(setup: SetupDto): string[] {
  if (setup.setupType === 'Mother') {
    return [
      setup.cloneCounterTotal !== null ? `${setup.cloneCounterTotal} Clone gesamt` : null,
      setup.lastCloneCutAt ? `Letzter Schnitt ${formatDate(setup.lastCloneCutAt)}` : null,
      setup.motherHealthStatus ? `Health ${setup.motherHealthStatus}` : null,
    ].filter((value): value is string => Boolean(value))
  }

  if (setup.setupType === 'Quarantine') {
    return [
      setup.quarantineStartedAt ? `Start ${formatDate(setup.quarantineStartedAt)}` : null,
      setup.quarantinePlannedEndAt ? `Ende ${formatDate(setup.quarantinePlannedEndAt)}` : null,
      setup.quarantineResult ? `Ergebnis ${setup.quarantineResult}` : null,
    ].filter((value): value is string => Boolean(value))
  }

  return setup.notes ? [setup.notes] : []
}

function formatDate(value: string): string {
  return value.slice(0, 10)
}

function formatPlantLine(plant: PlantInstanceDto): string {
  const strain = plant.strainName ?? (plant.strainId ? `Strain #${plant.strainId}` : 'Ohne Strain')
  const pheno = plant.phenoLabel ? ` | ${plant.phenoLabel}` : ''
  return `${plant.label} | ${plant.plantRole} | ${plant.plantStatus} | ${strain}${pheno}`
}

function isActiveSetup(setup: SetupDto): boolean {
  return setup.status === 'Planning' || setup.status === 'Active'
}

function isDecidablePlant(plant: PlantInstanceDto): boolean {
  return plant.plantStatus === 'Planned' || plant.plantStatus === 'Active'
}

function getCompatibleGrowOptions(grows: GrowSummary[], productionSetups: SetupDto[], selectedSetupId: number | null): GrowSummary[] {
  if (!selectedSetupId) return grows

  const selectedSetup = productionSetups.find((setup) => setup.id === selectedSetupId)
  return grows.filter((grow) => {
    if (grow.setupId !== null && grow.setupId !== selectedSetupId) return false
    if (selectedSetup && grow.tentId !== null && grow.tentId !== selectedSetup.tentId) return false
    return true
  })
}

async function fetchPlantsForSetups(setups: SetupDto[], signal?: AbortSignal): Promise<Array<readonly [number, PlantInstanceDto[]]>> {
  return Promise.all(
    setups.map(async (setup) => {
      const plants = await apiFetch<PlantInstanceDto[]>(`/api/plants?setupId=${setup.id}`, { signal })
      return [setup.id, plants] as const
    }),
  )
}

function normalizeDraftText(value: string): string | null {
  const trimmed = value.trim()
  return trimmed.length > 0 ? trimmed : null
}

export default TentDetailPage
