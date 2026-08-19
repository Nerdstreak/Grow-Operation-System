/**
 * Deutsche Namen für die Messgrößen.
 *
 * <b>Wozu.</b> Auf der Diagnose-Seite stand der Enum-Name des Backends roh auf
 * dem Schirm: „Ec" statt „EC", „Ph" statt „pH", „WaterTemp" statt
 * „Wassertemperatur". Dieselbe Klasse Fehler wie die 65 rohen Symptom-Schlüssel
 * im Wissen und wie der Aufgaben-Titel, der in beta.47 gerichtet wurde — dort
 * im Backend, hier in der Oberfläche.
 *
 * <b>Groß- und Kleinschreibung ist hier fachlich.</b> pH ist nicht PH, und EC
 * schreibt sich groß. Ein unbekannter Wert wird durchgereicht statt verschluckt
 * — ein englisches Wort ist besser als ein leeres Feld.
 *
 * Die Werte stammen aus `DeviationMetric` in GrowDiary.Web/Models/GrowDeviation.cs.
 */
const NAMEN: Record<string, string> = {
  Ph: 'pH',
  Ec: 'EC',
  Orp: 'ORP',
  WaterTemp: 'Wassertemperatur',
  Vpd: 'VPD',
  Ppfd: 'PPFD',
  Co2: 'CO₂',
  DissolvedOxygen: 'Gelöster Sauerstoff',
  GerminationStatus: 'Keimung',
}

export function metrikName(wert: string | null | undefined): string {
  if (!wert) return ''
  return NAMEN[wert] ?? wert
}

/** Nur für Tests: was übersetzt ist. */
export const METRIK_NAMEN = NAMEN
