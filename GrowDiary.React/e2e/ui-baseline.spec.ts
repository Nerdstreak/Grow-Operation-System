import { test, expect, type Page } from '@playwright/test'

/**
 * A before/after reference for the design-system refactor.
 *
 * The handoff assumes an audit pipeline this project does not have, so this stands in for
 * it: every core route at the three widths that matter, captured as a screenshot plus a
 * handful of measured facts. The point is not pixel-perfect comparison — the refactor is
 * *meant* to change how things look — but to catch the failures that are never intentional:
 * a page that stops rendering, a body that scrolls sideways, an element wider than the
 * viewport.
 *
 * 924 px is the one to watch. A wall tablet and the Home Assistant ingress iframe both land
 * there, and that is where the old layout gave way.
 */

const WIDTHS = [
  { name: '360', width: 360, height: 800 },
  { name: '924', width: 924, height: 900 },
  { name: '1440', width: 1440, height: 900 },
]

const ROUTES = [
  { path: '/', name: 'live' },
  { path: '/messung', name: 'messung' },
  { path: '/addback', name: 'addback' },
  { path: '/aufgaben', name: 'aufgaben' },
  { path: '/diagnose', name: 'diagnose' },
  { path: '/grows', name: 'grows' },
  { path: '/hydro', name: 'hydro' },
  { path: '/zelte', name: 'zelte' },
  { path: '/hardware', name: 'sensoren' },
  { path: '/automatik', name: 'automatik' },
  { path: '/alarme', name: 'alarme' },
  { path: '/benachrichtigungen', name: 'benachrichtigungen' },
  { path: '/assistent', name: 'assistent' },
  { path: '/sops', name: 'sops' },
  { path: '/wissen', name: 'wissen' },
  { path: '/settings', name: 'settings' },
]

/** Things that are never intended, at any width, in any design. */
async function structuralFacts(page: Page) {
  return page.evaluate(() => {
    const docWidth = document.documentElement.clientWidth
    const overflowing = [...document.querySelectorAll<HTMLElement>('body *')]
      .filter((element) => {
        const box = element.getBoundingClientRect()
        return box.width > 0 && box.right > docWidth + 1
      })
      .slice(0, 10)
      .map((element) => `${element.tagName.toLowerCase()}.${(element.className || '').toString().split(' ')[0]}`)

    return {
      scrollsSideways: document.documentElement.scrollWidth > docWidth + 1,
      overflowingCount: overflowing.length,
      overflowing,
      // A route frame with no children means the page rendered nothing at all.
      routeFrameChildren: document.querySelector('.v1-route-frame')?.childElementCount ?? -1,
    }
  })
}

for (const size of WIDTHS) {
  for (const route of ROUTES) {
    test(`baseline ${route.name} @ ${size.name}`, async ({ page }) => {
      await page.setViewportSize({ width: size.width, height: size.height })
      await page.goto(route.path, { waitUntil: 'networkidle' })
      await expect(page.locator('.v1-app-shell')).toBeVisible()

      await page.screenshot({
        path: `artifacts/ui-baseline/${size.name}/${route.name}.png`,
        fullPage: true,
      })

      const facts = await structuralFacts(page)
      // Recorded rather than asserted: the current state already has findings, and failing
      // the baseline run would defeat its purpose. The numbers are the reference.
      test.info().annotations.push({
        type: 'facts',
        description: JSON.stringify({ route: route.path, width: size.width, ...facts }),
      })
    })
  }
}
