import { useEffect, useState } from 'react'

/**
 * Ist das Fenster breiter als die angegebene Grenze?
 *
 * <b>Wozu.</b> Manche Umbauten lassen sich nicht mit CSS allein machen: die
 * Messliste ist am Telefon eine Zeitachse und am Schreibtisch eine Tabelle —
 * das sind zwei verschiedene Auszeichnungen, nicht zwei Anstriche desselben.
 * Beide gleichzeitig in die Seite zu schreiben und eine per CSS auszublenden
 * hiesse, jede Messung doppelt in den Baum zu setzen; Vorlesegeräte lesen dann
 * alles zweimal.
 *
 * <b>Vorsicht.</b> Das hier ist die Ausnahme, nicht der Weg. Was mit einer
 * Medienabfrage in CSS geht, gehoert in die Stildatei — dort kostet es kein
 * Neuzeichnen und keine Zustandsvariable.
 */
export function useAbBreite(px: number): boolean {
  const [passt, setPasst] = useState(() =>
    typeof window !== 'undefined' && !!window.matchMedia && window.matchMedia(`(min-width: ${px}px)`).matches)

  useEffect(() => {
    if (typeof window === 'undefined' || !window.matchMedia) return
    const abfrage = window.matchMedia(`(min-width: ${px}px)`)
    const merke = () => setPasst(abfrage.matches)
    merke()
    abfrage.addEventListener('change', merke)
    return () => abfrage.removeEventListener('change', merke)
  }, [px])

  return passt
}
