import { expect, test } from '@playwright/test'
import { darfUeberspringen } from './pflicht'

/**
 * Ein Knopf, dessen Wirkung ausserhalb des Sichtbaren liegt, ist für den
 * Nutzer kaputt.
 *
 * <b>Der Anlass.</b> Auf „Sensoren & Wartung" öffnet „Bearbeiten" ein Formular,
 * das <b>unter</b> der Geräteliste steht. Bei zwei Geräten fällt das nicht auf.
 * Der Nutzer hat sieben — bei ihm ging das Formular ausserhalb des Fensters auf
 * und nichts scrollte hin. Seine Meldung: „er reagiert nicht auf den Klick auf
 * den Bearbeiten Button." Der Knopf hat funktioniert; nur gesehen hat es
 * niemand.
 *
 * <b>Warum keine bestehende Prüfung das fand.</b> Sie hätten alle bestanden:
 * der Knopf existiert, der Zustand ändert sich, das Formular ist im DOM. Das
 * Einzige, was fehlte, war der Blick darauf, <i>wo</i> es landet. Genau die
 * Lücke, gegen die die Ergebnis-Regel in CLAUDE.md steht.
 *
 * Deshalb misst diese Datei nicht „ist es da", sondern „sieht man es".
 */

/** Ein kurzes Fenster — es ersetzt die Zeilen, die der Testbestand nicht hat. */
const FENSTER = { width: 1440, height: 600 }

test.describe('Sichtbare Wirkung', () => {
  test.use({ viewport: FENSTER })

  test('„Bearbeiten" auf /sensoren holt das Formular ins Bild', async ({ page }) => {
    darfUeberspringen(!(await page.request.get('/api/grows')).ok(),
      'Kein Backend — ohne Geräte gibt es keinen Bearbeiten-Knopf.')

    await page.goto('/sensoren', { waitUntil: 'networkidle' })

    const knoepfe = page.getByRole('button', { name: 'Bearbeiten' })
    darfUeberspringen(await knoepfe.count() === 0,
      'Kein Gerät im Bestand — Demobestand.cs sollte Hardware anlegen.')

    // Die UNTERSTE Zeile: dort ist der Weg zum Formular am weitesten.
    await knoepfe.last().click()

    const formular = page.locator('[data-audit="hardware-edit-form"]')
    await expect(formular).toBeVisible()

    // `toBeVisible` genügt hier NICHT: Playwright nennt ein Element sichtbar,
    // sobald es im Baum steht und eine Fläche hat — auch weit unterhalb des
    // Fensters. Genau so sah der Fehler aus. Also die Lage messen.
    await page.waitForTimeout(600)   // sanftes Scrollen abwarten
    const lage = await formular.evaluate((el) => {
      const r = el.getBoundingClientRect()
      return { oben: Math.round(r.top), unten: Math.round(r.bottom), fenster: window.innerHeight }
    })

    expect(lage.oben,
      `Das Formular öffnet bei y = ${lage.oben} in einem ${lage.fenster} px hohen Fenster — `
      + 'ausserhalb des Sichtbaren. Für den Nutzer passiert beim Klick nichts.')
      .toBeLessThan(lage.fenster)

    expect(lage.unten,
      'Das Formular liegt oberhalb des Fensters — auch das sieht niemand.')
      .toBeGreaterThan(0)
  })

  test('„+ Gerät anlegen" ebenso', async ({ page }) => {
    darfUeberspringen(!(await page.request.get('/api/grows')).ok(), 'Kein Backend — siehe oben.')

    await page.goto('/sensoren', { waitUntil: 'networkidle' })
    await page.getByRole('button', { name: /Gerät anlegen/ }).first().click()

    const formular = page.locator('[data-audit="hardware-edit-form"]')
    await expect(formular).toBeVisible()
    await page.waitForTimeout(600)

    const lage = await formular.evaluate((el) => {
      const r = el.getBoundingClientRect()
      return { oben: Math.round(r.top), fenster: window.innerHeight }
    })
    expect(lage.oben, `Formular bei y = ${lage.oben}, Fenster ${lage.fenster} px.`)
      .toBeLessThan(lage.fenster)
  })
})
