import { useCallback, useEffect, useState } from 'react'
import { apiFetch, formatApiError } from '../api'
import {
  V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section, V1Skeleton,
} from '../components/v1'
import type { TentDto } from '../types'
import '../features/actest/actest.css'

/**
 * Zelt (AC-Test) — der Versuchsaufbau.
 *
 * **Die Frage dahinter:** kann Grow OS die Zentrale sein, von der aus der ganze
 * Grow läuft? Beantworten kann das kein Entwurf, sondern jemand, der es
 * benutzt. Deshalb steht es als eigener Menüpunkt da und nicht versteckt in den
 * Einstellungen — dort probiert es niemand aus und niemand gibt Rückmeldung.
 *
 * **Was hier absichtlich fehlt:** jede Automatik. Gestellt wird genau dann,
 * wenn jemand klickt. Der Controller behält sein eigenes Gehirn; zwei Systeme,
 * die dasselbe Gerät regeln, sind die Falle, die beim Kühler schon eine eigene
 * Regel bekommen hat.
 */

type Geraet = { name: string; leistungEntityId: string; modusEntityId: string | null }

type GeraetStand = {
  geraet: Geraet
  stufe: number | null
  modus: string | null
  fehler: string | null
}

type Stand = {
  zeltId: number
  geraete: GeraetStand[]
  haVerbunden: boolean
  testbetrieb: boolean
}

const STUFEN = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]

export function AcTestPage() {
  const [zelte, setZelte] = useState<TentDto[]>([])
  const [zeltId, setZeltId] = useState<number | null>(null)
  const [stand, setStand] = useState<Stand | null>(null)
  const [laedt, setLaedt] = useState(true)
  const [fehler, setFehler] = useState<string | null>(null)
  const [meldung, setMeldung] = useState<string | null>(null)
  const [arbeitet, setArbeitet] = useState<string | null>(null)

  // Der Entwurf der Geräteliste — getrennt vom Stand, damit ein halb getipptes
  // Feld nichts an einem laufenden Gerät ändert.
  const [entwurf, setEntwurf] = useState<Geraet[]>([])

  useEffect(() => {
    const abbruch = new AbortController()
    void (async () => {
      try {
        const liste = await apiFetch<TentDto[]>('/api/settings/tents', { signal: abbruch.signal })
        if (abbruch.signal.aborted) return
        setZelte(liste)
        setZeltId((aktuell) => aktuell ?? liste[0]?.id ?? null)
      } catch (caught) {
        if (!abbruch.signal.aborted) setFehler(formatApiError(caught, 'Zelte nicht ladbar.'))
      } finally {
        if (!abbruch.signal.aborted) setLaedt(false)
      }
    })()
    return () => abbruch.abort()
  }, [])

  // `useState`-Aufrufe hängen alle am `await`, laufen also erst nach dem
  // Rendern. Ein setState im Effektrumpf löst eine zweite Renderrunde aus,
  // bevor die erste fertig ist — `react-hooks` macht daraus zu Recht einen
  // Fehler, und derselbe Lint fährt in ci.yml.
  const laden = useCallback(async (id: number) => {
    try {
      const geholt = await apiFetch<Stand>(`/api/ac-test/${id}`)
      setStand(geholt)
      setEntwurf(geholt.geraete.map((g) => g.geraet))
    } catch (caught) {
      setFehler(formatApiError(caught, 'Der Versuchsaufbau liess sich nicht laden.'))
    }
  }, [])

  useEffect(() => {
    if (zeltId == null) return

    // Der AbortController ist nicht nur Aufräumen: ohne ihn schreibt beim
    // schnellen Zeltwechsel die Antwort zum ALTEN Zelt in den Zustand.
    const abbruch = new AbortController()
    void (async () => {
      try {
        const geholt = await apiFetch<Stand>(`/api/ac-test/${zeltId}`, { signal: abbruch.signal })
        if (abbruch.signal.aborted) return
        setStand(geholt)
        setEntwurf(geholt.geraete.map((g) => g.geraet))
      } catch (caught) {
        if (!abbruch.signal.aborted) {
          setFehler(formatApiError(caught, 'Der Versuchsaufbau liess sich nicht laden.'))
        }
      }
    })()

    return () => abbruch.abort()
  }, [zeltId])

  async function speichern() {
    if (zeltId == null) return
    setFehler(null)
    setMeldung(null)
    try {
      await apiFetch(`/api/ac-test/${zeltId}`, { method: 'PUT', body: JSON.stringify(entwurf) })
      setMeldung('Gespeichert.')
      await laden(zeltId)
    } catch (caught) {
      setFehler(formatApiError(caught, 'Konnte nicht gespeichert werden.'))
    }
  }

  async function stellen(entityId: string, stufe: number) {
    if (zeltId == null) return
    setFehler(null)
    setMeldung(null)
    setArbeitet(`${entityId}:${stufe}`)
    try {
      await apiFetch(`/api/ac-test/${zeltId}/stufe`, {
        method: 'POST',
        body: JSON.stringify({ entityId, stufe }),
      })
      setMeldung(`Auf Stufe ${stufe} gestellt.`)
      await laden(zeltId)
    } catch (caught) {
      setFehler(formatApiError(caught, 'Die Stufe liess sich nicht stellen.'))
    } finally {
      setArbeitet(null)
    }
  }

  const untertitel = 'Geräte am AC-Infinity-Controller direkt aus Grow OS stellen.'

  if (laedt) {
    return (
      <V1Page eyebrow="Versuch" title="Zelt (AC-Test)" subtitle={untertitel}>
        <V1Skeleton rows={5} label="Lade Versuchsaufbau" />
      </V1Page>
    )
  }

  const zeltWahl = zelte.length > 1 ? (
    <label className="v1-scope-picker">
      <span>Zelt</span>
      <select
        value={zeltId ?? ''}
        aria-label="Zelt wählen"
        onChange={(e) => setZeltId(Number(e.target.value))}
      >
        {zelte.map((z) => <option key={z.id} value={z.id}>{z.name}</option>)}
      </select>
    </label>
  ) : undefined

  return (
    <V1Page eyebrow="Versuch" title="Zelt (AC-Test)" subtitle={untertitel} action={zeltWahl}>
      {/* Der Hinweis steht GANZ oben und vor allem anderen — wer hier etwas
          stellt, soll wissen, worauf er sich einlässt. */}
      <V1Alert
        tone="warn"
        title="Das ist ein Test"
        message={
          'Dieser Bereich ist ein Versuch und noch nicht fertig. Er schreibt echte Werte an '
          + 'deine Geräte, sobald du eine Stufe anklickst — sonst passiert nichts von selbst. '
          + 'Sag uns, was fehlt und was hakt: davon hängt ab, wie die Gerätesteuerung in '
          + 'Grow OS am Ende aussieht.'
        } />

      {fehler && <V1Alert title="Fehler" message={fehler} tone="critical" />}
      {meldung && <V1Alert message={meldung} tone="ok" />}

      {stand?.testbetrieb && (
        <V1Alert
          tone="neutral"
          message={
            'Testbetrieb: die Messwerte sind erfunden und es wird nichts an ein Gerät '
            + 'geschickt. Ohne GROW_OS_DEMO=1 gilt die echte Verbindung.'
          } />
      )}

      {stand && !stand.haVerbunden && !stand.testbetrieb && (
        <V1Alert
          tone="warn"
          message="Home Assistant ist nicht verbunden — Einrichtung → Home Assistant." />
      )}

      {/* ---------- Stellen ---------- */}
      <V1Section title="Stufe stellen">
        {stand == null || stand.geraete.length === 0 ? (
          <V1Empty
            title="Noch kein Gerät eingetragen"
            text={'Trag unten die Entität deines Geräts ein — bei AC Infinity heisst sie '
              + '„… Einschaltleistung" und beginnt mit number.'} />
        ) : (
          <div className="ac-geraete">
            {stand.geraete.map((g) => (
              <V1Card key={g.geraet.leistungEntityId}>
                <div className="ac-kopf">
                  <strong>{g.geraet.name}</strong>
                  {g.stufe != null && <V1Badge tone="accent">Stufe {g.stufe}</V1Badge>}
                  {g.modus && <V1Badge>{g.modus}</V1Badge>}
                </div>
                <p className="ac-entity">{g.geraet.leistungEntityId}</p>

                {g.fehler ? (
                  <V1Alert tone="warn" message={g.fehler} />
                ) : (
                  <div className="ac-stufen" role="group" aria-label={`Stufe für ${g.geraet.name}`}>
                    {STUFEN.map((stufe) => (
                      <button
                        key={stufe}
                        type="button"
                        className={`ac-stufe${g.stufe === stufe ? ' is-jetzt' : ''}`}
                        disabled={arbeitet != null}
                        aria-pressed={g.stufe === stufe}
                        onClick={() => void stellen(g.geraet.leistungEntityId, stufe)}
                      >
                        {stufe}
                      </button>
                    ))}
                  </div>
                )}
                <p className="ac-hinweis">
                  {/* 0 heisst aus — das steht dabei, damit niemand es ausprobieren muss. */}
                  Stufe 0 schaltet das Gerät aus. Es passiert nur, was du hier anklickst.
                </p>
              </V1Card>
            ))}
          </div>
        )}
      </V1Section>

      {/* ---------- Einrichten ---------- */}
      <V1Section
        title="Geräte eintragen"
        action={(
          <V1Button
            onClick={() => setEntwurf([...entwurf, { name: '', leistungEntityId: '', modusEntityId: null }])}
          >
            + Gerät
          </V1Button>
        )}
      >
        <V1Card>
          <div data-audit="ac-test-form">
            {entwurf.length === 0 && (
              <p className="ac-hinweis">
                Die Entitäten findest du in Home Assistant unter deinem AC-Infinity-Gerät.
                Für die Stufe brauchst du „… Einschaltleistung" (beginnt mit <code>number.</code>);
                der aktive Modus (<code>select.</code>) ist freiwillig und wird nur angezeigt.
              </p>
            )}

            {entwurf.map((g, i) => (
              <div key={i} className="v1-form-grid ac-zeile">
                <V1Field label="Name" hint={'Frei wählbar — „LED Top", „Abluft".'}>
                  <input
                    value={g.name}
                    placeholder="LED Top"
                    onChange={(e) => setEntwurf(entwurf.map((x, k) => k === i ? { ...x, name: e.target.value } : x))} />
                </V1Field>
                <V1Field label="Stufe 0–10 (number)" hint={'Bei AC Infinity: „… Einschaltleistung".'}>
                  <input
                    value={g.leistungEntityId}
                    placeholder="number.led_top_eingeschaltete_leistung"
                    onChange={(e) => setEntwurf(entwurf.map((x, k) => k === i ? { ...x, leistungEntityId: e.target.value } : x))} />
                </V1Field>
                <V1Field label="Modus (select, freiwillig)" hint="Wird nur angezeigt, nicht gestellt.">
                  <input
                    value={g.modusEntityId ?? ''}
                    placeholder="select.led_top_aktiver_modus"
                    onChange={(e) => setEntwurf(entwurf.map((x, k) => k === i ? { ...x, modusEntityId: e.target.value || null } : x))} />
                </V1Field>
                <div className="ac-weg">
                  <V1Button variant="danger" onClick={() => setEntwurf(entwurf.filter((_, k) => k !== i))}>
                    Entfernen
                  </V1Button>
                </div>
              </div>
            ))}

            <div className="v1-form-actions">
              <V1Button variant="primary" onClick={() => void speichern()} audit="ac-test-speichern">
                Speichern
              </V1Button>
            </div>
          </div>
        </V1Card>
      </V1Section>
    </V1Page>
  )
}
