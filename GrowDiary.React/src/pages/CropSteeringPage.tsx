import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { apiFetch, formatApiError } from '../api'
import { GrowScopePicker } from '../features/grow-scope/GrowScopePicker'
import { useSelectedGrow } from '../features/grow-scope/useSelectedGrow'
import {
  V1Alert, V1Badge, V1Button, V1Card, V1Empty, V1Field, V1Page, V1Section, V1Skeleton, V1Stat, V1Switch,
} from '../components/v1'
import '../features/cropsteering/cropsteering.css'

/**
 * Crop Steering: die Wassertemperatur über den Tag steuern.
 *
 * **Die eine Stelle.** Der Plan, das Schreiben des Sollwerts und das Schalten
 * des Kühlers stehen hier zusammen. Die Karte am Grow zeigt nur noch den Stand
 * und führt hierher — zwei Formulare für dieselbe Sache sind in diesem Projekt
 * schon einmal ausgeliefert worden.
 *
 * **Zwei Hälften, nicht vier Kästen.** Der erste Wurf hatte vier gleich
 * aussehende Abschnitte untereinander und 496 Wörter; der Nutzer nannte das
 * „unstrukturiert und sehr viel Text". Jetzt trennt die Seite sichtbar: oben
 * **Heute** — nur ansehen, nichts zum Anfassen —, unten **Einstellen** — ein
 * Formular. Kein Reiter und kein Aufklapper: an `data-audit="kuehler"` hängt
 * eine Sichtbarkeitsprüfung, und die Gerätebedingung des Kühlers ist eine
 * Bedingung und keine Fußnote.
 */

type AbsenkWoche = { bluetewocheAb1: number; tagC: number; nachtC: number; erreicht: boolean }

type Absenkplan = {
  wochen: AbsenkWoche[]
  heuteTagC: number | null
  heuteNachtC: number | null
  aktuelleWoche: number | null
  herkunft: string
  luecke: string | null
}

type Kuehler = {
  enabled: boolean
  switchEntityId: string | null
  hysteresisC: number
  minRunMinutes: number
  minPauseMinutes: number
  maxReadingAgeMinutes: number
  lastSwitchUtc: string | null
}

type Steuerung = {
  enabled: boolean
  floorC: number | null
  hardFloorC: number
  targetEntityId: string | null
  plan: Absenkplan
  chiller: Kuehler | null
}

const grad = (wert: number | null | undefined) =>
  wert == null ? '–' : wert.toLocaleString('de-DE', { maximumFractionDigits: 1 })

export function CropSteeringPage() {
  const { grows, growId, setGrowId, loading: growsLaedt } = useSelectedGrow()

  const [stand, setStand] = useState<Steuerung | null>(null)
  const [laedt, setLaedt] = useState(true)
  const [fehler, setFehler] = useState<string | null>(null)
  const [meldung, setMeldung] = useState<string | null>(null)
  const [speichert, setSpeichert] = useState(false)

  // Der Entwurf steht getrennt vom geladenen Stand: solange nicht gespeichert
  // ist, ändert ein Tippfehler im Feld nichts an einem laufenden Kompressor.
  const [rampeAn, setRampeAn] = useState(false)
  const [ziel, setZiel] = useState('')
  const [boden, setBoden] = useState('')
  const [kuehler, setKuehler] = useState<Kuehler | null>(null)

  // Das Totband als TEXT, nicht als Zahl.
  //
  // Ein <input type="number"> nimmt in vielen Browsern kein deutsches Komma:
  // wer „0,6" tippt, hat am Ende ein leeres Feld und speichert stillschweigend
  // den alten Wert. Die Minutenfelder daneben bleiben Zahlenfelder — dort gibt
  // es keine Nachkommastelle und damit auch kein Komma-Problem.
  const [totband, setTotband] = useState('')

  const uebernehmen = useCallback((geladen: Steuerung) => {
    setStand(geladen)
    setRampeAn(geladen.enabled)
    setZiel(geladen.targetEntityId ?? '')
    setBoden(geladen.floorC != null ? String(geladen.floorC).replace('.', ',') : '')
    setKuehler(geladen.chiller)
    setTotband(geladen.chiller != null ? String(geladen.chiller.hysteresisC).replace('.', ',') : '')
  }, [])

  // Laden im Effekt, aber KEIN setState im Effektrumpf: die Zustandsänderungen
  // hängen alle am `await`, laufen also erst nach dem Rendern. Ein
  // `setLaedt(true)` gleich zu Beginn löst eine zweite Renderrunde aus, bevor
  // die erste fertig ist — `react-hooks` macht daraus zu Recht einen Fehler,
  // und derselbe Lint fährt in ci.yml.
  //
  // Der AbortController ist nicht nur Aufräumen: ohne ihn schreibt eine
  // Antwort zum alten Grow in den Zustand, wenn jemand schnell umschaltet.
  useEffect(() => {
    // Kein setState im Effektrumpf, auch nicht hier: ohne Grow wird gar nicht
    // geladen, und `laedt` ist dann ohnehin bedeutungslos — siehe `zeigeSkelett`.
    if (!growId) return

    const abbruch = new AbortController()
    void (async () => {
      try {
        const geladen = await apiFetch<Steuerung>(
          `/api/grows/${growId}/night-ramp`, { signal: abbruch.signal })
        if (abbruch.signal.aborted) return
        setFehler(null)
        uebernehmen(geladen)
      } catch (caught) {
        if (abbruch.signal.aborted) return
        setFehler(formatApiError(caught, 'Die Steuerung ließ sich nicht laden.'))
      } finally {
        if (!abbruch.signal.aborted) setLaedt(false)
      }
    })()

    return () => abbruch.abort()
  }, [growId, uebernehmen])

  async function speichern(ereignis: FormEvent) {
    ereignis.preventDefault()
    if (!growId) return
    setSpeichert(true)
    setFehler(null)
    setMeldung(null)
    try {
      // Unlesbares NICHT stillschweigend verschlucken. `Number('16x')` ist NaN,
      // und daraus wurde bisher `floorC: null` plus „Gespeichert." — dieselbe
      // Fehlerklasse, die im Messformular schon einmal 21 Zahlenfelder betraf.
      const unlesbar = [
        boden.trim() !== '' && !Number.isFinite(Number(boden.replace(',', '.'))) ? 'Untergrenze' : null,
        totband.trim() !== '' && !Number.isFinite(Number(totband.replace(',', '.'))) ? 'Totband' : null,
      ].filter(Boolean)
      if (unlesbar.length > 0) {
        setFehler(`${unlesbar.join(' und ')}: das ist keine Zahl. Nichts gespeichert.`)
        setSpeichert(false)
        return
      }

      const zahl = boden.trim() === '' ? null : Number(boden.replace(',', '.'))
      const totbandZahl = Number(totband.replace(',', '.'))
      uebernehmen(await apiFetch<Steuerung>(`/api/grows/${growId}/night-ramp`, {
        method: 'PUT',
        body: JSON.stringify({
          enabled: rampeAn,
          floorC: Number.isFinite(zahl as number) ? zahl : null,
          targetEntityId: ziel.trim(),
          chiller: kuehler && {
            enabled: kuehler.enabled,
            switchEntityId: kuehler.switchEntityId,
            hysteresisC: Number.isFinite(totbandZahl) ? totbandZahl : kuehler.hysteresisC,
            minRunMinutes: kuehler.minRunMinutes,
            minPauseMinutes: kuehler.minPauseMinutes,
            maxReadingAgeMinutes: kuehler.maxReadingAgeMinutes,
          },
        }),
      }))
      setMeldung('Gespeichert.')
    } catch (caught) {
      setFehler(formatApiError(caught, 'Konnte nicht gespeichert werden.'))
    } finally {
      setSpeichert(false)
    }
  }

  const kopf = <GrowScopePicker grows={grows} growId={growId} onChange={setGrowId} />
  const untertitel = 'Die Wassertemperatur über den Tag.'

  // `laedt` zaehlt nur, wenn es ueberhaupt einen Grow zu laden gibt. Sonst
  // stuende das Skelett fuer immer da, weil der Effekt gar nicht erst laeuft.
  const zeigeSkelett = growsLaedt || (growId != null && laedt)

  if (zeigeSkelett) {
    return (
      <V1Page eyebrow="Betrieb" title="Crop Steering" subtitle={untertitel}>
        <V1Skeleton rows={6} label="Lade Crop Steering" />
      </V1Page>
    )
  }

  if (grows.length === 0 || !growId || !stand) {
    return (
      <V1Page eyebrow="Betrieb" title="Crop Steering" subtitle={untertitel} action={kopf}>
        {fehler && <V1Alert title="Fehler" message={fehler} tone="critical" />}
        <V1Empty
          title="Noch kein laufender Grow"
          text="Der Plan rechnet ab dem Flip. Sobald ein Grow läuft, steht er hier." />
      </V1Page>
    )
  }

  const plan = stand.plan

  // Der Boden, an dem die Rampe stehen bleibt. Ohne eigene Angabe ist das NICHT
  // die harte Grenze von 12 °C, sondern der Finish-Nachtwert des Profils — und
  // der steht als tiefster Nachtwert in der Rampe selbst.
  const wirksamerBoden = stand.floorC
    ?? (plan.wochen.length > 0 ? Math.min(...plan.wochen.map((w) => w.nachtC)) : null)

  // Steht die Blüte hinter der letzten Rampenwoche, ist keine Zeile
  // hervorgehoben — die Tabelle sah dann aus, als gälte nichts davon.
  const letzteWoche = plan.wochen.at(-1) ?? null
  const hinterDerRampe = plan.aktuelleWoche != null && letzteWoche != null
    && plan.aktuelleWoche > letzteWoche.bluetewocheAb1

  return (
    <V1Page eyebrow="Betrieb" title="Crop Steering" subtitle={untertitel} action={kopf}>
      {fehler && <V1Alert title="Fehler" message={fehler} tone="critical" />}
      {meldung && <V1Alert message={meldung} tone="ok" />}

      {/* ======================= Ansehen ======================= */}
      <V1Section title="Heute">
        {/* Ohne Plan (kein Flip, Autoflower) fällt die zweite Karte weg — dann
            wäre eine halbe Seite leer, deshalb hängt der Split am Plan. */}
        <div className={plan.wochen.length > 0 ? 'v1-split' : undefined}>
          <V1Card>
            <div className="v1-metric-grid compact">
              <V1Stat
                label="Soll Tag" value={grad(plan.heuteTagC)} unit="°C"
                tone={plan.heuteTagC == null ? 'neutral' : 'accent'} />
              <V1Stat
                label="Soll Nacht" value={grad(plan.heuteNachtC)} unit="°C"
                tone={plan.heuteNachtC == null ? 'neutral' : 'accent'}
                hint={hinterDerRampe ? 'Unten angekommen, bleibt so.' : 'Je Blütewoche ein Grad tiefer.'} />
              <V1Stat label="Blütewoche" value={plan.aktuelleWoche ?? '–'} />
              <V1Stat
                label="Untergrenze" value={grad(wirksamerBoden)} unit="°C"
                hint={stand.floorC == null ? 'Finish-Wert deines Profils.' : 'Von dir gesetzt.'} />
            </div>
            {/* Deckt den Null-Fall ab: „Noch keine Blüte …" kommt vom Server. */}
            {plan.luecke && <V1Alert tone="neutral" message={plan.luecke} />}
          </V1Card>

          {plan.wochen.length > 0 && (
            <V1Card>
              {/* Die Tabelle scrollt in sich, nicht die Seite — die Grid-Falle
                  aus beta.40. Die Quelle steht AUSSERHALB der Hülle, sonst
                  läge sie im Querscrollbereich. */}
              <div className="cs-tabelle-huelle">
                <table className="cs-tabelle">
                  <thead>
                    <tr>
                      <th scope="col">Blütewoche</th>
                      <th scope="col">Tag °C</th>
                      <th scope="col">Nacht °C</th>
                      <th scope="col">Hinweis</th>
                    </tr>
                  </thead>
                  <tbody>
                    {plan.wochen.map((woche) => {
                      const jetzt = woche.bluetewocheAb1 === plan.aktuelleWoche
                      return (
                        <tr key={woche.bluetewocheAb1} className={jetzt ? 'is-jetzt' : undefined}>
                          <th scope="row">{woche.bluetewocheAb1}</th>
                          <td>{grad(woche.tagC)}</td>
                          <td>{grad(woche.nachtC)}</td>
                          <td className="cs-hinweis">
                            {[jetzt ? 'jetzt' : null, woche.erreicht ? 'Untergrenze' : null]
                              .filter(Boolean).join(' · ')}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
              {hinterDerRampe && letzteWoche && (
                <p className="cs-hinter-rampe">
                  Blüte {plan.aktuelleWoche} liegt hinter der Rampe: seit Woche
                  {' '}{letzteWoche.bluetewocheAb1} gilt {grad(letzteWoche.nachtC)} °C.
                </p>
              )}
              <p className="cs-quelle">{plan.herkunft}</p>
            </V1Card>
          )}
        </div>
      </V1Section>

      {/* ======================= Stellen ======================= */}
      <form className="cs-formular" onSubmit={(e) => void speichern(e)}>
        <V1Section title="Einstellen" className="cs-einstellen">
          {/* Lautere Überschriften als die des Abschnitts: `.v1-section-head h2`
              ist die leiseste Schrift der Seite, `.v1-card h2` die gewohnte
              Kartenüberschrift. Vier gleich laute Kästen waren die Hälfte des
              Eindrucks „unstrukturiert". */}
          <V1Card>
            <h2>Sollwert an Home Assistant</h2>
            <div data-audit="night-ramp">
              <V1Switch
                label="Nachtabsenkung aktiv"
                checked={rampeAn}
                onChange={setRampeAn}
                hint="Schreibt bei Licht an und aus." />

              <div className="v1-form-grid">
                <V1Field
                  label="Zielgerät in Home Assistant"
                  hint="Thermostat oder Zahlenfeld. Leer: nur planen.">
                  <input value={ziel} onChange={(e) => setZiel(e.target.value)} placeholder="climate.chiller" />
                </V1Field>
                <V1Field
                  label="Untergrenze (°C)"
                  hint={`Leer: Finish-Wert des Profils. Nie unter ${grad(stand.hardFloorC)} °C.`}>
                  <input inputMode="decimal" value={boden} onChange={(e) => setBoden(e.target.value)} placeholder="16" />
                </V1Field>
              </div>
            </div>
          </V1Card>

          <V1Card>
            <h2>Kühler über die Steckdose</h2>
            {kuehler == null ? (
              <V1Empty
                title="Kein Zelt zugeordnet"
                text="Der Kühler hängt am Zelt. Ordne dem Grow ein Zelt zu." />
            ) : (
              <div data-audit="kuehler">
                {/* Die Bedingung steht VOR dem Schalter. Eine Bedingung liest
                    man vor der Handlung, nicht danach. */}
                <V1Alert
                  tone="warn"
                  title="Bedingung: der Kühler braucht seine eigene Grenze"
                  message={
                    'Nur nötig, wenn dein Kühler keinen Sollwert annimmt: häng ihn an die Steckdose '
                    + 'und stell ihn selbst fest ein — Faustregel etwa 15 °C, knapp unter deine '
                    + 'Untergrenze. Grow OS schaltet dann nur den Strom. Bleibt die Steckdose hängen '
                    + '(Add-on aus, Netz weg), stoppt dort sein eigener Thermostat. Tiefer stellen '
                    + 'hilft nicht: bei 5 °C wäre derselbe Fehler ein Wurzelschaden.'
                  } />

                <V1Switch
                  label="Kühler von Grow OS schalten lassen"
                  checked={kuehler.enabled}
                  onChange={(an) => setKuehler({ ...kuehler, enabled: an })} />

                <div className="v1-form-grid">
                  <V1Field
                    label="Steckdose in Home Assistant"
                    hint="Der Schalter, an dem der Kühler hängt.">
                    <input
                      value={kuehler.switchEntityId ?? ''}
                      placeholder="switch.kuehler"
                      onChange={(e) => setKuehler({ ...kuehler, switchEntityId: e.target.value })} />
                  </V1Field>
                  <V1Field
                    label="Totband (± °C)"
                    hint="An bei Soll + Wert, aus bei Soll − Wert. Erlaubt 0,1–3,0.">
                    <input
                      inputMode="decimal"
                      value={totband}
                      placeholder="0,4"
                      onChange={(e) => setTotband(e.target.value)} />
                  </V1Field>
                </div>

                {/* Die Gruppenzeile trägt „jede Minute" — ohne sie sind drei
                    Minutenfelder nebeneinander nicht zu deuten. */}
                <p className="cs-gruppe">Kompressorschutz — geprüft wird jede Minute</p>
                <div className="v1-form-grid">
                  <V1Field label="Mindestlaufzeit (Minuten)">
                    <input
                      type="number" min="1" max="60"
                      value={kuehler.minRunMinutes}
                      onChange={(e) => setKuehler({ ...kuehler, minRunMinutes: Number(e.target.value) })} />
                  </V1Field>
                  <V1Field
                    label="Mindestpause (Minuten)"
                    hint="Druckausgleich; 5 min sind Herstellerrichtwert.">
                    <input
                      type="number" min="1" max="60"
                      value={kuehler.minPauseMinutes}
                      onChange={(e) => setKuehler({ ...kuehler, minPauseMinutes: Number(e.target.value) })} />
                  </V1Field>
                  <V1Field
                    label="Messwert höchstens alt (Minuten)"
                    hint="Auf ältere Werte wird nicht geschaltet.">
                    <input
                      type="number" min="1" max="120"
                      value={kuehler.maxReadingAgeMinutes}
                      onChange={(e) => setKuehler({ ...kuehler, maxReadingAgeMinutes: Number(e.target.value) })} />
                  </V1Field>
                </div>

                <p className="cs-quelle">
                  {kuehler.lastSwitchUtc
                    ? `Zuletzt geschaltet: ${new Date(kuehler.lastSwitchUtc).toLocaleString('de-DE')}.`
                    : 'Bisher wurde nichts geschaltet.'}
                </p>
              </div>
            )}
          </V1Card>
        </V1Section>

        <div className="v1-form-actions">
          {/* type="submit" AUSDRÜCKLICH: V1Button setzt ohne Angabe
              type="button", und genau daran hing der tote Speichern-Knopf im
              Messformular. */}
          <V1Button type="submit" variant="primary" disabled={speichert} audit="cropsteering-speichern">
            {speichert ? 'Speichere…' : 'Speichern'}
          </V1Button>
          {/* Nur die zwei Fälle, in denen etwas AN ist und trotzdem nichts
              passiert. Der alte Statussatz sagte auch in den stillen Fällen,
              was Schalter und Felder zwei Zeilen darüber schon zeigen. */}
          {rampeAn && !ziel.trim() && <V1Badge tone="warn">Rampe an, aber ohne Zielgerät</V1Badge>}
          {kuehler?.enabled && !kuehler.switchEntityId?.trim()
            && <V1Badge tone="warn">Kühler an, aber ohne Steckdose</V1Badge>}
        </div>
      </form>
    </V1Page>
  )
}
