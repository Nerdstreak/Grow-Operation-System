import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type {
  CreateSetupRequest,
  CreateLightScheduleRequest,
  HomeAssistantSettingsDto,
  HvacControllerType,
  LightScheduleDto,
  LightControllerType,
  LightSource,
  MotherHealthStatus,
  QuarantineResult,
  SensorMetricType,
  SettingsOverviewDto,
  SetupDto,
  SetupType,
  TentDto,
  TentSensorDto,
  TentType,
  UpdateLightScheduleRequest,
  UpdateSetupRequest,
  UpdateTentRequest,
  CreateStrainRequest,
  StrainDto,
  StrainDominance,
  UpdateStrainRequest,
} from '../types'

type SensorDefinition = {
  metricType: SensorMetricType
  label: string
  placeholder?: string
}

type SensorGroup = {
  title: string
  sensors: SensorDefinition[]
}

type SetupDraft = {
  name: string
  setupType: SetupType
  notes: string
}

type StrainDraft = {
  name: string
  breeder: string
  dominance: StrainDominance
  flowerWeeksMin: string
  flowerWeeksMax: string
  notes: string
  nutrientDemandFactor: string
  stretchFactor: string
  vpdPreferenceShift: string
}

type LightScheduleDraft = {
  name: string
  isActive: boolean
  lightsOnTime: string
  lightsOffTime: string
  timeZoneId: string
  source: LightSource
}

const tentTypeOptions: TentType[] = ['Production', 'Mother', 'Quarantine', 'Propagation', 'MultiPurpose']
const setupTypeOptions: SetupType[] = ['Production', 'Mother', 'Quarantine']
const motherHealthOptions: Array<MotherHealthStatus | ''> = ['', 'Stable', 'Watch', 'Critical']
const quarantineResultOptions: Array<QuarantineResult | ''> = ['', 'Pending', 'Cleared', 'Rejected']
const strainDominanceOptions: StrainDominance[] = ['Unknown', 'Indica', 'Sativa', 'Hybrid']
const lightSourceOptions: LightSource[] = ['Manual', 'HomeAssistant']
const lightControllerOptions: Array<LightControllerType | ''> = ['', 'AcInfinityPro69', 'AcInfinityCloudline', 'GenericRelay', 'Manual', 'Other']
const hvacControllerOptions: Array<HvacControllerType | ''> = ['', 'AcInfinityPro69', 'AcInfinityCloudline', 'GenericRelay', 'Manual', 'Other']

const sensorGroups: SensorGroup[] = [
  {
    title: 'Klima',
    sensors: [
      { metricType: 'AirTemperature', label: 'Lufttemperatur', placeholder: 'sensor.tent_air_temp' },
      { metricType: 'Humidity', label: 'Luftfeuchte', placeholder: 'sensor.tent_humidity' },
      { metricType: 'Vpd', label: 'VPD', placeholder: 'sensor.tent_vpd' },
      { metricType: 'Co2', label: 'CO2', placeholder: 'sensor.tent_co2' },
      { metricType: 'Ppfd', label: 'PPFD', placeholder: 'sensor.tent_ppfd' },
      { metricType: 'LightStatus', label: 'Lichtstatus', placeholder: 'binary_sensor.tent_light' },
    ],
  },
  {
    title: 'Reservoir',
    sensors: [
      { metricType: 'ReservoirPh', label: 'Reservoir pH', placeholder: 'sensor.reservoir_ph' },
      { metricType: 'ReservoirEc', label: 'Reservoir EC', placeholder: 'sensor.reservoir_ec' },
      { metricType: 'ReservoirOrp', label: 'Reservoir ORP', placeholder: 'sensor.reservoir_orp' },
      { metricType: 'ReservoirDissolvedOxygen', label: 'DO', placeholder: 'sensor.reservoir_do' },
      { metricType: 'ReservoirWaterTemp', label: 'Wassertemperatur', placeholder: 'sensor.reservoir_temp' },
      { metricType: 'ReservoirLevel', label: 'Level', placeholder: 'sensor.reservoir_level' },
    ],
  },
  {
    title: 'Aktoren & Infrastruktur',
    sensors: [
      { metricType: 'PumpCirculation', label: 'Pumpe Umwaelzung', placeholder: 'switch.pump_circulation' },
      { metricType: 'PumpAir', label: 'Luftpumpe', placeholder: 'switch.pump_air' },
      { metricType: 'Chiller', label: 'Chiller', placeholder: 'switch.chiller' },
      { metricType: 'UpsBattery', label: 'UPS Batterie', placeholder: 'sensor.ups_battery' },
      { metricType: 'UpsStatus', label: 'UPS Status', placeholder: 'sensor.ups_status' },
    ],
  },
]

function SettingsPage() {
  const [settings, setSettings] = useState<SettingsOverviewDto | null>(null)
  const [setups, setSetups] = useState<SetupDto[]>([])
  const [strains, setStrains] = useState<StrainDto[]>([])
  const [strainDraft, setStrainDraft] = useState<StrainDraft>(createStrainDraft())
  const [strainError, setStrainError] = useState<string | null>(null)
  const [lightSchedulesByTent, setLightSchedulesByTent] = useState<Record<number, LightScheduleDto[]>>({})
  const [lightScheduleDrafts, setLightScheduleDrafts] = useState<Record<number, LightScheduleDraft>>({})
  const [lightScheduleErrors, setLightScheduleErrors] = useState<Record<number, string>>({})
  const [setupDrafts, setSetupDrafts] = useState<Record<number, SetupDraft>>({})
  const [setupErrors, setSetupErrors] = useState<Record<number, string>>({})
  const [setupEditErrors, setSetupEditErrors] = useState<Record<number, string>>({})
  const [savedTentTypes, setSavedTentTypes] = useState<Record<number, TentType>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      setError(null)

      try {
        const [data, setupData, strainData] = await Promise.all([
          apiFetch<SettingsOverviewDto>('/api/settings', { signal: controller.signal }),
          apiFetch<SetupDto[]>('/api/setups', { signal: controller.signal }),
          apiFetch<StrainDto[]>('/api/strains', { signal: controller.signal }),
        ])
        const scheduleEntries = await Promise.all(data.tents.map(async (tent) => [
          tent.id,
          await apiFetch<LightScheduleDto[]>(`/api/light-schedules?tentId=${tent.id}`, { signal: controller.signal }),
        ] as const))
        setSettings(data)
        setSetups(setupData)
        setStrains(strainData)
        setLightSchedulesByTent(Object.fromEntries(scheduleEntries))
        setSavedTentTypes(Object.fromEntries(data.tents.map((tent) => [tent.id, tent.tentType])))
      } catch (caught) {
        if (controller.signal.aborted) return
        setError(caught instanceof ApiRequestError ? caught.message : 'Setup konnte nicht geladen werden.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  async function handleHomeAssistantSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!settings) return

    setSaving('ha')
    setError(null)

    try {
      const saved = await apiFetch<HomeAssistantSettingsDto>('/api/settings/home-assistant', {
        method: 'PUT',
        body: JSON.stringify(settings.homeAssistant),
      })

      setSettings((current) => current ? { ...current, homeAssistant: saved } : current)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Home Assistant konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function saveTent(tent: TentDto) {
    setSaving(`tent-${tent.id}`)
    setError(null)

    try {
      const saved = await apiFetch<TentDto>(`/api/settings/tents/${tent.id}`, {
        method: 'PUT',
        body: JSON.stringify(toTentUpdateRequest(tent)),
      })

      setSettings((current) => current ? { ...current, tents: current.tents.map((item) => item.id === tent.id ? saved : item) } : current)
      setSavedTentTypes((current) => ({ ...current, [saved.id]: saved.tentType }))
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Zelt konnte nicht gespeichert werden.')
    } finally {
      setSaving(null)
    }
  }

  async function handleCreateSetup(event: FormEvent<HTMLFormElement>, tent: TentDto) {
    event.preventDefault()

    const savedTentType = getSavedTentType(tent, savedTentTypes)
    if (tent.tentType !== savedTentType) {
      setSetupErrors((current) => ({ ...current, [tent.id]: 'Zelttyp erst speichern, bevor Setups angelegt werden.' }))
      return
    }

    const allowedTypes = getAllowedSetupTypes(savedTentType)
    if (allowedTypes.length === 0) return

    const draft = getSetupDraft(tent, setupDrafts, savedTentType)
    const setupType = allowedTypes.includes(draft.setupType) ? draft.setupType : allowedTypes[0]
    const name = draft.name.trim()

    if (!name) {
      setSetupErrors((current) => ({ ...current, [tent.id]: 'Name darf nicht leer sein.' }))
      return
    }

    const request: CreateSetupRequest = {
      tentId: tent.id,
      name,
      setupType,
      notes: toNullableString(draft.notes),
    }

    setSaving(`setup-${tent.id}`)
    setSetupErrors((current) => {
      const next = { ...current }
      delete next[tent.id]
      return next
    })
    setError(null)

    try {
      const saved = await apiFetch<SetupDto>('/api/setups', {
        method: 'POST',
        body: JSON.stringify(request),
      })

      setSetups((current) => [...current, saved])
      setSetupDrafts((current) => ({ ...current, [tent.id]: createSetupDraft(savedTentType) }))
    } catch (caught) {
      setSetupErrors((current) => ({ ...current, [tent.id]: formatApiError(caught, 'Setup konnte nicht angelegt werden.') }))
    } finally {
      setSaving(null)
    }
  }

  async function handleCreateStrain(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const request = toStrainRequest(strainDraft)
    if (!request.name) {
      setStrainError('Name darf nicht leer sein.')
      return
    }

    setSaving('strain-new')
    setStrainError(null)

    try {
      const saved = await apiFetch<StrainDto>('/api/strains', {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setStrains((current) => [...current, saved])
      setStrainDraft(createStrainDraft())
    } catch (caught) {
      setStrainError(formatApiError(caught, 'Strain konnte nicht angelegt werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function saveStrain(strain: StrainDto) {
    const request: UpdateStrainRequest = {
      name: strain.name.trim(),
      breeder: toNullableString(strain.breeder),
      dominance: strain.dominance,
      flowerWeeksMin: strain.flowerWeeksMin,
      flowerWeeksMax: strain.flowerWeeksMax,
      notes: toNullableString(strain.notes),
      nutrientDemandFactor: strain.nutrientDemandFactor,
      stretchFactor: strain.stretchFactor,
      vpdPreferenceShift: strain.vpdPreferenceShift,
    }

    if (!request.name) {
      setStrainError('Name darf nicht leer sein.')
      return
    }

    setSaving(`strain-${strain.id}`)
    setStrainError(null)

    try {
      const saved = await apiFetch<StrainDto>(`/api/strains/${strain.id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      })
      setStrains((current) => current.map((item) => item.id === saved.id ? saved : item))
    } catch (caught) {
      setStrainError(formatApiError(caught, 'Strain konnte nicht gespeichert werden.'))
    } finally {
      setSaving(null)
    }
  }

  async function saveSetup(setup: SetupDto) {
    setSaving(`setup-edit-${setup.id}`)
    setSetupEditErrors((current) => {
      const next = { ...current }
      delete next[setup.id]
      return next
    })
    setError(null)

    const request: UpdateSetupRequest = {
      name: setup.name,
      status: setup.status,
      notes: setup.notes,
      cloneCounterTotal: setup.cloneCounterTotal,
      lastCloneCutAt: setup.lastCloneCutAt,
      motherHealthStatus: setup.motherHealthStatus,
      quarantineStartedAt: setup.quarantineStartedAt,
      quarantinePlannedEndAt: setup.quarantinePlannedEndAt,
      quarantineResult: setup.quarantineResult,
    }

    try {
      const saved = await apiFetch<SetupDto>(`/api/setups/${setup.id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      })

      setSetups((current) => current.map((item) => item.id === saved.id ? saved : item))
    } catch (caught) {
      setSetupEditErrors((current) => ({ ...current, [setup.id]: formatApiError(caught, 'Setup konnte nicht gespeichert werden.') }))
    } finally {
      setSaving(null)
    }
  }

  async function loadLightSchedules(tentId: number) {
    const schedules = await apiFetch<LightScheduleDto[]>(`/api/light-schedules?tentId=${tentId}`)
    setLightSchedulesByTent((current) => ({ ...current, [tentId]: schedules }))
  }

  async function handleCreateLightSchedule(event: FormEvent<HTMLFormElement>, tent: TentDto) {
    event.preventDefault()
    const draft = getLightScheduleDraft(tent.id, lightScheduleDrafts)
    const request: CreateLightScheduleRequest = {
      tentId: tent.id,
      name: draft.name.trim(),
      isActive: draft.isActive,
      lightsOnTime: draft.lightsOnTime.trim(),
      lightsOffTime: draft.lightsOffTime.trim(),
      timeZoneId: toNullableString(draft.timeZoneId),
      source: draft.source,
    }

    if (!request.name) {
      setLightScheduleErrors((current) => ({ ...current, [tent.id]: 'Name darf nicht leer sein.' }))
      return
    }

    setSaving(`light-schedule-${tent.id}`)
    setLightScheduleErrors((current) => {
      const next = { ...current }
      delete next[tent.id]
      return next
    })

    try {
      const saved = await apiFetch<LightScheduleDto>('/api/light-schedules', {
        method: 'POST',
        body: JSON.stringify(request),
      })
      setLightSchedulesByTent((current) => ({ ...current, [tent.id]: [...(current[tent.id] ?? []), saved] }))
      setLightScheduleDrafts((current) => ({ ...current, [tent.id]: createLightScheduleDraft() }))
    } catch (caught) {
      setLightScheduleErrors((current) => ({ ...current, [tent.id]: formatApiError(caught, 'LightSchedule konnte nicht angelegt werden.') }))
    } finally {
      setSaving(null)
    }
  }

  async function saveLightSchedule(schedule: LightScheduleDto) {
    const request: UpdateLightScheduleRequest = {
      name: schedule.name.trim(),
      isActive: schedule.isActive,
      lightsOnTime: schedule.lightsOnTime.trim(),
      lightsOffTime: schedule.lightsOffTime.trim(),
      timeZoneId: toNullableString(schedule.timeZoneId),
      source: schedule.source,
    }

    if (!request.name) {
      setLightScheduleErrors((current) => ({ ...current, [schedule.tentId]: 'Name darf nicht leer sein.' }))
      return
    }

    setSaving(`light-schedule-edit-${schedule.id}`)
    setLightScheduleErrors((current) => {
      const next = { ...current }
      delete next[schedule.tentId]
      return next
    })

    try {
      await apiFetch<LightScheduleDto>(`/api/light-schedules/${schedule.id}`, {
        method: 'PUT',
        body: JSON.stringify(request),
      })
      await loadLightSchedules(schedule.tentId)
    } catch (caught) {
      setLightScheduleErrors((current) => ({ ...current, [schedule.tentId]: formatApiError(caught, 'LightSchedule konnte nicht gespeichert werden.') }))
    } finally {
      setSaving(null)
    }
  }

  function updateTent(id: number, patch: Partial<TentDto>) {
    setSettings((current) => current ? {
      ...current,
      tents: current.tents.map((tent) => tent.id === id ? { ...tent, ...patch } : tent),
    } : current)
  }

  function updateSetupDraft(tent: TentDto, patch: Partial<SetupDraft>) {
    setSetupDrafts((current) => ({
      ...current,
      [tent.id]: { ...getSetupDraft(tent, current), ...patch },
    }))
  }

  function updateSetup(id: number, patch: Partial<SetupDto>) {
    setSetups((current) => current.map((setup) => setup.id === id ? { ...setup, ...patch } : setup))
  }

  function updateLightScheduleDraft(tentId: number, patch: Partial<LightScheduleDraft>) {
    setLightScheduleDrafts((current) => ({
      ...current,
      [tentId]: { ...getLightScheduleDraft(tentId, current), ...patch },
    }))
  }

  function updateLightSchedule(id: number, tentId: number, patch: Partial<LightScheduleDto>) {
    setLightSchedulesByTent((current) => ({
      ...current,
      [tentId]: (current[tentId] ?? []).map((schedule) => schedule.id === id ? { ...schedule, ...patch } : schedule),
    }))
  }

  function updateStrain(id: number, patch: Partial<StrainDto>) {
    setStrains((current) => current.map((strain) => strain.id === id ? { ...strain, ...patch } : strain))
  }

  function updateTentSensor(tentId: number, metricType: SensorMetricType, patch: Partial<TentSensorDto>) {
    setSettings((current) => current ? {
      ...current,
      tents: current.tents.map((tent) => {
        if (tent.id !== tentId) return tent

        const index = tent.sensors.findIndex((sensor) => sensor.metricType === metricType)
        if (index >= 0) {
          const sensors = [...tent.sensors]
          sensors[index] = { ...sensors[index], ...patch }
          return { ...tent, sensors }
        }

        return {
          ...tent,
          sensors: [...tent.sensors, { ...createEmptySensor(tentId, metricType), ...patch }],
        }
      }),
    } : current)
  }

  if (loading) {
    return (
      <>
        <div className="topbar"><span className="topbar-title">Einstellungen</span></div>
        <div className="page-scroll"><div className="empty-hint">Lade Einstellungen...</div></div>
      </>
    )
  }

  if (!settings) {
    return (
      <>
        <div className="topbar"><span className="topbar-title">Einstellungen</span></div>
        <div className="page-scroll">
          <div className="empty-hint" style={{ color: 'var(--red)' }}>{error ?? 'Setup nicht verfuegbar.'}</div>
        </div>
      </>
    )
  }

  return (
    <>
      <div className="topbar">
        <span className="topbar-title">Einstellungen</span>
        <div className="topbar-right">
          <span className={`badge ${settings.homeAssistant.enabled ? 'badge-ok' : 'badge-neutral'}`}>
            {settings.homeAssistant.enabled ? 'HA aktiv' : 'HA inaktiv'}
          </span>
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

        <div className="settings-layout">
          <nav className="settings-nav" aria-label="Einstellungsbereiche">
            <ul className="settings-nav-list">
              <li><a className="settings-nav-link" href="#settings-ha">Home Assistant</a></li>
              <li><a className="settings-nav-link" href="#settings-tents">Zelte</a></li>
              <li><a className="settings-nav-link" href="#settings-tents">Sensoren</a></li>
              <li><a className="settings-nav-link" href="#settings-tents">Licht</a></li>
              <li><a className="settings-nav-link" href="#settings-tents">Setups</a></li>
              <li><a className="settings-nav-link" href="#settings-strains">Strains</a></li>
            </ul>
          </nav>

          <div className="settings-grid" style={{ maxWidth: 'none' }}>
        <section id="settings-ha" className="admin-section">
        <div className="section-label">Home Assistant</div>
        <div className="card" style={{ marginBottom: 24, maxWidth: 640 }}>
          <div className="card-header"><span className="card-title">Verbindung</span></div>
          <form onSubmit={handleHomeAssistantSubmit} style={{ padding: '16px 20px', display: 'grid', gap: 14 }}>
            <label style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, fontSize: 14 }}>
              <span>Integration aktiv</span>
              <input
                type="checkbox"
                style={{ width: 'auto' }}
                checked={settings.homeAssistant.enabled}
                onChange={(event) => setSettings((current) => current ? {
                  ...current,
                  homeAssistant: { ...current.homeAssistant, enabled: event.target.checked },
                } : current)}
              />
            </label>

            <div className="field">
              <label>Base URL</label>
              <input
                value={settings.homeAssistant.baseUrl ?? ''}
                onChange={(event) => setSettings((current) => current ? {
                  ...current,
                  homeAssistant: { ...current.homeAssistant, baseUrl: event.target.value },
                } : current)}
                placeholder="http://homeassistant.local:8123/api/"
              />
            </div>

            <div className="field">
              <label>Access Token</label>
              <textarea
                value={settings.homeAssistant.accessToken ?? ''}
                onChange={(event) => setSettings((current) => current ? {
                  ...current,
                  homeAssistant: { ...current.homeAssistant, accessToken: event.target.value },
                } : current)}
                rows={3}
                className="mono"
              />
            </div>

            <button className="btn btn-primary" style={{ justifySelf: 'start' }} disabled={saving === 'ha'}>
              {saving === 'ha' ? 'Speichert...' : 'HA-Einstellungen speichern'}
            </button>
          </form>
        </div>
        </section>

        <section id="settings-strains" className="admin-section">
        <div className="section-label">Strains</div>
        <div className="card" style={{ marginBottom: 24 }}>
          <div className="card-header"><span className="card-title">Sorten</span></div>
          <div style={{ padding: '14px 16px', display: 'grid', gap: 12 }}>
            {strainError && <div style={{ fontSize: 13, color: 'var(--red)' }}>{strainError}</div>}
            {strains.length === 0 ? (
              <div style={{ fontSize: 13, color: 'var(--faint)' }}>Keine Strains angelegt.</div>
            ) : (
              <div style={{ display: 'grid', gap: 8 }}>
                {strains.map((strain) => (
                  <div
                    key={strain.id}
                    style={{
                      display: 'grid',
                      gridTemplateColumns: 'minmax(150px, 1.2fr) minmax(120px, 1fr) minmax(110px, 0.8fr) repeat(5, minmax(70px, 0.7fr)) auto',
                      gap: 8,
                      alignItems: 'end',
                      padding: '9px 10px',
                      border: '1px solid var(--border)',
                      borderRadius: 7,
                      background: 'var(--surface2)',
                    }}
                  >
                    <label className="field">
                      <span>Name</span>
                      <input value={strain.name} onChange={(event) => updateStrain(strain.id, { name: event.target.value })} />
                    </label>
                    <label className="field">
                      <span>Breeder</span>
                      <input value={strain.breeder ?? ''} onChange={(event) => updateStrain(strain.id, { breeder: toNullableString(event.target.value) })} />
                    </label>
                    <label className="field">
                      <span>Dominanz</span>
                      <select value={strain.dominance} onChange={(event) => updateStrain(strain.id, { dominance: event.target.value as StrainDominance })}>
                        {strainDominanceOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                      </select>
                    </label>
                    <label className="field">
                      <span>Bluete min</span>
                      <input type="number" value={strain.flowerWeeksMin ?? ''} onChange={(event) => updateStrain(strain.id, { flowerWeeksMin: toNullableInteger(event.target.value) })} />
                    </label>
                    <label className="field">
                      <span>Bluete max</span>
                      <input type="number" value={strain.flowerWeeksMax ?? ''} onChange={(event) => updateStrain(strain.id, { flowerWeeksMax: toNullableInteger(event.target.value) })} />
                    </label>
                    <label className="field">
                      <span>Naehrstoff</span>
                      <input type="number" step="0.1" value={strain.nutrientDemandFactor ?? ''} onChange={(event) => updateStrain(strain.id, { nutrientDemandFactor: toNullableNumber(event.target.value) })} />
                    </label>
                    <label className="field">
                      <span>Stretch</span>
                      <input type="number" step="0.1" value={strain.stretchFactor ?? ''} onChange={(event) => updateStrain(strain.id, { stretchFactor: toNullableNumber(event.target.value) })} />
                    </label>
                    <label className="field">
                      <span>VPD Shift</span>
                      <input type="number" step="0.1" value={strain.vpdPreferenceShift ?? ''} onChange={(event) => updateStrain(strain.id, { vpdPreferenceShift: toNullableNumber(event.target.value) })} />
                    </label>
                    <button type="button" className="btn" disabled={saving === `strain-${strain.id}`} onClick={() => void saveStrain(strain)}>
                      {saving === `strain-${strain.id}` ? 'Speichert...' : 'Speichern'}
                    </button>
                  </div>
                ))}
              </div>
            )}

            <form onSubmit={(event) => void handleCreateStrain(event)} style={{ display: 'grid', gridTemplateColumns: 'minmax(150px, 1fr) minmax(120px, 1fr) minmax(110px, 0.8fr) repeat(5, minmax(70px, 0.7fr)) auto', gap: 8, alignItems: 'end' }}>
              <label className="field">
                <span>Neuer Strain</span>
                <input value={strainDraft.name} onChange={(event) => setStrainDraft((current) => ({ ...current, name: event.target.value }))} />
              </label>
              <label className="field">
                <span>Breeder</span>
                <input value={strainDraft.breeder} onChange={(event) => setStrainDraft((current) => ({ ...current, breeder: event.target.value }))} />
              </label>
              <label className="field">
                <span>Dominanz</span>
                <select value={strainDraft.dominance} onChange={(event) => setStrainDraft((current) => ({ ...current, dominance: event.target.value as StrainDominance }))}>
                  {strainDominanceOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                </select>
              </label>
              <label className="field">
                <span>Bluete min</span>
                <input type="number" value={strainDraft.flowerWeeksMin} onChange={(event) => setStrainDraft((current) => ({ ...current, flowerWeeksMin: event.target.value }))} />
              </label>
              <label className="field">
                <span>Bluete max</span>
                <input type="number" value={strainDraft.flowerWeeksMax} onChange={(event) => setStrainDraft((current) => ({ ...current, flowerWeeksMax: event.target.value }))} />
              </label>
              <label className="field">
                <span>Naehrstoff</span>
                <input type="number" step="0.1" value={strainDraft.nutrientDemandFactor} onChange={(event) => setStrainDraft((current) => ({ ...current, nutrientDemandFactor: event.target.value }))} />
              </label>
              <label className="field">
                <span>Stretch</span>
                <input type="number" step="0.1" value={strainDraft.stretchFactor} onChange={(event) => setStrainDraft((current) => ({ ...current, stretchFactor: event.target.value }))} />
              </label>
              <label className="field">
                <span>VPD Shift</span>
                <input type="number" step="0.1" value={strainDraft.vpdPreferenceShift} onChange={(event) => setStrainDraft((current) => ({ ...current, vpdPreferenceShift: event.target.value }))} />
              </label>
              <button className="btn" disabled={saving === 'strain-new'}>
                {saving === 'strain-new' ? 'Legt an...' : 'Strain anlegen'}
              </button>
              <label className="field" style={{ gridColumn: '1 / -1' }}>
                <span>Notizen</span>
                <textarea rows={2} value={strainDraft.notes} onChange={(event) => setStrainDraft((current) => ({ ...current, notes: event.target.value }))} />
              </label>
            </form>
          </div>
        </div>
        </section>

        <section id="settings-tents" className="admin-section">
        <div className="section-label">Zelte</div>
        <div className="tents-grid">
          {settings.tents.map((tent) => {
            const savedTentType = getSavedTentType(tent, savedTentTypes)
            const hasUnsavedTentType = tent.tentType !== savedTentType
            const allowedSetupTypes = getAllowedSetupTypes(savedTentType)
            const normalizedSetupType = getNormalizedSetupType(tent, setupDrafts, savedTentType)
            const lightSchedules = lightSchedulesByTent[tent.id] ?? []
            const lightScheduleDraft = getLightScheduleDraft(tent.id, lightScheduleDrafts)

            return (
              <div key={tent.id} className="card">
              <div className="card-header">
                <div>
                  <div style={{ fontWeight: 600, fontSize: 15 }}>{tent.name}</div>
                  <div style={{ fontSize: 12, color: 'var(--muted)', marginTop: 2 }}>
                    {tent.kind} · {tent.activeGrowCount} aktiv · {tent.archivedGrowCount} archiviert
                  </div>
                </div>
                <span className={`badge ${tent.activeGrowCount > 0 ? 'badge-ok' : 'badge-neutral'}`}>
                  {tent.activeGrowCount} aktiv
                </span>
              </div>

              <div style={{ padding: '14px 16px', display: 'grid', gap: 14 }}>
                <div style={{ fontSize: 12, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--muted)' }}>
                  Stammdaten
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                  <label className="field">
                    <span>Name</span>
                    <input value={tent.name} onChange={(event) => updateTent(tent.id, { name: event.target.value })} />
                  </label>
                  <label className="field">
                    <span>Typbezeichnung</span>
                    <input value={tent.kind} onChange={(event) => updateTent(tent.id, { kind: event.target.value })} />
                  </label>
                  <label className="field">
                    <span>Tent-Typ</span>
                    <select value={tent.tentType} onChange={(event) => updateTent(tent.id, { tentType: event.target.value as TentType })}>
                      {tentTypeOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                    </select>
                  </label>
                  <label className="field">
                    <span>Sortierung</span>
                    <input type="number" value={tent.displayOrder} onChange={(event) => updateTent(tent.id, { displayOrder: toInteger(event.target.value, tent.displayOrder) })} />
                  </label>
                  <label className="field">
                    <span>Akzentfarbe</span>
                    <input type="color" value={tent.accentColor} onChange={(event) => updateTent(tent.id, { accentColor: event.target.value })} />
                  </label>
                  <label className="field">
                    <span>Kamera</span>
                    <input className="mono" value={tent.cameraEntityId ?? ''} onChange={(event) => updateTent(tent.id, { cameraEntityId: toNullableString(event.target.value) })} placeholder="camera.main_tent" />
                  </label>
                </div>

                <label className="field">
                  <span>Notizen</span>
                  <textarea rows={3} value={tent.notes ?? ''} onChange={(event) => updateTent(tent.id, { notes: toNullableString(event.target.value) })} />
                </label>

                <div style={{ fontSize: 12, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--muted)' }}>
                  Setups
                </div>
                <div style={{ display: 'grid', gap: 10 }}>
                  {setups.filter((setup) => setup.tentId === tent.id).length === 0 ? (
                    <div style={{ fontSize: 13, color: 'var(--faint)' }}>Keine Setups angelegt.</div>
                  ) : (
                    <div style={{ display: 'grid', gap: 8 }}>
                      {setups.filter((setup) => setup.tentId === tent.id).map((setup) => (
                        <div
                          key={setup.id}
                          style={{
                            display: 'grid',
                            gridTemplateColumns: 'minmax(0, 1fr) auto auto',
                            gap: 8,
                            alignItems: 'center',
                            padding: '9px 10px',
                            border: '1px solid var(--border)',
                            borderRadius: 7,
                            background: 'var(--surface2)',
                          }}
                        >
                          <div style={{ minWidth: 0 }}>
                            <div style={{ fontSize: 14, fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{setup.name}</div>
                            {setup.notes && (
                              <div style={{ fontSize: 12, color: 'var(--muted)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{setup.notes}</div>
                            )}
                          </div>
                          <span className="badge badge-info">{setup.setupType}</span>
                          <span className={`badge ${setup.status === 'Active' ? 'badge-ok' : setup.status === 'Archived' ? 'badge-neutral' : 'badge-warn'}`}>
                            {setup.status}
                          </span>
                          {setup.setupType === 'Mother' && (
                            <div style={{ gridColumn: '1 / -1', display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr)) auto', gap: 8, alignItems: 'end' }}>
                              <label className="field">
                                <span>Clone gesamt</span>
                                <input
                                  type="number"
                                  value={setup.cloneCounterTotal ?? ''}
                                  onChange={(event) => updateSetup(setup.id, { cloneCounterTotal: toNullableInteger(event.target.value) })}
                                />
                              </label>
                              <label className="field">
                                <span>Letzter Schnitt</span>
                                <input
                                  type="date"
                                  value={toDateInputValue(setup.lastCloneCutAt)}
                                  onChange={(event) => updateSetup(setup.id, { lastCloneCutAt: toNullableDate(event.target.value) })}
                                />
                              </label>
                              <label className="field">
                                <span>Health</span>
                                <select
                                  value={setup.motherHealthStatus ?? ''}
                                  onChange={(event) => updateSetup(setup.id, { motherHealthStatus: toNullableString(event.target.value) as MotherHealthStatus | null })}
                                >
                                  {motherHealthOptions.map((value) => <option key={value || 'empty'} value={value}>{value || 'Nicht gesetzt'}</option>)}
                                </select>
                              </label>
                              <button type="button" className="btn" disabled={saving === `setup-edit-${setup.id}`} onClick={() => void saveSetup(setup)}>
                                {saving === `setup-edit-${setup.id}` ? 'Speichert...' : 'Speichern'}
                              </button>
                            </div>
                          )}
                          {setup.setupType === 'Quarantine' && (
                            <div style={{ gridColumn: '1 / -1', display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr)) auto', gap: 8, alignItems: 'end' }}>
                              <label className="field">
                                <span>Start</span>
                                <input
                                  type="date"
                                  value={toDateInputValue(setup.quarantineStartedAt)}
                                  onChange={(event) => updateSetup(setup.id, { quarantineStartedAt: toNullableDate(event.target.value) })}
                                />
                              </label>
                              <label className="field">
                                <span>Geplantes Ende</span>
                                <input
                                  type="date"
                                  value={toDateInputValue(setup.quarantinePlannedEndAt)}
                                  onChange={(event) => updateSetup(setup.id, { quarantinePlannedEndAt: toNullableDate(event.target.value) })}
                                />
                              </label>
                              <label className="field">
                                <span>Ergebnis</span>
                                <select
                                  value={setup.quarantineResult ?? ''}
                                  onChange={(event) => updateSetup(setup.id, { quarantineResult: toNullableString(event.target.value) as QuarantineResult | null })}
                                >
                                  {quarantineResultOptions.map((value) => <option key={value || 'empty'} value={value}>{value || 'Nicht gesetzt'}</option>)}
                                </select>
                              </label>
                              <button type="button" className="btn" disabled={saving === `setup-edit-${setup.id}`} onClick={() => void saveSetup(setup)}>
                                {saving === `setup-edit-${setup.id}` ? 'Speichert...' : 'Speichern'}
                              </button>
                            </div>
                          )}
                          {setupEditErrors[setup.id] && (
                            <div style={{ gridColumn: '1 / -1', fontSize: 13, color: 'var(--red)' }}>{setupEditErrors[setup.id]}</div>
                          )}
                        </div>
                      ))}
                    </div>
                  )}

                  {hasUnsavedTentType ? (
                    <div className="field-hint">Zelttyp erst speichern, bevor Setups angelegt werden.</div>
                  ) : allowedSetupTypes.length === 0 ? (
                    <div className="field-hint">Propagation wird spaeter unterstuetzt.</div>
                  ) : (
                    <form onSubmit={(event) => void handleCreateSetup(event, tent)} style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) minmax(130px, 170px)', gap: 10, alignItems: 'end' }}>
                      <label className="field">
                        <span>Neues Setup</span>
                        <input
                          value={getSetupDraft(tent, setupDrafts, savedTentType).name}
                          onChange={(event) => updateSetupDraft(tent, { name: event.target.value })}
                          placeholder="Name"
                          disabled={hasUnsavedTentType}
                        />
                      </label>
                      <label className="field">
                        <span>Typ</span>
                        <select
                          value={normalizedSetupType}
                          onChange={(event) => updateSetupDraft(tent, { setupType: event.target.value as SetupType })}
                          disabled={hasUnsavedTentType}
                        >
                          {allowedSetupTypes.map((value) => <option key={value} value={value}>{value}</option>)}
                        </select>
                      </label>
                      <label className="field" style={{ gridColumn: '1 / -1' }}>
                        <span>Notizen</span>
                        <textarea
                          rows={2}
                          value={getSetupDraft(tent, setupDrafts, savedTentType).notes}
                          onChange={(event) => updateSetupDraft(tent, { notes: event.target.value })}
                          placeholder="Optional"
                          disabled={hasUnsavedTentType}
                        />
                      </label>
                      {setupErrors[tent.id] && (
                        <div style={{ gridColumn: '1 / -1', fontSize: 13, color: 'var(--red)' }}>{setupErrors[tent.id]}</div>
                      )}
                      <button
                        type="submit"
                        className="btn"
                        style={{ justifySelf: 'start' }}
                        disabled={hasUnsavedTentType || saving === `setup-${tent.id}`}
                      >
                        {saving === `setup-${tent.id}` ? 'Legt an...' : 'Setup anlegen'}
                      </button>
                    </form>
                  )}
                </div>

                <div style={{ fontSize: 12, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--muted)' }}>
                  LightSchedules
                </div>
                <div style={{ display: 'grid', gap: 10 }}>
                  {lightSchedules.length === 0 ? (
                    <div style={{ fontSize: 13, color: 'var(--faint)' }}>Keine LightSchedules angelegt.</div>
                  ) : (
                    <div style={{ display: 'grid', gap: 8 }}>
                      {lightSchedules.map((schedule) => (
                        <div
                          key={schedule.id}
                          style={{
                            display: 'grid',
                            gridTemplateColumns: 'minmax(120px, 1fr) 92px 92px minmax(110px, 140px) minmax(120px, 1fr) auto auto',
                            gap: 8,
                            alignItems: 'end',
                            padding: '9px 10px',
                            border: '1px solid var(--border)',
                            borderRadius: 7,
                            background: 'var(--surface2)',
                          }}
                        >
                          <label className="field">
                            <span>Name</span>
                            <input value={schedule.name} onChange={(event) => updateLightSchedule(schedule.id, tent.id, { name: event.target.value })} />
                          </label>
                          <label className="field">
                            <span>An</span>
                            <input type="time" value={schedule.lightsOnTime} onChange={(event) => updateLightSchedule(schedule.id, tent.id, { lightsOnTime: event.target.value })} />
                          </label>
                          <label className="field">
                            <span>Aus</span>
                            <input type="time" value={schedule.lightsOffTime} onChange={(event) => updateLightSchedule(schedule.id, tent.id, { lightsOffTime: event.target.value })} />
                          </label>
                          <label className="field">
                            <span>Source</span>
                            <select value={schedule.source} onChange={(event) => updateLightSchedule(schedule.id, tent.id, { source: event.target.value as LightSource })}>
                              {lightSourceOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                            </select>
                          </label>
                          <label className="field">
                            <span>TimeZoneId</span>
                            <input value={schedule.timeZoneId ?? ''} onChange={(event) => updateLightSchedule(schedule.id, tent.id, { timeZoneId: toNullableString(event.target.value) })} placeholder="Europe/Berlin" />
                          </label>
                          <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, paddingBottom: 9 }}>
                            <input type="checkbox" style={{ width: 'auto' }} checked={schedule.isActive} onChange={(event) => updateLightSchedule(schedule.id, tent.id, { isActive: event.target.checked })} />
                            <span>Aktiv</span>
                          </label>
                          <button type="button" className="btn" disabled={saving === `light-schedule-edit-${schedule.id}`} onClick={() => void saveLightSchedule(schedule)}>
                            {saving === `light-schedule-edit-${schedule.id}` ? 'Speichert...' : 'Speichern'}
                          </button>
                        </div>
                      ))}
                    </div>
                  )}

                  <form onSubmit={(event) => void handleCreateLightSchedule(event, tent)} style={{ display: 'grid', gridTemplateColumns: 'minmax(120px, 1fr) 92px 92px minmax(110px, 140px) minmax(120px, 1fr) auto auto', gap: 8, alignItems: 'end' }}>
                    <label className="field">
                      <span>Neuer Plan</span>
                      <input value={lightScheduleDraft.name} onChange={(event) => updateLightScheduleDraft(tent.id, { name: event.target.value })} placeholder="Bluete 12/12" />
                    </label>
                    <label className="field">
                      <span>An</span>
                      <input type="time" value={lightScheduleDraft.lightsOnTime} onChange={(event) => updateLightScheduleDraft(tent.id, { lightsOnTime: event.target.value })} />
                    </label>
                    <label className="field">
                      <span>Aus</span>
                      <input type="time" value={lightScheduleDraft.lightsOffTime} onChange={(event) => updateLightScheduleDraft(tent.id, { lightsOffTime: event.target.value })} />
                    </label>
                    <label className="field">
                      <span>Source</span>
                      <select value={lightScheduleDraft.source} onChange={(event) => updateLightScheduleDraft(tent.id, { source: event.target.value as LightSource })}>
                        {lightSourceOptions.map((value) => <option key={value} value={value}>{value}</option>)}
                      </select>
                    </label>
                    <label className="field">
                      <span>TimeZoneId</span>
                      <input value={lightScheduleDraft.timeZoneId} onChange={(event) => updateLightScheduleDraft(tent.id, { timeZoneId: event.target.value })} placeholder="Optional" />
                    </label>
                    <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, paddingBottom: 9 }}>
                      <input type="checkbox" style={{ width: 'auto' }} checked={lightScheduleDraft.isActive} onChange={(event) => updateLightScheduleDraft(tent.id, { isActive: event.target.checked })} />
                      <span>Aktiv</span>
                    </label>
                    <button type="submit" className="btn" disabled={saving === `light-schedule-${tent.id}`}>
                      {saving === `light-schedule-${tent.id}` ? 'Legt an...' : 'Anlegen'}
                    </button>
                    {lightScheduleErrors[tent.id] && (
                      <div style={{ gridColumn: '1 / -1', fontSize: 13, color: 'var(--red)' }}>{lightScheduleErrors[tent.id]}</div>
                    )}
                  </form>
                </div>

                <div style={{ fontSize: 12, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--muted)' }}>
                  Abmessungen & Licht
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                  <label className="field">
                    <span>Breite cm</span>
                    <input type="number" value={tent.widthCm ?? ''} onChange={(event) => updateTent(tent.id, { widthCm: toNullableInteger(event.target.value) })} />
                  </label>
                  <label className="field">
                    <span>Tiefe cm</span>
                    <input type="number" value={tent.depthCm ?? ''} onChange={(event) => updateTent(tent.id, { depthCm: toNullableInteger(event.target.value) })} />
                  </label>
                  <label className="field">
                    <span>Hoehe cm</span>
                    <input type="number" value={tent.tentHeightCm ?? ''} onChange={(event) => updateTent(tent.id, { tentHeightCm: toNullableInteger(event.target.value) })} />
                  </label>
                  <label className="field">
                    <span>Lichttyp</span>
                    <input value={tent.lightType ?? ''} onChange={(event) => updateTent(tent.id, { lightType: toNullableString(event.target.value) })} placeholder="LED Bar 480W" />
                  </label>
                  <label className="field">
                    <span>Licht Watt</span>
                    <input type="number" value={tent.lightWatt ?? ''} onChange={(event) => updateTent(tent.id, { lightWatt: toNullableInteger(event.target.value) })} />
                  </label>
                  <label className="field">
                    <span>Licht-Controller</span>
                    <select value={tent.lightController ?? ''} onChange={(event) => updateTent(tent.id, { lightController: toNullableString(event.target.value) as LightControllerType | null })}>
                      <option value="">Nicht gesetzt</option>
                      {lightControllerOptions.filter(Boolean).map((value) => <option key={value} value={value}>{value}</option>)}
                    </select>
                  </label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}>
                    <span>Licht-Controller Entity</span>
                    <input className="mono" value={tent.lightControllerEntityId ?? ''} onChange={(event) => updateTent(tent.id, { lightControllerEntityId: toNullableString(event.target.value) })} placeholder="climate.light_controller" />
                  </label>
                </div>

                <div style={{ fontSize: 12, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--muted)' }}>
                  Klima & Hardware
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                  <label className="field">
                    <span>Abluft Ventilatoren</span>
                    <input type="number" value={tent.exhaustFanCount ?? ''} onChange={(event) => updateTent(tent.id, { exhaustFanCount: toNullableInteger(event.target.value) })} />
                  </label>
                  <label className="field">
                    <span>Abluft m3/h</span>
                    <input type="number" value={tent.exhaustM3h ?? ''} onChange={(event) => updateTent(tent.id, { exhaustM3h: toNullableInteger(event.target.value) })} />
                  </label>
                  <label className="field">
                    <span>Umluft Ventilatoren</span>
                    <input type="number" value={tent.circulationFanCount ?? ''} onChange={(event) => updateTent(tent.id, { circulationFanCount: toNullableInteger(event.target.value) })} />
                  </label>
                  <label className="field">
                    <span>HVAC-Controller</span>
                    <select value={tent.hvacController ?? ''} onChange={(event) => updateTent(tent.id, { hvacController: toNullableString(event.target.value) as HvacControllerType | null })}>
                      <option value="">Nicht gesetzt</option>
                      {hvacControllerOptions.filter(Boolean).map((value) => <option key={value} value={value}>{value}</option>)}
                    </select>
                  </label>
                  <label className="field" style={{ gridColumn: '1 / -1' }}>
                    <span>HVAC-Controller Entity</span>
                    <input className="mono" value={tent.hvacControllerEntityId ?? ''} onChange={(event) => updateTent(tent.id, { hvacControllerEntityId: toNullableString(event.target.value) })} placeholder="climate.hvac_controller" />
                  </label>
                </div>

                <label style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, fontSize: 14 }}>
                  <span>CO2 verfuegbar</span>
                  <input type="checkbox" style={{ width: 'auto' }} checked={tent.co2Available} onChange={(event) => updateTent(tent.id, { co2Available: event.target.checked })} />
                </label>

                <div style={{ fontSize: 12, fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--muted)' }}>
                  Sensor-Mapping
                </div>
                {sensorGroups.map((group) => (
                  <div key={group.title} style={{ display: 'grid', gap: 10 }}>
                    <div style={{ fontSize: 12, color: 'var(--muted)', fontWeight: 700 }}>{group.title}</div>
                    {group.sensors.map((definition) => {
                      const sensor = getTentSensor(tent, definition.metricType)
                      return (
                        <div key={definition.metricType} className="sensor-map-row">
                          <label className="field">
                            <span>{definition.label}</span>
                            <input
                              className="mono"
                              value={sensor.haEntityId}
                              onChange={(event) => updateTentSensor(tent.id, definition.metricType, { haEntityId: event.target.value, isActive: sensor.isActive || event.target.value.trim().length > 0 })}
                              placeholder={definition.placeholder ?? 'sensor.entity_id'}
                            />
                          </label>
                          <label className="field">
                            <span>Display-Label</span>
                            <input
                              value={sensor.displayLabel ?? ''}
                              onChange={(event) => updateTentSensor(tent.id, definition.metricType, { displayLabel: toNullableString(event.target.value) })}
                              placeholder={definition.label}
                            />
                          </label>
                          <div className="field">
                            <label>Status</label>
                            <select
                              value={sensor.isActive ? 'active' : 'inactive'}
                              onChange={(event) => updateTentSensor(tent.id, definition.metricType, { isActive: event.target.value === 'active' })}
                            >
                              <option value="inactive">Inaktiv</option>
                              <option value="active">Aktiv</option>
                            </select>
                          </div>
                          <button
                            type="button"
                            className="btn"
                            onClick={() => updateTentSensor(tent.id, definition.metricType, { haEntityId: '', displayLabel: null, isActive: false })}
                          >
                            Reset
                          </button>
                        </div>
                      )
                    })}
                  </div>
                ))}

                <button
                  type="button"
                  className="btn btn-primary"
                  style={{ marginTop: 4, justifySelf: 'start' }}
                  disabled={saving === `tent-${tent.id}`}
                  onClick={() => void saveTent(tent)}
                >
                  {saving === `tent-${tent.id}` ? 'Speichert...' : 'Zelt speichern'}
                </button>
              </div>
            </div>
            )
          })}
        </div>
        </section>
          </div>
        </div>
      </div>
    </>
  )
}

function createEmptySensor(tentId: number, metricType: SensorMetricType): TentSensorDto {
  return {
    id: 0,
    tentId,
    metricType,
    haEntityId: '',
    displayLabel: null,
    isActive: false,
  }
}

function getTentSensor(tent: TentDto, metricType: SensorMetricType): TentSensorDto {
  return tent.sensors.find((sensor) => sensor.metricType === metricType) ?? createEmptySensor(tent.id, metricType)
}

function createSetupDraft(tentType: TentType): SetupDraft {
  return {
    name: '',
    setupType: getAllowedSetupTypes(tentType)[0] ?? 'Production',
    notes: '',
  }
}

function createStrainDraft(): StrainDraft {
  return {
    name: '',
    breeder: '',
    dominance: 'Unknown',
    flowerWeeksMin: '',
    flowerWeeksMax: '',
    notes: '',
    nutrientDemandFactor: '',
    stretchFactor: '',
    vpdPreferenceShift: '',
  }
}

function createLightScheduleDraft(): LightScheduleDraft {
  return {
    name: '',
    isActive: true,
    lightsOnTime: '08:00',
    lightsOffTime: '20:00',
    timeZoneId: '',
    source: 'Manual',
  }
}

function getLightScheduleDraft(tentId: number, drafts: Record<number, LightScheduleDraft>): LightScheduleDraft {
  return drafts[tentId] ?? createLightScheduleDraft()
}

function toStrainRequest(draft: StrainDraft): CreateStrainRequest {
  return {
    name: draft.name.trim(),
    breeder: toNullableString(draft.breeder),
    dominance: draft.dominance,
    flowerWeeksMin: toNullableInteger(draft.flowerWeeksMin),
    flowerWeeksMax: toNullableInteger(draft.flowerWeeksMax),
    notes: toNullableString(draft.notes),
    nutrientDemandFactor: toNullableNumber(draft.nutrientDemandFactor),
    stretchFactor: toNullableNumber(draft.stretchFactor),
    vpdPreferenceShift: toNullableNumber(draft.vpdPreferenceShift),
  }
}

function getSetupDraft(tent: TentDto, drafts: Record<number, SetupDraft>, tentType: TentType = tent.tentType): SetupDraft {
  return drafts[tent.id] ?? createSetupDraft(tentType)
}

function getNormalizedSetupType(tent: TentDto, drafts: Record<number, SetupDraft>, tentType: TentType = tent.tentType): SetupType {
  const allowedTypes = getAllowedSetupTypes(tentType)
  const draft = getSetupDraft(tent, drafts, tentType)
  return allowedTypes.includes(draft.setupType) ? draft.setupType : allowedTypes[0]
}

function getSavedTentType(tent: TentDto, savedTentTypes: Record<number, TentType>): TentType {
  return savedTentTypes[tent.id] ?? tent.tentType
}

function getAllowedSetupTypes(tentType: TentType): SetupType[] {
  switch (tentType) {
    case 'Production':
      return ['Production']
    case 'Mother':
      return ['Mother']
    case 'Quarantine':
      return ['Quarantine']
    case 'MultiPurpose':
      return setupTypeOptions
    case 'Propagation':
      return []
  }
}

function formatApiError(caught: unknown, fallback: string): string {
  if (caught instanceof ApiRequestError) {
    const firstFieldError = caught.payload?.fieldErrors ? Object.values(caught.payload.fieldErrors).flat()[0] : null
    return firstFieldError ?? caught.message
  }

  return caught instanceof Error ? caught.message : fallback
}

function toTentUpdateRequest(tent: TentDto): UpdateTentRequest {
  return {
    name: tent.name.trim(),
    kind: tent.kind.trim() || 'Grow Tent',
    tentType: tent.tentType,
    notes: toNullableString(tent.notes),
    displayOrder: tent.displayOrder,
    accentColor: tent.accentColor.trim() || '#69b578',
    widthCm: tent.widthCm,
    depthCm: tent.depthCm,
    tentHeightCm: tent.tentHeightCm,
    lightType: toNullableString(tent.lightType),
    lightWatt: tent.lightWatt,
    lightController: tent.lightController,
    lightControllerEntityId: toNullableString(tent.lightControllerEntityId),
    exhaustFanCount: tent.exhaustFanCount,
    exhaustM3h: tent.exhaustM3h,
    circulationFanCount: tent.circulationFanCount,
    hvacController: tent.hvacController,
    hvacControllerEntityId: toNullableString(tent.hvacControllerEntityId),
    co2Available: tent.co2Available,
    cameraEntityId: toNullableString(tent.cameraEntityId),
    sensors: tent.sensors
      .map((sensor) => ({
        id: sensor.id,
        metricType: sensor.metricType,
        haEntityId: toNullableString(sensor.haEntityId),
        displayLabel: toNullableString(sensor.displayLabel),
        isActive: sensor.isActive,
      }))
      .filter((sensor) => sensor.haEntityId || sensor.displayLabel || sensor.isActive),
  }
}

function toNullableString(value: string | null | undefined): string | null {
  const normalized = value?.trim() ?? ''
  return normalized.length > 0 ? normalized : null
}

function toNullableDate(value: string): string | null {
  return value ? value : null
}

function toDateInputValue(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : ''
}

function toNullableInteger(value: string): number | null {
  const normalized = value.trim()
  if (!normalized) return null

  const parsed = Number.parseInt(normalized, 10)
  return Number.isNaN(parsed) ? null : parsed
}

function toNullableNumber(value: string): number | null {
  const normalized = value.trim()
  if (!normalized) return null

  const parsed = Number.parseFloat(normalized)
  return Number.isNaN(parsed) ? null : parsed
}

function toInteger(value: string, fallback: number): number {
  return toNullableInteger(value) ?? fallback
}

export default SettingsPage
