import type { AssessmentVerdict, MeasurementAssessmentDto, MeasurementAssessmentReportDto, MetricAssessmentDto } from '../../types'

/**
 * Die Anzeige-Seite der Messungs-Beurteilung.
 *
 * <b>Hier wird nicht geurteilt.</b> Das Urteil kommt fertig aus dem Backend
 * (`MeasurementAssessmentService`); diese Datei übersetzt es nur in Zeichen,
 * Klassen und Sätze. Die Trennung ist Absicht: die Zielbereiche hängen an der
 * Profil-Kette, der Wissensbasis und dem Phasen-Rechner, und ein Nachbau im
 * Browser wäre die zweite Wahrheit — genau die ist zwischen Diagnose und
 * Live-Kachel schon einmal entstanden.
 */

/**
 * Das Zeichen, das neben dem Wert steht.
 *
 * <b>Warum überhaupt ein Zeichen.</b> Farbe darf nie die einzige Auskunft
 * sein — rund acht Prozent der Männer unterscheiden Rot und Grün schlecht. Der
 * Grund ist ausdrücklich NICHT zu wenig Kontrast: der wurde gemessen und liegt
 * bei 5,84 (hell) und 11,72 (dunkel) für „im Ziel“.
 */
export function urteilZeichen(verdict: AssessmentVerdict): string {
  return verdict === 'Above' ? '▲' : verdict === 'Below' ? '▼' : ''
}

/** Die Zellklasse. `.co-td.is-good` und `.is-bad` gibt es schon. */
export function urteilKlasse(verdict: AssessmentVerdict): string {
  return verdict === 'InTarget' ? 'co-td is-good'
    : verdict === 'Above' || verdict === 'Below' ? 'co-td is-bad'
      : 'co-td'
}

/**
 * Der ausgeschriebene Satz — für Vorlesegeräte und für die Zeile, die man am
 * Telefon aufklappt. Am Telefon gibt es keinen anderen Weg zum Zielband:
 * `title` ist auf einem Berührungsbildschirm tot.
 */
export function urteilSatz(m: MetricAssessmentDto): string {
  const wert = `${m.label} ${String(m.value).replace('.', ',')}${m.unit ? ' ' + m.unit : ''}`
  if (m.verdict === 'NoTarget') return `${wert} — ${m.note}`
  if (m.verdict === 'InTarget') return `${wert} — im Ziel. ${m.note}`
  const richtung = m.verdict === 'Above' ? 'über dem Ziel' : 'unter dem Ziel'
  return `${wert} — ${richtung}. ${m.note}`
}

/** Die Beurteilung einer Messung, nach Messgröße greifbar. */
export function urteilFuer(bericht: MeasurementAssessmentReportDto | null, messungId: number): MeasurementAssessmentDto | null {
  return bericht?.measurements.find((m) => m.measurementId === messungId) ?? null
}

export function wertUrteil(zeile: MeasurementAssessmentDto | null, metrik: string): MetricAssessmentDto | null {
  return zeile?.metrics.find((m) => m.metric === metrik) ?? null
}

/** Deutsche Namen für die Herkunft. Alle vier, nicht nur die ersten zwei. */
export function herkunftWort(source: string): string {
  return source === 'HomeAssistant' ? 'Automatik'
    : source === 'Manual' ? 'Hand'
      : source === 'Imported' ? 'Importiert'
        : source === 'Derived' ? 'Abgeleitet'
          : source
}

/**
 * Die Bilanz über dem Protokoll — mit sichtbarer Rechnung.
 *
 * <b>Warum die Rechnung mit dasteht.</b> Eine nackte Zahl wie „12 daneben“
 * beantwortet die nächste Frage nicht: von wie vielen, und was ist mit den
 * Zeilen, die gar nicht zählen? Erst aussortieren, dann zählen, und beides
 * hinschreiben.
 *
 * <b>Kein Schreien.</b> Es gibt keine Note und keine Ampel für den ganzen Grow:
 * es fehlt jede Quelle dafür, wie pH gegen EC gegen VPD zu gewichten wäre, und
 * so eine Zahl könnte niemand nachprüfen.
 */
export function bilanzSatz(bericht: MeasurementAssessmentReportDto): string {
  const teile = [`${bericht.measurementCount} Messungen`]
  if (bericht.excludedCount > 0) {
    teile.push(`${bericht.excludedCount} mit unplausiblem Zeitpunkt fließen nicht ein`)
  }
  teile.push(`${bericht.checkedValueCount} Werte geprüft, ${bericht.inTargetCount} im Ziel, ${bericht.offTargetCount} daneben`)
  return teile.join(' · ')
}

/** Kurzform für die enge Spalte am Telefon. */
export function bilanzKurz(zeile: MeasurementAssessmentDto | null): string {
  if (!zeile || zeile.excluded) return ''
  const gezaehlt = zeile.metrics.filter((m) => m.verdict !== 'NoTarget')
  if (gezaehlt.length === 0) return ''
  const daneben = gezaehlt.filter((m) => m.verdict !== 'InTarget').length
  return daneben === 0 ? 'alle im Ziel' : `${daneben} von ${gezaehlt.length} daneben`
}
