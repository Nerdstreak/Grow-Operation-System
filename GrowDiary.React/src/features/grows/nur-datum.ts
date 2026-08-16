/**
 * Ein API-Datum in das einzige Format, das `input[type="date"]` anzeigen kann.
 *
 * Der Fehler dahinter kam aus dem Feld: die API liefert „2026-05-20T00:00:00",
 * das Datumsfeld kennt nur „2026-05-20" — und zeigt bei allem anderen LEER.
 * Der Tester setzte ein Startdatum, speicherte, öffnete erneut: scheinbar weg.
 * Gespeichert war es die ganze Zeit; nur das Formular konnte es nicht zeigen.
 */
export function nurDatum(wert: string | null | undefined): string | null {
  if (!wert) return null
  const datum = wert.slice(0, 10)
  return /^\d{4}-\d{2}-\d{2}$/.test(datum) ? datum : null
}
