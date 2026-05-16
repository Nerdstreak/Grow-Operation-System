import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type { KnowledgeOverviewDto, NutrientProgramDto, WearTemplateDto } from '../types'
import { V1Alert, V1Badge, V1Card, V1Empty, V1Field, V1LinkButton, V1Page, V1Section, V1Tabs } from '../components/v1'

type KnowledgeCategory = 'guide' | 'programs' | 'sops' | 'treatments' | 'symptoms' | 'setpoints' | 'pathogens' | 'wear'
type DataCategory = Exclude<KnowledgeCategory, 'guide'>
type KnowledgeRecord = Record<string, unknown>
type KnowledgeItem = KnowledgeRecord | WearTemplateDto | NutrientProgramDto

interface CategoryDefinition {
  key: DataCategory
  label: string
  endpoint: string
  purpose: string
}

interface KnowledgeCatalogs {
  treatments: KnowledgeRecord[]
  sops: KnowledgeRecord[]
  symptoms: KnowledgeRecord[]
  wear: WearTemplateDto[]
  programs: NutrientProgramDto[]
  setpoints: KnowledgeRecord[]
  pathogens: KnowledgeRecord[]
}

const categories: CategoryDefinition[] = [
  { key: 'programs', label: 'Programme', endpoint: '/api/knowledge', purpose: 'Athena, Canna Aqua und weitere Nährstofflinien' },
  { key: 'sops', label: 'SOPs', endpoint: '/api/knowledge/sops', purpose: 'Geführte Abläufe für Kalibrierung, Hygiene und Behandlung' },
  { key: 'treatments', label: 'Treatments', endpoint: '/api/knowledge/treatments', purpose: 'Maßnahmen gegen Symptome und Risiken' },
  { key: 'symptoms', label: 'Symptome', endpoint: '/api/knowledge/symptoms', purpose: 'Symptom → Ursache → Check → Behandlung' },
  { key: 'setpoints', label: 'Setpoints', endpoint: '/api/knowledge/setpoints', purpose: 'Zielbereiche nach System, Phase und Programm' },
  { key: 'pathogens', label: 'Pathogene', endpoint: '/api/knowledge/pathogens', purpose: 'Risiken, Root Rot, Prävention und Behandlung' },
  { key: 'wear', label: 'Verschleiß', endpoint: '/api/knowledge/wear', purpose: 'Sensoren, Hardware, Wartung und Austausch' },
]

const emptyCatalogs: KnowledgeCatalogs = {
  treatments: [],
  sops: [],
  symptoms: [],
  wear: [],
  programs: [],
  setpoints: [],
  pathogens: [],
}

const emptyOverview: KnowledgeOverviewDto = { programs: [], playbooks: [] }

function KnowledgePage() {
  const [activeCategory, setActiveCategory] = useState<KnowledgeCategory>('guide')
  const [searchQuery, setSearchQuery] = useState('')
  const [catalogs, setCatalogs] = useState<KnowledgeCatalogs>(emptyCatalogs)
  const [catalogErrors, setCatalogErrors] = useState<Partial<Record<DataCategory, string>>>({})
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      const nextErrors: Partial<Record<DataCategory, string>> = {}

      async function fetchCatalog<T>(category: DataCategory, endpoint: string, fallback: T): Promise<T> {
        try {
          return await apiFetch<T>(endpoint, { signal: controller.signal })
        } catch (caught) {
          if (controller.signal.aborted) throw caught
          nextErrors[category] = caught instanceof ApiRequestError ? caught.message : 'Catalog konnte nicht geladen werden.'
          return fallback
        }
      }

      try {
        const [overview, treatments, sops, symptoms, wear, setpoints, pathogens] = await Promise.all([
          fetchCatalog<KnowledgeOverviewDto>('programs', '/api/knowledge', emptyOverview),
          fetchCatalog<KnowledgeRecord[]>('treatments', '/api/knowledge/treatments', []),
          fetchCatalog<KnowledgeRecord[]>('sops', '/api/knowledge/sops', []),
          fetchCatalog<KnowledgeRecord[]>('symptoms', '/api/knowledge/symptoms', []),
          fetchCatalog<WearTemplateDto[]>('wear', '/api/knowledge/wear', []),
          fetchCatalog<KnowledgeRecord[]>('setpoints', '/api/knowledge/setpoints', []),
          fetchCatalog<KnowledgeRecord[]>('pathogens', '/api/knowledge/pathogens', []),
        ])

        if (controller.signal.aborted) return
        setCatalogs({ programs: overview.programs, treatments, sops, symptoms, wear, setpoints, pathogens })
        setCatalogErrors(nextErrors)
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  const counts = useMemo(() => ({
    programs: catalogs.programs.length,
    sops: catalogs.sops.length,
    treatments: catalogs.treatments.length,
    symptoms: catalogs.symptoms.length,
    setpoints: catalogs.setpoints.length,
    pathogens: catalogs.pathogens.length,
    wear: catalogs.wear.length,
  }), [catalogs])

  const activeItems = activeCategory === 'guide' ? [] : catalogs[activeCategory] as KnowledgeItem[]
  const filteredItems = useMemo(() => activeItems.filter((item) => matchesSearch(item, searchQuery)), [activeItems, searchQuery])
  const activeDefinition = activeCategory === 'guide' ? null : categories.find((category) => category.key === activeCategory)

  return (
    <V1Page eyebrow="Wissen" title="Wissen & SOPs" subtitle="Nicht als Datenbank-Browser gedacht: Die Seite zeigt dir zuerst, wofür die Wissensbasis im Grow-Alltag genutzt wird.">
      <section className="v1-kpi-grid">
        <V1Card><span className="v1-card-kicker">Programme</span><h2>{counts.programs}</h2><p>Athena, Canna Aqua und Nährstofflinien</p></V1Card>
        <V1Card><span className="v1-card-kicker">SOPs</span><h2>{counts.sops}</h2><p>Arbeitsabläufe für Behandlung und Wartung</p></V1Card>
        <V1Card><span className="v1-card-kicker">Symptome</span><h2>{counts.symptoms}</h2><p>Diagnosebasis für Empfehlungen</p></V1Card>
        <V1Card><span className="v1-card-kicker">Setpoints</span><h2>{counts.setpoints}</h2><p>Zielbereiche für Score und Addback</p></V1Card>
      </section>

      <V1Tabs
        label="Wissensbereich"
        active={activeCategory}
        onChange={(value) => setActiveCategory(value)}
        items={[
          { value: 'guide', label: 'Empfehlungen', meta: 'Workflow' },
          ...categories.map((category) => ({ value: category.key, label: category.label, meta: String(counts[category.key]) })),
        ]}
      />

      {activeCategory === 'guide' ? (
        <KnowledgeGuide catalogs={catalogs} />
      ) : (
        <>
          <V1Section title={activeDefinition?.label ?? 'Catalog'}>
            <div className="v1-card-grid">
              <V1Card>
                <span className="v1-card-kicker">Zweck</span>
                <h2>{activeDefinition?.label}</h2>
                <p>{activeDefinition?.purpose}</p>
                <small>{activeDefinition?.endpoint}</small>
              </V1Card>
              <V1Card>
                <span className="v1-card-kicker">Filter</span>
                <V1Field label="Suche" hint="ID, Name, Kategorie oder Hersteller">
                  <input type="search" value={searchQuery} onChange={(event) => setSearchQuery(event.target.value)} placeholder="z. B. Athena, Root Rot, pH..." />
                </V1Field>
              </V1Card>
            </div>
            {catalogErrors[activeCategory] && <V1Alert title="Fehler" message={catalogErrors[activeCategory] ?? ''} tone="warn" />}
          </V1Section>

          {loading ? (
            <V1Empty title="Lade Wissensbasis..." />
          ) : filteredItems.length === 0 ? (
            <V1Empty title="Keine Einträge gefunden" text="Filter zurücksetzen oder andere Kategorie wählen." />
          ) : (
            <div className="v1-card-grid">
              {filteredItems.map((item, index) => (
                <KnowledgeCard key={`${activeCategory}-${getItemKey(item, index)}`} category={activeCategory} item={item} />
              ))}
            </div>
          )}
        </>
      )}
    </V1Page>
  )
}

function KnowledgeGuide({ catalogs }: { catalogs: KnowledgeCatalogs }) {
  const rootRotSop = findFirst(catalogs.sops, ['root', 'rot', 'wurzel'])
  const phSop = findFirst(catalogs.sops, ['ph', 'nährstoff', 'naehrstoff'])
  const addbackTreatment = findFirst(catalogs.treatments, ['addback', 'ec', 'reservoir'])
  const athena = catalogs.programs.find((program) => /athena/i.test(`${program.name} ${program.manufacturer}`))
  const canna = catalogs.programs.find((program) => /canna/i.test(`${program.name} ${program.manufacturer}`))

  return (
    <>
      <V1Section title="Geführte Nutzung">
        <div className="v1-card-grid">
          <GuideCard title="Grow starten" kicker="Programm wählen" text="Beim Grow-Start wird das Nährstoffprogramm gespeichert. Darauf bauen Addback, Setpoints und spätere Empfehlungen auf." to="/grows/new" action="Grow starten" />
          <GuideCard title="Addback" kicker="RDWC/DWC Alltag" text="Der Addback-Assistent nutzt Grow, Hydro-System, Reservoirvolumen und Programm als Grundlage." to="/addback" action="Addback öffnen" />
          <GuideCard title="Sensorvertrauen" kicker="Kalibrierung" text="SOPs und Wear-Templates erklären, wann pH/EC/ORP/DO-Sonden geprüft oder kalibriert werden müssen." to="/hardware" action="Sensoren öffnen" />
          <GuideCard title="Live Score" kicker="Setpoints" text="Die Live-Bewertung bleibt konservativ: Ohne Sensorwerte kein falsches Stabil. Mit Setpoints wird der Score später phasenfeiner." to="/" action="Live öffnen" />
        </div>
      </V1Section>

      <V1Section title="Schnelle Empfehlungen">
        <div className="v1-card-grid">
          <RecommendationCard title="pH/EC schwankt" item={phSop} fallback="SOP für pH- und Nährstoff-Stabilisierung prüfen." />
          <RecommendationCard title="Root Rot Risiko" item={rootRotSop} fallback="Root-Rot-Behandlung/SOP prüfen und Hygiene kontrollieren." />
          <RecommendationCard title="Addback unklar" item={addbackTreatment} fallback="Addback-Prinzip, Ziel-EC und Nachmessung prüfen." />
          <V1Card>
            <span className="v1-card-kicker">Programme</span>
            <h2>{[athena?.name, canna?.name].filter(Boolean).join(' / ') || 'Nährstoffprogramm wählen'}</h2>
            <p>Programme sind nicht nur Text. Sie werden im Grow gespeichert und dienen als Kontext für spätere Handlungsempfehlungen.</p>
            <div className="v1-action-row">
              <V1ButtonLike onClick={() => null}>Athena</V1ButtonLike>
              <V1ButtonLike onClick={() => null}>Canna Aqua</V1ButtonLike>
            </div>
          </V1Card>
        </div>
      </V1Section>

      <V1Section title="Datenquellen">
        <div className="v1-card-grid">
          <SourceSummary label="Programme" count={catalogs.programs.length} />
          <SourceSummary label="SOPs" count={catalogs.sops.length} />
          <SourceSummary label="Treatments" count={catalogs.treatments.length} />
          <SourceSummary label="Symptome" count={catalogs.symptoms.length} />
          <SourceSummary label="Setpoints" count={catalogs.setpoints.length} />
          <SourceSummary label="Pathogene" count={catalogs.pathogens.length} />
          <SourceSummary label="Verschleiß" count={catalogs.wear.length} />
        </div>
      </V1Section>
    </>
  )
}

function GuideCard({ title, kicker, text, to, action }: { title: string; kicker: string; text: string; to: string; action: string }) {
  return (
    <V1Card>
      <span className="v1-card-kicker">{kicker}</span>
      <h2>{title}</h2>
      <p>{text}</p>
      <V1LinkButton to={to} variant="primary">{action}</V1LinkButton>
    </V1Card>
  )
}

function RecommendationCard({ title, item, fallback }: { title: string; item: KnowledgeRecord | null; fallback: string }) {
  return (
    <V1Card>
      <span className="v1-card-kicker">Empfehlung</span>
      <h2>{title}</h2>
      <p>{item ? getTitle(item) : fallback}</p>
      {item && <small>{getString(item, 'id', 'key') ?? 'Knowledge'}</small>}
    </V1Card>
  )
}

function SourceSummary({ label, count }: { label: string; count: number }) {
  return <V1Card><span className="v1-card-kicker">{label}</span><h2>{count}</h2><p>{count === 0 ? 'Noch keine Daten geladen.' : 'Einträge verfügbar.'}</p></V1Card>
}

function V1ButtonLike({ children }: { children: ReactNode; onClick: () => void }) {
  return <span className="v1-badge tone-neutral">{children}</span>
}

function KnowledgeCard({ category, item }: { category: DataCategory; item: KnowledgeItem }) {
  if (category === 'programs') return <ProgramCard item={item as NutrientProgramDto} />
  if (category === 'wear') return <WearCard item={item as WearTemplateDto} />
  if (category === 'treatments') return <TreatmentCard item={item as KnowledgeRecord} />
  if (category === 'sops') return <SopCard item={item as KnowledgeRecord} />
  if (category === 'symptoms') return <SymptomCard item={item as KnowledgeRecord} />
  if (category === 'setpoints') return <SetpointCard item={item as KnowledgeRecord} />
  return <PathogenCard item={item as KnowledgeRecord} />
}

function TreatmentCard({ item }: { item: KnowledgeRecord }) {
  return (
    <BaseCard item={item} subtitle={getString(item, 'type')}>
      <FieldList label="Zielsymptome" values={getStringArray(item, 'targetSymptoms')} />
      <FieldList label="Hardware" values={getStringArray(item, 'hardwareRequirements')} />
      <RecordList label="Konflikte" values={getRecordArray(item, 'conflicts')} keys={['treatmentId', 'reason']} />
      <SourceList item={item} />
    </BaseCard>
  )
}

function SopCard({ item }: { item: KnowledgeRecord }) {
  const steps = getRecordArray(item, 'steps')
  return (
    <BaseCard item={item} subtitle={getString(item, 'type')}>
      <MetaRow entries={[formatNumberLabel(getNumber(item, 'estimatedDurationMinutes'), 'min'), formatNumberLabel(getNumber(item, 'durationDays'), 'Tage Dauer'), formatNumberLabel(getNumber(item, 'intervalDays'), 'Tage Intervall'), `${steps.length} Schritte`]} />
      <RecordList label="Trigger" values={getRecordArray(item, 'triggers')} keys={['type', 'intervalDays', 'warningAfterDays', 'criticalAfterDays']} />
      <FieldList label="Material" values={getStringArray(item, 'requiredMaterials')} />
      <SourceList item={item} />
    </BaseCard>
  )
}

function SymptomCard({ item }: { item: KnowledgeRecord }) {
  return (
    <BaseCard item={item} subtitle={getString(item, 'category')}>
      <FieldList label="Treatments" values={getStringArray(item, 'suggestedTreatmentIds')} />
      <FieldList label="SOPs" values={getStringArray(item, 'suggestedSopIds')} />
      <FieldList label="Mögliche Ursachen" values={getStringArray(item, 'possibleCauses')} />
      <FieldList label="Checks" values={getStringArray(item, 'diagnosticChecks')} />
    </BaseCard>
  )
}

function WearCard({ item }: { item: WearTemplateDto }) {
  return (
    <BaseCard item={item as unknown as KnowledgeRecord} subtitle={item.category}>
      <MetaRow entries={[formatNumberLabel(item.expectedLifespanDays, 'Tage Lebensdauer'), formatNumberLabel(item.inspectionIntervalDays, 'Tage Inspektion')]} />
      <FieldList label="Austausch-Trigger" values={item.replacementTriggers} />
    </BaseCard>
  )
}

function ProgramCard({ item }: { item: NutrientProgramDto }) {
  return (
    <BaseCard item={{ id: item.key, name: item.name }} subtitle={`${item.manufacturer} · ${item.category}`}>
      {item.summary && <p>{item.summary}</p>}
      <MetaRow entries={[`${item.stages.length} Phasen`, item.bestFor || null]} />
      <FieldList label="Guidance" values={[item.phGuidance, item.ecGuidance, item.waterGuidance].filter(Boolean)} />
      <FieldList label="Tipps" values={item.tips.slice(0, 4)} />
    </BaseCard>
  )
}

function SetpointCard({ item }: { item: KnowledgeRecord }) {
  const stages = getRecord(item, 'stages')
  const stageEntries = stages ? Object.entries(stages) : []
  return (
    <BaseCard item={item} subtitle={getString(item, 'systemType')}>
      <MetaRow entries={[getString(item, 'programKey'), `${stageEntries.length} Phasen`]} />
      {stageEntries.slice(0, 4).map(([stageName, values]) => <p key={stageName}><strong>{stageName}:</strong> {formatCompact(values)}</p>)}
    </BaseCard>
  )
}

function PathogenCard({ item }: { item: KnowledgeRecord }) {
  return (
    <BaseCard item={item} subtitle={[getString(item, 'category'), getString(item, 'riskLevel')].filter(Boolean).join(' · ')}>
      {getString(item, 'scientificName') && <p>{getString(item, 'scientificName')}</p>}
      <FieldList label="Symptome" values={getStringArray(item, 'symptoms')} />
      <FieldList label="SOPs" values={compactStrings([getString(item, 'treatmentSopId'), getString(item, 'preventiveSopId')])} />
      {getString(item, 'notes') && <p>{getString(item, 'notes')}</p>}
      <SourceList item={item} />
    </BaseCard>
  )
}

function BaseCard({ item, subtitle, children }: { item: KnowledgeRecord; subtitle: string | null; children: ReactNode }) {
  return (
    <V1Card>
      <span className="v1-card-kicker">{getString(item, 'id', 'key') ?? 'Knowledge'}</span>
      <h2>{getTitle(item)}</h2>
      {subtitle && <V1Badge>{subtitle}</V1Badge>}
      <div style={{ display: 'grid', gap: 10, marginTop: 10 }}>{children}</div>
    </V1Card>
  )
}

function MetaRow({ entries }: { entries: Array<string | null> }) {
  const values = entries.filter((entry): entry is string => Boolean(entry))
  if (values.length === 0) return null
  return <div className="v1-action-row">{values.map((entry) => <V1Badge key={entry}>{entry}</V1Badge>)}</div>
}

function FieldList({ label, values }: { label: string; values: string[] }) {
  const visible = values.filter(Boolean).slice(0, 8)
  if (visible.length === 0) return null
  return <div><strong>{label}</strong><div className="v1-action-row" style={{ marginTop: 6 }}>{visible.map((value) => <V1Badge key={value}>{value}</V1Badge>)}</div></div>
}

function RecordList({ label, values, keys }: { label: string; values: KnowledgeRecord[]; keys: string[] }) {
  if (values.length === 0) return null
  return <div><strong>{label}</strong>{values.slice(0, 4).map((value, index) => <p key={`${label}-${index}`}>{keys.map((key) => getString(value, key)).filter(Boolean).join(' · ') || formatCompact(value)}</p>)}</div>
}

function SourceList({ item }: { item: KnowledgeRecord }) {
  const sources = getRecordArray(item, 'sources')
  if (sources.length === 0) return null
  return <RecordList label="Quellen" values={sources} keys={['title', 'reference', 'url', 'credibility']} />
}

function matchesSearch(item: KnowledgeItem, query: string) {
  const normalized = query.trim().toLowerCase()
  if (!normalized) return true
  const haystack = [getString(item, 'id', 'key'), getString(item, 'name', 'title'), getString(item, 'category', 'type', 'manufacturer', 'systemType', 'riskLevel')].filter(Boolean).join(' ').toLowerCase()
  return haystack.includes(normalized)
}

function getItemKey(item: KnowledgeItem, index: number) {
  return getString(item, 'id', 'key') ?? index.toString()
}

function getTitle(item: KnowledgeRecord) {
  return getString(item, 'name', 'title') ?? getString(item, 'id', 'key') ?? 'Unbenannt'
}

function findFirst(items: KnowledgeRecord[], terms: string[]) {
  return items.find((item) => terms.some((term) => JSON.stringify(item).toLowerCase().includes(term))) ?? null
}

function getField(item: unknown, ...keys: string[]) {
  if (!isRecord(item)) return null
  for (const key of keys) if (item[key] !== undefined && item[key] !== null) return item[key]
  return null
}

function getString(item: unknown, ...keys: string[]) {
  const value = getField(item, ...keys)
  if (typeof value === 'string') return value
  if (typeof value === 'number' || typeof value === 'boolean') return String(value)
  return null
}

function getNumber(item: unknown, ...keys: string[]) {
  const value = getField(item, ...keys)
  return typeof value === 'number' ? value : null
}

function getStringArray(item: unknown, ...keys: string[]) {
  const value = getField(item, ...keys)
  if (Array.isArray(value)) return value.map((entry) => String(entry)).filter(Boolean)
  if (typeof value === 'string' && value) return [value]
  return []
}

function getRecord(item: unknown, ...keys: string[]) {
  const value = getField(item, ...keys)
  return isRecord(value) ? value : null
}

function getRecordArray(item: unknown, ...keys: string[]) {
  const value = getField(item, ...keys)
  return Array.isArray(value) ? value.filter(isRecord) : []
}

function compactStrings(values: Array<string | null>) {
  return values.filter((value): value is string => Boolean(value))
}

function formatCompact(value: unknown) {
  if (value === null || value === undefined) return '–'
  if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') return String(value)
  try { return JSON.stringify(value) } catch { return String(value) }
}

function formatNumberLabel(value: number | null | undefined, label: string) {
  return value == null ? null : `${value} ${label}`
}

function isRecord(value: unknown): value is KnowledgeRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

export default KnowledgePage
