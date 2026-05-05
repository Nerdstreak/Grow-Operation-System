import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch, ApiRequestError } from '../api'
import type { GrowActionResultDto, GrowDetail, GrowTaskDto, JournalEntryDto, MeasurementDto, PhotoAssetDto, PhotoTag, ValueOrigin } from '../types'
import { formatDate, formatDateTime, formatNumber, toLocalInputValue } from '../utils'

interface DetailBundle {
  grow: GrowDetail | null
  measurements: MeasurementDto[]
  tasks: GrowTaskDto[]
  journal: JournalEntryDto[]
}

const photoTags: PhotoTag[] = ['Overview', 'Canopy', 'Leaf', 'Root', 'Training', 'Flower', 'Problem', 'Comparison', 'Other']

const emptyMeasurementForm = () => ({
  takenAtLocal: toLocalInputValue(),
  stage: 'Veg',
  source: 'Manual',
  airTemperatureC: '',
  humidityPercent: '',
  reservoirPh: '',
  reservoirEc: '',
  reservoirWaterTempC: '',
  notes: '',
})

const emptyTaskForm = () => ({
  title: '',
  dueAtLocal: '',
  priority: 'Normal',
  notes: '',
})

const emptyJournalForm = () => ({
  title: '',
  body: '',
  entryType: 'Observation',
  source: 'Manual',
  occurredAtLocal: toLocalInputValue(),
})

const emptyPhotoForm = () => ({
  photoCaption: '',
  photoTag: 'Overview' as PhotoTag,
  useAsReferenceShot: false,
  source: 'Manual' as ValueOrigin,
  files: [] as File[],
})

function GrowDetailPage() {
  const { growId } = useParams()
  const [bundle, setBundle] = useState<DetailBundle>({ grow: null, measurements: [], tasks: [], journal: [] })
  const [photos, setPhotos] = useState<PhotoAssetDto[]>([])
  const [selectedMeasurementId, setSelectedMeasurementId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [photoLoading, setPhotoLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState<string | null>(null)
  const [measurementForm, setMeasurementForm] = useState(emptyMeasurementForm)
  const [taskForm, setTaskForm] = useState(emptyTaskForm)
  const [journalForm, setJournalForm] = useState(emptyJournalForm)
  const [photoForm, setPhotoForm] = useState(emptyPhotoForm)

  const loadPhotos = useCallback(async (measurementId: number, signal?: AbortSignal) => {
    setPhotoLoading(true)
    try {
      const nextPhotos = await apiFetch<PhotoAssetDto[]>(`/api/measurements/${measurementId}/photos`, { signal })
      setPhotos(nextPhotos)
    } catch (caught) {
      if (signal?.aborted) return
      setError(caught instanceof ApiRequestError ? caught.message : 'Fotos konnten nicht geladen werden.')
    } finally {
      if (!signal?.aborted) setPhotoLoading(false)
    }
  }, [])

  const loadBundle = useCallback(async (signal?: AbortSignal) => {
    if (!growId) return
    try {
      const [grow, measurements, tasks, journal] = await Promise.all([
        apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal }),
        apiFetch<MeasurementDto[]>(`/api/grows/${growId}/measurements`, { signal }),
        apiFetch<GrowTaskDto[]>(`/api/grows/${growId}/tasks`, { signal }),
        apiFetch<JournalEntryDto[]>(`/api/grows/${growId}/journal`, { signal }),
      ])
      const nextMeasurementId = measurements.find((measurement) => measurement.id === selectedMeasurementId)?.id ?? measurements[0]?.id ?? null
      setBundle({ grow, measurements, tasks, journal })
      setSelectedMeasurementId(nextMeasurementId)
      setError(null)
      if (nextMeasurementId) {
        await loadPhotos(nextMeasurementId, signal)
      } else {
        setPhotos([])
      }
    } catch (caught) {
      if (signal?.aborted) return
      setError(caught instanceof ApiRequestError ? caught.message : 'Grow-Details konnten nicht geladen werden.')
    } finally {
      if (!signal?.aborted) setLoading(false)
    }
  }, [growId, loadPhotos, selectedMeasurementId])

  useEffect(() => {
    const controller = new AbortController()
    const handle = window.setTimeout(() => { void loadBundle(controller.signal) }, 0)
    return () => {
      window.clearTimeout(handle)
      controller.abort()
    }
  }, [loadBundle])

  const openTasks = useMemo(() => bundle.tasks.filter((task) => task.status === 'Open'), [bundle.tasks])
  const closedTasks = useMemo(() => bundle.tasks.filter((task) => task.status !== 'Open'), [bundle.tasks])
  const selectedMeasurement = useMemo(
    () => bundle.measurements.find((measurement) => measurement.id === selectedMeasurementId) ?? null,
    [bundle.measurements, selectedMeasurementId],
  )

  async function handleMeasurementSelection(nextId: number | null) {
    setSelectedMeasurementId(nextId)
    if (!nextId) {
      setPhotos([])
      return
    }

    await loadPhotos(nextId)
  }

  async function handleMeasurementSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!growId) return

    setSaving('measurement')
    try {
      await apiFetch(`/api/grows/${growId}/measurements`, {
        method: 'POST',
        body: JSON.stringify({
          takenAtLocal: measurementForm.takenAtLocal,
          stage: measurementForm.stage,
          source: measurementForm.source,
          airTemperatureC: toNullableNumber(measurementForm.airTemperatureC),
          humidityPercent: toNullableNumber(measurementForm.humidityPercent),
          reservoirPh: toNullableNumber(measurementForm.reservoirPh),
          reservoirEc: toNullableNumber(measurementForm.reservoirEc),
          reservoirWaterTempC: toNullableNumber(measurementForm.reservoirWaterTempC),
          notes: measurementForm.notes || null,
        }),
      })
      setMeasurementForm(emptyMeasurementForm())
      setNotice('Messung gespeichert.')
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Messung konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function handleTaskSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!growId) return

    setSaving('task')
    try {
      await apiFetch(`/api/grows/${growId}/tasks`, {
        method: 'POST',
        body: JSON.stringify({
          title: taskForm.title,
          notes: taskForm.notes || null,
          dueAtLocal: taskForm.dueAtLocal || null,
          priority: taskForm.priority,
        }),
      })
      setTaskForm(emptyTaskForm())
      setNotice('Task gespeichert.')
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Aufgabe konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function handleJournalSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!growId) return

    setSaving('journal')
    try {
      await apiFetch(`/api/grows/${growId}/journal`, {
        method: 'POST',
        body: JSON.stringify({
          title: journalForm.title || null,
          body: journalForm.body || null,
          entryType: journalForm.entryType,
          source: journalForm.source,
          occurredAtLocal: journalForm.occurredAtLocal,
        }),
      })
      setJournalForm(emptyJournalForm())
      setNotice('Journal gespeichert.')
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Journal konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function handlePhotoSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedMeasurement || photoForm.files.length === 0) {
      setError('Bitte waehle eine Messung und mindestens ein Foto aus.')
      return
    }

    setSaving('photo')
    try {
      const formData = new FormData()
      formData.append('photoCaption', photoForm.photoCaption)
      formData.append('photoTag', photoForm.photoTag)
      formData.append('useAsReferenceShot', String(photoForm.useAsReferenceShot))
      formData.append('source', photoForm.source)
      for (const file of photoForm.files) {
        formData.append('photos', file)
      }

      await apiFetch<PhotoAssetDto[]>(`/api/measurements/${selectedMeasurement.id}/photos`, { method: 'POST', body: formData })
      setPhotoForm(emptyPhotoForm())
      setNotice('Fotos hochgeladen.')
      await Promise.all([loadBundle(), loadPhotos(selectedMeasurement.id)])
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Fotos konnten nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function updateTaskStatus(taskId: number, status: 'Open' | 'Done' | 'Skipped') {
    setSaving(`task-status-${taskId}`)
    try {
      await apiFetch(`/api/tasks/${taskId}/status`, { method: 'PATCH', body: JSON.stringify({ status }) })
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Task-Status konnte nicht geaendert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function handleGrowAction(action: 'germination' | 'rooting' | 'flip') {
    if (!growId) return

    const route = action === 'germination'
      ? 'confirm-germination'
      : action === 'rooting'
        ? 'confirm-rooting'
        : 'flip-to-flower'

    setSaving(`action-${action}`)
    try {
      const result = await apiFetch<GrowActionResultDto>(`/api/grows/${growId}/actions/${route}`, { method: 'POST' })
      setNotice(result.message)
      setError(null)
      await loadBundle()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Grow-Aktion konnte nicht ausgefuehrt werden.')
    } finally {
      setSaving(null)
    }
  }

  if (loading) {
    return (
      <>
        <div className="topbar"><span className="topbar-title">Grow-Detail</span></div>
        <div className="page-scroll"><div className="empty-hint">Lade Daten...</div></div>
      </>
    )
  }

  if (!bundle.grow) {
    return (
      <>
        <div className="topbar"><Link className="btn" to="/">Zurueck</Link></div>
        <div className="page-scroll">
          <div className="empty-hint" style={{ color: 'var(--red)' }}>{error ?? 'Grow nicht gefunden.'}</div>
        </div>
      </>
    )
  }

  const grow = bundle.grow
  const latest = grow.latestMeasurement
  const canConfirmGermination = grow.startMaterial === 'Seed' && !grow.germinatedAt
  const canConfirmRooting = grow.startMaterial === 'Clone' && !grow.rootedAt
  const canFlipToFlower = grow.seedType !== 'Autoflower' && !grow.flipDate

  return (
    <>
      <div className="topbar">
        <div className="topbar-left">
          <Link className="btn" to="/">Zurueck</Link>
          <span className="topbar-title">{grow.name}</span>
        </div>
        <div className="topbar-right">
          <span className={`badge ${grow.status === 'Running' ? 'badge-ok' : grow.status === 'Planning' ? 'badge-warn' : 'badge-neutral'}`}>{grow.status}</span>
          <Link className="btn btn-primary" to={`/grows/${grow.id}/setup`}>Setup bearbeiten</Link>
        </div>
      </div>

      <div className="page-scroll">
        {error && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)' }}>
            <div className="alert-dot" />
            <strong>Fehler</strong>
            <span>{error}</span>
          </div>
        )}
        {notice && (
          <div className="alert-bar" style={{ marginBottom: 14, borderRadius: 'var(--radius)', background: 'var(--green-bg)', borderColor: 'var(--green)' }}>
            <div className="alert-dot" style={{ background: 'var(--green)' }} />
            <strong style={{ color: 'var(--green)' }}>Info</strong>
            <span>{notice}</span>
          </div>
        )}

        <div className="grow-hero">
          <div className="grow-hero-title">{grow.name}</div>
          <div className="grow-hero-sub">{grow.strain ?? 'Unbekannter Strain'} · {grow.breeder ?? 'kein Breeder'} · {grow.hydroStyle} · {grow.tentName ?? 'ohne Zelt'}</div>
          <div className="grow-kpis">
            <div className="grow-kpi">
              <div className="grow-kpi-val">{formatNumber(latest?.reservoirPh, 2)}</div>
              <div className="grow-kpi-label">Reservoir pH</div>
            </div>
            <div className="grow-kpi">
              <div className="grow-kpi-val">{formatNumber(latest?.reservoirEc, 2)}</div>
              <div className="grow-kpi-label">Reservoir EC</div>
            </div>
            <div className="grow-kpi">
              <div className="grow-kpi-val">{latest ? `${formatNumber(latest.airTemperatureC, 1)}°` : '—'}</div>
              <div className="grow-kpi-label">Lufttemp</div>
            </div>
            <div className="grow-kpi">
              <div className="grow-kpi-val">{latest ? `${formatNumber(latest.humidityPercent, 0)}%` : '—'}</div>
              <div className="grow-kpi-label">Luftfeuchte</div>
            </div>
            <div className="grow-kpi">
              <div className="grow-kpi-val">{bundle.measurements.length}</div>
              <div className="grow-kpi-label">Messungen</div>
            </div>
            <div className="grow-kpi">
              <div className="grow-kpi-val">{openTasks.length}</div>
              <div className="grow-kpi-label">Offene Tasks</div>
            </div>
          </div>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, marginTop: 14 }}>
            <Link className="btn" to={`/grows/${grow.id}/addback`}>Addback</Link>
            <Link className="btn" to={`/grows/${grow.id}/harvest`}>Harvest</Link>
            <Link className="btn" to={`/analyse?leftGrowId=${grow.id}`}>Vergleichen</Link>
            <a className="btn" href={`/grows/${grow.id}/export`}>Export</a>
            {canConfirmGermination && (
              <button type="button" className="btn" disabled={saving === 'action-germination'} onClick={() => void handleGrowAction('germination')}>
                {saving === 'action-germination' ? 'Bestaetigt...' : 'Keimung bestaetigen'}
              </button>
            )}
            {canConfirmRooting && (
              <button type="button" className="btn" disabled={saving === 'action-rooting'} onClick={() => void handleGrowAction('rooting')}>
                {saving === 'action-rooting' ? 'Bestaetigt...' : 'Bewurzelung bestaetigen'}
              </button>
            )}
            {canFlipToFlower && (
              <button type="button" className="btn" disabled={saving === 'action-flip'} onClick={() => void handleGrowAction('flip')}>
                {saving === 'action-flip' ? 'Traegt ein...' : 'Flip zu 12/12'}
              </button>
            )}
          </div>
        </div>

        <div className="detail-layout">
          <div>
            <div className="section-label">Messungen</div>
            <div className="card" style={{ marginBottom: 14 }}>
              <div className="card-header">
                <span className="card-title">Verlauf</span>
                <span className="text-muted" style={{ fontSize: 13 }}>{bundle.measurements.length} gesamt</span>
              </div>
              {bundle.measurements.length === 0 ? (
                <div className="empty-hint">Noch keine Messungen vorhanden.</div>
              ) : (
                bundle.measurements.slice(0, 15).map((measurement) => (
                  <div
                    key={measurement.id}
                    className="timeline-item"
                    style={{ cursor: 'pointer', padding: '12px 16px', background: selectedMeasurementId === measurement.id ? 'var(--surface2)' : undefined }}
                    onClick={() => void handleMeasurementSelection(measurement.id)}
                  >
                    <div className="tl-dot-col">
                      <div className="tl-dot measurement" />
                      <div className="tl-line" />
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="tl-title">{measurement.stage} · pH {formatNumber(measurement.reservoirPh, 2)} · EC {formatNumber(measurement.reservoirEc, 2)}</div>
                      <div className="tl-sub">{formatNumber(measurement.airTemperatureC, 1)}°C · {formatNumber(measurement.humidityPercent, 0)}% rF</div>
                    </div>
                    <div style={{ display: 'grid', gap: 6, justifyItems: 'end' }}>
                      <div className="tl-time">{formatDateTime(measurement.takenAt)}</div>
                      <Link className="btn" to={`/grows/measurements/${measurement.id}/edit`} onClick={(event) => event.stopPropagation()}>Bearbeiten</Link>
                    </div>
                  </div>
                ))
              )}
            </div>

            <div className="section-label">Journal</div>
            <div className="card" style={{ marginBottom: 14 }}>
              <div className="card-header">
                <span className="card-title">Eintraege</span>
                <span className="text-muted" style={{ fontSize: 13 }}>{bundle.journal.length}</span>
              </div>
              {bundle.journal.length === 0 ? (
                <div className="empty-hint">Noch keine Journal-Eintraege.</div>
              ) : (
                bundle.journal.map((entry) => (
                  <div key={entry.id} className="timeline-item" style={{ padding: '12px 16px' }}>
                    <div className="tl-dot-col">
                      <div className="tl-dot journal" />
                      <div className="tl-line" />
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="tl-title">{entry.title ?? entry.entryType}</div>
                      {entry.body && <div className="tl-sub">{entry.body}</div>}
                    </div>
                    <div className="tl-time">{formatDateTime(entry.occurredAtUtc)}</div>
                  </div>
                ))
              )}
            </div>

            <div className="section-label">Neue Messung</div>
            <div className="card" style={{ marginBottom: 14 }}>
              <div className="card-header"><span className="card-title">Messung eintragen</span></div>
              <form onSubmit={handleMeasurementSubmit} style={{ padding: '16px 20px' }}>
                <div className="meas-fields" style={{ marginBottom: 16 }}>
                  <div className="meas-field">
                    <label>Zeitpunkt</label>
                    <input className="meas-input" style={{ fontSize: 15 }} type="datetime-local" value={measurementForm.takenAtLocal} onChange={(event) => setMeasurementForm((current) => ({ ...current, takenAtLocal: event.target.value }))} />
                  </div>
                  <div className="meas-field">
                    <label>Phase</label>
                    <select className="meas-input" style={{ fontSize: 15 }} value={measurementForm.stage} onChange={(event) => setMeasurementForm((current) => ({ ...current, stage: event.target.value }))}>
                      <option>Seedling</option><option>Clone</option><option>Veg</option><option>Transition</option><option>Flower</option><option>Finish</option><option>Dry</option><option>Cure</option>
                    </select>
                  </div>
                  <div className="meas-field">
                    <label>pH</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={measurementForm.reservoirPh} onChange={(event) => setMeasurementForm((current) => ({ ...current, reservoirPh: event.target.value }))} placeholder="5.8" />
                      <span className="meas-unit">pH</span>
                    </div>
                  </div>
                  <div className="meas-field">
                    <label>EC</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={measurementForm.reservoirEc} onChange={(event) => setMeasurementForm((current) => ({ ...current, reservoirEc: event.target.value }))} placeholder="1.6" />
                      <span className="meas-unit">mS/cm</span>
                    </div>
                  </div>
                  <div className="meas-field">
                    <label>Wassertemp</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={measurementForm.reservoirWaterTempC} onChange={(event) => setMeasurementForm((current) => ({ ...current, reservoirWaterTempC: event.target.value }))} placeholder="19.0" />
                      <span className="meas-unit">°C</span>
                    </div>
                  </div>
                  <div className="meas-field">
                    <label>Lufttemp</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={measurementForm.airTemperatureC} onChange={(event) => setMeasurementForm((current) => ({ ...current, airTemperatureC: event.target.value }))} placeholder="24.0" />
                      <span className="meas-unit">°C</span>
                    </div>
                  </div>
                  <div className="meas-field">
                    <label>Luftfeuchte</label>
                    <div className="meas-field-inner">
                      <input className="meas-input" value={measurementForm.humidityPercent} onChange={(event) => setMeasurementForm((current) => ({ ...current, humidityPercent: event.target.value }))} placeholder="60" />
                      <span className="meas-unit">%</span>
                    </div>
                  </div>
                </div>
                <div className="field" style={{ marginBottom: 14 }}>
                  <label>Notiz</label>
                  <textarea value={measurementForm.notes} onChange={(event) => setMeasurementForm((current) => ({ ...current, notes: event.target.value }))} rows={2} placeholder="Zustand, Auffaelligkeiten, Korrekturen..." />
                </div>
                <button className="btn btn-primary" disabled={saving === 'measurement'}>{saving === 'measurement' ? 'Speichert...' : 'Messung speichern'}</button>
              </form>
            </div>
          </div>

          <div className="side-panel">
            <div className="panel-card">
              <div className="panel-card-header">
                <span className="panel-card-title">Offene Tasks</span>
                <span className="panel-card-count">{openTasks.length}</span>
              </div>
              {openTasks.length === 0 ? (
                <div style={{ padding: '14px', fontSize: '12px', color: 'var(--faint)' }}>Keine offenen Tasks.</div>
              ) : (
                openTasks.map((task) => (
                  <div key={task.id} className="task-item">
                    <button
                      type="button"
                      className="task-check"
                      disabled={saving === `task-status-${task.id}`}
                      onClick={() => void updateTaskStatus(task.id, 'Done')}
                    />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="task-title">{task.title}</div>
                      {task.dueAtUtc && <div className="task-sub">faellig {formatDate(task.dueAtUtc)}</div>}
                    </div>
                  </div>
                ))
              )}
              {closedTasks.length > 0 && (
                <div style={{ borderTop: '1px solid var(--border)', padding: '8px 14px' }}>
                  {closedTasks.slice(0, 5).map((task) => (
                    <div key={task.id} className="task-item" style={{ opacity: 0.5 }}>
                      <button type="button" className="task-check done" disabled={saving === `task-status-${task.id}`} onClick={() => void updateTaskStatus(task.id, 'Open')} />
                      <div className="task-title" style={{ textDecoration: 'line-through' }}>{task.title}</div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="panel-card">
              <div className="panel-card-header"><span className="panel-card-title">Grow-Info</span></div>
              <div style={{ padding: '12px 14px', fontSize: 13, display: 'grid', gap: 8 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Start</span><span>{formatDate(grow.startDate)}</span></div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Medium</span><span>{grow.mediumType}</span></div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Wasser</span><span>{grow.waterSource}</span></div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Licht</span><span>{grow.light ?? '—'}</span></div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Reservoir</span><span>{grow.reservoirSize ?? '—'}</span></div>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}><span className="text-muted">Naehrstoffe</span><span>{grow.nutrients ?? '—'}</span></div>
              </div>
            </div>

            <div className="panel-card">
              <div className="panel-card-header"><span className="panel-card-title">Journal-Eintrag</span></div>
              <form onSubmit={handleJournalSubmit} style={{ padding: '12px 14px', display: 'grid', gap: 10 }}>
                <div className="field">
                  <label>Titel</label>
                  <input value={journalForm.title} onChange={(event) => setJournalForm((current) => ({ ...current, title: event.target.value }))} placeholder="Heute deutlich mehr Durst" />
                </div>
                <div className="field">
                  <label>Typ</label>
                  <select value={journalForm.entryType} onChange={(event) => setJournalForm((current) => ({ ...current, entryType: event.target.value }))}>
                    <option>Observation</option><option>Action</option><option>Problem</option><option>Solution</option><option>Training</option><option>Feeding</option><option>ReservoirChange</option>
                  </select>
                </div>
                <div className="field">
                  <label>Eintrag</label>
                  <textarea value={journalForm.body} onChange={(event) => setJournalForm((current) => ({ ...current, body: event.target.value }))} rows={3} placeholder="Was ist passiert?" />
                </div>
                <button className="btn btn-primary" disabled={saving === 'journal'}>{saving === 'journal' ? 'Speichert...' : 'Journal speichern'}</button>
              </form>
            </div>

            <div className="panel-card">
              <div className="panel-card-header"><span className="panel-card-title">Task anlegen</span></div>
              <form onSubmit={handleTaskSubmit} style={{ padding: '12px 14px', display: 'grid', gap: 10 }}>
                <div className="field">
                  <label>Titel</label>
                  <input value={taskForm.title} onChange={(event) => setTaskForm((current) => ({ ...current, title: event.target.value }))} placeholder="z. B. EC nach Addback pruefen" />
                </div>
                <div className="field">
                  <label>Prioritaet</label>
                  <select value={taskForm.priority} onChange={(event) => setTaskForm((current) => ({ ...current, priority: event.target.value }))}>
                    <option>Low</option><option>Normal</option><option>High</option><option>Critical</option>
                  </select>
                </div>
                <div className="field">
                  <label>Faellig</label>
                  <input type="datetime-local" value={taskForm.dueAtLocal} onChange={(event) => setTaskForm((current) => ({ ...current, dueAtLocal: event.target.value }))} />
                </div>
                <button className="btn btn-primary" disabled={saving === 'task'}>{saving === 'task' ? 'Speichert...' : 'Task speichern'}</button>
              </form>
            </div>

            <div className="panel-card">
              <div className="panel-card-header">
                <span className="panel-card-title">Fotos</span>
                <span className="panel-card-count">{photoLoading ? '...' : photos.length}</span>
              </div>
              <form onSubmit={handlePhotoSubmit} style={{ padding: '12px 14px', display: 'grid', gap: 10 }}>
                <div className="field">
                  <label>Messung</label>
                  <select value={selectedMeasurementId ?? ''} onChange={(event) => void handleMeasurementSelection(event.target.value ? parseInt(event.target.value, 10) : null)} disabled={bundle.measurements.length === 0}>
                    {bundle.measurements.length === 0 ? <option value="">Keine Messungen</option> : null}
                    {bundle.measurements.map((measurement) => (
                      <option key={measurement.id} value={measurement.id}>#{measurement.id} · {measurement.stage} · {formatDateTime(measurement.takenAt)}</option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label>Tag</label>
                  <select value={photoForm.photoTag} onChange={(event) => setPhotoForm((current) => ({ ...current, photoTag: event.target.value as PhotoTag }))}>
                    {photoTags.map((tag) => <option key={tag} value={tag}>{tag}</option>)}
                  </select>
                </div>
                <div className="field">
                  <label>Caption</label>
                  <input value={photoForm.photoCaption} onChange={(event) => setPhotoForm((current) => ({ ...current, photoCaption: event.target.value }))} />
                </div>
                <div className="field">
                  <label>Dateien</label>
                  <input type="file" accept="image/png,image/jpeg,image/webp" multiple onChange={(event) => setPhotoForm((current) => ({ ...current, files: Array.from(event.target.files ?? []) }))} />
                </div>
                <button className="btn btn-primary" disabled={saving === 'photo' || bundle.measurements.length === 0}>{saving === 'photo' ? 'Laedt hoch...' : 'Fotos hochladen'}</button>
              </form>
              {photos.length > 0 && (
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8, padding: '0 14px 14px' }}>
                  {photos.map((photo) => (
                    <div key={photo.id} style={{ borderRadius: 8, overflow: 'hidden', border: '1px solid var(--border)' }}>
                      <img src={photo.relativePath} alt={photo.caption ?? `Foto ${photo.id}`} loading="lazy" style={{ width: '100%', aspectRatio: '4/3', objectFit: 'cover', display: 'block' }} />
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </>
  )
}

function toNullableNumber(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  const parsed = Number(trimmed.replace(',', '.'))
  return Number.isNaN(parsed) ? null : parsed
}

export default GrowDetailPage
