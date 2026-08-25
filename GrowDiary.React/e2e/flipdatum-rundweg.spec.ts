import { test, expect } from '@playwright/test'
import { darfUeberspringen } from './pflicht'

/**
 * Das Flipdatum: eintragen, speichern, neu laden — und wiederfinden.
 *
 * <b>Der Anlass (25.08.2026).</b> Gemeldet als „das Flipdatum wird nicht
 * übernommen". Das Formular zeigt das Feld für jeden Grow, der keine
 * Autoflower ist; das Backend nahm es nur an, wenn der Einstiegspunkt
 * <i>Blüte</i> war. Der Normalfall ist der andere — ein Grow startet in der
 * Keimung und wird später geflippt. Der Nutzer bekam HTTP 200 und einen
 * unveränderten Wert zurück.
 *
 * <b>Warum hier und nicht nur im Backend.</b> Der Backend-Fall
 * (<c>FlipdatumUeberAlleEinstiegeTests</c>) prüft den Controller. Er sieht
 * nicht, ob das Formular das Feld überhaupt zeigt und ob es den Wert
 * mitschickt — genau in dieser Lücke lag der Fehler. Und laut CLAUDE.md gilt
 * ein Formular erst als geprüft, wenn jemand es ausgefüllt, abgeschickt und
 * den Wert nach dem Neuladen wiedergefunden hat.
 */
test('Flipdatum eintragen, speichern, neu laden — und es steht noch da', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(),
    'Kein Backend — ohne Grow gibt es kein Formular.')

  const grows = await (await request.get('/api/grows')).json()
  const ziel = grows.find((g: { entryPoint: string, seedType: string }) =>
    g.entryPoint !== 'Flower' && g.seedType !== 'Autoflower')
  darfUeberspringen(!ziel,
    'Kein Grow, der ausserhalb der Blüte eingestiegen ist — dann prüft dieser Fall nichts.')

  const vorher = await (await request.get(`/api/grows/${ziel.id}`)).json()

  try {
    for (const [durchgang, datum] of [[1, '2026-06-10'], [2, '2026-06-24']] as const) {
      // Die Regel „die Reparatur einmal WIEDERHOLEN": beim zweiten Mal steht
      // schon ein Wert im Feld, und genau daran hing der Fehler.
      await page.goto(`/grows/${ziel.id}/setup`, { waitUntil: 'networkidle' })

      const feld = page.locator('input[type="date"]').nth(1)
      await expect(feld, 'Das Flipdatum-Feld fehlt im Formular.').toBeVisible()
      await feld.fill(datum)

      await page.getByRole('button', { name: 'Speichern' }).click()
      await page.waitForURL(`**/grows/${ziel.id}`, { timeout: 15_000 })

      // Nicht die Antwort glauben — neu laden und nachsehen.
      await page.goto(`/grows/${ziel.id}/setup`, { waitUntil: 'networkidle' })
      const stehtDa = await page.locator('input[type="date"]').nth(1).inputValue()
      expect(stehtDa,
        `Durchgang ${durchgang}: ${datum} eingetragen und gespeichert, im Formular steht ${stehtDa || '(nichts)'}.`)
        .toBe(datum)

      const gespeichert = await (await request.get(`/api/grows/${ziel.id}`)).json()
      expect(String(gespeichert.flipDate ?? ''),
        `Durchgang ${durchgang}: der Server hat ${datum} nicht gespeichert.`)
        .toContain(datum)
    }
  } finally {
    // Der Demobestand bleibt, wie er war (belegt: ein Rundweg mit Spuren
    // verschmutzt jede spätere Messung).
    await request.put(`/api/grows/${ziel.id}`, {
      data: { ...vorher, flipDate: String(vorher.flipDate ?? '').slice(0, 10) },
    })
    const zurueck = await (await request.get(`/api/grows/${ziel.id}`)).json()
    expect(String(zurueck.flipDate ?? ''), 'Aufräumen fehlgeschlagen.')
      .toBe(String(vorher.flipDate ?? ''))
  }
})
