import { test, expect } from '@playwright/test'
import { darfUeberspringen } from './pflicht'

/**
 * NACHEINANDER, nicht parallel.
 *
 * Alle Faelle in dieser Datei aendern die Pflanzen desselben Grows am
 * laufenden Stand — Topf, Sorte, Anzahl. `fullyParallel` liesse sie
 * gleichzeitig laufen; dann raeumt der eine auf, waehrend der andere
 * misst, und das Ergebnis haengt am Zufall. Ein Test, dessen Ausgang vom
 * Zeitpunkt abhaengt, hat nichts geprueft.
 */
test.describe.configure({ mode: 'serial' })

/**
 * Je Topf eine eigene Sorte — und kein Feld geht dabei verloren.
 *
 * <b>Der Anlass.</b> Ein Nutzer fährt im RDWC in jedem Topf eine andere Sorte
 * und konnte das nicht angeben. Die Sorte je Pflanze gab es längst; es fehlte
 * der <i>Ort</i> — und die Auffindbarkeit.
 *
 * <b>Warum hier auch der zweite Klick zählt.</b> Das PUT auf eine Pflanze
 * überschreibt <b>alle</b> Felder. Die alte Fassung zählte sie von Hand auf;
 * ein neues Feld wäre dabei stillschweigend genullt worden, sobald jemand nur
 * die Sorte wechselt. Genau diese Klasse — „speichern, dann NOCHMAL speichern"
 * aus CLAUDE.md — prüft dieser Fall: erst die Sorte ändern, dann den Topf, und
 * nach jedem Schritt nachsehen, ob das andere Feld noch steht.
 */
test('je Pflanze Sorte UND Topf — beides überlebt die Änderung des anderen', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(),
    'Kein Backend — ohne Pflanzen gibt es nichts zu ändern.')

  const stand = async () => (await request.get('/api/plants?growId=1')).json()

  const vorher = await stand()
  darfUeberspringen(vorher.length < 2,
    'Weniger als zwei Pflanzen im Bestand — Demobestand.cs sollte vier anlegen.')

  // Die Grundmenge muss den Fall wirklich zeigen: mehrere Sorten, eigene Töpfe.
  const sorten = new Set(vorher.map((p: { strainId: number | null }) => p.strainId))
  expect(sorten.size, 'Alle Pflanzen tragen dieselbe Sorte — der Fall ist unsichtbar.')
    .toBeGreaterThan(1)
  expect(vorher.every((p: { siteIndex: number | null }) => p.siteIndex != null),
    'Nicht jede Pflanze hat einen Topf.').toBe(true)

  await page.goto('/grows/1', { waitUntil: 'networkidle' })
  const karte = page.locator('[data-audit="grow-plants"]')
  await expect(karte).toBeVisible()
  await karte.scrollIntoViewIfNeeded()

  const zweite = vorher[1]
  const andereSorte = vorher.find(
    (p: { strainId: number | null }) => p.strainId !== zweite.strainId)
  expect(andereSorte, 'Keine zweite Sorte gefunden.').toBeTruthy()

  try {
    // --- Schritt 1: die SORTE ändern. Der Topf muss stehen bleiben.
    await karte.locator('select').nth(1).selectOption(String(andereSorte.strainId))
    await page.waitForTimeout(900)

    const nachSorte = (await stand()).find((p: { id: number }) => p.id === zweite.id)
    expect(nachSorte.strainId, 'Die Sorte wurde nicht übernommen.').toBe(andereSorte.strainId)
    expect(nachSorte.siteIndex,
      `Beim Sortenwechsel ging der Topf verloren (war ${zweite.siteIndex}, ist ${nachSorte.siteIndex}). `
      + 'Das PUT überschreibt alle Felder — es muss das ganze DTO mitschicken.')
      .toBe(zweite.siteIndex)

    // --- Schritt 2, erschwert: jetzt den TOPF ändern. Die Sorte muss stehen.
    //
    // Seit dem 25.08.2026 gibt es nur Töpfe, die das System auch hat, und
    // jeden höchstens einmal. Ein frei erfundener Topf 9 wird abgelehnt —
    // richtig so. Also wird erst einer frei gemacht.
    const dritte = vorher.find(
      (p: { id: number, siteIndex: number | null }) => p.id !== zweite.id && p.siteIndex != null)
    expect(dritte, 'Keine zweite Pflanze mit Topf gefunden.').toBeTruthy()
    await request.put(`/api/plants/${dritte.id}`, { data: { ...dritte, siteIndex: null } })

    const neuerTopf = dritte.siteIndex
    await karte.locator('.gp-topf input').nth(1).fill(String(neuerTopf))
    await karte.locator('.gp-topf input').nth(1).blur()
    await page.waitForTimeout(900)

    const nachTopf = (await stand()).find((p: { id: number }) => p.id === zweite.id)
    expect(nachTopf.siteIndex, 'Der Topf wurde nicht übernommen.').toBe(neuerTopf)
    expect(nachTopf.strainId,
      'Beim Topfwechsel ging die Sorte verloren — dieselbe Falle, andere Richtung.')
      .toBe(andereSorte.strainId)

    await request.put(`/api/plants/${zweite.id}`, { data: { ...zweite, siteIndex: null } })
    await request.put(`/api/plants/${dritte.id}`, { data: dritte })
  } finally {
    // Aufräumen: der Demobestand bleibt, wie er war. Ein Rundweg, der Spuren
    // hinterlässt, verschmutzt jede spätere Messung (belegt, beta.51).
    await request.put(`/api/plants/${zweite.id}`, { data: zweite })
    const zurueck = (await stand()).find((p: { id: number }) => p.id === zweite.id)
    expect(zurueck.strainId, 'Aufräumen fehlgeschlagen.').toBe(zweite.strainId)
    expect(zurueck.siteIndex, 'Aufräumen fehlgeschlagen.').toBe(zweite.siteIndex)
  }
})

test('der Grow-Überblick behauptet bei mehreren Sorten keine einzelne', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(), 'Kein Backend — siehe oben.')

  const pflanzen = await (await request.get('/api/plants?growId=1')).json()
  const sorten = new Set(
    pflanzen.map((p: { strainName: string | null }) => p.strainName).filter(Boolean))
  darfUeberspringen(sorten.size < 2, 'Nur eine Sorte im Bestand — dann prüft dieser Fall nichts.')

  await page.goto('/grows/1', { waitUntil: 'networkidle' })
  await page.waitForTimeout(600)

  const kachel = await page.locator('[data-audit="grow-detail-summary"]').innerText()
  expect(kachel,
    `Die Kachel nennt eine einzelne Sorte, obwohl ${sorten.size} im Zelt stehen: ${kachel.slice(0, 120)}`)
    .toContain('gemischt')
})

/**
 * Nicht mehr Pflanzen als Töpfe — und ein Weg zurück.
 *
 * <b>Der Anlass (25.08.2026).</b> „Du kannst mehr Sorten angeben, als es Töpfe
 * gibt." Am laufenden Stand belegt: acht Pflanzen in einem Vier-Topf-System,
 * Topf 1 doppelt, ein Topf 999 — jedes Mal HTTP 201. Dazu gab es keinen
 * Löschweg: wer eine zu viel anlegte, behielt sie.
 *
 * <b>Warum die Oberfläche und nicht nur die API.</b> Ein gesperrter Knopf ohne
 * Grund ist ein kaputter Knopf, und eine Ablehnung, die „Eingaben konnten
 * nicht validiert werden" sagt, hilft niemandem. Beides sieht nur ein Lauf am
 * gerenderten Stand.
 */
test('sind alle Töpfe belegt, sagt die Karte es — und das Entfernen macht wieder Platz', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(), 'Kein Backend.')

  const grow = await (await request.get('/api/grows/1')).json()
  darfUeberspringen(grow.systemId == null, 'Der Grow hängt an keinem Hydro-System.')
  const system = await (await request.get(`/api/hydro-setups/${grow.systemId}`)).json()
  const toepfe: number = system.potCount
  darfUeberspringen(!toepfe, 'Das System kennt keine Töpfe.')

  const stand = async () => (await request.get('/api/plants?growId=1')).json()
  const vorher = await stand()
  darfUeberspringen(vorher.length !== toepfe,
    `Der Bestand hat ${vorher.length} Pflanzen bei ${toepfe} Töpfen — dann prüft der Fall etwas anderes.`)

  await page.goto('/grows/1', { waitUntil: 'networkidle' })
  const karte = page.locator('[data-audit="grow-plants"]')
  await karte.scrollIntoViewIfNeeded()

  // 1. Voll heisst voll — und der Grund steht daneben.
  await expect(karte.locator('.gp-bilanz')).toContainText(`${toepfe} von ${toepfe}`)
  await expect(karte.locator('.gp-neu button')).toBeDisabled()
  await expect(karte.locator('.gp-voll'),
    'Der Knopf ist gesperrt, aber niemand sagt warum.').toBeVisible()

  // 2. Ein belegter Topf wird abgelehnt — mit einem Satz, der den Topf nennt.
  const felder = karte.locator('.gp-topf input')
  await felder.nth(1).fill('1')
  await felder.nth(1).blur()
  await expect(karte.locator('.gp-fehler').first()).toContainText('Topf 1')
  await expect(karte.locator('.gp-fehler').first(),
    'Der generische Satz kommt beim Nutzer an statt der Begründung.')
    .not.toContainText('validiert werden')
  expect((await stand()).filter((p: { siteIndex: number }) => p.siteIndex === 1).length,
    'Der doppelte Topf wurde gespeichert.').toBe(1)

  // 3. Entfernen macht Platz — an einer Pflanze, die dieser Lauf selbst
  //    angelegt hat. Eine Demopflanze zu löschen und neu anzulegen gäbe ihr
  //    eine neue Id; alles, was daran hängt (Pheno-Bogen, Abstammung), wäre ab.
  //    Deshalb wird kurz ein Topf dazugegeben statt einer weggenommen.
  let angelegt: number | null = null
  try {
    await request.put(`/api/hydro-setups/${grow.systemId}`, {
      data: { ...system, potCount: toepfe + 1 },
    })
    await page.reload({ waitUntil: 'networkidle' })
    await karte.scrollIntoViewIfNeeded()
    await expect(karte.locator('.gp-neu button'),
      'Ein Topf mehr, und der Knopf lädt trotzdem nicht ein.').toBeEnabled()

    await karte.locator('.gp-neu button').click()
    await expect.poll(async () => (await stand()).length).toBe(toepfe + 1)
    angelegt = (await stand()).find(
      (p: { id: number }) => !vorher.some((v: { id: number }) => v.id === p.id))?.id ?? null
    expect(angelegt, 'Die neue Pflanze ist nicht auffindbar.').toBeTruthy()

    // Wieder voll, wieder gesperrt.
    await expect(karte.locator('.gp-neu button')).toBeDisabled()

    // Und jetzt weg damit — über den Knopf, mit Rückfrage.
    page.once('dialog', (dialog) => void dialog.accept())
    await karte.locator('.gp-weg').last().click()
    await expect.poll(async () => (await stand()).length).toBe(toepfe)
  } finally {
    if (angelegt != null) await request.delete(`/api/plants/${angelegt}`)
    await request.put(`/api/hydro-setups/${grow.systemId}`, { data: system })

    // Nachprüfen, dass wirklich nichts liegen bleibt — dieselben Ids wie vorher.
    const zurueck = await stand()
    expect(zurueck.map((p: { id: number }) => p.id).sort(),
      'Aufräumen fehlgeschlagen — der Bestand hat andere Pflanzen als vorher.')
      .toEqual(vorher.map((p: { id: number }) => p.id).sort())
    const system2 = await (await request.get(`/api/hydro-setups/${grow.systemId}`)).json()
    expect(system2.potCount, 'Die Topfzahl wurde nicht zurückgesetzt.').toBe(toepfe)
  }
})
