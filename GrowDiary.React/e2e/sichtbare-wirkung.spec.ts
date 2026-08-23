import { expect, test, type Page } from '@playwright/test'
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

  test('„Bearbeiten" holt das Formular auch beim ZWEITEN Klick ins Bild', async ({ page }) => {
    darfUeberspringen(!(await page.request.get('/api/grows')).ok(), 'Kein Backend — siehe oben.')

    // <b>Der Fall, den die erste Reparatur nicht traf.</b> Sie hing an
    // `formOpen`; beim zweiten Klick ist das schon `true`, der Effekt lief
    // nicht mehr. Der Inhalt wechselte, das Formular blieb unten stehen — der
    // Tester hat das als Video geschickt, NACHDEM der erste Klick behoben war.
    //
    // Eine Prüfung, die nur einmal klickt, findet das nie. Genau so ist es
    // passiert.
    await page.goto('/sensoren', { waitUntil: 'networkidle' })

    const knoepfe = page.getByRole('button', { name: 'Bearbeiten' })
    darfUeberspringen(await knoepfe.count() < 2,
      'Weniger als zwei Geräte im Bestand — für den zweiten Klick braucht es zwei.')

    const formular = page.locator('[data-audit="hardware-edit-form"]')

    await knoepfe.first().click()
    await expect(formular).toBeVisible()
    await page.waitForTimeout(600)

    // Zurück nach oben, so wie der Nutzer es tut.
    await page.evaluate(() => {
      document.body.scrollTop = 0
      document.documentElement.scrollTop = 0
      window.scrollTo(0, 0)
    })
    await page.waitForTimeout(300)

    await knoepfe.last().click()
    await page.waitForTimeout(600)

    const lage = await formular.evaluate((el) => {
      const r = el.getBoundingClientRect()
      return { oben: Math.round(r.top), fenster: window.innerHeight }
    })

    expect(lage.oben,
      `Beim ZWEITEN Klick öffnet das Formular bei y = ${lage.oben} in einem `
      + `${lage.fenster} px hohen Fenster. Der Inhalt wechselt, aber niemand sieht es.`)
      .toBeLessThan(lage.fenster)
  })

  test('Crop Steering sagt, ob es gerade aktiv ist', async ({ page }) => {
    darfUeberspringen(!(await page.request.get('/api/grows')).ok(), 'Kein Backend — siehe oben.')

    // Rückmeldung des Testers: „dort steht nicht, wann es aktiv ist." Der Plan
    // sah im Betrieb genauso aus wie im ausgeschalteten Zustand.
    await page.goto('/cropsteering', { waitUntil: 'networkidle' })

    const ketten = page.locator('[data-audit^="kette-"]')
    await expect(ketten).toHaveCount(2)

    // Die Antwort muss OHNE Scrollen dastehen — sie ist der Grund der Seite.
    const erste = ketten.first()
    await expect(erste).toBeVisible()
    const oben = await erste.evaluate((el) => Math.round(el.getBoundingClientRect().top))
    expect(oben, `Die Kette steht bei y = ${oben} — ausserhalb des Fensters.`)
      .toBeLessThan(FENSTER.height)

    // Und sie sagt etwas Konkretes: entweder aktiv, oder WAS fehlt.
    const text = await page.locator('.cs-kurzfassung').innerText()
    expect(text, `Die Kurzfassung sagt nichts Verwertbares: „${text}"`)
      .toMatch(/Aktiv\.|Nicht aktiv\. Es fehlt:|wird nicht/)
  })

  /**
   * Dieselbe Bauform an drei weiteren Stellen — gefunden, indem die Seiten
   * daraufhin durchgesehen wurden, nicht indem jemand geklickt hat.
   *
   * Je Fall: der Auslöser sitzt in einer LISTE, das Ziel steht ausserhalb.
   * Bei zwei Einträgen sieht man beides, bei sieben nicht mehr.
   */
  //
  // NACHGEWIESEN beisst davon bisher nur der erste Fall: nimmt man den
  // scrollIntoView auf /sensoren wieder heraus, wird er rot. Beim Profil-Panel
  // auf /sollwerte bleibt er auch ohne Fix gruen — dort ist die Reparatur also
  // Vorsorge und kein belegter Fehler. Das steht hier, damit niemand spaeter
  // annimmt, beide seien bewiesen.
  const GESCHWISTER = [
    {
      name: 'Kalibrierung eintragen auf /sensoren',
      pfad: '/sensoren',
      oeffnen: async (page: Page) => {
        const zeile = page.getByRole('button', { name: /Kalibriert/ })
        if (await zeile.count() === 0) return false
        await zeile.last().click()
        return true
      },
      ziel: '[data-audit="pflege-formular"]',
    },
    {
      name: 'Profil bearbeiten auf /sollwerte',
      pfad: '/sollwerte',
      oeffnen: async (page: Page) => {
        // „Ansehen" bei mitgelieferten Profilen, „Bearbeiten" bei eigenen —
        // beide oeffnen dasselbe Panel unter der Liste.
        const knopf = page.getByRole('button', { name: /Ansehen|Bearbeiten/ })
        if (await knopf.count() === 0) return false
        await knopf.last().click()
        return true
      },
      ziel: '[data-audit="profil-panel"]',
    },
  ]

  for (const fall of GESCHWISTER) {
    test(`${fall.name} holt sein Ziel ins Bild`, async ({ page }) => {
      darfUeberspringen(!(await page.request.get('/api/grows')).ok(), 'Kein Backend — siehe oben.')

      // Ein SEHR kurzes Fenster. Der Testbestand hat zwei Geraete und keine
      // eigenen Profile; bei so wenig Inhalt liegt das Ziel ohnehin im Bild,
      // und die Pruefung waere vakuum — sie bestand auch ohne den Fix. Der
      // Nutzer hat sieben Geraete. 300 px stellen seine Lage her, ohne dass
      // der Bestand aufgeblaeht werden muss.
      await page.setViewportSize({ width: 1440, height: 300 })

      await page.goto(fall.pfad, { waitUntil: 'networkidle' })
      darfUeberspringen(!await fall.oeffnen(page),
        `Kein Auslöser auf ${fall.pfad} — im Demobestand fehlt der passende Datensatz.`)

      await page.waitForTimeout(700)

      const lage = await page.evaluate((ziel) => {
        const el = document.querySelector(ziel)
        if (!el) return null
        const r = el.getBoundingClientRect()
        return { oben: Math.round(r.top), unten: Math.round(r.bottom), fenster: window.innerHeight }
      }, fall.ziel)

      expect(lage, `Nach dem Klick gibt es ${fall.ziel} nicht.`).not.toBeNull()
      expect(lage!.oben,
        `Das Ziel öffnet bei y = ${lage!.oben} in einem ${lage!.fenster} px hohen Fenster — `
        + 'ausserhalb des Sichtbaren. Für den Nutzer passiert beim Klick nichts.')
        .toBeLessThan(lage!.fenster)
      expect(lage!.unten, 'Das Ziel liegt oberhalb des Fensters.').toBeGreaterThan(0)
    })
  }

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
