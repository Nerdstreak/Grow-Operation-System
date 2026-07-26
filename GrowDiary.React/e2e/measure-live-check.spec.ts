import { test, expect } from '@playwright/test'

/**
 * Die Prüfung neben dem Messformular muss beim *Tippen* reagieren, nicht erst
 * beim Speichern — das ist ihr ganzer Zweck. Deshalb tippt dieser Test wirklich,
 * statt nur den Anfangszustand anzusehen.
 */
test('prüft die Werte während der Eingabe', async ({ page }) => {
  await page.goto('/messung', { waitUntil: 'networkidle' })

  const panel = page.locator('[data-audit="live-check"], .chk-idle')
  await expect(panel.first()).toBeVisible()

  const ph = page.getByLabel('pH', { exact: true }).first()
  if (await ph.count() === 0) test.skip(true, 'Kein pH-Feld — vermutlich kein Hydro-Grow im Testdatenbestand')
  await expect(ph).toBeVisible()

  await ph.fill('9,5')
  // Ein pH von 9,5 liegt weit ausserhalb jedes Zielbands und muss als kritisch
  // erscheinen, ohne dass irgendetwas gespeichert wurde. Auf die pH-Zeile
  // eingegrenzt, weil im Testbestand ohnehin andere Werte danebenliegen.
  await expect(page.locator('.chk-row.is-crit').filter({ hasText: 'pH 9,5' })).toBeVisible()

  await ph.fill('6,0')
  await expect(page.locator('.chk-row.is-ok')).toContainText('pH')
})

test('sagt vor der ersten Eingabe, was passieren wird', async ({ page }) => {
  await page.goto('/messung', { waitUntil: 'networkidle' })
  // Entweder es liegen schon Live-Werte an (dann prüft es sofort), oder der
  // Hinweis erklärt, worauf es wartet. Ein leerer Kasten wäre die schlechteste
  // der drei Möglichkeiten.
  const hasFindings = await page.locator('[data-audit="live-check"]').count()
  const hasHint = await page.locator('.chk-idle').count()
  expect(hasFindings + hasHint).toBeGreaterThan(0)
})
