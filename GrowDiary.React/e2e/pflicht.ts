import { test } from '@playwright/test'

/**
 * Ein übersprungener Test ist kein bestandener.
 *
 * <b>Der Anlass.</b> Die E2E-Mappe fährt im Tor gegen einen statischen Server
 * ohne Backend. Von 34 Fällen aus vier Dateien sind dort <b>31 übersprungen und
 * 3 grün</b> — und im Bericht steht „275 passed", was niemand als Warnung
 * liest. Genau die Prüfung, die das leere Archiv gefunden hätte, lief nie mit;
 * gefunden hat es der Tester.
 *
 * <b>Was diese Datei ändert.</b> Ein Übersprung braucht ab jetzt einen Grund,
 * und mit <c>E2E_STRENG=1</c> wird er zum Fehler statt zur stillen Zeile. Im
 * Tor läuft die Mappe gegen die laufende App mit vollem Demobestand — dort
 * darf sich nichts mehr wegducken. Auf dem Entwicklungsrechner, wo oft kein
 * Backend läuft, bleibt der Übersprung erlaubt.
 */

/** Läuft dieser Durchgang streng? */
export const streng = process.env.E2E_STRENG === '1'

/**
 * Überspringen — aber nur, wenn es erlaubt ist.
 *
 * @param bedingung Wahr heißt: es fehlt etwas, der Test kann nicht laufen.
 * @param grund Ausgeschrieben, in ganzen Worten. Steht im Bericht und ist im
 *   strengen Lauf die Fehlermeldung — „kein Grow" hilft dort niemandem,
 *   „kein laufender Grow im Bestand, obwohl der Demobestand einen anlegen
 *   sollte" schon.
 */
export function darfUeberspringen(bedingung: boolean, grund: string): void {
  if (!bedingung) return

  if (streng) {
    throw new Error(
      `Übersprungen wegen „${grund}" — im strengen Lauf ist das ein Fehler.\n`
      + 'Das Tor fährt gegen die laufende App mit vollem Demobestand; wenn hier etwas fehlt, '
      + 'fehlt es im Demobestand (GrowDiary.Web/Services/Demobestand.cs) oder die App läuft nicht.',
    )
  }

  test.skip(true, grund)
}
