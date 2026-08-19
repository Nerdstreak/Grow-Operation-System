import { useEffect, useState } from 'react'
import { apiFetch } from '../../api'
import type { MeasurementAssessmentReportDto } from '../../types'

/**
 * Holt die Beurteilung des Messprotokolls.
 *
 * <b>Warum ein eigener Abruf.</b> Die Beurteilung kostet im Backend die
 * Profil-Auflösung und die Wissensbasis. Sie an die Messungsliste zu hängen
 * hieße, sie überall dort mitzuschleppen, wo nur die Zahlen gebraucht werden.
 *
 * <b>Warum sie stillschweigend ausfallen darf.</b> Fehlt sie, zeigt das
 * Protokoll seine Zahlen ohne Urteil — genau wie vorher. Ein Fehlerbalken über
 * einer Tabelle, die funktioniert, wäre schlimmer als kein Urteil.
 *
 * @param growId Der Grow, oder null wenn dieser Abschnitt gar nicht sichtbar ist.
 * @param anzahl Die Zahl der Messungen. Ändert sie sich, wird neu geholt —
 *   sonst stünde nach dem Eintragen einer Messung ein Urteil da, das sie noch
 *   nicht kennt.
 */
export function useMessBeurteilung(growId: string | null, anzahl: number): MeasurementAssessmentReportDto | null {
  const [bericht, setBericht] = useState<MeasurementAssessmentReportDto | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    if (!growId) {
      // Kein Grow, kein Bericht — aber NICHT synchron zuruecksetzen: React
      // beanstandet das zu Recht als Auslöser für eine Kaskade. Der Abbruch
      // unten räumt ohnehin auf, und ein alter Bericht wird von der nächsten
      // Antwort ersetzt.
      return () => controller.abort()
    }

    void apiFetch<MeasurementAssessmentReportDto>(`/api/grows/${growId}/measurements/assessment`, { signal: controller.signal })
      .then(setBericht)
      .catch(() => setBericht(null))

    return () => controller.abort()
  }, [growId, anzahl])

  return bericht
}
