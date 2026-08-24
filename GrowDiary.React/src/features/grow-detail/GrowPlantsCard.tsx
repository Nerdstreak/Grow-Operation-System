import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../../api'
import type { PlantInstanceDto, StrainDto } from '../../types'
import { pflanzenRolleName } from '../../deutsche-woerter'
import { V1Button, V1Card, V1Section } from '../../components/v1'

/**
 * Die Pflanzen dieses Grows — jede mit ihrer eigenen Sorte.
 *
 * Aus dem Feld: „Ich habe 3 Sorten im Zelt stehen, kann dem Grow aber nur eine
 * zuweisen." Das Modell konnte es die ganze Zeit — `PlantInstance` trägt eine
 * eigene StrainId je Pflanze —, es gab nur keine Stelle, an der man es sieht
 * und pflegt. Der Grow behält seine Hauptsorte für Listen und Strahl; wer
 * gemischt fährt, trägt es hier ein.
 */
export function GrowPlantsCard({ growId, growPlantCount, onSorten }: {
  growId: number
  growPlantCount: number | null
  /**
   * Meldet die Sorten der Pflanzen nach oben — die Detailseite ersetzt damit
   * ihre „Sorte"-Kachel durch „gemischt", statt bei einem Mehrsorten-Grow
   * eine einzelne Hauptsorte zu behaupten.
   */
  onSorten?: (sorten: string[]) => void
}) {
  const [plants, setPlants] = useState<PlantInstanceDto[]>([])
  const [strains, setStrains] = useState<StrainDto[]>([])
  const [neuStrainId, setNeuStrainId] = useState('')
  const [busy, setBusy] = useState(false)
  const [fehler, setFehler] = useState<string | null>(null)
  const [geladen, setGeladen] = useState(false)

  async function laden(signal?: AbortSignal) {
    const [pflanzen, sorten] = await Promise.all([
      apiFetch<PlantInstanceDto[]>(`/api/plants?growId=${growId}`, { signal }),
      apiFetch<StrainDto[]>('/api/strains', { signal }),
    ])
    if (signal?.aborted) return
    setPlants(pflanzen)
    setStrains(sorten.sort((a, b) => a.name.localeCompare(b.name, 'de')))
    onSorten?.([...new Set(pflanzen.map((p) => p.strainName).filter((n): n is string => !!n))])
  }

  useEffect(() => {
    const controller = new AbortController()
    async function start() {
      try {
        await laden(controller.signal)
      } catch {
        /* Ohne Pflanzen-API bleibt die Karte leer. */
      } finally {
        if (!controller.signal.aborted) setGeladen(true)
      }
    }
    void start()
    return () => controller.abort()
    // eslint-disable-next-line react-hooks/exhaustive-deps -- laden haengt nur an growId
  }, [growId])

  /** „3× RS11 · 1× Purple Lemonade" — der Satz, der den Mischgrow beschreibt. */
  const verteilung = useMemo(() => {
    const zaehler = new Map<string, number>()
    for (const plant of plants) {
      const name = plant.strainName ?? 'ohne Sorte'
      zaehler.set(name, (zaehler.get(name) ?? 0) + 1)
    }
    return [...zaehler.entries()]
      .sort((a, b) => b[1] - a[1])
      .map(([name, anzahl]) => `${anzahl}× ${name}`)
      .join(' · ')
  }, [plants])

  /**
   * EIN Feld ändern, alle anderen unverändert mitschicken.
   *
   * Das PUT überschreibt ALLE Felder. Die erste Fassung zählte sie von Hand
   * auf — und hätte beim nächsten neuen Feld genau das getan, was beim
   * `siteIndex` fast passiert wäre: es stillschweigend genullt, sobald jemand
   * die Sorte wechselt. Deshalb wird jetzt das ganze DTO kopiert und nur das
   * eine Feld ersetzt.
   */
  async function feldAendern(plant: PlantInstanceDto, aenderung: Partial<PlantInstanceDto>, was: string) {
    setFehler(null)
    try {
      await apiFetch(`/api/plants/${plant.id}`, {
        method: 'PUT',
        body: JSON.stringify({ ...plant, ...aenderung }),
      })
      await laden()
    } catch (caught) {
      setFehler(caught instanceof Error ? caught.message : `${was} konnte nicht geändert werden.`)
    }
  }

  function sorteAendern(plant: PlantInstanceDto, strainId: string) {
    return feldAendern(plant, { strainId: strainId === '' ? null : Number(strainId) }, 'Sorte')
  }

  /** Der Topf ab 1 — leer heisst „kein Topf zugeordnet". */
  function topfAendern(plant: PlantInstanceDto, wert: string) {
    const zahl = wert.trim() === '' ? null : Math.trunc(Number(wert))
    if (zahl != null && (!Number.isFinite(zahl) || zahl < 1)) return
    return feldAendern(plant, { siteIndex: zahl }, 'Topf')
  }

  async function hinzufuegen() {
    setBusy(true)
    setFehler(null)
    try {
      // Der naechste freie Topf ab 1 — dieselbe Zaehlung, die die Draufsicht
      // an ihre Sites zeichnet. Wer anders bestueckt, aendert die Nummer.
      const belegt = new Set(plants.map((p) => p.siteIndex).filter((n): n is number => n != null))
      let freierTopf = 1
      while (belegt.has(freierTopf)) freierTopf++

      await apiFetch('/api/plants', {
        method: 'POST',
        body: JSON.stringify({
          growId,
          strainId: neuStrainId === '' ? null : Number(neuStrainId),
          label: `Pflanze ${plants.length + 1}`,
          plantRole: 'Production',
          plantStatus: 'Active',
          siteIndex: freierTopf,
        }),
      })
      await laden()
    } catch (caught) {
      setFehler(caught instanceof Error ? caught.message : 'Pflanze konnte nicht angelegt werden.')
    } finally {
      setBusy(false)
    }
  }

  if (!geladen) return null

  return (
    <V1Section title="Pflanzen & Sorten">
      <V1Card>
        <div data-audit="grow-plants">
          {fehler && <p className="gp-fehler">{fehler}</p>}

          {plants.length === 0 ? (
            <p className="gc-facts">
              {growPlantCount != null && growPlantCount > 0
                ? `Der Grow zählt ${growPlantCount} Pflanzen, aber keine ist einzeln erfasst. `
                : ''}
              Leg die Pflanzen einzeln an, wenn du <strong>mehrere Sorten</strong> in einem Zelt fährst —
              dann trägt jede ihre eigene.
            </p>
          ) : (
            <>
              {verteilung && <p className="gc-facts">Im Zelt: {verteilung}</p>}
              <ul className="gp-liste">
                {plants.map((plant) => (
                  <li key={plant.id}>
                    <span className="gp-label">
                      {plant.label}
                      {plant.plantRole !== 'Production' && <em className="gp-rolle"> · {pflanzenRolleName(plant.plantRole)}</em>}
                    </span>
                    {/* Der Topf ab 1 — die Nummer aus der Draufsicht des
                        Hydro-Systems. „In jedem Topf eine eigene Sorte" war
                        die Meldung; die Sorte gab es, der Ort fehlte. */}
                    <label className="gp-topf">
                      Topf
                      <input
                        type="number"
                        min={1}
                        value={plant.siteIndex ?? ''}
                        onChange={(event) => void topfAendern(plant, event.target.value)}
                        aria-label={`Topf von ${plant.label}`}
                      />
                    </label>
                    <select
                      value={plant.strainId ?? ''}
                      onChange={(event) => void sorteAendern(plant, event.target.value)}
                      aria-label={`Sorte von ${plant.label}`}
                    >
                      <option value="">Ohne Sorte</option>
                      {strains.map((strain) => <option key={strain.id} value={strain.id}>{strain.name}</option>)}
                    </select>
                  </li>
                ))}
              </ul>
            </>
          )}

          <div className="gp-neu">
            <select value={neuStrainId} onChange={(event) => setNeuStrainId(event.target.value)} aria-label="Sorte der neuen Pflanze">
              <option value="">Sorte wählen …</option>
              {strains.map((strain) => <option key={strain.id} value={strain.id}>{strain.name}</option>)}
            </select>
            <V1Button onClick={() => void hinzufuegen()} disabled={busy}>
              {busy ? 'Lege an…' : 'Pflanze hinzufügen'}
            </V1Button>
          </div>

          {/* Der vergessene Weg — existiert seit der Klon-Phase, wusste nur niemand mehr. */}
          <p className="gp-hinweis">
            Eigene Stecklinge? Ein Setup vom Typ <strong>Mutter</strong> unter{' '}
            <Link to="/zelte">Zelte &amp; Räume</Link> kann Klone direkt von einer Mutterpflanze erzeugen —
            samt Abstammung.
          </p>
        </div>
      </V1Card>
    </V1Section>
  )
}
