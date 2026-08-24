import { test, expect, type Page } from '@playwright/test'
import { darfUeberspringen } from './pflicht'
import { TEXTSEITEN } from './seiten'

/**
 * Kein Text darf in seinen Nachbarn laufen.
 *
 * <b>Der Anlass.</b> Im Archiv stand am Telefon „21.05.202688 T" — das
 * Erntedatum lief ohne jedes Anzeichen in die Dauer-Spalte. Ursache war eine
 * Regel, die genau richtig ist: seit dem 18.08. bricht `.co-td.is-muted`
 * bewusst nicht mitten im Wort um, weil dort „Fe/mi/ni/si/er/t" über sechs
 * Zeilen stand. Nur bricht ein <i>Datum</i> eben auch nicht — und die Spalte
 * war 87 px breit für 96 px Inhalt.
 *
 * <b>Warum die Überlauf-Prüfung das nicht gefunden hat.</b>
 * `handy-zuschnitt.spec.ts` misst, was über den <i>Seitenrand</i> ragt. Die
 * Tabelle liegt in einem Wischbereich, also ragte nichts über den Rand — der
 * Zusammenstoß fand <i>innerhalb</i> der Tabelle statt. Zwei verschiedene
 * Fehler, und die vorhandene Prüfung kann den zweiten gar nicht sehen.
 *
 * <b>Was hier ein Fehler ist und was nicht.</b> Abschneiden mit
 * Auslassungszeichen ist eine Entscheidung: man <i>sieht</i>, dass etwas fehlt
 * (so arbeitet die Phasen-Zeitachse, wo die Balkenlänge die Dauer ist).
 * Überlaufen ohne Abschneiden ist keine Entscheidung, sondern ein Unfall: der
 * Text liegt über dem Nachbarn, und niemand sieht, dass da zwei Dinge sind.
 */


const BREITEN = [360, 390, 768]

type Stoss = {
  text: string
  klasse: string
  sicht: number
  inhalt: number
}

/**
 * Zellen, deren Inhalt breiter ist als sie selbst — und die ihn nicht
 * abschneiden.
 */
async function zusammenstoesse(page: Page): Promise<Stoss[]> {
  return page.evaluate(() => {
    const raus: Array<{ text: string, klasse: string, sicht: number, inhalt: number }> = []

    for (const el of Array.from(document.querySelectorAll<HTMLElement>('main *'))) {
      // Nur Blätter: bei einem Behälter sagt scrollWidth etwas über sein
      // Innenleben, nicht über einen Zusammenstoß.
      if (el.children.length > 0) continue

      const text = (el.textContent || '').trim()
      if (!text) continue

      const stil = getComputedStyle(el)

      // Nur für Vorleseprogramme — 1 px breit und nie zu sehen.
      if (el.clientWidth <= 1 || el.clientHeight <= 1) continue
      if (el.classList.contains('sr-only')) continue

      // Wischbereiche scrollen absichtlich in sich.
      if (stil.overflowX === 'auto' || stil.overflowX === 'scroll') continue

      // Sichtbar abgeschnitten ist eine Entscheidung, kein Unfall.
      if (stil.textOverflow === 'ellipsis') continue

      // Ein Rand von 2 px faengt Rundungen ab.
      if (el.scrollWidth <= el.clientWidth + 2) continue

      raus.push({
        text: text.slice(0, 60),
        klasse: String(el.className).slice(0, 60),
        sicht: el.clientWidth,
        inhalt: el.scrollWidth,
      })
    }

    return raus
  })
}

for (const breite of BREITEN) {
  test.describe(`Zellen bei ${breite} px`, () => {
    test.use({ viewport: { width: breite, height: 900 } })

    for (const pfad of TEXTSEITEN) {
      test(`${pfad} — kein Text laeuft in den Nachbarn`, async ({ page }) => {
        const antwort = await page.goto(pfad, { waitUntil: 'networkidle' })
        darfUeberspringen(
          antwort == null || antwort.status() >= 400,
          `${pfad} antwortet nicht — laeuft die App unter GROW_OS_URL?`,
        )
        await page.waitForTimeout(300)

        // Mengenwaechter: eine Seite ohne Textknoten prueft nichts.
        const blaetter = await page.evaluate(() =>
          Array.from(document.querySelectorAll('main *'))
            .filter((el) => el.children.length === 0 && (el.textContent || '').trim()).length)
        darfUeberspringen(blaetter < 5, `${pfad} zeigt fast keinen Text — vermutlich ein Ladezustand`)

        const stoesse = await zusammenstoesse(page)
        expect(
          stoesse,
          stoesse.map((s) => `„${s.text}" braucht ${s.inhalt} px, hat ${s.sicht} px (${s.klasse})`).join('\n'),
        ).toEqual([])
      })
    }
  })
}
