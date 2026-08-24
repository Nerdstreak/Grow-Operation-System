import { test, expect, type Page } from '@playwright/test'
import { darfUeberspringen } from './pflicht'

/**
 * Auf dem Schirm steht kein englischer Dezimalpunkt.
 *
 * <b>Der Anlass.</b> Am Zelt-Verlauf standen die Achsen auf „5.80" und
 * „1.24" — englische Punkte in einer deutschen Oberfläche, seit es die
 * Diagramme gibt. Der Grund war banal: <c>toFixed()</c> und <c>String()</c>
 * schreiben immer mit Punkt.
 *
 * <b>Warum der vorhandene Test das nicht gesehen hat.</b> Es gibt
 * <c>DeutscheZahlenTests.cs</c> — der prüft aber Texte, die das <i>Backend</i>
 * erzeugt. Eine Zahl, die im Browser aus einer JavaScript-Zahl entsteht,
 * kommt dort nie vorbei. Beide Seiten brauchen dieselbe Prüfung, sonst deckt
 * die eine ab, was die andere durchlässt.
 *
 * <b>Was ausgenommen ist und warum.</b> Nicht alles mit einem Punkt zwischen
 * Ziffern ist eine Zahl: Entitäts-Kennungen (<c>sensor.demo_reservoir_ph</c>),
 * Versionsnummern (<c>2.0.0-beta.54</c>), Adressen und Uhrzeiten mit Sekunden
 * gehören dazu. Ausgenommen wird deshalb <b>über den Zusammenhang</b> — Code,
 * Links, Monospace-Kennungen — und nicht über eine Liste von Zahlen, die man
 * gerade nicht sehen will.
 */

const SEITEN = [
  '/', '/live', '/zelte/1', '/messungen', '/archiv', '/grows/1', '/aufgaben',
  '/dosierung', '/wasser', '/diagnose', '/aushaerten', '/sollwerte',
  '/cropsteering', '/hydro/1', '/sensoren', '/einkaufsliste',
]

/** Eine Zahl mit Punkt zwischen Ziffern: 5.80, 1.24, 24.0 */
const PUNKTZAHL = /(?<![\w.])\d+\.\d+(?![\w.])/

type Fund = { text: string, klasse: string, tag: string }

async function punktzahlen(page: Page): Promise<Fund[]> {
  return page.evaluate(() => {
    const muster = /(?<![\w.])\d+\.\d+(?![\w.])/
    const raus: Array<{ text: string, klasse: string, tag: string }> = []

    // Kennungen und Quelltext sind keine Zahlen — sie stehen bewusst so da.
    const istKennung = (el: Element): boolean => {
      for (let n: Element | null = el; n && n !== document.body; n = n.parentElement) {
        const tag = n.tagName.toLowerCase()
        if (tag === 'code' || tag === 'pre' || tag === 'kbd' || tag === 'a') return true
        const klasse = n.getAttribute('class') || ''
        if (/entity|kennung|mono|url|version/i.test(klasse)) return true
      }
      return false
    }

    const lauf = document.createTreeWalker(
      document.querySelector('main') ?? document.body, NodeFilter.SHOW_TEXT)

    for (let knoten = lauf.nextNode(); knoten; knoten = lauf.nextNode()) {
      const text = (knoten.textContent || '').trim()
      if (!text || !muster.test(text)) continue

      const el = knoten.parentElement
      if (!el || istKennung(el)) continue

      // Was niemand sieht, stoert auch niemanden.
      const stil = getComputedStyle(el)
      if (stil.display === 'none' || stil.visibility === 'hidden') continue

      raus.push({
        text: text.slice(0, 80),
        klasse: (el.getAttribute('class') || '').slice(0, 50),
        tag: el.tagName.toLowerCase(),
      })
    }

    return raus
  })
}

for (const pfad of SEITEN) {
  test(`${pfad} — keine Zahl mit englischem Punkt`, async ({ page }) => {
    const antwort = await page.goto(pfad, { waitUntil: 'networkidle' })
    darfUeberspringen(
      antwort == null || antwort.status() >= 400,
      `${pfad} antwortet nicht — laeuft die App unter GROW_OS_URL?`,
    )
    await page.waitForTimeout(400)

    // Mengenwaechter: hat die Seite ueberhaupt etwas gezeigt?
    //
    // BEWUSST ueber die Textlaenge und NICHT ueber die Anzahl der Ziffern. Der
    // erste Anlauf verlangte fuenf Ziffern und liess /sollwerte durchfallen —
    // die Seite listet Profile und zeigt Zahlen erst, wenn man eines oeffnet.
    // Eine Seite ohne Zahlen ist kein Ladezustand, sondern eine Seite ohne
    // Zahlen; ein Waechter, der das verwechselt, meldet Fehler, wo keine sind.
    const laenge = await page.evaluate(() =>
      ((document.querySelector('main') as HTMLElement | null)?.innerText || '').trim().length)
    darfUeberspringen(laenge < 200, `${pfad} zeigt fast nichts — vermutlich ein Ladezustand`)

    const funde = await punktzahlen(page)
    expect(
      funde,
      funde.map((f) => `„${f.text}" in <${f.tag} class="${f.klasse}">`).join('\n'),
    ).toEqual([])
  })
}

test('die Suche findet einen englischen Punkt ueberhaupt', () => {
  // Der Bissnachweis im Test selbst: ohne ihn koennte das Muster still
  // nichts treffen und alle Seiten waeren gruen.
  expect(PUNKTZAHL.test('zuletzt 5.80')).toBe(true)
  expect(PUNKTZAHL.test('zuletzt 5,80')).toBe(false)
  expect(PUNKTZAHL.test('sensor.demo_reservoir_ph')).toBe(false)
  expect(PUNKTZAHL.test('2.0.0-beta.54')).toBe(false)
})
