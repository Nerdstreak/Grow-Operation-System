import { test, expect, type Page } from '@playwright/test'
import { darfUeberspringen } from './pflicht'

/**
 * Kein Wort bricht mitten durch.
 *
 * <b>Der Anlass.</b> Im Dosier-Protokoll stand am Telefon „6,3" über „1" —
 * eine pH-Zahl auf zwei Zeilen verteilt, dazu „MEN/GE" und „VOR/HER" in den
 * Überschriften. Ursache: die Spalten sind Anteile (`fr`), und bei 560 px
 * Mindestbreite blieben 49 px je Zahlenspalte. Auf „Sensoren &amp; Wartung"
 * dasselbe, sobald der Testbestand von einem auf neun Geräte wuchs:
 * „HA-Senso/r", „Messg/erät".
 *
 * <b>Warum keine der vorhandenen Prüfungen das sieht.</b> Es läuft nichts
 * über, es wird nichts abgeschnitten, der Kontrast stimmt, und die Seite
 * scrollt nicht seitwärts. Gemessen an jedem Kasten ist alles in Ordnung —
 * nur lesen kann man es nicht. Es ist genau die Rückmeldung des Testers vom
 * 18.08.2026: „kleine Anzeigefehler durch zu viel Text".
 *
 * <b>Wie gemessen wird.</b> Für jedes Wort ein `Range` über seine Zeichen. Hat
 * es Kästen auf zwei Zeilen, ist es geteilt. Zwei Fälle sind dabei
 * <i>richtig</i> und werden ausgenommen:
 *
 * <ul>
 *   <li>Ein Bruch <b>nach einem Bindestrich</b> — „VPD-/Ziel" ist korrektes
 *       Deutsch, und die App setzt viele Bindestrich-Wörter.</li>
 *   <li>Ein Wort, das <b>breiter als sein Kasten</b> ist. Das muss brechen;
 *       ein Vorwurf daraus wäre eine Aufforderung, es abzuschneiden.</li>
 * </ul>
 */

const SEITEN = [
  '/', '/live', '/zelte', '/zelte/1', '/grows', '/grows/new', '/grows/1',
  '/messungen', '/messung', '/archiv', '/aufgaben', '/dosierung', '/hydro',
  '/hydro/1', '/sensoren', '/sorten', '/journal', '/diagnose', '/aushaerten',
  '/wasser', '/regeln', '/sollwerte', '/cropsteering', '/ac-test', '/einkaufsliste',
]

const BREITEN = [390, 1440]

type Bruch = { wort: string, tag: string, klasse: string, kasten: number, wortBreite: number }

async function geteilteWoerter(page: Page): Promise<Bruch[]> {
  return page.evaluate(() => {
    const raus: Array<{ wort: string, tag: string, klasse: string, kasten: number, wortBreite: number }> = []

    // Bezeichner duerfen brechen — sie sind kein Wort, sondern eine Kennung.
    const istKennung = (el: Element): boolean => {
      for (let n: Element | null = el; n && n !== document.body; n = n.parentElement) {
        const tag = n.tagName.toLowerCase()
        if (tag === 'code' || tag === 'pre' || tag === 'kbd') return true
        if (/entity|kennung|mono/i.test(n.getAttribute('class') || '')) return true
      }
      return false
    }

    for (const el of Array.from(document.querySelectorAll<HTMLElement>('main *'))) {
      if (el.children.length > 0) continue

      const knoten = el.firstChild
      if (!knoten || knoten.nodeType !== Node.TEXT_NODE) continue

      const text = knoten.textContent || ''
      if (!text.trim() || istKennung(el)) continue
      if (el.clientWidth <= 1 || el.clientHeight <= 1) continue

      // Mit Trennstrich ist ein Bruch angekuendigt und damit lesbar.
      if (getComputedStyle(el).hyphens === 'auto') continue

      const muster = /\S+/g
      let treffer: RegExpExecArray | null
      while ((treffer = muster.exec(text)) !== null) {
        const wort = treffer[0]
        if (wort.length < 4) continue

        const bereich = document.createRange()
        bereich.setStart(knoten, treffer.index)
        bereich.setEnd(knoten, treffer.index + wort.length)
        const kaesten = Array.from(bereich.getClientRects()).filter((r) => r.width > 0)
        if (kaesten.length < 2) continue
        if (new Set(kaesten.map((r) => Math.round(r.top))).size < 2) continue

        // Passt es ueberhaupt in eine Zeile? Sonst MUSS es brechen.
        const breite = kaesten.reduce((summe, r) => summe + r.width, 0)
        if (breite > el.clientWidth) continue

        // Wo genau bricht es — und steht dort ein Bindestrich?
        const ersteZeile = Math.round(kaesten[0].top)
        let bruch = -1
        for (let i = 1; i < wort.length; i++) {
          const zeichen = document.createRange()
          zeichen.setStart(knoten, treffer.index + i)
          zeichen.setEnd(knoten, treffer.index + i + 1)
          const kasten = Array.from(zeichen.getClientRects()).filter((r) => r.width > 0)[0]
          if (kasten && Math.round(kasten.top) !== ersteZeile) { bruch = i; break }
        }
        if (bruch <= 0) continue
        if (/[-/–—]/.test(wort[bruch - 1])) continue

        raus.push({
          wort,
          tag: el.tagName.toLowerCase(),
          klasse: (el.getAttribute('class') || '').slice(0, 40),
          kasten: el.clientWidth,
          wortBreite: Math.round(breite),
        })
      }
    }

    return raus
  })
}

for (const breite of BREITEN) {
  test.describe(`Worttrennung bei ${breite} px`, () => {
    test.use({ viewport: { width: breite, height: 900 } })

    for (const pfad of SEITEN) {
      test(`${pfad} — kein Wort bricht mitten durch`, async ({ page }) => {
        const antwort = await page.goto(pfad, { waitUntil: 'networkidle' })
        darfUeberspringen(
          antwort == null || antwort.status() >= 400,
          `${pfad} antwortet nicht — laeuft die App unter GROW_OS_URL?`,
        )
        await page.waitForTimeout(300)

        const laenge = await page.evaluate(() =>
          ((document.querySelector('main') as HTMLElement | null)?.innerText || '').trim().length)
        darfUeberspringen(laenge < 200, `${pfad} zeigt fast nichts — vermutlich ein Ladezustand`)

        const brueche = await geteilteWoerter(page)
        expect(
          brueche,
          brueche.map((b) => `„${b.wort}" (${b.wortBreite} px in ${b.kasten} px, ${b.tag}.${b.klasse})`).join('\n'),
        ).toEqual([])
      })
    }
  })
}
