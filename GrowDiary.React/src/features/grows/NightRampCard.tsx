import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'
import { V1Alert, V1Button, V1Card, V1Field, V1Section } from '../../components/v1'

type AbsenkWoche = { bluetewocheAb1: number; tagC: number; nachtC: number; erreicht: boolean }
type Absenkplan = {
  wochen: AbsenkWoche[]
  heuteTagC: number | null
  heuteNachtC: number | null
  aktuelleWoche: number | null
  herkunft: string
  luecke: string | null
}
type NightRamp = {
  enabled: boolean
  floorC: number | null
  hardFloorC: number
  targetEntityId: string | null
  plan: Absenkplan
}

const grad = (wert: number) => `${wert.toLocaleString('de-DE', { maximumFractionDigits: 1 })} °C`

/**
 * Die Nachtabsenkung — Crop Steering über die Wassertemperatur.
 *
 * Der Plan steht hier vollständig, BEVOR etwas geschrieben wird. Eine
 * Automatik, deren Wirkung man erst am Chiller merkt, gehört nicht in eine
 * Anlage; deshalb ist die Tabelle nicht schmückendes Beiwerk, sondern der
 * eigentliche Zweck dieser Karte.
 */
export function NightRampCard({ growId }: { growId: number }) {
  const [rampe, setRampe] = useState<NightRamp | null>(null)
  const [entity, setEntity] = useState('')
  const [boden, setBoden] = useState('')
  const [busy, setBusy] = useState(false)
  const [fehler, setFehler] = useState<string | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    async function laden() {
      try {
        const geladen = await apiFetch<NightRamp>(`/api/grows/${growId}/night-ramp`, { signal: controller.signal })
        if (controller.signal.aborted) return
        setRampe(geladen)
        setEntity(geladen.targetEntityId ?? '')
        setBoden(geladen.floorC != null ? String(geladen.floorC).replace('.', ',') : '')
      } catch {
        /* Ohne Rampe bleibt die Karte einfach leer. */
      }
    }
    void laden()
    return () => controller.abort()
  }, [growId])

  async function speichern(enabled: boolean) {
    setBusy(true)
    setFehler(null)
    try {
      const zahl = boden.trim() === '' ? null : Number(boden.replace(',', '.'))
      setRampe(await apiFetch<NightRamp>(`/api/grows/${growId}/night-ramp`, {
        method: 'PUT',
        body: JSON.stringify({
          enabled,
          floorC: Number.isFinite(zahl as number) ? zahl : null,
          targetEntityId: entity.trim(),
        }),
      }))
    } catch (caught) {
      setFehler(caught instanceof Error ? caught.message : 'Konnte nicht gespeichert werden.')
    } finally {
      setBusy(false)
    }
  }

  if (!rampe) return null
  const { plan } = rampe

  return (
    <V1Section title="Nachtabsenkung · Crop Steering">
      <V1Card>
        <div data-audit="night-ramp">
          {fehler && <V1Alert title="Fehler" message={fehler} tone="warn" />}

          <p className="gc-facts">
            Die Wassertemperatur ist im RDWC der Steuerungshebel — im Substrat wären es Trockenphasen,
            die es hier nicht gibt. Die Nachttemperatur sinkt je Blütewoche um ein Grad, bis zur Untergrenze.
          </p>

          {plan.wochen.length > 0 ? (
            <>
              <div className="nr-plan">
                {plan.wochen.map((woche) => (
                  <div
                    key={woche.bluetewocheAb1}
                    className={`nr-week${woche.bluetewocheAb1 === plan.aktuelleWoche ? ' is-now' : ''}`}
                  >
                    <span className="nr-week-label">Blüte {woche.bluetewocheAb1}</span>
                    <span className="nr-week-val">{grad(woche.tagC)} / {grad(woche.nachtC)}</span>
                    {woche.erreicht && <span className="nr-week-note">Untergrenze</span>}
                  </div>
                ))}
              </div>
              <p className="nr-source">{plan.herkunft}</p>
            </>
          ) : (
            <V1Alert tone="neutral" message={plan.luecke ?? 'Kein Plan verfügbar.'} />
          )}

          {plan.luecke && plan.wochen.length > 0 && <V1Alert tone="neutral" message={plan.luecke} />}

          <div className="v1-form-grid">
            {/* Kein `wide`: das erste Feld lief ueber die ganze Seite, das
                zweite blieb winzig daneben. Nebeneinander sind beide gleich
                breit. */}
            <V1Field
              label="Zielgerät in Home Assistant"
              hint="Der Thermostat oder das Zahlenfeld, das den Sollwert annimmt — z. B. climate.chiller oder number.wasser_ziel. Leer lassen heißt: nur planen, nichts schalten."
            >
              <input value={entity} onChange={(event) => setEntity(event.target.value)} placeholder="climate.chiller" />
            </V1Field>
            <V1Field label="Untergrenze (°C)" hint={`Tiefer als ${grad(rampe.hardFloorC)} geht es nie. Ohne Angabe gilt der Finish-Nachtwert deines Profils.`}>
              <input inputMode="decimal" value={boden} onChange={(event) => setBoden(event.target.value)} placeholder="16" />
            </V1Field>
          </div>

          <div className="v1-form-actions">
            <V1Button onClick={() => void speichern(!rampe.enabled)} disabled={busy}>
              {busy ? 'Speichere…' : rampe.enabled ? 'Absenkung ausschalten' : 'Absenkung einschalten'}
            </V1Button>
            <span className="nr-state">
              {rampe.enabled
                ? rampe.targetEntityId
                  ? `Aktiv — schreibt an ${rampe.targetEntityId}, bei Licht an und Licht aus.`
                  : 'Aktiv, aber ohne Zielgerät — es wird nur geplant, nichts geschaltet.'
                : 'Aus. Die Tabelle oben ist die Vorschau.'}
            </span>
          </div>
        </div>
      </V1Card>
    </V1Section>
  )
}
