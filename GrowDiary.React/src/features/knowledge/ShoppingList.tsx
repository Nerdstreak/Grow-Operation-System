import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'
import { V1Section } from '../../components/v1'

type Posten = { material: string; wofuer: string[] }
type Gruppe = { titel: string; posten: Posten[] }

/**
 * Was man dahaben muss, damit die Abläufe durchführbar sind.
 *
 * Die Materiallisten stecken seit jeher in den Abläufen und wurden nur beim
 * Drucken der Mappe gelesen. Wer im Laden steht, hatte davon nichts.
 *
 * Auf der Wissensseite zugeklappt, weil sie dort nicht der Zweck ist. Auf der
 * eigenen Seite offen — wer sie über das Menü ansteuert, will sie lesen und
 * nicht erst einen Knopf suchen.
 *
 * Jeder Posten nennt, wofür er gebraucht wird. Ohne diesen Grund wäre es eine
 * Liste, die man abnickt statt sie zu benutzen.
 */
export function ShoppingList({ initialOffen = false }: { initialOffen?: boolean } = {}) {
  const [gruppen, setGruppen] = useState<Gruppe[] | null>(null)
  const [offen, setOffen] = useState(initialOffen)

  useEffect(() => {
    const controller = new AbortController()
    apiFetch<Gruppe[]>('/api/shopping-list', { signal: controller.signal })
      .then((geladen) => { if (!controller.signal.aborted) setGruppen(geladen) })
      .catch(() => { /* Ohne Liste bleibt der Abschnitt weg. */ })
    return () => controller.abort()
  }, [])

  if (!gruppen || gruppen.length === 0) return null
  const anzahl = gruppen.reduce((summe, gruppe) => summe + gruppe.posten.length, 0)

  return (
    <V1Section title="Einkaufsliste">
      {/* KEINE Karte und kein zweiter Koerper: V1Section bringt Rahmen und
          Innenabstand schon mit. Mit Karte kosteten zwei geschachtelte Rahmen
          am Telefon 24,5 % der Bildschirmbreite; ein eigener
          `.v1-section-body` darin waere derselbe Fehler eine Stufe kleiner. */}
      <div data-audit="shopping-list">
          <button type="button" className="ls-btn is-small" onClick={() => setOffen((wert) => !wert)}>
            {offen ? 'Zuklappen' : `${anzahl} Posten anzeigen`}
          </button>
          <p className="gc-facts sl-intro">
            Alles, was die Abläufe verlangen — einmal je Sache, mit dem Ablauf dahinter, der sie braucht.
          </p>

          {offen && gruppen.map((gruppe) => (
            <div key={gruppe.titel} className="sl-group">
              <div className="sl-group-head">{gruppe.titel}</div>
              <ul className="sl-list">
                {gruppe.posten.map((posten) => (
                  <li key={posten.material}>
                    <span className="sl-item">{posten.material}</span>
                    <span className="sl-for">
                      {posten.wofuer.length > 2
                        ? `für ${posten.wofuer.length} Abläufe`
                        : `für ${posten.wofuer.join(' und ')}`}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
    </V1Section>
  )
}
