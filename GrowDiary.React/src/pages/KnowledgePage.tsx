import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../api'
import type { KnowledgeOverviewDto, NutrientProgramDto, WearTemplateDto } from '../types'
import '../features/knowledge/knowledge-instrument.css'

type TopicId = 'rdwc' | 'addback' | 'rootrot' | 'ph-ec' | 'athena' | 'canna' | 'sensors' | 'troubleshooting'
type KnowledgeRecord = Record<string, unknown>

type Catalogs = {
  programs: NutrientProgramDto[]
  sops: KnowledgeRecord[]
  treatments: KnowledgeRecord[]
  symptoms: KnowledgeRecord[]
  setpoints: KnowledgeRecord[]
  pathogens: KnowledgeRecord[]
  wear: WearTemplateDto[]
}

type Topic = {
  id: TopicId
  title: string
  kicker: string
  intro: string
  keywords: string[]
  sections: Array<{ title: string; text: string }>
  action?: { label: string; to: string }
}

type Entry = {
  key: string
  title: string
  subtitle: string
  preview: string
  search: string
  refId?: string
  topic?: Topic
  record?: KnowledgeRecord
}

type Category = {
  id: string
  label: string
  kicker: string
  desc: string
  entries: Entry[]
}

const emptyCatalogs: Catalogs = { programs: [], sops: [], treatments: [], symptoms: [], setpoints: [], pathogens: [], wear: [] }

const topics: Topic[] = [
  {
    id: 'rdwc',
    title: 'RDWC Grundlagen',
    kicker: 'System verstehen',
    intro: 'Reservoir, Umlauf, Sauerstoff, Temperatur und Hygiene hängen in RDWC enger zusammen als bei Erde oder Coco.',
    keywords: ['rdwc', 'dwc', 'reservoir', 'wasser', 'umlauf', 'sauerstoff'],
    sections: [
      { title: 'Worum geht es?', text: 'RDWC ist ein rezirkulierendes Wassersystem. Ein Fehler im Reservoir betrifft deshalb sehr schnell alle Pflanzen.' },
      { title: 'Worauf achten?', text: 'pH, EC, Wassertemperatur, Sauerstoff, Wasserstand und saubere Hardware sind die Kernwerte.' },
      { title: 'In der App', text: 'Hydro-Setup, Addback, Sensoren und Live-Dashboard sind deshalb getrennte Workflows.' },
    ],
    action: { label: 'Hydro öffnen', to: '/hydro' },
  },
  {
    id: 'addback',
    title: 'Addback & Wasserwechsel',
    kicker: 'Nährlösung logisch ergänzen',
    intro: 'Addback ist kein blindes Nachkippen. Ziel ist, Wasserstand und Ziel-EC kontrolliert wieder in den Bereich zu bringen.',
    keywords: ['addback', 'topoff', 'ec', 'reservoir', 'nachfuellen', 'wasserwechsel'],
    sections: [
      { title: 'Prinzip', text: 'Erst Ist-Zustand messen, dann Zielwert bestimmen, dann Wasser oder Nährlösung ergänzen und nachmessen.' },
      { title: 'Fehler vermeiden', text: 'Nicht nur EC korrigieren. Wasserstand, pH, Temperatur und Pflanzenphase gehören dazu.' },
      { title: 'In der App', text: 'Der Addback-Assistent nutzt Grow, Hydro-System, Reservoirvolumen und Programm als Kontext.' },
    ],
    action: { label: 'Addback öffnen', to: '/addback' },
  },
  {
    id: 'rootrot',
    title: 'Pathogene / Root Rot',
    kicker: 'Risiko & Sofortmaßnahmen',
    intro: 'Wurzelfäule ist in Hydro kritisch, weil sich Probleme über Wasser, Biofilm und Sauerstoffmangel schnell ausbreiten können.',
    keywords: ['root', 'rot', 'wurzel', 'faeule', 'pathogen', 'hygiene', 'pythium'],
    sections: [
      { title: 'Symptome', text: 'Braune oder slimige Wurzeln, muffiger Geruch, fallender Sauerstoff, instabile Werte und schlapper Wuchs.' },
      { title: 'Sofortmaßnahmen', text: 'Temperatur, Sauerstoff, Biofilm, tote Wurzelmasse und Hygiene prüfen. Keine hektischen Mehrfachkorrekturen.' },
      { title: 'In der App', text: 'Wissen, SOPs, Sensorvertrauen und Risiken laufen hier zusammen.' },
    ],
  },
  {
    id: 'ph-ec',
    title: 'pH / EC / ORP / DO',
    kicker: 'Werte richtig deuten',
    intro: 'pH und EC müssen zusammen mit Wasserstand, Sauerstoff und Pflanzenphase gelesen werden.',
    keywords: ['ph', 'ec', 'orp', 'do', 'stabilisierung', 'naehrstoff'],
    sections: [
      { title: 'pH', text: 'pH-Drift kann normal sein, aber starke Sprünge deuten auf Puffer-, Hygiene- oder Dosierprobleme hin.' },
      { title: 'EC', text: 'Steigender EC bei fallendem Wasserstand kann auf stärkere Wasseraufnahme als Nährstoffaufnahme hinweisen.' },
      { title: 'In der App', text: 'Addback und Live-Score bewerten Werte konservativ und behaupten keine Stabilität, wenn Daten fehlen.' },
    ],
    action: { label: 'Live öffnen', to: '/' },
  },
  {
    id: 'athena',
    title: 'Athena Blended',
    kicker: 'Programm',
    intro: 'Athena wird als auswählbares Nährstoffprogramm im Grow gespeichert, damit Empfehlungen den richtigen Kontext haben.',
    keywords: ['athena', 'blended', 'grow', 'bloom'],
    sections: [
      { title: 'Ziel', text: 'Programmkontext für Grow, Addback, Zielwerte und spätere Empfehlungen.' },
      { title: 'Wichtig', text: 'Nicht nur Herstellername speichern, sondern auch Phase, Wasserquelle und Systemart berücksichtigen.' },
      { title: 'In der App', text: 'Beim Grow-Start wird das Programm gewählt und in Addback/Knowledge weiterverwendet.' },
    ],
    action: { label: 'Grow starten', to: '/grows/new' },
  },
  {
    id: 'canna',
    title: 'Canna Aqua',
    kicker: 'Programm',
    intro: 'Canna Aqua ist für rezirkulierende Systeme relevant und bleibt als Programmkontext auswählbar.',
    keywords: ['canna', 'aqua', 'vega', 'flores'],
    sections: [
      { title: 'Ziel', text: 'Nährstofflogik passend zu rezirkulierendem Hydro-System dokumentieren.' },
      { title: 'Wichtig', text: 'Programmauswahl ist kein Ersatz für Messwerte. Sie liefert Kontext für die Interpretation.' },
      { title: 'In der App', text: 'Programmwahl wird im Grow gespeichert und später für Empfehlungen genutzt.' },
    ],
    action: { label: 'Grow starten', to: '/grows/new' },
  },
  {
    id: 'sensors',
    title: 'Sensoren & Kalibrierung',
    kicker: 'Vertrauen in Messwerte',
    intro: 'Automatisierung ist nur so gut wie die Sensoren. pH/EC/ORP/DO brauchen Kalibrierung, Wartung und Plausibilitätsprüfung.',
    keywords: ['sensor', 'kalibrierung', 'wartung', 'ph', 'ec', 'orp', 'do'],
    sections: [
      { title: 'Sensorvertrauen', text: 'Keine Sensoren bedeutet nicht 100 % stabil. Dann ist die Bewertung offen und muss eingerichtet werden.' },
      { title: 'Kalibrierung', text: 'pH und EC sollten dokumentiert kalibriert werden, sonst sind Empfehlungen nicht belastbar.' },
      { title: 'In der App', text: 'Sensoren-Seite, HA-Mapping und Live-Dashboard greifen hier zusammen.' },
    ],
    action: { label: 'Sensoren öffnen', to: '/hardware' },
  },
  {
    id: 'troubleshooting',
    title: 'Symptome & Diagnose',
    kicker: 'Symptom → Ursache → Handlung',
    intro: 'Probleme sollen nicht als lose Datensätze erscheinen, sondern als geführte Diagnose.',
    keywords: ['symptom', 'diagnose', 'fehler', 'treatment', 'risiko'],
    sections: [
      { title: 'Vorgehen', text: 'Symptom beschreiben, Kontext prüfen, wahrscheinlichste Ursache wählen, eine Maßnahme durchführen und nachmessen.' },
      { title: 'Vermeiden', text: 'Mehrere starke Korrekturen gleichzeitig machen spätere Auswertung unmöglich.' },
      { title: 'In der App', text: 'Aufgaben, Wissen, Addback und Messungen machen diese Diagnose nachvollziehbar.' },
    ],
    action: { label: 'Aufgaben öffnen', to: '/aufgaben' },
  },
]

/* ── value helpers ─────────────────────────────────────────────── */
function isRecord(value: unknown): value is KnowledgeRecord {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}
function asStr(v: unknown): string | undefined {
  return typeof v === 'string' && v.trim() ? v : undefined
}
function asStrArr(v: unknown): string[] | undefined {
  if (!Array.isArray(v)) return undefined
  const out = v.filter((x): x is string => typeof x === 'string' && x.trim().length > 0)
  return out.length ? out : undefined
}
function asRecArr(v: unknown): KnowledgeRecord[] | undefined {
  if (!Array.isArray(v)) return undefined
  const out = v.filter(isRecord)
  return out.length ? out : undefined
}
function getId(record: KnowledgeRecord): string | undefined {
  return asStr(record.id) ?? asStr(record.key)
}
function getTitle(record: KnowledgeRecord, fallback: unknown): string {
  return asStr(record.name) ?? asStr(record.title) ?? asStr(record.id) ?? asStr(record.key) ?? String(fallback ?? 'Eintrag')
}
function getRecordTag(record: KnowledgeRecord): string {
  return asStr(record.type) ?? asStr(record.category) ?? asStr(record.systemType) ?? asStr(record.riskLevel) ?? asStr(record.manufacturer) ?? ''
}
// A short human-readable preview line for list entries — the first descriptive
// field, or the longest plain string, so the list shows content instead of only a
// title + cryptic tag.
function getPreview(record: KnowledgeRecord): string {
  const candidates = ['summary', 'description', 'intro', 'context', 'goal', 'overview', 'note', 'notes', 'method', 'standard']
  for (const key of candidates) {
    const value = asStr(record[key])
    if (value && value.length > 4) return value
  }
  let best = ''
  for (const [key, value] of Object.entries(record)) {
    if (['name', 'title', 'id', 'key', 'type', 'category'].includes(key)) continue
    const str = asStr(value)
    if (str && str.length > best.length) best = str
  }
  return best
}

const KEY_LABELS: Record<string, string> = {
  standard: 'Standard-Dosis', context: 'Kontext', method: 'Methode', timing: 'Zeitpunkt', frequency: 'Häufigkeit',
  durationStandard: 'Dauer (Standard)', durationHeavy: 'Dauer (stark)', heavy: 'Bei starkem Befall', light: 'Bei leichtem Befall',
  preventive: 'Vorbeugend', maxConcentration: 'Max. Konzentration', notes: 'Hinweis', warning: 'Warnung', compatibility: 'Kompatibilität',
  reference: 'Referenz', title: 'Titel', bestFor: 'Geeignet für', avoidFor: 'Ungeeignet für', manufacturer: 'Hersteller',
}
function humanize(key: string): string {
  if (KEY_LABELS[key]) return KEY_LABELS[key]
  const spaced = key.replace(/([A-Z])/g, ' $1').replace(/[_-]+/g, ' ').trim()
  const cased = spaced.charAt(0).toUpperCase() + spaced.slice(1)
  return cased.replace(/\b(ph|ec|orp|do|co2|vpd|ppfd|ipm|hocl|dwc|rdwc)\b/gi, (m) => m.toUpperCase())
}

const STAGE_LABELS: Record<string, string> = {
  seedling: 'Sämling', clone: 'Steckling', earlyVeg: 'Frühe Veg', veg: 'Vegetativ', lateVeg: 'Späte Veg',
  earlyFlower: 'Frühe Blüte', midFlower: 'Mittlere Blüte', flower: 'Blüte', lateFlower: 'Späte Blüte',
  ripen: 'Reife', flush: 'Spülen',
}
function num(v: unknown): number | undefined {
  return typeof v === 'number' && Number.isFinite(v) ? v : undefined
}
function range(a: unknown, b: unknown): string {
  const x = num(a)
  const y = num(b)
  if (x === undefined && y === undefined) return '–'
  if (x === undefined) return String(y)
  if (y === undefined) return String(x)
  return x === y ? String(x) : `${x}–${y}`
}

const STAGE_COLS: Array<{ label: string; render: (s: KnowledgeRecord) => string }> = [
  { label: 'pH', render: (s) => range(s.phMin, s.phMax) },
  { label: 'EC', render: (s) => range(s.ecMin, s.ecMax) },
  { label: 'ORP', render: (s) => range(s.orpMin, s.orpMax) },
  { label: 'H₂O °C', render: (s) => (num(s.waterTempDayC) !== undefined ? `${num(s.waterTempDayC)}/${num(s.waterTempNightC) ?? '–'}` : '–') },
  { label: 'VPD', render: (s) => range(s.vpdMin, s.vpdMax) },
  { label: 'PPFD', render: (s) => range(s.ppfdMin, s.ppfdMax) },
  { label: 'CO₂', render: (s) => range(s.co2Min, s.co2Max) },
]

/* ── detail renderers ──────────────────────────────────────────── */
function StageTable({ stages }: { stages: KnowledgeRecord }) {
  const rows = Object.entries(stages).filter(([, v]) => isRecord(v)) as Array<[string, KnowledgeRecord]>
  if (!rows.length) return null
  const cols = STAGE_COLS.filter((col) => rows.some(([, s]) => col.render(s) !== '–'))
  return (
    <div className="ix-kb-table-wrap">
      <table className="ix-kb-table">
        <thead>
          <tr>
            <th>Phase</th>
            {cols.map((c) => <th key={c.label}>{c.label}</th>)}
          </tr>
        </thead>
        <tbody>
          {rows.map(([stage, s]) => (
            <tr key={stage}>
              <td>{STAGE_LABELS[stage] ?? humanize(stage)}</td>
              {cols.map((c) => <td key={c.label}>{c.render(s)}</td>)}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function ObjFacts({ obj }: { obj: KnowledgeRecord }) {
  const facts = Object.entries(obj).filter(([, v]) => asStr(v) !== undefined || num(v) !== undefined)
  if (!facts.length) return null
  return (
    <dl className="ix-kb-facts">
      {facts.map(([k, v]) => (
        <div key={k} className="ix-kb-fact">
          <dt>{humanize(k)}</dt>
          <dd>{asStr(v) ?? String(v)}</dd>
        </div>
      ))}
    </dl>
  )
}

function RefChips({ ids, index, onNavigate }: { ids: string[]; index: Map<string, RefTarget>; onNavigate: (id: string) => void }) {
  return (
    <div className="ix-kb-refs">
      {ids.map((id) => {
        const hit = index.get(id)
        if (!hit) return <span key={id} className="ix-kb-ref dead">{id}</span>
        return (
          <button key={id} type="button" className="ix-kb-ref" onClick={() => onNavigate(id)}>
            {hit.title}<span className="arr">↗</span>
          </button>
        )
      })}
    </div>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="ix-kb-sec">
      <h4>{title}</h4>
      {children}
    </div>
  )
}

function BulletList({ items }: { items: string[] }) {
  return <ul className="ix-kb-ul">{items.map((it, i) => <li key={i}>{it}</li>)}</ul>
}

const ID_ARRAY_FIELDS: Array<[string, string]> = [
  ['symptoms', 'Symptome'],
  ['targetSymptoms', 'Ziel-Symptome'],
  ['suggestedTreatmentIds', 'Empfohlene Maßnahmen'],
  ['suggestedSopIds', 'Empfohlene SOPs'],
]
const TEXT_ARRAY_FIELDS: Array<[string, string]> = [
  ['requiredMaterials', 'Material'],
  ['applicableSetups', 'Geeignete Setups'],
  ['possibleCauses', 'Mögliche Ursachen'],
  ['diagnosticChecks', 'Diagnose-Checks'],
  ['replacementTriggers', 'Austausch-Anzeichen'],
]

function RecordDetail({ category, record, index, onNavigate }: { category: Category; record: KnowledgeRecord; index: Map<string, RefTarget>; onNavigate: (id: string) => void }) {
  const consumed = new Set<string>(['id', 'key', 'name', 'title', 'schemaVersion', 'slug', 'icon', 'stepType'])
  const mark = (...keys: string[]) => keys.forEach((k) => consumed.add(k))

  const title = getTitle(record, record)

  const metaKeys: Array<{ k: string; fmt?: (v: unknown) => string; tone?: (v: unknown) => string }> = [
    { k: 'type' },
    { k: 'category' },
    { k: 'systemType' },
    { k: 'scientificName' },
    { k: 'riskLevel', fmt: (v) => `Risiko: ${v}`, tone: (v) => (v === 'High' || v === 'Critical' ? 'crit' : v === 'Medium' ? 'warn' : 'ok') },
    { k: 'treatable', fmt: (v) => (v ? 'Behandelbar' : 'Nicht behandelbar'), tone: (v) => (v ? 'ok' : 'crit') },
    { k: 'durationDays', fmt: (v) => `${v} Tage` },
    { k: 'estimatedDurationMinutes', fmt: (v) => `~${v} min` },
    { k: 'expectedLifespanDays', fmt: (v) => `Lebensdauer ${v} T` },
    { k: 'inspectionIntervalDays', fmt: (v) => `Prüfung alle ${v} T` },
  ]
  const metaTags = metaKeys
    .map((spec) => {
      const v = record[spec.k]
      mark(spec.k)
      if (v === undefined || v === null || v === '') return null
      return { key: spec.k, text: spec.fmt ? spec.fmt(v) : String(v), tone: spec.tone ? spec.tone(v) : '' }
    })
    .filter((t): t is { key: string; text: string; tone: string } => t !== null)

  const lede = asStr(record.summary) ?? asStr(record.description) ?? asStr(record.notes) ?? asStr(record.intro)
  mark('summary', 'description', 'notes', 'intro')

  const bestFor = asStr(record.bestFor)
  const avoidFor = asStr(record.avoidFor)
  mark('bestFor', 'avoidFor')

  const steps = (asRecArr(record.steps) ?? []).slice().sort((a, b) => (num(a.order) ?? 0) - (num(b.order) ?? 0))
  mark('steps')

  const treatmentSopId = asStr(record.treatmentSopId)
  mark('treatmentSopId')

  const dosage = isRecord(record.dosage) ? record.dosage : undefined
  const application = isRecord(record.application) ? record.application : undefined
  const stages = isRecord(record.stages) ? record.stages : undefined
  mark('dosage', 'application', 'stages')

  const triggers = asRecArr(record.triggers)
  const triggerTypes = triggers ? triggers.map((t) => asStr(t.type)).filter((x): x is string => !!x) : undefined
  mark('triggers')

  const sources = asRecArr(record.sources)
  mark('sources')

  TEXT_ARRAY_FIELDS.forEach(([k]) => mark(k))
  ID_ARRAY_FIELDS.forEach(([k]) => mark(k))

  // generic leftovers
  const leftover = Object.keys(record).filter((k) => !consumed.has(k))
  const leftoverFacts = leftover.filter((k) => asStr(record[k]) !== undefined || num(record[k]) !== undefined || typeof record[k] === 'boolean')
  const leftoverArrays = leftover.map((k) => [k, asStrArr(record[k])] as const).filter((p): p is [string, string[]] => p[1] !== undefined)

  return (
    <>
      <span className="kk">{category.kicker}</span>
      <h2>{title}</h2>
      {metaTags.length > 0 && (
        <div className="ix-kb-meta">
          {metaTags.map((t) => <span key={t.key} className={t.tone ? `ix-kb-tag ${t.tone}` : 'ix-kb-tag'}>{t.text}</span>)}
        </div>
      )}
      {lede && <p className="ix-kb-lede">{lede}</p>}

      {bestFor && <Section title="Geeignet für"><p>{bestFor}</p></Section>}
      {avoidFor && <Section title="Ungeeignet für"><p>{avoidFor}</p></Section>}

      {steps.length > 0 && (
        <Section title="Ablauf">
          <div className="ix-kb-steps">
            {steps.map((s, i) => (
              <div key={asStr(s.id) ?? i} className="ix-kb-step">
                <div className="h">
                  <span className="num">{String(num(s.order) ?? i + 1).padStart(2, '0')}</span>
                  <strong>{asStr(s.title) ?? `Schritt ${i + 1}`}</strong>
                </div>
                {asStr(s.description) && <p>{asStr(s.description)}</p>}
              </div>
            ))}
          </div>
        </Section>
      )}

      {TEXT_ARRAY_FIELDS.map(([k, label]) => {
        const items = asStrArr(record[k])
        return items ? <Section key={k} title={label}><BulletList items={items} /></Section> : null
      })}

      {ID_ARRAY_FIELDS.map(([k, label]) => {
        const items = asStrArr(record[k])
        return items ? <Section key={k} title={label}><RefChips ids={items} index={index} onNavigate={onNavigate} /></Section> : null
      })}

      {treatmentSopId && (
        <Section title="Behandlungs-SOP"><RefChips ids={[treatmentSopId]} index={index} onNavigate={onNavigate} /></Section>
      )}

      {dosage && <Section title="Dosierung"><ObjFacts obj={dosage} /></Section>}
      {application && <Section title="Anwendung"><ObjFacts obj={application} /></Section>}
      {stages && <Section title="Phasen-Sollwerte"><StageTable stages={stages} /></Section>}

      {triggerTypes && triggerTypes.length > 0 && (
        <Section title="Auslöser"><BulletList items={triggerTypes.map(humanize)} /></Section>
      )}

      {sources && (
        <Section title="Quellen">
          <BulletList items={sources.map((s) => [asStr(s.title), asStr(s.reference)].filter(Boolean).join(' — ') || asStr(s.url) || 'Quelle')} />
        </Section>
      )}

      {leftoverArrays.map(([k, items]) => (
        <Section key={k} title={humanize(k)}><BulletList items={items} /></Section>
      ))}
      {leftoverFacts.length > 0 && (
        <Section title="Weitere Angaben">
          <dl className="ix-kb-facts">
            {leftoverFacts.map((k) => {
              const v = record[k]
              const text = typeof v === 'boolean' ? (v ? 'Ja' : 'Nein') : (asStr(v) ?? String(v))
              return <div key={k} className="ix-kb-fact"><dt>{humanize(k)}</dt><dd>{text}</dd></div>
            })}
          </dl>
        </Section>
      )}
    </>
  )
}

function TopicDetail({ topic }: { topic: Topic }) {
  return (
    <>
      <span className="kk">{topic.kicker}</span>
      <h2>{topic.title}</h2>
      <p className="ix-kb-lede">{topic.intro}</p>
      {topic.sections.map((s) => <Section key={s.title} title={s.title}><p>{s.text}</p></Section>)}
      {topic.action && (
        <div className="ix-kb-sec">
          <Link to={topic.action.to} className="v1-button is-primary">{topic.action.label}</Link>
        </div>
      )}
    </>
  )
}

type RefTarget = { catId: string; entryKey: string; title: string }

function KnowledgePage() {
  const [catalogs, setCatalogs] = useState<Catalogs>(emptyCatalogs)
  const [query, setQuery] = useState('')
  const [categoryId, setCategoryId] = useState<string | null>(null)
  const [entryKey, setEntryKey] = useState<string | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      setLoading(true)
      const safe = async <T,>(path: string, fallback: T): Promise<T> => {
        try { return await apiFetch<T>(path, { signal: controller.signal }) } catch { return fallback }
      }

      const [overview, sops, treatments, symptoms, setpoints, pathogens, wear] = await Promise.all([
        safe<KnowledgeOverviewDto>('/api/knowledge', { programs: [], playbooks: [] }),
        safe<KnowledgeRecord[]>('/api/knowledge/sops', []),
        safe<KnowledgeRecord[]>('/api/knowledge/treatments', []),
        safe<KnowledgeRecord[]>('/api/knowledge/symptoms', []),
        safe<KnowledgeRecord[]>('/api/knowledge/setpoints', []),
        safe<KnowledgeRecord[]>('/api/knowledge/pathogens', []),
        safe<WearTemplateDto[]>('/api/knowledge/wear', []),
      ])

      if (controller.signal.aborted) return
      setCatalogs({ programs: overview.programs ?? [], sops, treatments, symptoms, setpoints, pathogens, wear })
      setLoading(false)
    }

    void load()
    return () => controller.abort()
  }, [])

  const categories = useMemo<Category[]>(() => {
    const recCat = (id: string, label: string, kicker: string, desc: string, records: KnowledgeRecord[]): Category => ({
      id, label, kicker, desc,
      entries: records.map((raw, i) => {
        const rec = isRecord(raw) ? raw : {}
        return {
          key: `${id}-${getId(rec) ?? i}`,
          refId: getId(rec),
          title: getTitle(rec, raw),
          subtitle: getRecordTag(rec),
          preview: getPreview(rec),
          search: `${getTitle(rec, raw)} ${JSON.stringify(raw)}`.toLowerCase(),
          record: rec,
        }
      }),
    })

    const grundlagen: Category = {
      id: 'grundlagen', label: 'Grundlagen', kicker: 'Guides',
      desc: 'Kompakte Erklärungen zu RDWC, Addback, Werten und Diagnose.',
      entries: topics.map((t) => ({
        key: `grundlagen-${t.id}`,
        title: t.title,
        subtitle: t.kicker,
        preview: t.intro,
        search: [t.title, t.kicker, t.intro, ...t.keywords, ...t.sections.map((s) => `${s.title} ${s.text}`)].join(' ').toLowerCase(),
        topic: t,
      })),
    }

    return [
      grundlagen,
      recCat('sops', 'SOPs', 'Arbeitsabläufe', 'Schritt-für-Schritt-Prozeduren für den Betrieb.', catalogs.sops),
      recCat('treatments', 'Maßnahmen', 'Treatments', 'Behandlungen gegen Symptome und Schädlinge.', catalogs.treatments),
      recCat('symptoms', 'Symptome', 'Diagnose', 'Symptom, mögliche Ursache und empfohlene Maßnahme.', catalogs.symptoms),
      recCat('pathogens', 'Pathogene', 'Risiken', 'Erreger, Risiko-Level und Gegenmaßnahmen.', catalogs.pathogens),
      recCat('setpoints', 'Sollwerte', 'Zielbereiche', 'Phasen-Zielwerte für pH, EC, ORP und Klima.', catalogs.setpoints),
      recCat('programs', 'Programme', 'Nährstoffe', 'Nährstoff-Programme und ihr Einsatzkontext.', catalogs.programs as unknown as KnowledgeRecord[]),
      recCat('wear', 'Verschleiß', 'Hardware', 'Lebensdauer und Austausch-Anzeichen für Hardware.', catalogs.wear as unknown as KnowledgeRecord[]),
    ]
  }, [catalogs])

  const refIndex = useMemo<Map<string, RefTarget>>(() => {
    const map = new Map<string, RefTarget>()
    for (const cat of categories) {
      for (const entry of cat.entries) {
        if (entry.refId && !map.has(entry.refId)) map.set(entry.refId, { catId: cat.id, entryKey: entry.key, title: entry.title })
      }
    }
    return map
  }, [categories])

  const q = query.trim().toLowerCase()
  const searchResults = useMemo(() => {
    if (!q) return []
    // Rank by match quality so exact/title hits come first and body-only matches
    // (via the serialized record) rank lowest — "calmag" should surface the CalMag
    // entries before an SOP that merely references them.
    const scored: Array<{ cat: Category; entry: Entry; score: number }> = []
    for (const cat of categories) {
      for (const entry of cat.entries) {
        const title = entry.title.toLowerCase()
        let score = 0
        if (title === q) score = 100
        else if (title.startsWith(q)) score = 80
        else if (title.includes(q)) score = 60
        else if (entry.subtitle.toLowerCase().includes(q)) score = 40
        else if (entry.preview.toLowerCase().includes(q)) score = 30
        else if (entry.search.includes(q)) score = 15
        if (score > 0) scored.push({ cat, entry, score })
      }
    }
    scored.sort((a, b) => b.score - a.score || a.entry.title.localeCompare(b.entry.title))
    return scored.slice(0, 60)
  }, [q, categories])

  const currentCategory = categories.find((c) => c.id === categoryId) ?? categories[0] ?? null
  const entries = currentCategory?.entries ?? []
  const selectedEntry = entries.find((e) => e.key === entryKey) ?? entries[0] ?? null

  function openCategory(id: string) {
    const cat = categories.find((c) => c.id === id)
    setCategoryId(id)
    setEntryKey(cat?.entries[0]?.key ?? null)
    setDetailOpen(false)
  }
  function openEntry(key: string) {
    setEntryKey(key)
    setDetailOpen(true)
  }
  function openTarget(target: RefTarget) {
    setQuery('')
    setCategoryId(target.catId)
    setEntryKey(target.entryKey)
    setDetailOpen(true)
  }
  function navigateToId(id: string) {
    const hit = refIndex.get(id)
    if (hit) openTarget(hit)
  }
  let body: ReactNode
  if (loading) {
    body = <div className="ix-kb-empty"><h2>Lade Wissensbasis…</h2></div>
  } else if (q) {
    body = (
      <>
        <div className="ix-kb-backrow"><button type="button" className="ix-kb-back" onClick={() => setQuery('')}>← Übersicht</button></div>
        {searchResults.length === 0 ? (
          <div className="ix-kb-empty"><h2>Keine Treffer</h2><p>Für „{query}" wurde nichts gefunden.</p></div>
        ) : (
          <div className="ix-kb-results">
            {searchResults.map(({ cat, entry }) => (
              <button key={`${cat.id}-${entry.key}`} type="button" className="ix-kb-entry" onClick={() => openTarget({ catId: cat.id, entryKey: entry.key, title: entry.title })}>
                <span className="ix-kb-result-cat">{cat.label}{entry.subtitle ? ` · ${entry.subtitle}` : ''}</span>
                <strong>{entry.title}</strong>
                {entry.preview && <span className="ix-kb-entry-preview">{entry.preview}</span>}
              </button>
            ))}
          </div>
        )}
      </>
    )
  } else {
    body = (
      <>
        <div className="ix-kb-tabs" role="tablist">
          {categories.map((cat) => (
            <button
              key={cat.id}
              type="button"
              role="tab"
              aria-selected={cat.id === currentCategory?.id}
              className={cat.id === currentCategory?.id ? 'ix-kb-tab active' : 'ix-kb-tab'}
              onClick={() => openCategory(cat.id)}
            >
              <strong>{cat.label}</strong>
              <span className="count">{cat.entries.length}</span>
            </button>
          ))}
        </div>
        {currentCategory?.desc && <p className="ix-kb-cat-desc">{currentCategory.desc}</p>}
        <div className={detailOpen ? 'ix-kb-browse entry-open' : 'ix-kb-browse'}>
          <div className="ix-kb-list">
            {entries.map((e) => (
              <button key={e.key} type="button" className={e.key === selectedEntry?.key ? 'ix-kb-entry active' : 'ix-kb-entry'} onClick={() => openEntry(e.key)}>
                <span className="ix-kb-entry-top">
                  <strong>{e.title}</strong>
                  {e.subtitle && <span className="ix-kb-entry-tag">{e.subtitle}</span>}
                </span>
                {e.preview && <span className="ix-kb-entry-preview">{e.preview}</span>}
              </button>
            ))}
            {entries.length === 0 && <div className="ix-kb-empty"><p>Keine Einträge in dieser Kategorie.</p></div>}
          </div>
          <div className="ix-kb-detail">
            <button type="button" className="ix-kb-back ix-kb-detail-back" onClick={() => setDetailOpen(false)}>← Liste</button>
            {selectedEntry?.topic && <TopicDetail topic={selectedEntry.topic} />}
            {selectedEntry?.record && currentCategory && <RecordDetail category={currentCategory} record={selectedEntry.record} index={refIndex} onNavigate={navigateToId} />}
            {!selectedEntry && <div className="ix-kb-empty"><p>Eintrag links auswählen.</p></div>}
          </div>
        </div>
      </>
    )
  }

  return (
    <div className="ix-kb">
      <header className="ix-kb-head">
        <div className="ix-kb-brand">
          <span className="kk">Wissensbasis</span>
          <h1>WISSEN</h1>
        </div>
        <div className="ix-kb-search">
          <input
            data-audit="knowledge-search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Suche: Root Rot, Addback, EC, Athena…"
            aria-label="Wissensbasis durchsuchen"
          />
        </div>
      </header>
      {body}
    </div>
  )
}

export default KnowledgePage
