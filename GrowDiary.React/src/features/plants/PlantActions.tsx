import { useState, type FormEvent } from 'react'
import { apiFetch, ApiRequestError } from '../../api'
import type { CreateCloneFromMotherRequest, DecideQuarantinePlantRequest, GrowSummary, PlantInstanceDto, SetupDto } from '../../types'
import { V1Button, V1Field } from '../../components/v1'

type Props = {
  plant: PlantInstanceDto
  setup: SetupDto
  quarantineSetups: SetupDto[]
  productionSetups: SetupDto[]
  grows: GrowSummary[]
  onChanged: (notice: string) => void
}

export function PlantActions({ plant, setup, quarantineSetups, productionSetups, grows, onChanged }: Props) {
  const [mode, setMode] = useState<'clone' | 'release' | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [cloneLabel, setCloneLabel] = useState('')
  const [cloneTarget, setCloneTarget] = useState('')
  const [clonePheno, setClonePheno] = useState('')
  const [releaseSetup, setReleaseSetup] = useState('')
  const [releaseGrow, setReleaseGrow] = useState('')

  const canClone = setup.setupType === 'Mother' && plant.plantRole === 'Mother' && plant.plantStatus === 'Active'
  const canDecide = setup.setupType === 'Quarantine' && plant.plantStatus === 'Active'
  if (!canClone && !canDecide) return null

  function openClone() {
    setMode('clone'); setError(null)
    setCloneLabel(`${plant.label} Klon`)
    setCloneTarget(quarantineSetups[0] ? String(quarantineSetups[0].id) : '')
    setClonePheno(plant.phenoLabel ?? '')
  }
  function openRelease() {
    setMode('release'); setError(null)
    setReleaseSetup(productionSetups[0] ? String(productionSetups[0].id) : '')
    setReleaseGrow('')
  }

  async function run(action: () => Promise<void>, notice: string) {
    setBusy(true); setError(null)
    try {
      await action()
      setMode(null)
      onChanged(notice)
    } catch (caught) {
      setError(caught instanceof ApiRequestError ? caught.message : 'Aktion fehlgeschlagen.')
    } finally {
      setBusy(false)
    }
  }

  function submitClone(event: FormEvent) {
    event.preventDefault()
    void run(async () => {
      const body: CreateCloneFromMotherRequest = {
        motherPlantId: plant.id,
        label: cloneLabel.trim(),
        targetSetupId: cloneTarget ? Number(cloneTarget) : null,
        phenoLabel: clonePheno.trim() || null,
      }
      await apiFetch<PlantInstanceDto>('/api/plants/clone-from-mother', { method: 'POST', body: JSON.stringify(body) })
    }, `Klon von ${plant.label} erstellt.`)
  }

  function submitRelease(event: FormEvent) {
    event.preventDefault()
    void run(async () => {
      const body: DecideQuarantinePlantRequest = {
        plantId: plant.id,
        decision: 'Cleared',
        targetSetupId: releaseSetup ? Number(releaseSetup) : null,
        targetGrowId: releaseGrow ? Number(releaseGrow) : null,
      }
      await apiFetch<PlantInstanceDto>('/api/plants/decide-quarantine', { method: 'POST', body: JSON.stringify(body) })
    }, `${plant.label} freigegeben.`)
  }

  function reject() {
    if (!window.confirm(`${plant.label} ablehnen und ausmustern?`)) return
    void run(async () => {
      const body: DecideQuarantinePlantRequest = { plantId: plant.id, decision: 'Rejected' }
      await apiFetch<PlantInstanceDto>('/api/plants/decide-quarantine', { method: 'POST', body: JSON.stringify(body) })
    }, `${plant.label} abgelehnt.`)
  }

  return (
    <div className="plant-actions" data-audit="plant-actions">
      <div className="plant-actions-buttons">
        {canClone && <V1Button variant="secondary" disabled={busy} onClick={() => (mode === 'clone' ? setMode(null) : openClone())}>{mode === 'clone' ? 'Abbrechen' : 'Klonen'}</V1Button>}
        {canDecide && (
          <>
            <V1Button variant="primary" disabled={busy} onClick={() => (mode === 'release' ? setMode(null) : openRelease())}>{mode === 'release' ? 'Abbrechen' : 'Freigeben'}</V1Button>
            <V1Button variant="danger" disabled={busy} onClick={reject}>Ablehnen</V1Button>
          </>
        )}
      </div>

      {mode === 'clone' && (
        <form className="plant-action-form" onSubmit={submitClone}>
          <V1Field label="Klon-Label"><input value={cloneLabel} onChange={(event) => setCloneLabel(event.target.value)} placeholder="Label" /></V1Field>
          <V1Field label="Quarantäne-Ziel">
            <select value={cloneTarget} onChange={(event) => setCloneTarget(event.target.value)}>
              <option value="">— ohne Setup —</option>
              {quarantineSetups.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
          </V1Field>
          <V1Field label="Pheno"><input value={clonePheno} onChange={(event) => setClonePheno(event.target.value)} placeholder="optional" /></V1Field>
          <div className="plant-action-submit"><V1Button type="submit" variant="primary" disabled={busy || !cloneLabel.trim()}>{busy ? 'Speichert…' : 'Klon erstellen'}</V1Button></div>
        </form>
      )}

      {mode === 'release' && (
        <form className="plant-action-form" onSubmit={submitRelease}>
          <V1Field label="Production-Setup">
            <select value={releaseSetup} onChange={(event) => setReleaseSetup(event.target.value)}>
              <option value="">— ohne Setup —</option>
              {productionSetups.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
          </V1Field>
          <V1Field label="Grow (optional)">
            <select value={releaseGrow} onChange={(event) => setReleaseGrow(event.target.value)}>
              <option value="">— kein Grow —</option>
              {grows.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
          </V1Field>
          <div className="plant-action-submit"><V1Button type="submit" variant="primary" disabled={busy}>{busy ? 'Speichert…' : 'Freigeben'}</V1Button></div>
        </form>
      )}

      {error && <p className="plant-action-error">{error}</p>}
    </div>
  )
}
