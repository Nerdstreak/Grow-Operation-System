import { useEffect, useMemo, useRef, useState } from 'react'
import { nurDatum } from '../features/grows/nur-datum'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiFetch, formatApiError } from '../api'
import type { GrowDetail, GrowEntryPoint, GrowStatus, GrowSummary, GrowUpsertPayload, HydroSetupDto, KnowledgeOverviewDto, NutrientProgramDto, SeedType, StartMaterial, StrainDto, TentDto, PlantInstanceDto } from '../types'
import { V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1LinkButton, V1Page, V1Section, V1Skeleton } from '../components/v1'
import { formatLiters, toNullableInt } from '../components/v1-utils'
import { classNames } from '../utils'
import { aufstellungName, einstiegName, materialName, samenName, statusName, zeltZweckName } from '../deutsche-woerter'
import { ProfileSelect } from '../features/setpoints/ProfileSelect'
import { GrowPlanPanel } from '../features/grows/GrowPlanPanel'
import { buildTimeline, canCreate, checkPlan } from '../features/grows/grow-plan-model'
import '../features/grows/grows.css'

const entryPoints: GrowEntryPoint[] = ['Germination', 'Seedling', 'Veg', 'Flower', 'Flush']
const statuses: GrowStatus[] = ['Planning', 'Running', 'Completed', 'Aborted']
const seedTypes: SeedType[] = ['Feminized', 'Autoflower', 'Regular']
const startMaterials: StartMaterial[] = ['Seed', 'Clone']

function emptyForm(): GrowUpsertPayload {
  return {
    templateId: null, name: '', tentId: null, systemId: null, setupId: null, strain: null, breeder: null, seedType: 'Feminized', startMaterial: 'Seed', germinationMethod: 'PaperTowel',
    cloneSource: null, cloneIsRooted: false, phenoNumber: null, breederFlowerWeeksMin: null, breederFlowerWeeksMax: null, plannedVegDays: null, strainId: null, setpointProfileId: null, hydroStyle: 'RDWC', plantCount: null, reservoirSize: null,
    containerSize: null, propagationMedium: 'Rockwool', light: null, hasChiller: false, waterSource: 'RO', nutrients: null, startDate: new Date().toISOString().slice(0, 10),
    entryPoint: 'Germination', daysAlreadyInPhase: null, autoflowerDaysSinceGermination: null, flipDate: null, notes: null, status: 'Planning', environment: 'Indoor',
  }
}

function GrowSetupPage() {
  const { growId } = useParams()
  const navigate = useNavigate()
  const isEditing = Boolean(growId)
  const [tents, setTents] = useState<TentDto[]>([])
  const [strains, setStrains] = useState<StrainDto[]>([])
  const [hydroSetups, setHydroSetups] = useState<HydroSetupDto[]>([])
  const [programs, setPrograms] = useState<NutrientProgramDto[]>([])
  // Fuer die Belegungspruefung: welche anderen Grows sitzen schon im Zelt.
  const [otherGrows, setOtherGrows] = useState<GrowSummary[]>([])
  const [form, setForm] = useState<GrowUpsertPayload>(() => emptyForm())
  const [customProgram, setCustomProgram] = useState('')
  // Die gespeicherte Programm-Id des Grows. Ohne sie hing das Programm am
  // Namensvergleich — und ein fehlgeschlagener Wissens-Abruf haette es beim
  // Speichern still auf null gesetzt.
  const [feedProgramId, setFeedProgramId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Wie viele Pflanzen einzeln erfasst sind. Sind es welche, sind SIE die
  // Wahrheit ueber die Anzahl — der Server zieht plantCount danach.
  const [erfasstePflanzen, setErfasstePflanzen] = useState(0)

  useEffect(() => {
    if (!isEditing || !growId) return
    const controller = new AbortController()
    apiFetch<PlantInstanceDto[]>(`/api/plants?growId=${growId}`, { signal: controller.signal })
      .then((pflanzen) => setErfasstePflanzen(pflanzen.length))
      .catch(() => { /* Ohne Pflanzen-API bleibt das Feld beschreibbar. */ })
    return () => controller.abort()
  }, [isEditing, growId])

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError(null)
      try {
        const [tentData, hydroData, knowledge, grow, growsData, strainData] = await Promise.all([
          apiFetch<TentDto[]>('/api/settings/tents', { signal: controller.signal }),
          apiFetch<HydroSetupDto[]>('/api/hydro-setups?includeArchived=true', { signal: controller.signal }),
          apiFetch<KnowledgeOverviewDto>('/api/knowledge', { signal: controller.signal }),
          isEditing && growId ? apiFetch<GrowDetail>(`/api/grows/${growId}`, { signal: controller.signal }) : Promise.resolve(null),
          apiFetch<GrowSummary[]>('/api/grows?archived=false', { signal: controller.signal }).catch(() => []),
          apiFetch<StrainDto[]>('/api/strains', { signal: controller.signal }).catch(() => [] as StrainDto[]),
        ])
        if (controller.signal.aborted) return
        setTents(tentData)
        setHydroSetups(hydroData.filter((setup) => setup.status === 'Active'))
        setPrograms(knowledge.programs ?? [])
        setOtherGrows(growsData)
        setStrains(strainData)
        if (grow) {
          setFeedProgramId(grow.feedProgramId ?? null)
          // Ein Programm, das keiner Karte entspricht, ist ein eigenes — es
          // gehoert beim Bearbeiten sichtbar ins Freitextfeld, nicht ins Leere.
          const kartenTreffer = (knowledge.programs ?? []).some((program) => program.name === grow.nutrients || program.key === grow.nutrients)
          if (grow.nutrients && !kartenTreffer) setCustomProgram(grow.nutrients)
        }
        if (grow) setForm({ ...emptyForm(), name: grow.name, tentId: grow.tentId, systemId: grow.systemId, setupId: grow.setupId, strain: grow.strain, breeder: grow.breeder, seedType: grow.seedType, startMaterial: grow.startMaterial, hydroStyle: grow.hydroStyle, plantCount: grow.plantCount, reservoirSize: grow.reservoirSize, containerSize: grow.containerSize, light: grow.light, hasChiller: grow.hasChiller, waterSource: grow.waterSource, nutrients: grow.nutrients, startDate: nurDatum(grow.startDate) ?? emptyForm().startDate, entryPoint: grow.entryPoint, daysAlreadyInPhase: grow.daysAlreadyInPhase, autoflowerDaysSinceGermination: grow.autoflowerDaysSinceGermination, flipDate: nurDatum(grow.flipDate), notes: grow.notes, status: grow.status, environment: grow.environment, germinationMethod: grow.germinationMethod, propagationMedium: grow.propagationMedium, cloneSource: grow.cloneSource, cloneIsRooted: grow.cloneIsRooted, phenoNumber: grow.phenoNumber, breederFlowerWeeksMin: grow.breederFlowerWeeksMin, breederFlowerWeeksMax: grow.breederFlowerWeeksMax, plannedVegDays: grow.plannedVegDays, strainId: grow.strainId, setpointProfileId: grow.setpointProfileId ?? null })
      } catch (caught) {
        if (!controller.signal.aborted) setError(formatApiError(caught, 'Grow-Wizard konnte nicht geladen werden.'))
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [growId, isEditing])

  const selectedTent = tents.find((tent) => tent.id === form.tentId) ?? null
  const exactHydro = useMemo(() => hydroSetups.filter((setup) => form.tentId ? setup.tentId === form.tentId : true), [form.tentId, hydroSetups])
  const availableHydro = exactHydro.length > 0 ? exactHydro : hydroSetups
  const selectedHydro = hydroSetups.find((setup) => setup.id === form.systemId) ?? null
  const selectedProgram = programs.find((program) => program.key === feedProgramId)
    ?? programs.find((program) => program.name === form.nutrients || program.key === form.nutrients)
    ?? null

  function patch(value: Partial<GrowUpsertPayload>) { setForm((current) => ({ ...current, ...value })) }
  function selectTent(id: number) { setForm((current) => ({ ...current, tentId: id, systemId: hydroSetups.some((setup) => setup.id === current.systemId && setup.tentId === id) ? current.systemId : null, setupId: null })) }
  function selectHydro(setup: HydroSetupDto) { patch({ systemId: setup.id, setupId: null, hydroStyle: setup.hydroStyle, reservoirSize: formatLiters(setup.totalVolumeLiters ?? setup.reservoirLiters), containerSize: formatLiters(setup.potSizeLiters), hasChiller: setup.hasChiller }) }

  async function saveGrow() {
    // Der Validator lief frueher pro Wizard-Schritt. Auf einer Seite gilt er
    // einmal fuer alles — und meldet den ersten Einwand, statt zu einem Schritt
    // zu springen, den es nicht mehr gibt.
    for (let current = 1; current <= 5; current += 1) {
      const message = validateStep(current, form, selectedHydro)
      if (message) { setError(message); return }
    }
    setSaving(true)
    setError(null)
    try {
      // Die Programmkarte waehlte bisher nur einen NAMEN — jetzt traegt sie auch
      // die Id ins Wissen, und erst die macht den Mischplan moeglich.
      // Faellt der Wissens-Abruf aus, haelt die gespeicherte Id das Programm —
      // null wird nur daraus, wenn der Nutzer wirklich keins gewaehlt hat.
      const payload = { ...form, nutrients: form.nutrients || customProgram || null, setupId: form.setupId ?? null, feedProgramId: selectedProgram?.key ?? feedProgramId }
      const saved = await apiFetch<GrowDetail>(isEditing && growId ? `/api/grows/${growId}` : '/api/grows', { method: isEditing ? 'PUT' : 'POST', body: JSON.stringify(payload) })
      navigate(`/grows/${saved.id}`)
    } catch (caught) {
      setError(formatApiError(caught, 'Grow konnte nicht gespeichert werden.'))
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <V1Page eyebrow="Pflanzen" title={isEditing ? 'Grow bearbeiten' : 'Grow starten'}><V1Skeleton rows={5} label="Lade Formular" /></V1Page>

  // Prüfung und Timeline rechnen bei jeder Eingabe mit — das ist der Grund,
  // warum die sechs Schritte zu einer Seite werden konnten.
  const planInput = {
    plantCount: form.plantCount ?? null,
    // Pflichtfeld mit Regel statt Fehlermeldung: ohne Angabe ist heute Tag 1.
    startDate: form.startDate || new Date().toISOString().slice(0, 10),
    flipDate: form.flipDate ?? null,
    vegDays: null,
    flowerDays: null,
    tent: selectedTent,
    hydro: selectedHydro,
    otherGrows: otherGrows.filter((grow) => grow.id !== Number(growId)),
    programName: selectedProgram?.name ?? (customProgram.trim() || null),
  }
  const timeline = buildTimeline(planInput)
  const findings = checkPlan(planInput)
  const allowed = canCreate(findings)

  return (
    <V1Page eyebrow="Pflanzen" title={isEditing ? 'Grow bearbeiten' : 'Grow starten'} className="grow-wizard-page" action={<Link className="v1-button is-ghost" to={isEditing && growId ? `/grows/${growId}` : '/grows'}>Zurück</Link>}>
      <div className="grow-wizard-mobile-surface" data-audit="grow-wizard">
      {error && <V1Alert message={error} tone="warn" />}
      {/* Eine Seite statt sechs Schritte: links eintragen, rechts sofort sehen.
          Ob die Pflanzenzahl zu den Sites passt oder das Zelt am Starttag belegt
          ist, merkte man vorher erst am Ende — oder gar nicht. */}
      <div className="grow-wizard-shell">
        <div className="grow-wizard-main">
          <RunStep form={form} patch={patch} strains={strains} erfasstePflanzen={erfasstePflanzen} />
          <TentStep tents={tents} selectedId={form.tentId} onSelect={selectTent} />
          <HydroStep setups={availableHydro} exactCount={exactHydro.length} selectedId={form.systemId ?? null} onSelect={selectHydro} tent={selectedTent} />
          <TimeStep form={form} patch={patch} />
          <ProgramStep programs={programs} selected={form.nutrients ?? ''} custom={customProgram} setCustom={setCustomProgram} selectProgram={setFeedProgramId} patch={patch} />
        </div>

        <aside className="grow-wizard-context">
          <GrowPlanPanel
            timeline={timeline}
            findings={findings}
            summary={<Summary form={form} tent={selectedTent} hydro={selectedHydro} program={selectedProgram} custom={customProgram} />}
          />
        </aside>
      </div>

      <div className="v1-form-actions sticky-actions" data-audit="grow-wizard-actions">
        <V1Button variant="ghost" onClick={() => navigate(isEditing && growId ? `/grows/${growId}` : '/grows')}>Abbrechen</V1Button>
        <V1Button
          variant="primary"
          disabled={saving || !allowed}
          onClick={() => void saveGrow()}
        >
          {saving ? 'Speichert...' : isEditing ? 'Speichern' : 'Grow starten'}
        </V1Button>
      </div>
      </div>
    </V1Page>
  )
}

/**
 * Der Kopf des Grows: Name, Sorte, Pflanzenzahl.
 *
 * Die Sorte kommt aus der Bibliothek, statt frei getippt zu werden. Vorher
 * stand hier nur ein Textfeld — die Sorten-Tabelle musste ihre Laeufe deshalb
 * ueber Namensgleichheit suchen, und ein Tippfehler liess einen Lauf aus der
 * Statistik verschwinden. Wer eine Sorte waehlt, uebernimmt automatisch
 * Zuechter und Bluetewochen; „frei eintragen" bleibt fuer alles, was (noch)
 * nicht in der Bibliothek steht.
 */
function RunStep({ form, patch, strains, erfasstePflanzen }: { form: GrowUpsertPayload; patch: (value: Partial<GrowUpsertPayload>) => void; strains: StrainDto[]; erfasstePflanzen: number }) {
  const sorten = [...strains].sort((a, b) => a.name.localeCompare(b.name, 'de'))
  // Was der letzte Bibliotheks-Klick bei den Bluetewochen eingetragen hat.
  // Beim Wechsel auf eine andere Sorte wird nur genau das ersetzt \u2014 sonst
  // stuenden unter Sorte B noch die Wochen von Sorte A, als waeren sie eigene.
  const autofill = useRef<{ min: number | null; max: number | null } | null>(null)

  function waehleSorte(wert: string) {
    if (wert === '') {
      // Freie Eingabe: die Verknuepfung faellt weg, der Text bleibt stehen.
      patch({ strainId: null })
      return
    }
    const sorte = sorten.find((item) => String(item.id) === wert)
    if (!sorte) return
    // Die Bluetewochen treiben den Zeitstrahl \u2014 aus der Bibliothek sind sie
    // verlaesslicher als aus dem Gedaechtnis. Eigene Angaben bleiben stehen;
    // nur leere Felder und der Autofill der vorigen Sorte werden gefuellt.
    const min = form.breederFlowerWeeksMin == null || form.breederFlowerWeeksMin === autofill.current?.min
      ? sorte.flowerWeeksMin
      : form.breederFlowerWeeksMin
    const max = form.breederFlowerWeeksMax == null || form.breederFlowerWeeksMax === autofill.current?.max
      ? sorte.flowerWeeksMax
      : form.breederFlowerWeeksMax
    autofill.current = { min: sorte.flowerWeeksMin ?? null, max: sorte.flowerWeeksMax ?? null }
    patch({
      strainId: sorte.id,
      strain: sorte.name,
      breeder: sorte.breeder ?? form.breeder,
      breederFlowerWeeksMin: min,
      breederFlowerWeeksMax: max,
    })
  }

  return (
    <V1Section title="Run">
      <div className="v1-form-grid grow-form-grid">
        <V1Field label="Grow-Name" wide>
          <input value={form.name} onChange={(event) => patch({ name: event.target.value })} placeholder="Purple Lemonade RDWC" />
        </V1Field>

        <V1Field label="Sorte" hint={sorten.length === 0 ? 'Noch keine Sorte in der Bibliothek \u2014 unter „Sorten & Pheno" anlegen.' : 'Aus der Bibliothek: z\u00e4hlt sp\u00e4ter in Runs und \u00d8-Ertrag mit. Mehrere Sorten im Zelt? Leg den Grow an und trag danach unter \u201ePflanzen & Sorten\u201c jede Pflanze mit ihrer eigenen Sorte und ihrem Topf ein.'}>
          <select value={form.strainId != null ? String(form.strainId) : ''} onChange={(event) => waehleSorte(event.target.value)}>
            <option value="">— frei eintragen —</option>
            {sorten.map((sorte) => (
              <option key={sorte.id} value={sorte.id}>{sorte.name}{sorte.breeder ? ` \u00b7 ${sorte.breeder}` : ''}</option>
            ))}
          </select>
        </V1Field>

        {form.strainId == null && (
          <V1Field label="Sorte (frei)">
            <input value={form.strain ?? ''} onChange={(event) => patch({ strain: event.target.value })} placeholder="z. B. Purple Lemonade" />
          </V1Field>
        )}

        <V1Field label="Züchter">
          <input value={form.breeder ?? ''} onChange={(event) => patch({ breeder: event.target.value })} />
        </V1Field>

        {/* Sind Pflanzen EINZELN erfasst, sind sie die Wahrheit ueber die Zahl
            — der Server zieht plantCount seit dem 25.08.2026 nach. Ein
            beschreibbares Feld daneben behauptete etwas anderes: eingetragen,
            gespeichert, danach stand wieder die alte Zahl da. Gefunden von
            e2e/formularfelder-kommen-an.spec.ts. */}
        <V1Field
          label="Pflanzen"
          hint={erfasstePflanzen > 0
            ? `${erfasstePflanzen} einzeln erfasst — die Zahl folgt der Karte „Pflanzen & Sorten" am Grow.`
            : undefined}
        >
          <input
            type="number"
            min="1"
            value={erfasstePflanzen > 0 ? erfasstePflanzen : form.plantCount ?? ''}
            readOnly={erfasstePflanzen > 0}
            onChange={(event) => patch({ plantCount: toNullableInt(event.target.value) })}
          />
        </V1Field>

        <V1Field label="Samen-Typ">
          <select value={form.seedType} onChange={(event) => patch({ seedType: event.target.value as SeedType })}>
            {seedTypes.map((value) => <option key={value} value={value}>{samenName(value)}</option>)}
          </select>
        </V1Field>

        <V1Field label="Startmaterial">
          <select value={form.startMaterial} onChange={(event) => patch({ startMaterial: event.target.value as StartMaterial, entryPoint: (event.target.value === 'Clone' ? 'Veg' : form.entryPoint) as GrowEntryPoint })}>
            {startMaterials.map((value) => <option key={value} value={value}>{materialName(value)}</option>)}
          </select>
        </V1Field>
      </div>
    </V1Section>
  )
}


function TentStep({ tents, selectedId, onSelect }: { tents: TentDto[]; selectedId: number | null; onSelect: (id: number) => void }) {
  if (tents.length === 0) return <V1Empty title="Kein Zelt angelegt" action={<V1LinkButton to="/zelte/new" variant="primary">Zelt anlegen</V1LinkButton>} />
  return <V1Section title="Zelt"><div className="grow-select-grid">{tents.map((tent) => <button type="button" key={tent.id} className={classNames('grow-select-card', selectedId === tent.id && 'active')} onClick={() => onSelect(tent.id)}><span className="grow-card-topline"><strong>{tent.name}</strong><V1Badge tone={tent.status === 'Active' ? 'ok' : 'neutral'}>{tent.status === 'Active' ? 'aktiv' : 'archiviert'}</V1Badge></span><span className="grow-card-meta">{zeltZweckName(tent.tentType)} · {formatTentSize(tent)}</span><span className="grow-card-facts"><b>{tent.activeGrowCount} {tent.activeGrowCount === 1 ? 'Grow' : 'Grows'}</b><b>{tent.activeSetupCount} {tent.activeSetupCount === 1 ? 'Setup' : 'Setups'}</b></span></button>)}</div></V1Section>
}

function HydroStep({ setups, exactCount, selectedId, onSelect, tent }: { setups: HydroSetupDto[]; exactCount: number; selectedId: number | null; onSelect: (setup: HydroSetupDto) => void; tent: TentDto | null }) {
  if (setups.length === 0) return <V1Empty title="Kein Hydro-Setup vorhanden" text="Lege zuerst ein DWC/RDWC-System an." action={<V1LinkButton to="/hydro/new" variant="primary">Hydro anlegen</V1LinkButton>} />
  return <V1Section title="Hydro">{tent && exactCount === 0 && <V1Alert title="Kein Setup direkt am Zelt" message="Es gibt aktive Hydro-Setups, aber keines ist diesem Zelt zugeordnet. Du kannst eines wählen oder zuerst die Zeltzuordnung im Hydro-Setup korrigieren." tone="warn" />}<div className="grow-select-grid">{setups.map((setup) => <button type="button" key={setup.id} className={classNames('grow-select-card', selectedId === setup.id && 'active')} onClick={() => onSelect(setup)}><span className="grow-card-topline"><strong>{setup.name}</strong><V1Badge tone="accent">{setup.hydroStyle}</V1Badge></span><span className="grow-card-meta">{setup.tentName ?? 'ohne Zelt'} · {aufstellungName(setup.layoutType)}</span><span className="grow-card-facts"><b>{setup.potCount ?? 1} Sites</b><b>{formatLiters(setup.totalVolumeLiters)}</b><b>{setup.hasChiller ? 'Chiller' : 'ohne Chiller'}</b></span></button>)}</div></V1Section>
}

function TimeStep({ form, patch }: { form: GrowUpsertPayload; patch: (value: Partial<GrowUpsertPayload>) => void }) {
  return <V1Section title="Zeit"><div className="v1-form-grid grow-form-grid"><V1Field label="Startdatum *" hint="Tag 1 des Grows — daran haengen Phasen, Tageszaehlung und Zeitstrahl. Leer heisst heute.">
    <input type="date" required value={form.startDate} onChange={(event) => patch({ startDate: event.target.value })} />
  </V1Field><V1Field label="Startpunkt"><select value={form.entryPoint} onChange={(event) => patch({ entryPoint: event.target.value as GrowEntryPoint })}>{entryPoints.map((value) => <option key={value} value={value}>{einstiegName(value)}</option>)}</select></V1Field>{/* Nur anbieten, wo es auch ankommt: der Server nimmt „Tage in Phase" nur
    ausserhalb der Keimung und nur fuer Nicht-Autoflower an
    (GrowFormViewModel.NeedsDaysInPhase). Vorher stand das Feld immer da
    und wurde still verworfen — dieselbe Bauart wie beim Flipdatum, gefunden
    von e2e/formularfelder-kommen-an.spec.ts. */}
{form.entryPoint !== 'Germination' && form.seedType !== 'Autoflower' && (
  <V1Field label="Tage in Phase"><input type="number" min="0" value={form.daysAlreadyInPhase ?? ''} onChange={(event) => patch({ daysAlreadyInPhase: toNullableInt(event.target.value) })} /></V1Field>
)}
{/* Eine Autoflower hat keine Phasen-Tage, sondern ein Alter seit der
    Keimung — der Server kennt das Feld laengst, das Formular bot es nie an.
    Ohne diese Zeile konnte ein Autoflower-Grower sein Alter nirgends
    eintragen. */}
{form.seedType === 'Autoflower' && (
  <V1Field label="Tage seit Keimung" hint="Bei Autoflowern zählt das Alter, nicht die Phase.">
    <input type="number" min="0" value={form.autoflowerDaysSinceGermination ?? ''} onChange={(event) => patch({ autoflowerDaysSinceGermination: toNullableInt(event.target.value) })} />
  </V1Field>
)}{form.seedType !== 'Autoflower' && (
    <V1Field label="Veg-Dauer geplant (Tage)" hint={vegHinweis(form)}>
      <input
        type="number" min="1" max="365"
        value={form.plannedVegDays ?? ''}
        placeholder="z. B. 28"
        onChange={(event) => patch({ plannedVegDays: toNullableInt(event.target.value) })}
      />
    </V1Field>
  )}{form.seedType !== 'Autoflower' && <V1Field label="Flipdatum" hint="Erst ausfüllen, wenn wirklich geflippt wurde."><input type="date" value={form.flipDate ?? ''} onChange={(event) => patch({ flipDate: event.target.value })} /></V1Field>}<V1Field label="Status"><select value={form.status} onChange={(event) => patch({ status: event.target.value as GrowStatus })}>{statuses.map((value) => <option key={value} value={value}>{statusName(value)}</option>)}</select></V1Field>
    {/* Sollwerte sind, wie man DIESEN Lauf faehrt. Steht hier nichts, gilt das
        Profil des Hydro-Systems — das sagt der Hinweis auch. */}
    <ProfileSelect
      value={form.setpointProfileId ?? null}
      onChange={(value) => patch({ setpointProfileId: value })}
      inheritedLabel="Profil des Hydro-Systems"
      hint="Nur setzen, wenn dieser Lauf anders laufen soll als der Rest im selben System."
    /></div></V1Section>
}

/**
 * Der Termin, der sich aus der geplanten Veg-Dauer ergibt.
 *
 * Ohne ihn muesste man im Kopf rechnen -- und genau diese Rechnung macht der
 * Zeitstrahl spaeter auch. Sie hier zu zeigen ist die Probe darauf.
 */
function vegHinweis(form: GrowUpsertPayload): string {
  if (form.plannedVegDays == null || form.plannedVegDays <= 0) {
    return 'Leer lassen, wenn du nach Augenmass flippst \u2014 dann zeigt der Zeitstrahl keinen Termin.'
  }
  const start = new Date(form.startDate)
  if (Number.isNaN(start.getTime())) return 'Flip nach dieser Dauer ab Bewurzelung.'
  const flip = new Date(start.getTime() + form.plannedVegDays * 86_400_000)
  const datum = new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(flip)
  return `Flip am ${datum}, wenn ab Start gerechnet wird \u2014 mit Bewurzelungsdatum entsprechend spaeter.`
}

function ProgramStep({ programs, selected, custom, setCustom, selectProgram, patch }: { programs: NutrientProgramDto[]; selected: string; custom: string; setCustom: (value: string) => void; selectProgram: (key: string | null) => void; patch: (value: Partial<GrowUpsertPayload>) => void }) {
  return <V1Section title="Programm"><div className="program-grid">{programs.map((program) => <button key={program.key} type="button" className={classNames('program-card', (selected === program.name || selected === program.key) && 'active')} onClick={() => { setCustom(''); selectProgram(program.key); patch({ nutrients: program.name }) }}><span className="grow-card-topline"><strong>{program.name}</strong><V1Badge tone="accent">{program.manufacturer}</V1Badge></span><span className="program-summary">{program.summary}</span></button>)}</div><div className="grow-custom-program"><V1Field label="Eigenes Programm"><input value={custom} onChange={(event) => { setCustom(event.target.value); selectProgram(null); patch({ nutrients: event.target.value || null }) }} placeholder="Eigene Mischung" /></V1Field></div></V1Section>
}

function Summary({ form, tent, hydro, program, custom }: { form: GrowUpsertPayload; tent: TentDto | null; hydro: HydroSetupDto | null; program: NutrientProgramDto | null; custom: string }) {
  return <V1Card className="grow-summary-card"><span className="v1-card-kicker">Grow-Basis</span><h2>{form.name || 'Neuer Grow'}</h2><div className="grow-summary-list"><span><b>Zelt</b>{tent?.name ?? 'offen'}</span><span><b>Hydro</b>{hydro?.name ?? 'offen'}</span><span><b>Programm</b>{program?.name || custom || form.nutrients || 'offen'}</span></div></V1Card>
}

function formatTentSize(tent: TentDto) { return !tent.widthCm && !tent.depthCm && !tent.tentHeightCm ? 'Größe offen' : `${tent.widthCm ?? '–'}×${tent.depthCm ?? '–'}×${tent.tentHeightCm ?? '–'} cm` }
function validateStep(step: number, form: GrowUpsertPayload, hydro: HydroSetupDto | null) { if (step === 1 && !form.name.trim()) return 'Bitte Grow-Namen eingeben.'; if (step === 2 && !form.tentId) return 'Bitte Zelt wählen.'; if (step === 3 && !hydro) return 'Bitte Hydro-Setup wählen.'; return null }

export default GrowSetupPage
