import { test } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

type ViewportCase = {
  name: 'desktop' | 'mobile'
  width: number
  height: number
}

type RouteCase = {
  slug: string
  path: string
  title: string
}

type ReportRow = {
  viewport: string
  route: string
  url: string
  status: number | null
  screenshot: string
  bodyScrollWidth: number
  documentScrollWidth: number
  innerWidth: number
  horizontalOverflow: boolean
  heading: string | null
  note: string | null
  pageError: string | null
}

const viewports: ViewportCase[] = [
  { name: 'desktop', width: 1440, height: 1000 },
  { name: 'mobile', width: 390, height: 844 },
]

const routes: RouteCase[] = [
  { slug: 'dashboard', path: '/', title: 'Dashboard / Live' },
  { slug: 'addback', path: '/addback', title: 'Addback Hub' },
  { slug: 'action', path: '/action', title: 'Aktion' },
  { slug: 'zelte', path: '/zelte', title: 'Zelte' },
  { slug: 'hydro', path: '/hydro', title: 'Hydro' },
  { slug: 'home-assistant', path: '/home-assistant', title: 'Home Assistant' },
  { slug: 'grow-starten', path: '/grows/new', title: 'Grow starten' },
  { slug: 'settings', path: '/settings', title: 'Einstellungen' },
  { slug: 'wissen', path: '/wissen', title: 'Wissen' },
  { slug: 'hardware', path: '/hardware', title: 'Hardware' },
]

const repoRoot = path.resolve(process.cwd(), '..')
const outputDir = path.join(repoRoot, 'artifacts', 'visual-audit-current')
const reportRows: ReportRow[] = []
const messages: { type: string; target: string; text: string; url?: string }[] = []

function ensureCleanOutput() {
  fs.rmSync(outputDir, { recursive: true, force: true })
  fs.mkdirSync(outputDir, { recursive: true })
}

async function collectPageMetrics(page: import('@playwright/test').Page) {
  return await page.evaluate(() => {
    const firstHeading =
      document.querySelector('h1')?.textContent?.trim() ??
      document.querySelector('[data-audit-title]')?.textContent?.trim() ??
      null

    return {
      bodyScrollWidth: document.body.scrollWidth,
      documentScrollWidth: document.documentElement.scrollWidth,
      innerWidth: window.innerWidth,
      heading: firstHeading,
    }
  })
}

async function waitForAppIdle(page: import('@playwright/test').Page) {
  await page.waitForLoadState('domcontentloaded')
  await page.waitForTimeout(850)

  try {
    await page.waitForLoadState('networkidle', { timeout: 2500 })
  } catch {
    // Live-/HA-Requests dürfen noch laufen. Für Screenshots reicht der gerenderte Zustand.
  }
}

async function auditRoute(
  page: import('@playwright/test').Page,
  viewport: ViewportCase,
  route: RouteCase,
  pageError: string | null,
) {
  const response = await page.goto(route.path, { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)

  const metrics = await collectPageMetrics(page)
  const fileName = `${viewport.name}-${viewport.width}x${viewport.height}-${route.slug}.png`
  const screenshotPath = path.join(outputDir, fileName)

  await page.screenshot({ path: screenshotPath, fullPage: true })

  reportRows.push({
    viewport: `${viewport.name}-${viewport.width}x${viewport.height}`,
    route: route.path,
    url: page.url(),
    status: response?.status() ?? null,
    screenshot: fileName,
    bodyScrollWidth: metrics.bodyScrollWidth,
    documentScrollWidth: metrics.documentScrollWidth,
    innerWidth: metrics.innerWidth,
    horizontalOverflow:
      metrics.bodyScrollWidth > metrics.innerWidth ||
      metrics.documentScrollWidth > metrics.innerWidth,
    heading: metrics.heading,
    note: null,
    pageError,
  })
}

async function tryClickFirstAddbackStart(page: import('@playwright/test').Page) {
  const candidates = [
    page.locator('a[href*="/grows/"][href*="/addback"]').first(),
    page.getByRole('link', { name: /addback starten/i }).first(),
    page.getByRole('link', { name: /starten/i }).first(),
    page.getByRole('button', { name: /addback starten/i }).first(),
    page.getByRole('button', { name: /starten/i }).first(),
    page.locator('a').filter({ hasText: /addback/i }).first(),
  ]

  for (const candidate of candidates) {
    try {
      if ((await candidate.count()) === 0) {
        continue
      }

      await candidate.scrollIntoViewIfNeeded()
      await Promise.all([
        page.waitForURL(/\/grows\/[^/]+\/addback/i, { timeout: 4500 }).catch(() => null),
        candidate.click({ timeout: 3000 }),
      ])

      if (/\/grows\/[^/]+\/addback/i.test(page.url())) {
        return { opened: true, note: null }
      }
    } catch (error) {
      // nächster Kandidat
    }
  }

  return {
    opened: false,
    note:
      'Addback-Flow wurde nicht automatisch geöffnet. Wahrscheinlich kein aktiver Grow, kein eindeutiger Start-Link oder anderer Linktext.',
  }
}

async function auditAddbackDeepFlow(page: import('@playwright/test').Page, viewport: ViewportCase, pageError: string | null) {
  const response = await page.goto('/addback', { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)

  const result = await tryClickFirstAddbackStart(page)
  await waitForAppIdle(page)

  const slug = result.opened ? 'addback-flow' : 'addback-flow-not-opened'
  const metrics = await collectPageMetrics(page)
  const fileName = `${viewport.name}-${viewport.width}x${viewport.height}-${slug}.png`
  const screenshotPath = path.join(outputDir, fileName)
  await page.screenshot({ path: screenshotPath, fullPage: true })

  reportRows.push({
    viewport: `${viewport.name}-${viewport.width}x${viewport.height}`,
    route: result.opened ? '/grows/:growId/addback' : '/addback → Flow nicht geöffnet',
    url: page.url(),
    status: response?.status() ?? null,
    screenshot: fileName,
    bodyScrollWidth: metrics.bodyScrollWidth,
    documentScrollWidth: metrics.documentScrollWidth,
    innerWidth: metrics.innerWidth,
    horizontalOverflow:
      metrics.bodyScrollWidth > metrics.innerWidth ||
      metrics.documentScrollWidth > metrics.innerWidth,
    heading: metrics.heading,
    note: result.note,
    pageError,
  })
}

function writeReports() {
  fs.mkdirSync(outputDir, { recursive: true })

  const payload = {
    results: reportRows,
    messages,
  }

  fs.writeFileSync(path.join(outputDir, 'visual-audit-report.json'), JSON.stringify(payload, null, 2), 'utf8')

  const lines = [
    '# Grow OS Visual Audit',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
    '## Screenshots',
    '',
    '| Viewport | Route | Status | Overflow | Heading | Screenshot | Note | PageError |',
    '|---|---|---:|---|---|---|---|---|',
    ...reportRows.map((row) => {
      const note = row.note ? row.note.replace(/\|/g, '\\|').slice(0, 220) : ''
      const error = row.pageError ? row.pageError.replace(/\|/g, '\\|').slice(0, 160) : ''
      return `| ${row.viewport} | ${row.route} | ${row.status ?? ''} | ${row.horizontalOverflow ? 'YES' : 'no'} | ${row.heading ?? ''} | ${row.screenshot} | ${note} | ${error} |`
    }),
    '',
    '## Addback Deep Flow',
    '',
    'Der Audit versucht zusätzlich `/addback` zu öffnen und den ersten sichtbaren Addback-Start-Link zu klicken.',
    '',
    'Wenn `addback-flow-not-opened` erzeugt wird, ist das ab jetzt eine Warnung und kein Testabbruch. So bleiben die restlichen Screenshots erhalten.',
    '',
    '## Console / Network Hinweise',
    '',
    '| Typ | Ziel | Meldung | URL |',
    '|---|---|---|---|',
    ...messages.map((message) => `| ${message.type} | ${message.target} | ${message.text.replace(/\|/g, '\\|')} | ${message.url ?? ''} |`),
  ]

  fs.writeFileSync(path.join(outputDir, 'visual-audit-report.md'), lines.join('\n'), 'utf8')
}

test.describe.configure({ mode: 'serial' })

test.beforeAll(() => {
  ensureCleanOutput()
})

for (const viewport of viewports) {
  test.describe(`visual audit ${viewport.name}`, () => {
    test.beforeEach(async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height })

      page.on('requestfailed', (request) => {
        messages.push({
          type: 'requestfailed',
          target: `${viewport.name}-${viewport.width}x${viewport.height}:${page.url()}`,
          text: request.failure()?.errorText ?? 'request failed',
          url: request.url(),
        })
      })
    })

    test(`capture main routes ${viewport.name}`, async ({ page }) => {
      let pageError: string | null = null
      page.on('pageerror', (error) => {
        pageError = error.message
      })

      for (const route of routes) {
        await auditRoute(page, viewport, route, pageError)
        pageError = null
      }
    })

    test(`capture addback deep flow ${viewport.name}`, async ({ page }) => {
      let pageError: string | null = null
      page.on('pageerror', (error) => {
        pageError = error.message
      })

      await auditAddbackDeepFlow(page, viewport, pageError)
    })
  })
}

test.afterAll(() => {
  writeReports()
})
