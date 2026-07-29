import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../api'
import { resolveUrl } from '../base'
import { V1Alert, V1Card, V1Page, V1Section } from '../components/v1'
import { GrowScopePicker } from '../features/grow-scope/GrowScopePicker'
import { useSelectedGrow } from '../features/grow-scope/useSelectedGrow'
import './advisor.css'

/**
 * Der eigene KI-Berater — eine eigene Seite, kein Anhängsel.
 *
 * Vorher stand das unten auf der Grow-Seite, hinter allem anderen. Das ist
 * dieselbe Art von Verstecken, die schon einmal aufgeräumt wurde: Wer nicht
 * weiß, dass es etwas gibt, sucht auch nicht danach.
 *
 * Anweisung und Prüffragen stehen hier offen, nicht nur in der ZIP-Datei. Sie
 * sind das, was den Berater begrenzt — ungelesene Grenzen sind keine.
 */

type Pruefung = {
  titel: string
  frage: string
  richtig: string
  durchgefallen: string
  kuerzel: string | null
  hinweis: string | null
}

type Mappe = {
  anweisung: string
  pruefungen: Pruefung[]
  dateien: { name: string; inhalt: string }[]
}

function AdvisorPage() {
  const { grows, growId, setGrowId, loading, error } = useSelectedGrow()
  const [mappe, setMappe] = useState<Mappe | null>(null)
  const [ladefehler, setLadefehler] = useState<string | null>(null)
  const [kopiert, setKopiert] = useState(false)

  useEffect(() => {
    const controller = new AbortController()
    apiFetch<Mappe>('/api/agent-export/mappe', { signal: controller.signal })
      .then(setMappe)
      .catch((caught) => {
        if (!controller.signal.aborted) {
          setLadefehler(caught instanceof Error ? caught.message : 'Der Inhalt der Mappe ließ sich nicht laden.')
        }
      })
    return () => controller.abort()
  }, [])

  async function anweisungKopieren() {
    if (!mappe) return
    try {
      await navigator.clipboard.writeText(mappe.anweisung)
      setKopiert(true)
      window.setTimeout(() => setKopiert(false), 2500)
    } catch {
      // Ohne Zwischenablage-Recht bleibt der Text unten trotzdem lesbar.
      setLadefehler('Die Zwischenablage ist gesperrt — markier den Text unten und kopiere ihn von Hand.')
    }
  }

  const grow = grows.find((item) => String(item.id) === String(growId)) ?? null

  return (
    <V1Page
      eyebrow="Wissen"
      title="Eigener KI-Berater"
      subtitle="Das Fachwissen dieser Anlage als Mappe für einen Assistenten deiner Wahl."
      action={<GrowScopePicker grows={grows} growId={growId} onChange={setGrowId} />}
    >
      {error && <V1Alert message={error} tone="critical" />}
      {ladefehler && <V1Alert message={ladefehler} tone="warn" />}

      <V1Section title="Die Berater-Mappe">
        <V1Card>
          <p className="ab-text">
            Ein Sprachassistent allein ist ein Modell mit Forenwissen. Was ihn zum Fachmann für
            deine Anlage macht, liegt hier: die Abläufe, die Behandlungen mit Dosierung und
            Verboten, die Symptome mit ihren Ursachen, die Regeln und die Sollwerte. Die Mappe
            packt das zusammen mit dem Lagebericht deines Grows.
          </p>
          <p className="ab-text">
            Grow OS verschickt nichts und braucht keinen Schlüssel — die Dateien landen bei dir,
            und du entscheidest, wem du sie gibst. Der Berater schaltet auch nichts: Dosierung und
            Automatik bleiben hier, mit ihren Sperren.
          </p>

          {loading && <p className="ab-text">Lade die Grows…</p>}
          {!loading && grow === null && (
            <p className="ab-text">Leg zuerst einen Grow an — ohne ihn gibt es keinen Lagebericht.</p>
          )}

          {grow && (
            <div className="v1-action-row">
              <a className="v1-button is-primary" href={resolveUrl(`/api/agent-export/grows/${grow.id}/paket`)}>
                Berater-Mappe herunterladen
              </a>
              <a className="v1-button" href={resolveUrl(`/api/agent-export/grows/${grow.id}/download`)}>
                Nur den Lagebericht
              </a>
              <Link className="v1-button" to={`/grows/${grow.id}`}>Zum Grow</Link>
            </div>
          )}
        </V1Card>
      </V1Section>

      {mappe && (
        <>
          <V1Section title="So benutzt du sie">
            <V1Card>
              <ol className="ab-schritte">
                <li>Bei deinem Assistenten ein Projekt anlegen — bei ChatGPT ein eigenes GPT oder
                  einfach einen Chat mit Anhängen, bei Claude ein Projekt, lokal etwa Ollama.</li>
                <li><b>Alle</b> Dateien aus der Mappe anhängen.</li>
                <li>Den Text der Anweisung unten als Systemanweisung einsetzen.</li>
                <li>Zuerst die Prüffragen stellen — erst danach Fragen zu deiner Anlage.</li>
              </ol>
              <table className="ab-dateien">
                <tbody>
                  {mappe.dateien.map((datei) => (
                    <tr key={datei.name}>
                      <th scope="row">{datei.name}</th>
                      <td>{datei.inhalt}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </V1Card>
          </V1Section>

          <V1Section title="Prüffragen — testen, bevor du ihm glaubst">
            <V1Card>
              <p className="ab-text">
                Ein Sprachmodell klingt bei einer erfundenen Antwort genauso überzeugt wie bei einer
                belegten. Der Unterschied ist nicht zu hören, nur zu prüfen. Fällt dein Assistent
                hier durch, nimm ein anderes Modell.
              </p>
            </V1Card>
            <div className="ab-fragen">
              {mappe.pruefungen.map((pruefung, index) => (
                <V1Card key={pruefung.titel}>
                  <div className="ab-frage-kopf">
                    <span className="ab-nummer">{index + 1}</span>
                    <strong>{pruefung.titel}</strong>
                  </div>
                  <blockquote className="ab-frage">{pruefung.frage}</blockquote>
                  <p className="ab-text"><b>Richtig:</b> {pruefung.richtig}</p>
                  <p className="ab-text ab-schlecht"><b>Durchgefallen:</b> {pruefung.durchgefallen}</p>
                  {pruefung.kuerzel && (
                    <p className="ab-beleg">Beleg im Wissen: <code>{pruefung.kuerzel}</code></p>
                  )}
                  {pruefung.hinweis && <p className="ab-text ab-hinweis">{pruefung.hinweis}</p>}
                </V1Card>
              ))}
            </div>
          </V1Section>

          <V1Section title="Die Anweisung">
            <V1Card>
              <p className="ab-text">
                Das ist der Text, der dem Assistenten seine Rolle und seine Grenzen gibt. Er steckt
                auch in der Mappe — hier steht er offen, weil ungelesene Grenzen keine sind.
              </p>
              <div className="v1-action-row">
                <button type="button" className="v1-button" onClick={() => void anweisungKopieren()}>
                  {kopiert ? 'Kopiert' : 'Anweisung kopieren'}
                </button>
              </div>
              <pre className="ab-anweisung">{mappe.anweisung}</pre>
            </V1Card>
          </V1Section>
        </>
      )}
    </V1Page>
  )
}

export default AdvisorPage
