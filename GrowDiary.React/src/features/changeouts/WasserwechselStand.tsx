import type { WasserwechselStandDto } from '../../types'

/**
 * Der Stand des Wasserwechsels als Bild: ein Punkt je Tag.
 *
 * <b>Warum gezeichnet und nicht geschrieben.</b> „Letzter Wechsel vor 9 Tagen,
 * Plan alle 7" ist ein Satz, den man lesen und im Kopf verrechnen muss. Neun
 * Punkte, von denen zwei über den Plan hinausragen, sagt dasselbe auf einen
 * Blick — und genau das ist die Regel dieses Projekts: die Abbildung hat
 * Vorrang, der Text steht daneben.
 *
 * <b>Was ein Punkt ist.</b> Ein Tag. Die ersten <code>intervallTage</code>
 * gehören zum Plan; alles danach steht über. Ab <code>warnungAbTagen</code>
 * färbt sich der Überstand, ab <code>kritischAbTagen</code> deutlicher.
 */
export function WasserwechselStand({ stand }: { stand: WasserwechselStandDto }) {
  const tage = stand.tageSeit

  if (tage == null) {
    return (
      <div className="ww-stand is-unbekannt" data-audit="wasserwechsel-stand" data-zustand="unbekannt">
        <div className="ww-stand-zahl">
          <strong>—</strong>
          <span>noch kein Wechsel erfasst</span>
        </div>
        <p className="ww-stand-satz">
          Sobald du einen Wechsel einträgst, zählt die App ab hier — und die Mahnung
          „Wöchentlicher Wasserwechsel" richtet sich danach.
        </p>
      </div>
    )
  }

  // Immer den ganzen Plan zeigen, auch wenn erst zwei Tage vergangen sind:
  // der leere Rest IST die Auskunft „so lange ist es noch hin".
  const punkte = Math.max(stand.intervallTage, tage)
  const tageWort = tage === 1 ? 'Tag' : 'Tage'

  return (
    <div className={`ww-stand is-${stand.zustand}`} data-audit="wasserwechsel-stand" data-zustand={stand.zustand}>
      <div className="ww-stand-zahl">
        <strong>{tage}</strong>
        <span>{tageWort} seit dem letzten Wechsel</span>
      </div>

      <ol className="ww-stand-punkte" aria-hidden="true">
        {Array.from({ length: punkte }, (_, i) => {
          const tag = i + 1
          const gelaufen = tag <= tage
          const ueberPlan = tag > stand.intervallTage
          return (
            <li
              key={tag}
              className={[
                'ww-punkt',
                gelaufen ? 'is-gelaufen' : 'is-offen',
                ueberPlan ? 'is-ueber-plan' : '',
              ].filter(Boolean).join(' ')}
            />
          )
        })}
      </ol>

      {/* Die Punkte tragen aria-hidden — für Vorlesegeräte steht die Auskunft
          hier als ein Satz, statt als 9 namenlose Listenpunkte. */}
      <p className="ww-stand-satz">
        {stand.zustand === 'frisch' && `Im Plan — vorgesehen ist alle ${stand.intervallTage} Tage.`}
        {stand.zustand === 'faellig' && `Fällig: der Plan sieht alle ${stand.intervallTage} Tage vor.`}
        {stand.zustand === 'ueberfaellig' && `Überfällig: der Plan sieht alle ${stand.intervallTage} Tage vor.`}
      </p>
    </div>
  )
}
