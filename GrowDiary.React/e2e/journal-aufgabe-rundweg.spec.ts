import { test, expect } from '@playwright/test'
import { backendAntwortet, darfUeberspringen } from './pflicht'

/**
 * Eine Aufgabe aus dem Journal: anlegen, wiederfinden, wegräumen.
 *
 * **Der Anlass (02.09.2026).** Die neue Blockzählung
 * (`formularbloecke-vollstaendig.spec.ts`) fand genau einen Eingabeblock ohne
 * Rundweg und ohne Grund: `journal-task-form`. Die alte Zählung sah ihn nicht,
 * weil sie je **Datei** greift — und `JournalStreamSection.tsx` war über ihren
 * Eintrags-Rundweg schon abgedeckt.
 *
 * **Warum ausgerechnet dieser Block.** Am selben Tag kam heraus, dass eine
 * gelöschte Aufgabe eine tote Kennung in Kalibrierung und Wartung zurückliess
 * (`KeineKennungZeigtInsLeereTests`). Der Weg vom Formular bis in die
 * Aufgabenliste war dabei nirgends durchgegangen.
 *
 * **Die Priorität gehört mitgeprüft.** Sie entscheidet, wie weit oben die
 * Aufgabe steht; ein Auswahlfeld, das still auf „Normal" zurückfällt, sieht in
 * jedem Text-Rundweg richtig aus.
 */

const MARKE = `Rundweg-Aufgabe ${new Date().toISOString().slice(11, 19)}`

let aufgabeId: number | null = null

test.describe.configure({ mode: 'serial' })

test.describe('Journal-Aufgaben-Rundweg', () => {
  test.afterAll(async ({ playwright, baseURL }) => {
    const api = await playwright.request.newContext({ baseURL })
    try {
      if (aufgabeId != null) await api.delete(`/api/tasks/${aufgabeId}`)
    } finally {
      await api.dispose()
    }
  })

  test('Rundweg: journal-task-form — Aufgabe anlegen und wiederfinden', async ({ page }) => {
    darfUeberspringen(
      !(await backendAntwortet(page.request)),
      'Kein Backend unter GROW_OS_URL — die Seite zeigt dann nur einen Ladezustand.',
    )

    await page.goto('/journal', { waitUntil: 'networkidle' })

    // Der Composer ist zugeklappt — ohne den Klick gibt es das Formular nicht.
    await page.locator('[data-audit="journal-add-entry"]').click()

    const formular = page.locator('[data-audit="journal-task-form"]')
    await expect(formular).toBeVisible()

    await formular.getByLabel('Aufgabe').fill(MARKE)

    // Ein fester Zeitpunkt in der Zukunft: „faellig" muss auch morgen noch
    // stimmen, sonst haengt der Lauf an der Uhr.
    const morgen = new Date(Date.now() + 24 * 3600 * 1000)
    const jjjjmmtt = `${morgen.getFullYear()}-${String(morgen.getMonth() + 1).padStart(2, '0')}-${String(morgen.getDate()).padStart(2, '0')}`
    await formular.getByLabel('Fällig am').fill(`${jjjjmmtt}T09:00`)

    // Die Prioritaet mitnehmen: sie entscheidet ueber die Reihenfolge unter
    // „Aufgaben" und faellt still auf „Normal" zurueck, wenn sie verlorengeht.
    await formular.getByLabel('Priorität').selectOption('High')

    await formular.getByRole('button', { name: 'Aufgabe anlegen' }).click()

    // Nachlesen heisst NEU LADEN — und zwar dort, wo der Nutzer sie sucht.
    await page.goto('/aufgaben', { waitUntil: 'networkidle' })
    await expect(
      page.locator('main').last(),
      'Die Aufgabe wurde angelegt, steht nach dem Neuladen aber nicht unter „Aufgaben".',
    ).toContainText(MARKE)

    /* Aufgaben haengen am GROW: `GET /api/tasks` gibt es nicht (TasksApi hat
       nur `grows/{id}/tasks`). Gesucht wird deshalb ueber die aktiven Grows —
       auf welchem das Journal steht, entscheidet die Seite selbst. */
    const growsAntwort = await page.request.get('/api/grows?archived=false')
    expect(growsAntwort.ok()).toBeTruthy()
    const grows = (await growsAntwort.json()) as Array<{ id: number }>
    expect(grows.length, 'Kein aktiver Grow im Bestand — dann gibt es auch keine Aufgabe.')
      .toBeGreaterThan(0)

    const alle: Array<{
      id: number
      title: string
      priority?: string | null
      dueAtUtc?: string | null
      status?: string | null
    }> = []
    for (const grow of grows) {
      const antwort = await page.request.get(`/api/grows/${grow.id}/tasks`)
      if (antwort.ok()) alle.push(...(await antwort.json()))
    }

    const meine = alle.filter((a) => a.title === MARKE)
    aufgabeId = meine[0]?.id ?? null

    expect(meine.length, `„${MARKE}" ist in keiner Aufgabenliste eines aktiven Grows.`).toBe(1)
    expect(
      meine[0].priority,
      'Die Prioritaet ging still verloren — die Aufgabe steht damit an der falschen Stelle.',
    ).toBe('High')
    expect(meine[0].dueAtUtc, 'Ohne Faelligkeit taucht die Aufgabe bei den Terminen nie auf.')
      .toBeTruthy()
  })
})
