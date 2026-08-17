/**
 * Zahl ↔ Eingabefeld für das Wasserprofil, deutsch gelesen.
 *
 * Die Falle, gegen die diese Datei existiert: `toLocaleString('de-DE')`
 * schreibt 1234 als „1.234" — und ein naives `replace(',', '.')` liest den
 * Tausenderpunkt danach als Dezimalpunkt. Wer hartes Wasser eintrug
 * (über 1000 µS/cm sind in deutschen Netzen real), bekam beim nächsten
 * Speichern still ein Tausendstel seines Werts. Rundweg-Sicherheit heißt:
 * `textZuZahl(zahlZuText(x)) === x`, für jedes x.
 */

/** Zahl → Feldtext: Dezimalkomma, aber ohne Tausenderpunkte. */
export function zahlZuText(value: number | null | undefined): string {
  if (value === null || value === undefined) return ''
  return value.toLocaleString('de-DE', { maximumFractionDigits: 3, useGrouping: false })
}

/** Feldtext → Zahl: versteht „5,6", „5.6", „1.234" (gruppiert) und „1.234,5". */
export function textZuZahl(value: string): number | null {
  const roh = value.trim()
  if (roh === '') return null

  let normalisiert: string
  if (roh.includes(',')) {
    // Komma vorhanden → Punkte können nur Tausendergruppen sein.
    normalisiert = roh.replace(/\./g, '').replace(',', '.')
  } else if (/^\d{1,3}(\.\d{3})+$/.test(roh)) {
    // Nur gruppierte Punkte („1.234", „12.345.678") → Tausenderpunkte entfernen.
    normalisiert = roh.replace(/\./g, '')
  } else {
    // „5.6" oder ungruppiert → Punkt ist ein Dezimalpunkt.
    normalisiert = roh
  }

  const parsed = Number(normalisiert)
  return Number.isFinite(parsed) ? parsed : null
}
