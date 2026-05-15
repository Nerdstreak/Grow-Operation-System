import { useEffect, useMemo, useState } from 'react'
import { apiFetch, ApiRequestError } from '../api'
import type { KnowledgeOverviewDto, NutrientProgramDto, WearTemplateDto } from '../types'

type KnowledgeCategory = 'treatments' | 'sops' | 'symptoms' | 'wear' | 'programs' | 'setpoints' | 'pathogens'
type KnowledgeRecord = Record<string, unknown>
type KnowledgeItem = KnowledgeRecord | WearTemplateDto | NutrientProgramDto

interface CategoryDefinition {
  key: KnowledgeCategory
  label: string
  endpoint: string
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
  { key: 'treatments', label: 'Treatments', endpoint: '/api/knowledge/treatments' },
  { key: 'sops', label: 'SOPs', endpoint: '/api/knowledge/sops' },
  { key: 'symptoms', label: 'Symptoms', endpoint: '/api/knowledge/symptoms' },
  { key: 'wear', label: 'Wear', endpoint: '/api/knowledge/wear' },
  { key: 'programs', label: 'Programs', endpoint: '/api/knowledge' },
  { key: 'setpoints', label: 'Setpoints', endpoint: '/api/knowledge/setpoints' },
  { key: 'pathogens', label: 'Pathogens', endpoint: '/api/knowledge/pathogens' },
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

const emptyOverview: KnowledgeOverviewDto = {
  programs: [],
  playbooks: [],
}

function KnowledgePage() {
  const [activeCategory, setActiveCategory] = useState<KnowledgeCategory>('treatments')
  const [searchQuery, setSearchQuery] = useState('')
  const [catalogs, setCatalogs] = useState<KnowledgeCatalogs>(emptyCatalogs)
  const [catalogErrors, setCatalogErrors] = useState<Partial<Record<KnowledgeCategory, string>>>({})
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      const nextErrors: Partial<Record<KnowledgeCategory, string>> = {}

      async function fetchCatalog<T>(category: KnowledgeCategory, endpoint: string, fallback: T): Promise<T> {
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
        setCatalogs({
          programs: overview.programs,
          treatments,
          sops,
          symptoms,
          wear,
          setpoints,
          pathogens,
        })
        setCatalogErrors(nextErrors)
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  const activeItems = catalogs[activeCategory] as KnowledgeItem[]
  const filteredItems = useMemo(
    () => activeItems.filter((item) => matchesSearch(item, searchQuery)),
    [activeItems, searchQuery],
  )
  const activeDefinition = categories.find((category) => category.key === activeCategory)

  return (
    <>
      <div className="topbar">
        <span className="topbar-title">Wissen</span>
      </div>

      <div className="page-scroll">
        <div className="grow-hero" style={{ marginBottom: 16 }}>
          <h1 className="grow-hero-title">Knowledge-Browser</h1>
          <div className="grow-hero-sub">Read-only Catalogs für Treatments, SOPs, Symptome, Wear, Programme, Setpoints und Pathogene.</div>
        </div>

        <div className="section-tabs knowledge-tabs" style={{ marginBottom: 14 }}>
          {categories.map((category) => (
            <button
              key={category.key}
              type="button"
              className={`btn ${activeCategory === category.key ? 'btn-primary' : 'btn-secondary'}`}
              onClick={() => setActiveCategory(category.key)}
            >
              {category.label} ({catalogs[category.key].length})
            </button>
          ))}
        </div>

        <div className="panel-card" style={{ marginBottom: 16 }}>
          <div className="card-pad" style={{ display: 'grid', gap: 10 }}>
            <div className="section-label" style={{ margin: 0 }}>{activeDefinition?.label ?? 'Catalog'}</div>
            <input
              type="search"
              value={searchQuery}
              onChange={(event) => setSearchQuery(event.target.value)}
              placeholder="Aktive Kategorie nach ID, Name oder Titel filtern..."
              aria-label="Knowledge-Catalog filtern"
            />
            <div className="task-sub">Datenquelle: {activeDefinition?.endpoint}</div>
            {catalogErrors[activeCategory] && (
              <div className="alert-bar">
                <div className="alert-dot" />
                <strong>Fehler</strong>
                <span>{catalogErrors[activeCategory]}</span>
              </div>
            )}
          </div>
        </div>

        {loading ? (
          <div className="empty-hint">Lade Wissensbasis...</div>
        ) : filteredItems.length === 0 ? (
          <div className="empty-hint">Keine Einträge für diese Kategorie gefunden.</div>
        ) : (
          <div className="tents-grid">
            {filteredItems.map((item, index) => (
              <KnowledgeCard key={`${activeCategory}-${getItemKey(item, index)}`} category={activeCategory} item={item} />
            ))}
          </div>
        )}
      </div>
    </>
  )
}

function KnowledgeCard({ category, item }: { category: KnowledgeCategory; item: KnowledgeItem }) {
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
      <FieldList label="Target Symptoms" values={getStringArray(item, 'targetSymptoms')} />
      <CompactObject label="Phase Filter" value={getField(item, 'phaseFilter')} />
      <FieldList label="Hardware" values={getStringArray(item, 'hardwareRequirements')} />
      <RecordList label="Conflicts" values={getRecordArray(item, 'conflicts')} keys={['treatmentId', 'reason']} />
      <SourceList item={item} />
    </BaseCard>
  )
}

function SopCard({ item }: { item: KnowledgeRecord }) {
  const estimatedDuration = getNumber(item, 'estimatedDurationMinutes')
  const durationDays = getNumber(item, 'durationDays')
  const intervalDays = getNumber(item, 'intervalDays')
  const steps = getRecordArray(item, 'steps')

  return (
    <BaseCard item={item} subtitle={getString(item, 'type')}>
      <MetaRow
        entries={[
          estimatedDuration !== null ? `${estimatedDuration} min` : null,
          durationDays !== null ? `${durationDays} Tage Dauer` : null,
          intervalDays !== null ? `${intervalDays} Tage Intervall` : null,
          `${steps.length} Schritte`,
        ]}
      />
      <RecordList label="Triggers" values={getRecordArray(item, 'triggers')} keys={['type', 'intervalDays', 'warningAfterDays', 'criticalAfterDays']} />
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
      <MetaRow
        entries={[
          item.expectedLifespanDays ? `${item.expectedLifespanDays} Tage Lebensdauer` : null,
          item.inspectionIntervalDays ? `${item.inspectionIntervalDays} Tage Inspektion` : null,
        ]}
      />
      <FieldList label="Replacement Triggers" values={item.replacementTriggers} />
    </BaseCard>
  )
}

function ProgramCard({ item }: { item: NutrientProgramDto }) {
  return (
    <BaseCard item={{ id: item.key, name: item.name }} subtitle={`${item.manufacturer} · ${item.category}`}>
      {item.summary && <div className="focus-body">{item.summary}</div>}
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
      {stageEntries.slice(0, 4).map(([stageName, values]) => (
        <div key={stageName} className="task-sub" style={{ color: 'var(--text)' }}>
          <strong>{stageName}:</strong> {formatCompact(values)}
        </div>
      ))}
    </BaseCard>
  )
}

function PathogenCard({ item }: { item: KnowledgeRecord }) {
  return (
    <BaseCard item={item} subtitle={[getString(item, 'category'), getString(item, 'riskLevel')].filter(Boolean).join(' · ')}>
      {getString(item, 'scientificName') && <div className="task-sub">{getString(item, 'scientificName')}</div>}
      <FieldList label="Symptoms" values={getStringArray(item, 'symptoms')} />
      <FieldList label="SOPs" values={compactStrings([getString(item, 'treatmentSopId'), getString(item, 'preventiveSopId')])} />
      {getString(item, 'notes') && <div className="focus-body">{getString(item, 'notes')}</div>}
      <SourceList item={item} />
    </BaseCard>
  )
}

function BaseCard({ item, subtitle, children }: { item: KnowledgeRecord; subtitle: string | null; children: React.ReactNode }) {
  return (
    <div className="panel-card">
      <div className="card-pad" style={{ display: 'grid', gap: 10 }}>
        <div>
          <div className="task-title">{getTitle(item)}</div>
          <div className="task-sub">{getString(item, 'id', 'key')}</div>
          {subtitle && <div className="badge" style={{ marginTop: 8 }}>{subtitle}</div>}
        </div>
        {children}
      </div>
    </div>
  )
}

function MetaRow({ entries }: { entries: Array<string | null> }) {
  const values = entries.filter((entry): entry is string => Boolean(entry))
  if (values.length === 0) return null

  return (
    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
      {values.map((entry) => (
        <span key={entry} className="badge">{entry}</span>
      ))}
    </div>
  )
}

function FieldList({ label, values }: { label: string; values: string[] }) {
  const visible = values.filter(Boolean).slice(0, 8)
  if (visible.length === 0) return null

  return (
    <div>
      <div className="focus-label">{label}</div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, marginTop: 6 }}>
        {visible.map((value) => (
          <span key={value} className="badge">{value}</span>
        ))}
      </div>
    </div>
  )
}

function RecordList({ label, values, keys }: { label: string; values: KnowledgeRecord[]; keys: string[] }) {
  if (values.length === 0) return null

  return (
    <div>
      <div className="focus-label">{label}</div>
      <div style={{ display: 'grid', gap: 4, marginTop: 6 }}>
        {values.slice(0, 4).map((value, index) => (
          <div key={`${label}-${index}`} className="task-sub" style={{ color: 'var(--text)' }}>
            {keys.map((key) => getString(value, key)).filter(Boolean).join(' · ') || formatCompact(value)}
          </div>
        ))}
      </div>
    </div>
  )
}

function CompactObject({ label, value }: { label: string; value: unknown }) {
  if (value === null || value === undefined) return null
  return (
    <div>
      <div className="focus-label">{label}</div>
      <div className="task-sub" style={{ color: 'var(--text)' }}>{formatCompact(value)}</div>
    </div>
  )
}

function SourceList({ item }: { item: KnowledgeRecord }) {
  const sources = getRecordArray(item, 'sources')
  if (sources.length === 0) return null
  return <RecordList label="Quellen" values={sources} keys={['title', 'reference', 'url', 'credibility']} />
}

function matchesSearch(item: KnowledgeItem, query: string) {
  const normalized = query.trim().toLowerCase()
  if (!normalized) return true
  const haystack = [
    getString(item, 'id', 'key'),
    getString(item, 'name', 'title'),
    getString(item, 'category', 'type', 'manufacturer', 'systemType', 'riskLevel'),
  ].filter(Boolean).join(' ').toLowerCase()
  return haystack.includes(normalized)
}

function getItemKey(item: KnowledgeItem, index: number) {
  return getString(item, 'id', 'key') ?? index.toString()
}

function getTitle(item: KnowledgeRecord) {
  return getString(item, 'name', 'title') ?? getString(item, 'id', 'key') ?? 'Unbenannt'
}

function getField(item: unknown, ...keys: string[]) {
  if (!isRecord(item)) return null
  for (const key of keys) {
    if (item[key] !== undefined && item[key] !== null) return item[key]
  }
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

function isRecord(value: unknown): value is KnowledgeRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function formatCompact(value: unknown): string {
  if (value === null || value === undefined) return ''
  if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') return String(value)
  if (Array.isArray(value)) return value.map(formatCompact).filter(Boolean).join(', ')
  if (isRecord(value)) {
    return Object.entries(value)
      .filter(([, entry]) => entry !== null && entry !== undefined && entry !== '')
      .map(([key, entry]) => `${key}: ${formatCompact(entry)}`)
      .join(' · ')
  }
  return String(value)
}

export default KnowledgePage
