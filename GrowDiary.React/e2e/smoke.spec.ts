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
  { path: '/analyse', name: 'Vergleich' },
  { path: '/archiv', name: 'Archiv' },
  { path: '/automatik', name: 'Automatik' },
  { path: '/messungen', name: 'Messungen-Verlauf' },
  { path: '/diagnose', name: 'Diagnose' },
  { path: '/journal', name: 'Journal & Fotos' },
  { path: '/sops', name: 'SOPs' },
  { path: '/alarme', name: 'Grenzwerte' },
  { path: '/benachrichtigungen', name: 'Benachrichtigungen' },
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
