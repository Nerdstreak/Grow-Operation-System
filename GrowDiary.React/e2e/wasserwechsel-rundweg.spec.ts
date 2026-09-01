import { test, expect } from '@playwright/test'
import { darfUeberspringen } from './pflicht'
import { abraeumen, eigenenGrowAnlegen, type EigenerGrow } from './eigener-grow'

/**
 * Der Wasserwechsel: eintragen, **zurückdatieren**, wiederfinden.
 *
 * **Der Anlass.** Der Tester am 01.09.2026: „Der User findet den Wasserwechsel
 * nicht wirklich, das ist sehr umständlich von uns gelöst, weil er hat jetzt
 * einen gemacht und will den eintragen und **zurückdatieren**." Gebaut wurde
 * das in beta.60 — geprüft hat es nie jemand: `ChangeoutsPanel.tsx` stand in
 * `OHNE_RUNDWEG`, weil ein Wasserwechsel die Fälligkeiten verschiebt, gegen die
 * der Wächter im selben Bestand prüft.
 *
 * Mit einem eigenen Grow gilt der Grund nicht mehr. Und das Zurückdatieren ist
 * der Kern der Meldung: wer erst abends einträgt, was er mittags getan hat,
 * darf keinen falschen Zeitpunkt bekommen — sonst rechnet die App die nächste
 * Fälligkeit ab dem falschen Tag.
 */

let eigener: EigenerGrow | null = null

test.describe.configure({ mode: 'serial' })

test.describe('Wasserwechsel-Rundweg', () => {
  test.beforeAll(async ({ playwright, baseURL }) => {
    const api = await playwright.request.newContext({ baseURL })
    try {
      eigener = await eigenenGrowAnlegen(api, 'Rundweg-Wasserwechsel')
    } finally {
      await api.dispose()
    }
  })

  test.afterAll(async ({ playwright, baseURL }) => {
    const api = await playwright.request.newContext({ baseURL })
    try {
      await abraeumen(api, eigener)
    } finally {
      await api.dispose()
    }
  })

  test('Rundweg: ChangeoutsPanel — zurückdatiert eintragen und wiederfinden', async ({ page }) => {
    darfUeberspringen(eigener == null, 'Kein eigener Grow anlegbar — laeuft die App unter GROW_OS_URL?')

    // Der gewaehlte Grow steht in der Adresse (?growId=) — so ueberlebt er ein
    // Neuladen und laesst sich verschicken.
    await page.goto(`/wasserwechsel?growId=${eigener!.growId}`, { waitUntil: 'networkidle' })

    // Das Formular ist eingeklappt: „Wechsel erfassen" oeffnet es. Ein Rundweg,
    // der das ueberspringt, prueft ein Formular, das der Nutzer nie sieht.
    await page.getByRole('button', { name: 'Wechsel erfassen' }).click()

    // Zurueckdatieren: drei Tage her, 14:30 — genau der Fall aus der Meldung.
    const vorbei = new Date(Date.now() - 3 * 24 * 3600 * 1000)
    const jjjjmmtt = `${vorbei.getFullYear()}-${String(vorbei.getMonth() + 1).padStart(2, '0')}-${String(vorbei.getDate()).padStart(2, '0')}`
    const wann = `${jjjjmmtt}T14:30`

    const wannFeld = page.getByLabel('Wann')
    await expect(wannFeld).toBeVisible()
    await wannFeld.fill(wann)

    await page.getByLabel('Menge (L)').fill('40')
    await page.getByLabel('EC nachher').fill('1,2')
    await page.getByLabel('pH nachher').fill('5,9')

    const notiz = `Rundweg ${Date.now()}`
    await page.getByLabel('Notiz').fill(notiz)

    // Auf die ANTWORT warten, nicht auf die Anfrage: sonst liest das Neuladen
    // unten einen Stand, in dem der Eintrag noch gar nicht stehen kann.
    const antwort = page.waitForResponse(
      (r) => r.url().includes('/changeouts') && r.request().method() === 'POST')
    await page.getByRole('button', { name: 'Wasserwechsel speichern' }).click()
    const gespeichert = await antwort

    expect(gespeichert.ok(),
      `Das Speichern antwortete mit HTTP ${gespeichert.status()}.`).toBe(true)

    /* 1. Der Zeitpunkt ist der EINGETRAGENE, nicht „jetzt".
       Das ist die eigentliche Meldung des Testers. Nimmt die App still die
       aktuelle Uhrzeit, rechnet sie die naechste Faelligkeit ab dem falschen
       Tag — und der Nutzer merkt es erst, wenn die Erinnerung drei Tage zu
       spaet kommt. */
    const rumpf = await gespeichert.json()
    const gemeldet = new Date(rumpf.performedAtUtc)
    const gewollt = new Date(`${jjjjmmtt}T14:30:00`)
    const abstandStunden = Math.abs(gemeldet.getTime() - gewollt.getTime()) / 3600000

    expect(abstandStunden,
      `Eingetragen war ${wann} (Ortszeit), gespeichert ist ${rumpf.performedAtUtc}. `
      + `Das sind ${abstandStunden.toFixed(1)} Stunden Unterschied. Wer nachtraegt, `
      + 'bekommt sonst eine Faelligkeit, die ab dem falschen Tag rechnet.').toBeLessThan(1)

    // 2. Und beim naechsten Aufruf steht er in der Liste.
    await page.goto(`/wasserwechsel?growId=${eigener!.growId}`, { waitUntil: 'networkidle' })
    await expect(page.getByText(notiz, { exact: false })).toBeVisible()

    /* 3. Der Wasserwechsel-Stand rechnet ihn mit.
       Der Tester hat den Eintrag nicht zum Spass gemacht: die Karte „zuletzt
       gewechselt" ist der Grund. Zaehlt sie ihn nicht, war das Eintragen
       umsonst — und genau das war die zweite Haelfte seiner Meldung. */
    const stand = await page.request.get(`/api/grows/${eigener!.growId}/changeouts/stand`)
    expect(stand.ok(),
      `Der Wasserwechsel-Stand antwortet mit HTTP ${stand.status()}.`).toBe(true)

    const daten = await stand.json()
    expect(daten.zuletztUtc,
      'Der Stand kennt den eben eingetragenen Wechsel nicht — zuletztUtc ist leer.').not.toBeNull()
    expect(daten.tageSeit,
      `Der Wechsel war vor drei Tagen, der Stand sagt ${daten.tageSeit} Tage. Genau diese `
      + 'Zahl steht auf der Karte und entscheidet, wann die naechste Mahnung kommt.').toBe(3)
  })
})
