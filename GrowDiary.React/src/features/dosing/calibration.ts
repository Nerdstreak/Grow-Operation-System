/**
 * Kalibrieren auf eine Zielmenge statt auf eine feste Zeit.
 *
 * Warum überhaupt: wer 23 ml im Becher abliest, liest sich leicht um 1 ml —
 * das sind 4 % Fehler, und dieser Fehler steckt danach in jeder Dosis. Bei
 * 100 ml ist derselbe Ablesefehler 1 %. Also lieber länger laufen lassen und
 * dafür genauer messen.
 *
 * Der Haken: 100 ml brauchen bei einer langsamen Pumpe mehr Zeit, als ein
 * Kalibrierlauf laufen darf. Dann wird die Zielmenge kleiner gewählt, statt
 * eine Zahl zu versprechen, die nie im Becher landet.
 */

/** So lange darf ein Kalibrierlauf höchstens laufen — Spiegelbild von DosingGuard.MaxCalibrationSeconds. */
export const MAX_CALIBRATION_SECONDS = 300

/** Von groß nach klein: die größte Menge gewinnt, die noch in die Zeit passt. */
const ZIELE = [100, 50, 25]

/** Laufzeit für eine Zielmenge, in Sekunden. Ohne bekannte Fördermenge: null. */
export function secondsForTarget(targetMl: number, mlPerMinute: number | null | undefined): number | null {
  if (mlPerMinute == null || mlPerMinute <= 0 || targetMl <= 0) return null
  return Math.round((targetMl / mlPerMinute) * 60 * 10) / 10
}

/**
 * Die größte Zielmenge, die in die erlaubte Laufzeit passt.
 * Bei einer sehr langsamen Pumpe bleibt die kleinste übrig — dann ist 25 ml
 * immer noch besser gemessen als gar nichts.
 */
export function targetForPump(mlPerMinute: number | null | undefined): number {
  const passt = ZIELE.find((ziel) => {
    const sekunden = secondsForTarget(ziel, mlPerMinute)
    return sekunden != null && sekunden <= MAX_CALIBRATION_SECONDS
  })
  return passt ?? ZIELE[ZIELE.length - 1]
}

/** Wie lange der Lauf für diese Pumpe dauert — für die Anzeige vor dem Druck. */
export function runSecondsForPump(mlPerMinute: number | null | undefined): number {
  const sekunden = secondsForTarget(targetForPump(mlPerMinute), mlPerMinute)
  return Math.min(sekunden ?? 30, MAX_CALIBRATION_SECONDS)
}
