import type { AssessmentVerdict, MeasurementAssessmentDto, MeasurementAssessmentReportDto, MetricAssessmentDto } from '../../types'
import { formatNumber } from '../../utils'

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
 * Wie viele Nachkommastellen eine Messgröße verträgt — an EINER Stelle.
 *
 * <b>Warum eine Tabelle.</b> Die Stellenzahl stand in der Tabellenzelle
 * (`zelle('ph', …, 2)`) und im Vorlese-Satz gar nicht — der nahm die rohe
 * Zahl. Auf dem Bildschirm stand „5,95“, vorgelesen wurde „5,953333333333333“
 * und im Aufklapp-Text stand dasselbe. Zwei Wahrheiten für eine Zahl.
 *
 * <b>Die Schlüssel sind abgeschrieben, nicht erfunden</b> — sie stammen aus
 * `MeasurementAssessmentService`, wo sie erzeugt werden. `do` heißt der
 * gelöste Sauerstoff, nicht `o2`.
 */
export const NACHKOMMASTELLEN: Record<string, number> = {
  ph: 2,
  ec: 2,
  'water-temp': 1,
  'air-temp': 1,
  humidity: 0,
  vpd: 2,
  orp: 0,
  do: 2,
  co2: 0,
  ppfd: 0,
}

/** Eine Stelle als Rückfall — lieber zu grob als roh. */
export function stellenFuer(metrik: string): number {
  return NACHKOMMASTELLEN[metrik] ?? 1
}

/**
 * Das Zeichen, das neben dem Wert steht.
 *
 * <b>Warum überhaupt ein Zeichen.</b> Farbe darf nie die einzige Auskunft
 * sein — rund acht Prozent der Männer unterscheiden Rot und Grün schlecht. Der
 * Grund ist ausdrücklich NICHT zu wenig Kontrast: der wurde gemessen und liegt
 * bei 5,84 (hell) und 11,72 (dunkel) für „im Ziel“.
 */
export function urteilZeichen(verdict: AssessmentVerdict): string {
  if (verdict === 'Impossible') return '⚠'
  return verdict === 'Above' ? '▲' : verdict === 'Below' ? '▼' : ''
}

/**
 * Die Zellklasse. `.co-td.is-good` und `.is-bad` gibt es schon.
 *
 * <b>„Unmöglich" ist nicht „daneben".</b> Ein Wert über dem Ziel ist ein
 * Befund am Grow, ein Wert von 9000 °C ist ein Befund am Messgerät. Deshalb
 * eine eigene Klasse und nicht `.is-bad`: sonst stünde ein Tippfehler in
 * derselben Reihe wie eine echte Abweichung und würde die Bilanz verzerren.
 */
export function urteilKlasse(verdict: AssessmentVerdict): string {
  return verdict === 'InTarget' ? 'co-td is-good'
    : verdict === 'Impossible' ? 'co-td is-unmoeglich'
      : verdict === 'Above' || verdict === 'Below' ? 'co-td is-bad'
        : 'co-td'
}

/**
 * Der ausgeschriebene Satz — für Vorlesegeräte und für die Zeile, die man am
 * Telefon aufklappt. Am Telefon gibt es keinen anderen Weg zum Zielband:
 * `title` ist auf einem Berührungsbildschirm tot.
 */
export function urteilSatz(m: MetricAssessmentDto): string {
  // Dieselbe Zahl wie in der Zelle daneben — nicht die rohe.
  const wert = `${m.label} ${formatNumber(m.value, stellenFuer(m.metric))}${m.unit ? ' ' + m.unit : ''}`
  const notiz = grossAnfangen(m.note)
  if (m.verdict === 'Impossible') return `${wert} — den Wert kann es nicht geben. ${notiz}`
  if (m.verdict === 'NoTarget') return `${wert} — ${notiz}`
  if (m.verdict === 'InTarget') return `${wert} — im Ziel. ${notiz}`
  const richtung = m.verdict === 'Above' ? 'über dem Ziel' : 'unter dem Ziel'
  return `${wert} — ${richtung}. ${notiz}`
}

/**
 * Die Notiz beginnt einen neuen Satz und muss groß anfangen.
 *
 * Im Backend ist sie ein Halbsatz („dein Grenzwert 5,8–6,2.“), weil sie dort
 * auch hinter einem Komma stehen kann. Hier steht ein Punkt davor — gelesen
 * hat das „im Ziel. dein Grenzwert 5,8–6,2.“ ergeben.
 */
function grossAnfangen(text: string): string {
  return text.length === 0 ? text : text[0].toUpperCase() + text.slice(1)
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
  // Getrennt von „daneben“ genannt: das ist kein Befund am Grow, sondern einer
  // am Messgerät, und wer ihn nicht sieht, sucht die Ursache an der Pflanze.
  if (bericht.impossibleCount > 0) {
    teile.push(`${bericht.impossibleCount} unmöglich — bitte nachsehen`)
  }
  return teile.join(' · ')
}

/** Kurzform für die enge Spalte am Telefon. */
export function bilanzKurz(zeile: MeasurementAssessmentDto | null): string {
  if (!zeile) return ''

  // Zuerst das Unmögliche — und AUCH bei ausgeschlossenen Zeilen. Genau hier
  // war die Lücke: die Zeile mit EC 99.999 sah in der Zeitachse aus wie jede
  // andere, weil `excluded` und `NoTarget` beide zu „sag nichts“ führten.
  const unmoegliche = zeile.metrics.filter((m) => m.verdict === 'Impossible')
  const unmoeglich = unmoegliche.length
  // Bei EINEM Wert den Namen nennen statt „1 Wert": am Telefon stehen nur
  // Luft und Feuchte in der Zeile — ist die Wassertemperatur der unmögliche
  // Wert, sähe der Nutzer eine Warnung ohne die Zahl dazu und wüsste nicht,
  // wo er nachsehen soll.
  const unmoeglichWort = unmoeglich === 1
    ? `${unmoegliche[0].label} unmöglich`
    : `${unmoeglich} Werte unmöglich`

  if (zeile.excluded) return unmoeglich > 0 ? unmoeglichWort : ''

  const gezaehlt = zeile.metrics.filter((m) => m.verdict !== 'NoTarget' && m.verdict !== 'Impossible')
  if (gezaehlt.length === 0) return unmoeglich > 0 ? unmoeglichWort : ''

  const daneben = gezaehlt.filter((m) => m.verdict !== 'InTarget').length
  const satz = daneben === 0 ? 'alle im Ziel' : `${daneben} von ${gezaehlt.length} daneben`
  return unmoeglich > 0 ? `${unmoeglichWort} · ${satz}` : satz
}
