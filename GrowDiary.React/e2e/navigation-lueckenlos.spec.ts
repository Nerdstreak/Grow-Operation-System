import { expect, test } from '@playwright/test'

/**
 * Bei jeder Fensterbreite muss man irgendwohin klicken können.
 *
 * <b>Der Fehler, der diesen Test erzwungen hat.</b> Zwei Umschaltpunkte, die
 * nicht aneinander anschlossen: die Seitenleiste erschien erst ab 861 px
 * (`shell.css`), die Handy-Navigation verschwand schon ab 768 px
 * (`primitives-rc2.css`). Zwischen 768 und 860 px gab es damit <b>überhaupt
 * keine Navigation</b> — gemessen null sichtbare Links. In genau diesem Fenster
 * liegt ein iPad im Hochformat (768 px) und die meisten Wandtablets; wer die
 * App dort öffnete, kam von der Startseite nicht mehr weg.
 *
 * Beide Regeln für sich gelesen sehen richtig aus. Der Fehler entsteht erst
 * aus ihrem Abstand, und den sieht man nur, wenn man die Breiten durchgeht.
 */
const BREITEN = [320, 375, 414, 600, 767, 768, 800, 860, 861, 900, 1024, 1280, 1920]

for (const breite of BREITEN) {
  test(`bei ${breite} px führt ein Weg aus der Startseite heraus`, async ({ page }) => {
    await page.setViewportSize({ width: breite, height: 800 })
    await page.goto('/', { waitUntil: 'networkidle' })

    const erreichbar = await page.evaluate(() => {
      const sichtbar = (el: Element) => {
        const cs = getComputedStyle(el)
        const r = el.getBoundingClientRect()
        return cs.display !== 'none' && cs.visibility !== 'hidden' && Number(cs.opacity) > 0 && r.width > 0 && r.height > 0
      }
      // Alles zählt, was den Nutzer woandershin bringt: Links in der
      // Seitenleiste, in der Handy-Leiste, und der „Mehr"-Knopf, hinter dem
      // der Rest des Menüs liegt.
      const ziele = [...document.querySelectorAll('nav a, aside a, [data-audit="mobile-more-button"]')]
      return ziele.filter(sichtbar).length
    })

    expect(erreichbar, `Bei ${breite} px ist kein einziger Navigationspunkt sichtbar — die Seite ist eine Sackgasse.`)
      .toBeGreaterThan(0)
  })
}
