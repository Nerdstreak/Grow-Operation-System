import { test, expect } from '@playwright/test'

/**
 * Die Hydro-Übersicht bei den drei Breiten, die zählen.
 *
 * 924 px ist der kritische Fall: Wandtablet und das Home-Assistant-Ingress-Iframe
 * liegen genau dort. Die alte Fassung regelte das über drei Media-Queries, von
 * denen sich zwei mit `!important` gegenseitig überstimmten — dieser Test prüft
 * das Ergebnis statt der Regeln, damit der Umbau auf Umbruch statt Breakpoint
 * nicht unbemerkt zurückfällt.
 */
const WIDTHS = [
  { width: 1440, name: 'Desktop', nebeneinander: true },
  { width: 924, name: 'Wandtablet / HA-Ingress', nebeneinander: false },
  { width: 360, name: 'Telefon', nebeneinander: false },
]

for (const { width, name, nebeneinander } of WIDTHS) {
  test(`Hydro bei ${width}px (${name})`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 })
    await page.goto('/hydro', { waitUntil: 'networkidle' })

    const list = page.locator('.v1-hydro-list-section')
    const detail = page.locator('.v1-hydro-detail-section')
    if (await list.count() === 0) test.skip(true, 'Kein Hydro-Setup vorhanden')

    const listBox = (await list.boundingBox())!
    const detailBox = (await detail.boundingBox())!

    // Nebeneinander oder untereinander — aber nie halb überlappend.
    expect(Math.abs(listBox.y - detailBox.y) < 5, `${name}: Liste und Detail`).toBe(nebeneinander)

    // Keine Spalte darf unter ihre lesbare Mindestbreite gedrückt werden. Genau
    // das passierte mit `minmax(0, 1fr)` neben einer festen Spalte.
    expect(listBox.width, 'Liste zu schmal').toBeGreaterThanOrEqual(width < 640 ? 200 : 250)
    expect(detailBox.width, 'Detail zu schmal').toBeGreaterThanOrEqual(width < 640 ? 200 : 400)

    // Bricht die Liste um, gehört ihr die ganze Zeile — sonst steht sie schmal
    // neben einem leeren Streifen.
    if (!nebeneinander) {
      expect(Math.abs(listBox.width - detailBox.width), 'Liste füllt ihre Zeile nicht').toBeLessThan(2)
    }

    // Der Setup-Name steht in der Section-Überschrift und hat die volle Breite;
    // in der alten .95fr-Spalte brach er auf drei Zeilen.
    const heading = detail.locator('h2, h3').first()
    const headingBox = await heading.boundingBox()
    if (headingBox) {
      const lineHeight = await heading.evaluate((el) => parseFloat(getComputedStyle(el).lineHeight))
      expect(Math.round(headingBox.height / lineHeight), 'Titel bricht um').toBeLessThanOrEqual(2)
    }

    // Und die Seite scrollt nie seitwärts.
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
    expect(overflow, 'Seite läuft quer').toBeLessThanOrEqual(1)
  })
}
