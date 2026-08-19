import { test, expect } from '@playwright/test'
import { darfUeberspringen } from './pflicht'

/**
 * Die Prüfung neben dem Messformular muss beim *Tippen* reagieren, nicht erst
 * beim Speichern — das ist ihr ganzer Zweck. Deshalb tippt dieser Test wirklich,
 * statt nur den Anfangszustand anzusehen.
 *
 * In der CI ist die Datenbank leer — dann zeigt die Seite ihren Leer-Zustand
 * statt des Formulars. Das ist korrekt und kein Fehler; das Tipp-Szenario wird
 * dann übersprungen, statt an einem fehlenden Formular zu scheitern.
 */
test('prüft die Werte während der Eingabe', async ({ page }) => {
  await page.goto('/messung', { waitUntil: 'networkidle' })

  if (await page.locator('[data-audit="measurement-empty-state"]').count() > 0) {
    darfUeberspringen(true,
      'Kein Grow im Bestand — /messung zeigt den Leer-Zustand. Im Tor soll der Demobestand '
      + 'einen laufenden Grow anlegen; fehlt er, wird die Live-Prüfung nie geprüft.')
  }

  const panel = page.locator('[data-audit="live-check"], .chk-idle')
  await expect(panel.first()).toBeVisible()

  const ph = page.getByLabel('pH', { exact: true }).first()
  darfUeberspringen(await ph.count() === 0,
    'Kein pH-Feld — der Grow im Bestand ist kein Hydro-Grow. Der Demobestand muss einen '
    + 'RDWC-Grow anlegen, sonst gibt es die pH-Zeile nicht, um die es hier geht.')
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
  // Drei gültige Zustände: es liegen Live-Werte an (dann prüft es sofort), der
  // Hinweis erklärt, worauf es wartet, oder es gibt noch keinen Grow und die
  // Seite sagt das. Nur ein leerer Kasten wäre falsch.
  const hasFindings = await page.locator('[data-audit="live-check"]').count()
  const hasHint = await page.locator('.chk-idle').count()
  const hasEmptyState = await page.locator('[data-audit="measurement-empty-state"]').count()
  expect(hasFindings + hasHint + hasEmptyState).toBeGreaterThan(0)
})
