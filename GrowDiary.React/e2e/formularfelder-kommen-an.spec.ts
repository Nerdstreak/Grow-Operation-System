import { test, expect } from '@playwright/test'
import { darfUeberspringen } from './pflicht'

/**
 * Jedes Feld, das ein Formular ANBIETET, kommt auch an.
 *
 * <b>Der Anlass (25.08.2026).</b> Zwei Fehler derselben Bauart hintereinander:
 * das Flipdatum wurde verworfen, weil das Backend es nur bei Einstieg „Blüte"
 * annahm — angeboten wurde es immer. Die Rundweg-Zählung im Backend
 * (`RundwegVollstaendigTests`) fand denselben Bau danach ein zweites Mal bei
 * „Tage in Phase".
 *
 * <b>Warum das hier steht und nicht im Backend.</b> Die Backend-Zählung sieht,
 * welche Felder der Server verwirft. Sie sieht <i>nicht</i>, welche das
 * Formular anbietet — und genau in dieser Lücke lebt der Fehler. Ein Feld, das
 * dasteht und nichts bewirkt, ist schlimmer als ein fehlendes: der Nutzer hat
 * es ausgefüllt und glaubt, es sei gespeichert.
 *
 * <b>Ein Feld nach dem anderen.</b> Alle gleichzeitig zu füllen war instabil —
 * die Felder beeinflussen sich (der Startpunkt blendet Nachbarn ein und aus),
 * und ein Befund ließe sich keinem einzelnen Feld zuordnen.
 */
// NACHEINANDER, nicht parallel: beide Faelle in dieser Datei aendern DENSELBEN
// Grow und speichern ihn. Als sie in getrennten Dateien standen, liefen sie
// gleichzeitig — der eine raeumte auf, waehrend der andere las, und das
// Ergebnis hing am Zufall. Ein Test, dessen Ausgang vom Zeitpunkt abhaengt,
// hat nichts geprueft.
test.describe.configure({ mode: 'serial' })
test.setTimeout(180_000)

/** Nur echte Eingabefelder des Formulars; das Suchfeld der Hülle bleibt draußen. */
/**
 * ALLE Eingabefelder des Formulars.
 *
 * Die erste Fassung suchte `input[type="text"]` — und traf damit kein einziges
 * Textfeld: die Felder dieser Seite tragen gar kein `type`-Attribut, der
 * Browser nimmt dann still „text" an. Von acht Feldern sah die Zählung vier.
 * Gefunden vom Prüfer. Deshalb jetzt ALLE `input` unter `.v1-field`; was nicht
 * auszufüllen ist (Auswahl, Häkchen, nur-lesen), fällt in der Schleife heraus.
 */
const AUSWAHL = '.grow-wizard-shell label.v1-field input:visible'

test('was das Grow-Formular anbietet, steht nach dem Speichern noch da', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(), 'Kein Backend.')

  const vorher = await (await request.get('/api/grows/1')).json()
  const verloren: string[] = []
  let gefahren = 0

  try {
    await page.goto('/grows/1/setup', { waitUntil: 'networkidle' })
    const anzahl = await page.locator(AUSWAHL).count()
    // Vier: Pflanzen, Startdatum, Veg-Dauer, Flipdatum. „Tage in Phase" steht
    // seit dem 25.08.2026 nur da, wo es auch ankommt — bei einem Grow, der in
    // der Keimung eingestiegen ist, also nicht.
    expect(anzahl, 'Keine Felder gefunden — dieser Fall prüft dann nichts.')
      .toBeGreaterThanOrEqual(7)

    for (let i = 0; i < anzahl; i++) {
      await page.goto('/grows/1/setup', { waitUntil: 'networkidle' })
      const feld = page.locator(AUSWAHL).nth(i)
      await expect(feld).toBeVisible()

      // Nur-Lese-Felder gehoeren nicht zur Grundmenge: sie BIETEN nichts an,
      // sondern zeigen etwas. „Pflanzen" ist so eines, seit die einzeln
      // erfassten Pflanzen die Wahrheit ueber die Anzahl sind.
      if (await feld.evaluate((el: HTMLInputElement) => el.readOnly || el.disabled)) continue

      // Ohne type-Attribut ist es ein Textfeld — so liest es der Browser auch.
      const art = (await feld.getAttribute('type')) ?? 'text'
      // Häkchen und Auswahlknöpfe füllt man nicht aus.
      if (art === 'checkbox' || art === 'radio' || art === 'file') continue
      const name = (await feld.evaluate(
        (el) => (el.closest('label')?.textContent ?? '').trim().slice(0, 34))) || `Feld ${i}`
      const alt = await feld.inputValue()

      // Ein Wert, der sich vom bisherigen unterscheidet und die Fachprüfungen
      // der App einhält. Zahlen werden KLEINER gemacht: nach oben gibt es
      // echte Grenzen — „Pflanzen" über der Topfzahl sperrt das Speichern zu
      // Recht, und das wäre ein Befund über den Test, nicht über die App.
      const wert = art === 'number' ? String(Math.max(1, Number(alt || 2) - 1))
        : art === 'date' ? '2027-03-01'
          : `Probe ${i}`
      await feld.fill(wert)

      const knopf = page.getByRole('button', { name: 'Speichern' })
      if (await knopf.isDisabled()) {
        const grund = (await page.locator('.plan-finding, .v1-alert').first().innerText().catch(() => ''))
          .replaceAll(String.fromCharCode(10), ' ')
        verloren.push(`„${name}": Speichern ist gesperrt — ${grund || '(kein Grund genannt)'}`)
        continue
      }
      await knopf.click()
      const angekommen = await page.waitForURL('**/grows/1', { timeout: 20_000 })
        .then(() => true).catch(() => false)
      if (!angekommen) {
        const meldung = (await page.locator('.v1-alert').first().innerText().catch(() => ''))
          .replaceAll(String.fromCharCode(10), ' ')
        verloren.push(`„${name}": Speichern schlug fehl — ${meldung || '(keine Meldung)'}`)
        continue
      }

      gefahren++
      await page.goto('/grows/1/setup', { waitUntil: 'networkidle' })
      const steht = await page.locator(AUSWAHL).nth(i).inputValue()
      if (steht !== wert) {
        verloren.push(`„${name}": ${wert} eingetragen, danach steht ${steht || '(nichts)'}`)
      }
    }

    expect(gefahren, 'Kein einziges Feld gefahren — der Fall belegt nichts.')
      .toBeGreaterThanOrEqual(3)
    expect(verloren,
      `${verloren.length} Felder werden angeboten und nicht gespeichert:\n  ${verloren.join('\n  ')}\n\n`
      + 'Entweder das Feld annehmen — oder es gar nicht erst anbieten.')
      .toEqual([])
  } finally {
    await request.put('/api/grows/1', {
      data: { ...vorher, startDate: String(vorher.startDate ?? '').slice(0, 10) },
    })
    const zurueck = await (await request.get('/api/grows/1')).json()
    expect(zurueck.name, 'Aufräumen fehlgeschlagen.').toBe(vorher.name)
  }
})

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
