import { test, expect } from '@playwright/test'
import { backendAntwortet, darfUeberspringen } from './pflicht'

/**
 * Das Geräteformular: anlegen, wiederfinden — und **ein zweites Mal** öffnen.
 *
 * **Der Anlass (02.09.2026).** `HardwarePage.tsx` stand in `OHNE_RUNDWEG` mit
 * dem Grund: „Eine angelegte Kalibrierung erzeugt still eine Aufgabe und
 * verändert damit die Aufgabenseite, gegen die andere Prüfungen laufen."
 *
 * Der Grund war richtig — nur ist er kein Grund, das *Geräte*formular
 * ungeprüft zu lassen. Ein Gerät ohne Grow erzeugt keine Aufgabe, und was
 * dieser Rundweg anlegt, räumt er hinterher weg.
 *
 * Beim Wegräumen der Ausnahme kam ein echter Fehler heraus: das Löschen eines
 * Vorgangs ließ seine Erinnerung in der Aufgabenliste stehen
 * (`WerDieKalibrierungLoeschtLoeschtDieAufgabeTests`). Wer eine Ausnahme
 * ausräumt, sieht das, was dahinter lag.
 *
 * **Zweimal öffnen.** Am 24.08.2026 hat derselbe Knopf beim *zweiten* Klick
 * nicht mehr reagiert — der Fix hing an einer Zustandsflanke (`formOpen`), und
 * die gibt es nur einmal. Diese Seite hat genau so eine Flanke, also klickt
 * dieser Rundweg zweimal.
 */

const MARKE = `Rundweg-Geraet ${new Date().toISOString().slice(11, 19)}`

let angelegteIds: number[] = []

test.describe.configure({ mode: 'serial' })

test.describe('Hardware-Rundweg', () => {
  test.afterAll(async ({ playwright, baseURL }) => {
    const api = await playwright.request.newContext({ baseURL })
    try {
      for (const id of angelegteIds) {
        await api.delete(`/api/hardware-items/${id}`)
      }
    } finally {
      await api.dispose()
    }
  })

  test('Rundweg: HardwarePage — anlegen, neu laden, wiederfinden', async ({ page }) => {
    /* Zuerst das Backend, dann die Seite: /hardware liefert auch ohne Backend
       200 und zeigt einen Ladezustand. Wer das nicht trennt, bekommt „Element
       nicht gefunden" und sucht den Fehler in der Seite. */
    darfUeberspringen(
      !(await backendAntwortet(page.request)),
      'Kein Backend unter GROW_OS_URL — die Seite zeigt dann nur einen Ladezustand.',
    )

    const antwort = await page.goto('/hardware', { waitUntil: 'networkidle' })
    darfUeberspringen(antwort == null || antwort.status() >= 400, '/hardware antwortet nicht.')

    await page.getByRole('button', { name: '+ Gerät anlegen' }).click()

    const formular = page.locator('[data-audit="hardware-edit-form"]')
    await expect(formular).toBeVisible()

    await formular.getByLabel('Name').fill(MARKE)
    await formular.getByLabel('Kategorie').fill('Sensor')
    await formular.getByLabel('Hersteller').fill('Rundweg')
    // Eine ZAHL mit — sonst prueft der Rundweg nur Text, und der stille
    // Datenverlust bei 21 Zahlenfeldern war genau die Klasse, die der Tester
    // gefunden hat.
    await formular.getByLabel('Kalibrieren alle (Tage)').fill('21')

    await formular.getByRole('button', { name: 'Hardware anlegen' }).click()

    // Nachlesen heisst NEU LADEN. Was nur im Zustand der Seite steht, ist nicht
    // gespeichert — zwei Fehler dieser Klasse hat der Tester schon gemeldet.
    await page.goto('/hardware', { waitUntil: 'networkidle' })
    await expect(page.locator('[data-audit="hardware-table"]')).toContainText(MARKE)

    // Und der Wert ist auch wirklich angekommen, nicht nur der Name.
    const gespeichert = await page.request.get('/api/hardware-items')
    expect(gespeichert.ok()).toBeTruthy()
    const liste = (await gespeichert.json()) as Array<{
      id: number
      name: string
      manufacturer?: string | null
      calibrationIntervalDays?: number | null
    }>
    const meines = liste.filter((g) => g.name === MARKE)
    angelegteIds = meines.map((g) => g.id)

    expect(meines.length, `„${MARKE}" ist nach dem Neuladen nicht in /api/hardware-items.`).toBe(1)
    expect(meines[0].manufacturer, 'Der Hersteller ging beim Speichern verloren.').toBe('Rundweg')
    expect(
      meines[0].calibrationIntervalDays,
      'Das Zahlenfeld „Kalibrieren alle (Tage)" ging still verloren — der Name kam an, die Zahl nicht.',
    ).toBe(21)
  })

  test('Rundweg: HardwarePage — dasselbe Formular ein ZWEITES Mal', async ({ page }) => {
    darfUeberspringen(angelegteIds.length === 0, 'Der erste Durchgang hat nichts angelegt.')

    await page.goto('/hardware', { waitUntil: 'networkidle' })

    /* Erschwerte Umstaende, wie es die Regel verlangt: erst BEARBEITEN eines
       vorhandenen Geraets — das setzt `editingId` UND `formOpen` —, dann
       abbrechen, dann ANLEGEN. Wer nur zweimal denselben Knopf drueckt, sieht
       den Fall „schon offen mit fremdem Inhalt" nie. */
    const formular = page.locator('[data-audit="hardware-edit-form"]')

    await page.getByRole('row', { name: MARKE })
      .getByRole('button', { name: 'Bearbeiten' }).click()
    await expect(formular).toBeVisible()
    await expect(
      formular.getByLabel('Name'),
      'Bearbeiten oeffnet das Formular leer statt mit dem Geraet.',
    ).toHaveValue(MARKE)

    await formular.getByRole('button', { name: 'Abbrechen' }).click()
    await expect(formular).toBeHidden()

    await page.getByRole('button', { name: '+ Gerät anlegen' }).click()
    await expect(
      formular,
      'Nach Bearbeiten + Abbrechen ging „+ Gerät anlegen" nicht mehr auf. '
        + 'Genau diese Zustandsflanke war schon einmal der Fehler (24.08.2026).',
    ).toBeVisible()

    /* Und es ist LEER, nicht mit dem eben bearbeiteten Geraet gefuellt. Bliebe
       `editingId` stehen, ueberschriebe „Hardware anlegen" das vorhandene
       Geraet, statt ein neues anzulegen — ein stiller Datenverlust. */
    await expect(
      formular.getByLabel('Name'),
      'Das Formular oeffnet mit dem eben bearbeiteten Geraet. Wer jetzt speichert, '
        + 'ueberschreibt es, statt ein neues anzulegen.',
    ).toHaveValue('')
  })
})
