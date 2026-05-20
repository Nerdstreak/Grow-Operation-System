import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../api'
import type { KnowledgeOverviewDto, NutrientProgramDto, WearTemplateDto } from '../types'
import { V1Badge, V1Empty, V1Field, V1Page } from '../components/v1'

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
    action: { label: 'SOPs prüfen', to: '/wissen' },
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
    intro: 'Automatisierung ist nur so gut wie die Sensoren. pH/EC/ORP/DO brauchen Kalibrierung, Wartung und Plausibilitaetspruefung.',
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
    kicker: 'Symptom -> Ursache -> Handlung',
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

function KnowledgePage() {
  const [catalogs, setCatalogs] = useState<Catalogs>(emptyCatalogs)
  const [selectedTopicId, setSelectedTopicId] = useState<TopicId>('rdwc')
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(true)
  const [articleOpen, setArticleOpen] = useState(false)

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

  const filteredTopics = useMemo(() => {
    const normalized = query.trim().toLowerCase()
    if (!normalized) return topics
    return topics.filter((topic) => [topic.title, topic.kicker, topic.intro, ...topic.keywords].join(' ').toLowerCase().includes(normalized))
  }, [query])

  const selectedTopic = topics.find((topic) => topic.id === selectedTopicId) ?? filteredTopics[0] ?? topics[0]
  const related = useMemo(() => findRelated(selectedTopic, catalogs), [selectedTopic, catalogs])

  function selectTopic(topicId: TopicId) {
    setSelectedTopicId(topicId)
    setArticleOpen(true)
  }

  return (
    <V1Page eyebrow="Wissen" title="Wissen">
      <section className="wiki-shell">
        <aside className={articleOpen ? 'wiki-nav is-hidden-mobile' : 'wiki-nav'} data-audit="knowledge-topic-nav">
          <V1Field label="Suche">
            <input data-audit="knowledge-search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Root Rot, Addback, Athena..." />
          </V1Field>
          <nav aria-label="Wissen Oberthemen">
            {filteredTopics.map((topic) => (
              <button key={topic.id} type="button" className={topic.id === selectedTopic.id ? 'wiki-topic active' : 'wiki-topic'} onClick={() => selectTopic(topic.id)}>
                <span>{topic.kicker}</span>
                <strong>{topic.title}</strong>
              </button>
            ))}
          </nav>
        </aside>

        <article className={articleOpen ? 'wiki-article is-open-mobile' : 'wiki-article'} data-audit="knowledge-article">
          <button type="button" className="wiki-back" onClick={() => setArticleOpen(false)}>Zurück zu Themen</button>
          <header>
            <span>{selectedTopic.kicker}</span>
            <h2>{selectedTopic.title}</h2>
            <p>{selectedTopic.intro}</p>
          </header>
          {selectedTopic.sections.map((section) => (
            <section key={section.title}>
              <h3>{section.title}</h3>
              <p>{section.text}</p>
            </section>
          ))}
          {selectedTopic.action && <Link to={selectedTopic.action.to} className="v1-button is-primary">{selectedTopic.action.label}</Link>}
        </article>

        <aside className="wiki-related" data-audit="knowledge-related">
          <h2>Verknüpfte Daten</h2>
          {loading ? <V1Empty title="Lade Wissensbasis..." /> : related.length === 0 ? <V1Empty title="Keine Treffer" text="Zu diesem Thema wurden noch keine passenden Datensätze gefunden." /> : (
            <div className="wiki-related-list">
              {related.map((item) => (
                <div key={item.key} className="wiki-related-item">
                  <span>{item.type}</span>
                  <strong>{item.title}</strong>
                  <p>{item.description}</p>
                  <V1Badge>{item.source}</V1Badge>
                </div>
              ))}
            </div>
          )}
        </aside>
      </section>
    </V1Page>
  )
}

function findRelated(topic: Topic, catalogs: Catalogs) {
  const terms = topic.keywords.map((keyword) => keyword.toLowerCase())
  const items: Array<{ key: string; type: string; title: string; description: string; source: string }> = []

  const collect = (type: string, source: string, values: unknown[]) => {
    for (const value of values) {
      const text = JSON.stringify(value).toLowerCase()
      if (!terms.some((term) => text.includes(term))) continue
      const record = isRecord(value) ? value : {}
      items.push({
        key: `${type}-${items.length}`,
        type,
        source,
        title: getTitle(record, value),
        description: getDescription(record),
      })
      if (items.length >= 8) return
    }
  }

  collect('Programm', 'Nährstoffprogramm', catalogs.programs)
  collect('SOP', 'Arbeitsablauf', catalogs.sops)
  collect('Treatment', 'Maßnahme', catalogs.treatments)
  collect('Symptom', 'Diagnose', catalogs.symptoms)
  collect('Setpoint', 'Zielwert', catalogs.setpoints)
  collect('Pathogen', 'Risiko', catalogs.pathogens)
  collect('Verschleiß', 'Hardware', catalogs.wear)

  return items.slice(0, 8)
}

function getTitle(record: Record<string, unknown>, fallback: unknown) {
  const value = record.name ?? record.title ?? record.id ?? record.key
  return typeof value === 'string' ? value : String(value ?? fallback ?? 'Eintrag')
}

function getDescription(record: Record<string, unknown>) {
  const value = record.summary ?? record.description ?? record.notes ?? record.category ?? record.type
  if (typeof value === 'string') return value
  return 'Passender Knowledge-Eintrag aus der Datenbasis.'
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

export default KnowledgePage
