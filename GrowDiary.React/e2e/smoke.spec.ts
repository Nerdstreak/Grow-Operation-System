import { test, expect, type Page } from '@playwright/test'

// Every navigable route in the app. The backend is not running under this smoke
// suite, so each page is expected to reach its loading/error/empty state — but it
// must never crash while rendering. We assert: (1) no uncaught exception fires,
// and (2) the app shell + a page heading actually render (no white screen).
const ROUTES: { path: string; name: string }[] = [
  { path: '/', name: 'Live-Dashboard' },
  { path: '/messung', name: 'Messung' },
  { path: '/addback', name: 'Addback' },
  { path: '/aufgaben', name: 'Aufgaben' },
  { path: '/grows', name: 'Grows' },
  { path: '/grows/1', name: 'Grow-Detail' },
  { path: '/sorten', name: 'Sorten & Pheno' },
  { path: '/sorten?tab=pheno', name: 'Pheno Hunt (Tab)' },
  { path: '/archiv', name: 'Ernte & Archiv' },
  { path: '/archiv?tab=vergleich', name: 'Vergleich (Tab)' },
  { path: '/regeln', name: 'Regeln & Automatik' },
  { path: '/regeln?tab=grenzwerte', name: 'Grenzwerte (Tab)' },
  { path: '/regeln?tab=push', name: 'Benachrichtigungen (Tab)' },
  { path: '/regeln?tab=ki', name: 'KI-Assistent (Tab)' },
  { path: '/sensoren', name: 'Sensoren & Wartung' },
  { path: '/automatik', name: 'Automatik' },
  { path: '/messungen', name: 'Messungen-Verlauf' },
  { path: '/diagnose', name: 'Diagnose' },
  { path: '/journal', name: 'Journal & Fotos' },
  { path: '/sops', name: 'SOPs' },
  { path: '/alarme', name: 'Grenzwerte' },
  { path: '/benachrichtigungen', name: 'Benachrichtigungen' },
  { path: '/assistent', name: 'KI-Assistent' },
  { path: '/zelte', name: 'Zelte' },
  { path: '/zelte/1', name: 'Zelt-Detail' },
  { path: '/hydro', name: 'Hydro' },
  { path: '/hydro/1', name: 'Hydro-Detail' },
  { path: '/hardware', name: 'Sensoren' },
  { path: '/home-assistant', name: 'Home Assistant' },
  { path: '/wissen', name: 'Wissen' },
  { path: '/start', name: 'Erste Schritte' },
  { path: '/settings', name: 'Einstellungen' },
]

// Errors we don't care about in a backend-less smoke run: failed API fetches surface
// as console errors, and that's the expected state, not a bug.
function isExpectedNetworkNoise(text: string): boolean {
  return /Failed to load resource|Failed to fetch|NetworkError|ERR_|status of \d{3}|Load failed|api\//i.test(text)
}

async function collectPageErrors(page: Page): Promise<string[]> {
  const errors: string[] = []
  page.on('pageerror', (err) => errors.push(String(err)))
  page.on('console', (msg) => {
    if (msg.type() === 'error' && !isExpectedNetworkNoise(msg.text())) {
      errors.push(`console.error: ${msg.text()}`)
    }
  })
  return errors
}

for (const route of ROUTES) {
  test(`renders ${route.name} (${route.path}) without crashing`, async ({ page }) => {
    const errors = await collectPageErrors(page)

    await page.goto(route.path, { waitUntil: 'networkidle' })

    // App shell must be present (proves React mounted, not a white screen).
    await expect(page.locator('.v1-app-shell')).toBeVisible()
    await expect(page.getByText('Grow OS').first()).toBeVisible()

    // The route frame must have actually rendered the page (some content), not sit
    // empty — even in a loading/error/empty state the page component renders markup.
    const routeFrame = page.locator('.v1-route-frame')
    await expect(routeFrame).toBeVisible()
    await expect(routeFrame.locator(':scope > *').first()).toBeVisible()
    expect((await routeFrame.innerText()).trim().length).toBeGreaterThan(0)

    expect(errors, `Unerwartete Fehler auf ${route.path}:\n${errors.join('\n')}`).toEqual([])
  })
}

// Die alten Pfade stehen in Lesezeichen und in Home-Assistant-Dashboards. Sie muessen
// auf dem jeweiligen Tab landen statt ins Leere zu laufen. Die Erwartung steht hier
// bewusst ausgeschrieben und wird nicht aus navigation.ts importiert — sonst pruefte
// der Test die Tabelle gegen sich selbst.
const REDIRECTS: [from: string, to: string][] = [
  ['/automatik', '/regeln'],
  ['/alarme', '/regeln?tab=grenzwerte'],
  ['/benachrichtigungen', '/regeln?tab=push'],
  ['/assistent', '/regeln?tab=ki'],
  ['/phenohunt', '/sorten?tab=pheno'],
  ['/analyse', '/archiv?tab=vergleich'],
  ['/hardware', '/sensoren'],
  ['/action', '/aufgaben'],
]

for (const [from, to] of REDIRECTS) {
  test(`leitet ${from} auf ${to} um`, async ({ page }) => {
    await page.goto(from, { waitUntil: 'networkidle' })
    const url = new URL(page.url())
    expect(url.pathname + url.search).toBe(to)
  })
}

test('zeigt die vier Navigationsgruppen und die Kontextleiste', async ({ page }) => {
  await page.goto('/', { waitUntil: 'networkidle' })
  await page.setViewportSize({ width: 1440, height: 900 })

  const groups = page.locator('.v1-desktop-nav .v1-nav-group')
  await expect(groups).toHaveCount(4)
  for (const label of ['Jetzt', 'Grow', 'Anlage', 'Wissen']) {
    await expect(page.locator('.v1-nav-group-head', { hasText: label })).toBeVisible()
  }

  // Zelt und Grow werden einmal fuer die ganze App gewaehlt, nicht pro Seite.
  await expect(page.locator('[data-audit="context-bar"]')).toBeVisible()
})
