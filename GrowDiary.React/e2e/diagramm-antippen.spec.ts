import { test, expect } from '@playwright/test'
import { darfUeberspringen } from './pflicht'

/**
 * Im Verlaufsdiagramm antippen und den Wert ablesen.
 *
 * **Der Anlass (01.09.2026).** Der Nutzer: „beim diagram von den live daten und
 * den verlauf ins diagramm an einer stelle klicken und dann die werte angezeigt
 * werden und beim nächsten klick werden die daten aktualisiert."
 *
 * **Warum das eine Oberflächen-Prüfung braucht.** Die Rechnung darunter
 * (`diagramm-auswahl.ts`) ist einzeln geprüft — aber der Fehler, den sie
 * verhindern soll, entsteht erst im Browser: ein Klick kommt in echten Pixeln,
 * das SVG rechnet in seinem eigenen Koordinatensystem. Beim Bauen ist mir genau
 * das passiert: gemessen an einem Diagramm, dessen Kasten 0 px breit war, und
 * fast als „geht nicht" gemeldet.
 */

test('das Verlaufsdiagramm zeigt den Wert an der angetippten Stelle', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(), 'Kein Backend.')

  await page.goto('/zelte/1', { waitUntil: 'networkidle' })

  const svg = page.locator('svg[role="img"]').first()
  await expect(svg).toBeVisible()

  // Mengenwaechter: ein Diagramm ohne Flaeche laesst sich nicht antippen, und
  // der Fall wuerde dann nichts messen.
  const kasten = await svg.boundingBox()
  expect(kasten, 'Das Diagramm hat keinen Kasten.').not.toBeNull()
  expect(kasten!.width,
    'Das Diagramm ist keine 200 px breit — bei so einem Kasten trifft jeder Tipp '
    + 'denselben Punkt, und dieser Fall belegt nichts.').toBeGreaterThan(200)

  const bildunterschrift = svg.locator('xpath=../figcaption')
  const vorher = (await bildunterschrift.innerText()).trim()

  // Links antippen.
  // Auf das SVG selbst, mit Position darin: page.mouse trifft sonst, was
  // gerade darueber liegt.
  await svg.click({ position: { x: kasten!.width * 0.3, y: kasten!.height * 0.5 } })
  const links = (await bildunterschrift.innerText()).trim()

  expect(links,
    `Nach dem Antippen steht dort unveraendert „${vorher}". Am Handy gibt es kein `
    + 'Hover — ohne diese Auswahl ist das Diagramm dort stumm.').not.toBe(vorher)

  // Und der naechste Klick setzt sie neu.
  await svg.click({ position: { x: kasten!.width * 0.75, y: kasten!.height * 0.5 } })
  const rechts = (await bildunterschrift.innerText()).trim()

  expect(rechts,
    `Der zweite Tipp weiter rechts zeigt denselben Punkt („${links}"). Entweder wird `
    + 'die Stelle nicht umgerechnet, oder die Auswahl bleibt haengen.').not.toBe(links)

  /* Und der MESSWERT steht deutsch da.
     Nur der Messwert, nicht die ganze Zeile: davor steht das Datum „28.08.",
     und ein Suchausdruck ueber alles haelt den Punkt darin fuer einen
     englischen Dezimalpunkt. Genau das ist mir hier passiert — die Pruefung
     war rot, obwohl nichts falsch war. */
  const messwert = rechts.split(/\s+/).pop() ?? ''
  expect(messwert,
    `Der Messwert in der Ablesung lautet „${messwert}" (ganze Zeile: „${rechts}") — `
    + 'eine Zahl mit Punkt neben lauter Feldern mit Komma.').not.toMatch(/\d\.\d/)
})

test('das Fadenkreuz erscheint und verschwindet mit Escape', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(), 'Kein Backend.')

  await page.goto('/zelte/1', { waitUntil: 'networkidle' })
  const svg = page.locator('svg[role="img"]').first()
  await expect(svg).toBeVisible()

  const kasten = await svg.boundingBox()
  darfUeberspringen((kasten?.width ?? 0) <= 200, 'Diagramm zu schmal.')

  await svg.click({ position: { x: kasten!.width * 0.4, y: kasten!.height * 0.5 } })
  await expect(svg.locator('line[stroke-dasharray]'),
    'Nach dem Antippen fehlt das Fadenkreuz — ohne es ist nicht zu sehen, WELCHE '
    + 'Stelle die Zahl darunter meint.').toHaveCount(1)

  /* Mit der Tastatur wieder weg. Ein Diagramm, das nur auf Tippen hoert, ist
     mit der Tastatur unbedienbar — und eine Auswahl, die man nicht mehr
     loswird, steht dem naechsten Blick im Weg. */
  await svg.focus()
  await page.keyboard.press('Escape')
  await expect(svg.locator('line[stroke-dasharray]')).toHaveCount(0)
})
