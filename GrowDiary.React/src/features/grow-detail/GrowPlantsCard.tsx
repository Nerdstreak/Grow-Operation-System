import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../../api'
import type { PlantInstanceDto, StrainDto } from '../../types'
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
export function GrowPlantsCard({ growId, growPlantCount }: { growId: number; growPlantCount: number | null }) {
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

  async function sorteAendern(plant: PlantInstanceDto, strainId: string) {
    setFehler(null)
    try {
      await apiFetch(`/api/plants/${plant.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          strainId: strainId === '' ? null : Number(strainId),
          setupId: plant.setupId,
          growId: plant.growId,
          parentPlantId: plant.parentPlantId,
          label: plant.label,
          plantRole: plant.plantRole,
          plantStatus: plant.plantStatus,
          phenoLabel: plant.phenoLabel,
          startedAt: plant.startedAt,
          endedAt: plant.endedAt,
          notes: plant.notes,
        }),
      })
      await laden()
    } catch (caught) {
      setFehler(caught instanceof Error ? caught.message : 'Sorte konnte nicht geändert werden.')
    }
  }

  async function hinzufuegen() {
    setBusy(true)
    setFehler(null)
    try {
      await apiFetch('/api/plants', {
        method: 'POST',
        body: JSON.stringify({
          growId,
          strainId: neuStrainId === '' ? null : Number(neuStrainId),
          label: `Pflanze ${plants.length + 1}`,
          plantRole: 'Production',
          plantStatus: 'Active',
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
                      {plant.plantRole !== 'Production' && <em className="gp-rolle"> · {plant.plantRole === 'Mother' ? 'Mutter' : plant.plantRole === 'Clone' ? 'Klon' : 'Quarantäne'}</em>}
                    </span>
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
