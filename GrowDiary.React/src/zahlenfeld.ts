/**
 * Getippten Text in eine Zahl verwandeln — an einer Stelle für die ganze App.
 *
 * **Warum es diese Datei gibt.** Sieben Seiten hatten je eine eigene Fassung
 * davon, und sie waren nicht gleich:
 *
 * - `ManualMeasurementPage` prüfte mit `Number.isFinite` und meldete unlesbare
 *   Felder — dort war der Fehler 2026-08 schon einmal behoben worden.
 * - `MeasurementEditPage` prüfte mit `Number.isNaN`. Damit gilt `Infinity` als
 *   gültige Zahl, und aus „6,2x" wird stillschweigend `null`. Die Bearbeiten-
 *   Seite ist laut `e2e/formular-rundweg.spec.ts` „der einzige Weg, auf dem ein
 *   vorhandener Wert VERSCHWINDEN kann" — genau dort stand der Fehler noch.
 * - `DosingPumpSetupPage` hatte gar keine Leerprüfung: `Number('')` ist `0` und
 *   `Number.isFinite(0)` ist `true`. Ein geleertes Feld wurde also zur **Null**.
 *   Für den Mindestabstand einer Dosierpumpe heisst das: keine Mischpause mehr,
 *   still, mit Erfolgsmeldung.
 *
 * Die Unterscheidung, um die es geht, ist **leer gegen unlesbar**. Beides ergibt
 * `null`, meint aber Verschiedenes: „ich habe nichts gemessen" gegen „ich habe
 * mich vertippt". Wer sie gleich behandelt, verliert Daten ohne ein Wort.
 */

/** Leer heisst leer — auch bei Leerzeichen. */
export function istLeer(text: string): boolean {
  return text.trim() === ''
}

/**
 * Der Zahlenwert, oder `null` bei leer **und** bei unlesbar.
 *
 * Wer wissen muss, welcher der beiden Fälle vorliegt, fragt zusätzlich
 * {@link istUnlesbar} — sonst geht ein Tippfehler als „nicht gemessen" durch.
 */
export function zahlOderNull(text: string): number | null {
  if (istLeer(text)) return null
  // `Number.isFinite`, nicht `Number.isNaN`: sonst gilt `Infinity` als Zahl und
  // landet in der Datenbank.
  const wert = Number(text.trim().replace(',', '.'))
  return Number.isFinite(wert) ? wert : null
}

/** Steht da etwas, das keine Zahl ist? */
export function istUnlesbar(text: string): boolean {
  return !istLeer(text) && zahlOderNull(text) === null
}

/**
 * Die Beschriftungen aller Felder, in denen etwas Unlesbares steht.
 *
 * @param felder Paare aus Rohtext und Beschriftung — die Beschriftung ist das,
 *   was der Nutzer sieht („pH (Reservoir)", nicht `reservoirPh`).
 */
export function unlesbareFelder(felder: Array<[string, string]>): string[] {
  return felder.filter(([roh]) => istUnlesbar(roh)).map(([, beschriftung]) => beschriftung)
}

/**
 * Ein Satz für den Nutzer, wenn Felder unlesbar sind — oder `null`.
 *
 * Er sagt ausdrücklich, was sonst passiert. „Ungültige Eingabe" allein lässt
 * offen, ob gespeichert wurde; genau diese Unklarheit hat den Fehler damals so
 * teuer gemacht.
 */
export function unlesbarMeldung(beschriftungen: string[]): string | null {
  if (beschriftungen.length === 0) return null
  if (beschriftungen.length === 1) {
    return `„${beschriftungen[0]}" ist keine Zahl. Bitte korrigieren oder das Feld leeren — `
      + 'sonst geht der Wert verloren, ohne dass es jemand merkt.'
  }
  return `Diese Felder enthalten keine Zahl: ${beschriftungen.join(', ')}. `
    + 'Bitte korrigieren oder leeren — sonst gehen die Werte verloren, ohne dass es jemand merkt.'
}
