import { test, expect } from '@playwright/test'
import { darfUeberspringen } from './pflicht'
import { abraeumen, eigenenGrowAnlegen, type EigenerGrow } from './eigener-grow'

/**
 * Die Ernteseite: ausfüllen, absenden, nachlesen — mit einem eigenen Grow.
 *
 * **Der Anlass (01.09.2026).** `HarvestPage.tsx` stand in `OHNE_RUNDWEG` mit
 * dem Grund „braucht einen Grow, der geerntet werden darf — der Rundweg würde
 * den Demobestand verändern". In genau dieser Lücke saßen an einem Tag **zwei**
 * Fehler:
 *
 * - „21,5" im Gewichtsfeld wurde zu **215** gespeichert (das Komma fiel beim
 *   Tippen weg, der Ertrag war um Faktor 10 falsch),
 * - und die Summenzeile darunter schrieb **„21.5 g"** — englisch, direkt unter
 *   einem Feld, in dem „21,5" steht.
 *
 * Beides hätte ein Rundweg gefunden. Der Grund für die Ausnahme war richtig
 * (der Bestand ist geteilt) — die Folgerung war falsch: statt keinen Rundweg zu
 * haben, legt dieser sich **seinen eigenen Grow** an und räumt ihn wieder ab.
 *
 * Damit fällt die Begründung für vier weitere Ausnahmen ebenfalls weg; sie sind
 * das nächste Stück.
 */

/** Was dieser Lauf angelegt hat — wird am Ende wieder abgeräumt. */
let eigener: EigenerGrow | null = null

test.describe.configure({ mode: 'serial' })

test.describe('Ernte-Rundweg', () => {
  test.beforeAll(async ({ playwright, baseURL }) => {
    const api = await playwright.request.newContext({ baseURL })
    try {
      eigener = await eigenenGrowAnlegen(api, 'Rundweg-Ernte')
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

  test('Rundweg: HarvestPage — ein Gewicht mit Komma kommt als Komma zurück, nicht mal zehn', async ({ page }) => {
    darfUeberspringen(eigener == null, 'Kein eigener Grow anlegbar — laeuft die App unter GROW_OS_URL?')

    await page.goto(`/grows/${eigener!.growId}/harvest`, { waitUntil: 'networkidle' })

    const nass = page.getByLabel('Nassgewicht PL-01')
    await expect(nass).toBeVisible()

    await nass.fill('21,5')
    await page.getByLabel('Trockengewicht PL-01').fill('4,25')

    // Auf die ANTWORT warten, nicht auf die Anfrage: sonst liest das Neuladen
    // unten einen Stand, in dem der Wert noch gar nicht stehen kann.
    const antwort = page.waitForResponse(
      (r) => r.url().includes(`/api/grows/${eigener!.growId}/harvest`) && r.request().method() !== 'GET')
    await page.getByRole('button', { name: 'Ernte speichern' }).click()
    const gespeichert = await antwort

    expect(gespeichert.ok(),
      `Das Speichern antwortete mit HTTP ${gespeichert.status()}.`).toBe(true)

    // 1. Was WIRKLICH gespeichert wurde — nicht, was das Formular anzeigt.
    const rumpf = await gespeichert.json()
    const zeilen = JSON.parse(rumpf.plantWeightsJson ?? '[]')
    expect(zeilen.length,
      'Die Antwort traegt keine Gewichte je Pflanze — plantWeightsJson ist leer. '
      + 'Genau so ging die Aufteilung bis beta.61 still verloren.').toBeGreaterThan(0)
    expect(zeilen[0].wetG,
      `Getippt wurde „21,5", gespeichert ist ${zeilen[0].wetG}. Faellt das Komma weg, `
      + 'steht der zehnfache Ertrag in der Bilanz.').toBeCloseTo(21.5, 3)

    /* 2. Und beim naechsten Aufruf steht es wieder mit Komma im Feld.
       ERNEUT AUFRUFEN, nicht neu laden: die Seite leitet nach dem Speichern
       auf den Grow weiter (`/grows/{id}`). Ein reload() haette also die
       Grow-Seite geladen und nichts ueber die Ernte ausgesagt — der Fall waere
       rot geworden, ohne dass an der Ernte etwas falsch ist. */
    await page.goto(`/grows/${eigener!.growId}/harvest`, { waitUntil: 'networkidle' })
    await expect(page.getByLabel('Nassgewicht PL-01')).toHaveValue('21,5')

    /* Und das GROW-Feld darueber — es wird von einem ANDEREN Formatierer
       gefuellt (formatDraftNumber statt alsText). Ohne diese Zeile deckt der
       Rundweg nur die Haelfte ab: die Pflanzenzeile kann mit Komma stehen,
       waehrend „Frischgewicht" daneben „21.5" zeigt. */
    await expect(page.getByLabel('Frischgewicht')).toHaveValue('21,5')

    // 3. Die Summe daneben ebenso — dort stand „21.5 g".
    const summe = page.locator('.hv-sums').first()
    await expect(summe).toBeVisible()
    const text = (await summe.innerText()).trim()
    expect(text,
      `In der Summenzeile steht „${text}". Eine Zahl mit Punkt neben einem Feld `
      + 'mit Komma ist derselbe Fehler, nur eine Zeile tiefer.').not.toMatch(/\d+\.\d/)
  })
})
