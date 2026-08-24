import { test, expect } from '@playwright/test'
import { darfUeberspringen } from './pflicht'

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
    const neuerTopf = 9
    await karte.locator('.gp-topf input').nth(1).fill(String(neuerTopf))
    await karte.locator('.gp-topf input').nth(1).blur()
    await page.waitForTimeout(900)

    const nachTopf = (await stand()).find((p: { id: number }) => p.id === zweite.id)
    expect(nachTopf.siteIndex, 'Der Topf wurde nicht übernommen.').toBe(neuerTopf)
    expect(nachTopf.strainId,
      'Beim Topfwechsel ging die Sorte verloren — dieselbe Falle, andere Richtung.')
      .toBe(andereSorte.strainId)
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
