import { expect, test } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

type ViewportCase = {
  name: string
  width: number
  height: number
  category: 'desktop' | 'phone' | 'tablet'
  enforceMobileHardChecks?: boolean
}

type RouteCase = {
  slug: string
  path: string
  title: string
}

type AuditTent = {
  id: number
  name: string
  status?: string | null
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
  bottomNavFindings: LayoutFinding[]
  touchTargetFindings: AuditFinding[]
  safeAreaFindings: AuditFinding[]
  navStructureFindings: AuditFinding[]
  tabletLayoutFindings: AuditFinding[]
}

type LayoutFinding = {
  selector: string
  text: string
  problem: string
  top: number
  bottom: number
  bottomNavTop: number | null
  bottomNavGap: number | null
}

type AuditFinding = {
  selector: string
  text: string
  problem: string
  details: string
}

const viewports: ViewportCase[] = [
  { name: 'desktop', width: 1440, height: 1000, category: 'desktop' },
  { name: 'mobile', width: 390, height: 844, category: 'phone', enforceMobileHardChecks: true },
  { name: 'iphone17-near', width: 393, height: 852, category: 'phone' },
  { name: 'phone-plus', width: 430, height: 932, category: 'phone' },
  { name: 'ipad-portrait', width: 768, height: 1024, category: 'tablet' },
  { name: 'ipad-landscape', width: 1024, height: 768, category: 'tablet' },
]

const routes: RouteCase[] = [
  { slug: 'dashboard', path: '/', title: 'Dashboard / Live' },
  { slug: 'addback', path: '/addback', title: 'Addback Hub' },
  { slug: 'action', path: '/action', title: 'Aufgaben Legacy Route' },
  { slug: 'aufgaben', path: '/aufgaben', title: 'Aufgaben Redirect Route' },
  { slug: 'messung', path: '/messung', title: 'Messung erfassen' },
  { slug: 'zelte', path: '/zelte', title: 'Zelte' },
  { slug: 'zelte-new', path: '/zelte/new', title: 'Zelt anlegen' },
  { slug: 'hydro', path: '/hydro', title: 'Hydro' },
  { slug: 'hydro-new', path: '/hydro/new', title: 'Hydro anlegen' },
  { slug: 'home-assistant', path: '/home-assistant', title: 'Home Assistant' },
  { slug: 'connect', path: '/connect', title: 'Gerät verbinden' },
  { slug: 'grows', path: '/grows', title: 'Grows' },
  { slug: 'grow-new', path: '/grows/new', title: 'Grow starten' },
  { slug: 'settings', path: '/settings', title: 'Einstellungen' },
  { slug: 'wissen', path: '/wissen', title: 'Wissen' },
  { slug: 'release', path: '/release', title: 'Release' },
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

    function selectorFor(element: Element) {
      if (element.id) return `#${element.id}`
      const className = Array.from(element.classList).slice(0, 3).join('.')
      return `${element.tagName.toLowerCase()}${className ? `.${className}` : ''}`
    }

    function isBottomNavCandidate(element: HTMLElement) {
      const style = window.getComputedStyle(element)
      return (
        style.position === 'fixed' ||
        style.position === 'sticky' ||
        Boolean(element.closest('.sticky-actions, .ops1b-sticky-actions')) ||
        Boolean(element.matches('button, a, input, select, textarea, .v1-card, .v1-section, .v1-wizard-step, .v1-form-actions, .v1-action-row, .grow-select-card'))
      )
    }

    const bottomNav = document.querySelector('.v1-bottom-nav') as HTMLElement | null
    const bottomNavRect = bottomNav?.getBoundingClientRect() ?? null
    const mobileTopbar = document.querySelector('.v1-mobile-topbar') as HTMLElement | null
    const topbarRect = mobileTopbar?.getBoundingClientRect() ?? null
    const routeFrame = document.querySelector('.v1-route-frame') as HTMLElement | null
    const routeFrameRect = routeFrame?.getBoundingClientRect() ?? null
    const safeTop = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--safe-top') || '0')
    const safeBottom = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--safe-bottom') || '0')
    const isPhone = window.innerWidth < 768
    const isTablet = window.innerWidth >= 768 && window.innerWidth < 1024
    const bottomNavFindings = bottomNavRect
      ? Array.from(document.querySelectorAll('button, a, input, select, textarea, .v1-card, .v1-section, .v1-wizard-step, .v1-form-actions, .v1-action-row, .grow-select-card'))
        .map((element) => {
          const html = element as HTMLElement
          const rect = html.getBoundingClientRect()
          const style = window.getComputedStyle(html)
          const sampleX = Math.min(Math.max(rect.left + Math.min(rect.width / 2, 24), 0), window.innerWidth - 1)
          const sampleY = Math.min(Math.max(rect.bottom - 2, 0), window.innerHeight - 1)
          const elementAtPoint = document.elementFromPoint(sampleX, sampleY)
          const pointVisible = Boolean(elementAtPoint && (html === elementAtPoint || html.contains(elementAtPoint)))
          const visible = style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1 && rect.bottom > 0 && rect.top < window.innerHeight && pointVisible
          const ignored = Boolean(html.closest('.v1-bottom-nav, .v1-mobile-more-panel')) || html.hasAttribute('hidden') || html.getAttribute('aria-hidden') === 'true'
          const candidate = visible && !ignored && isBottomNavCandidate(html)
          const covered = candidate && rect.bottom > bottomNavRect.top + 4 && rect.top < bottomNavRect.bottom - 4
          const tooClose = candidate && rect.bottom > bottomNavRect.top - 12 && rect.top < bottomNavRect.top
          return {
            selector: selectorFor(html),
            text: (html.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 100),
            problem: covered ? 'coveredByBottomNav' : tooClose ? 'tooCloseToBottomNav' : '',
            top: Math.round(rect.top),
            bottom: Math.round(rect.bottom),
            bottomNavTop: Math.round(bottomNavRect.top),
            bottomNavGap: Math.round(bottomNavRect.top - rect.bottom),
          }
        })
        .filter((item) => item.problem)
      : []

    const touchTargetFindings = Array.from(document.querySelectorAll('button, a, input, select, textarea, [role="button"], [role="link"]'))
      .map((element) => {
        const html = element as HTMLElement
        const rect = html.getBoundingClientRect()
        const style = window.getComputedStyle(html)
        const visible = style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1 && rect.bottom > 0 && rect.top < window.innerHeight
        const ignored = Boolean(html.closest('[aria-hidden="true"]')) || html.hasAttribute('hidden')
        const tooSmall = visible && !ignored && (rect.width < 44 || rect.height < 44)
        return {
          selector: selectorFor(html),
          text: (html.textContent ?? html.getAttribute('aria-label') ?? '').trim().replace(/\s+/g, ' ').slice(0, 100),
          problem: tooSmall ? 'touchTargetBelow44px' : '',
          details: `${Math.round(rect.width)}x${Math.round(rect.height)}`,
        }
      })
      .filter((item) => item.problem)

    const navLabels = bottomNav
      ? Array.from(bottomNav.querySelectorAll('a, button')).map((item) => (item.textContent ?? '').trim().replace(/\s+/g, ' ')).filter(Boolean)
      : []
    const expectedPhoneNav = ['Live', 'Addback', 'Messung', 'Grows']
    const navStructureFindings = isPhone
      ? [
        !bottomNav ? { selector: '.v1-bottom-nav', text: '', problem: 'missingPhoneBottomNav', details: 'Phone requires a 4-item bottom navigation.' } : null,
        bottomNav && navLabels.length !== 4 ? { selector: '.v1-bottom-nav', text: navLabels.join(', '), problem: 'phoneBottomNavItemCount', details: `expected 4, got ${navLabels.length}` } : null,
        bottomNav && navLabels.join('|') !== expectedPhoneNav.join('|') ? { selector: '.v1-bottom-nav', text: navLabels.join(', '), problem: 'phoneBottomNavLabels', details: `expected ${expectedPhoneNav.join(', ')}` } : null,
      ].filter((item): item is AuditFinding => item !== null)
      : []

    const safeAreaFindings = [
      isPhone && topbarRect && topbarRect.top < safeTop - 1 ? { selector: '.v1-mobile-topbar', text: '', problem: 'topbarAboveSafeTop', details: `top=${Math.round(topbarRect.top)} safeTop=${safeTop}` } : null,
      isPhone && bottomNavRect && window.innerHeight - bottomNavRect.bottom > Math.max(1, safeBottom + 1) ? { selector: '.v1-bottom-nav', text: '', problem: 'bottomNavGapToViewport', details: `gap=${Math.round(window.innerHeight - bottomNavRect.bottom)} safeBottom=${safeBottom}` } : null,
      isPhone && bottomNavRect && routeFrameRect && routeFrameRect.bottom > bottomNavRect.top + 4 ? { selector: '.v1-route-frame', text: '', problem: 'contentCanExtendUnderBottomNav', details: `contentBottom=${Math.round(routeFrameRect.bottom)} navTop=${Math.round(bottomNavRect.top)}` } : null,
    ].filter((item): item is AuditFinding => item !== null)

    const tabletLayoutFindings = isTablet
      ? [
        bottomNavRect && bottomNavRect.width > 1 ? { selector: '.v1-bottom-nav', text: navLabels.join(', '), problem: 'tabletUsesPhoneBottomNav', details: 'Contract prefers sidebar or adaptive tablet navigation.' } : null,
        !document.querySelector('.v1-desktop-nav') ? { selector: '.v1-desktop-nav', text: '', problem: 'tabletNavigationMissing', details: 'No sidebar/adaptive navigation candidate found.' } : null,
      ].filter((item): item is AuditFinding => item !== null)
      : []

    return {
      bodyScrollWidth: document.body.scrollWidth,
      documentScrollWidth: document.documentElement.scrollWidth,
      innerWidth: window.innerWidth,
      heading: firstHeading,
      bottomNavFindings,
      touchTargetFindings,
      safeAreaFindings,
      navStructureFindings,
      tabletLayoutFindings,
    }
  })
}

async function waitForAppIdle(page: import('@playwright/test').Page) {
  await page.waitForLoadState('domcontentloaded')
  await page.waitForTimeout(850)
  try {
    await page.waitForLoadState('networkidle', { timeout: 2500 })
  } catch {
    // Live-/HA-Requests dürfen noch laufen.
  }
}

async function auditRoute(page: import('@playwright/test').Page, viewport: ViewportCase, route: RouteCase, pageError: string | null) {
  const response = await page.goto(route.path, { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)

  const metrics = await collectPageMetrics(page)
  const fileName = `${viewport.name}-${viewport.width}x${viewport.height}-${route.slug}.png`
  await page.screenshot({ path: path.join(outputDir, fileName), fullPage: true })

  reportRows.push({
    viewport: `${viewport.name}-${viewport.width}x${viewport.height}`,
    route: route.path,
    url: page.url(),
    status: response?.status() ?? null,
    screenshot: fileName,
    bodyScrollWidth: metrics.bodyScrollWidth,
    documentScrollWidth: metrics.documentScrollWidth,
    innerWidth: metrics.innerWidth,
    horizontalOverflow: metrics.bodyScrollWidth > metrics.innerWidth || metrics.documentScrollWidth > metrics.innerWidth,
    heading: metrics.heading,
    note: null,
    pageError,
    bottomNavFindings: metrics.bottomNavFindings,
    touchTargetFindings: metrics.touchTargetFindings,
    safeAreaFindings: metrics.safeAreaFindings,
    navStructureFindings: metrics.navStructureFindings,
    tabletLayoutFindings: metrics.tabletLayoutFindings,
  })

  if (viewport.enforceMobileHardChecks && metrics.bottomNavFindings.length > 0) {
    const details = metrics.bottomNavFindings.slice(0, 5).map((item) => `${item.selector} "${item.text}" ${item.problem} bottom=${item.bottom} navTop=${item.bottomNavTop ?? 'n/a'} gap=${item.bottomNavGap ?? 'n/a'}`).join(' | ')
    throw new Error(`${fileName}: mobile bottom nav spacing issue: ${details}`)
  }
  if (viewport.enforceMobileHardChecks) {
    await assertMobileBottomNavDocked(page, fileName)
  }
}

async function tryClickFirstAddbackStart(page: import('@playwright/test').Page) {
  const candidates = [
    page.locator('a[href*="/grows/"][href*="/addback"]').first(),
    page.getByRole('link', { name: /addback starten/i }).first(),
    page.getByRole('button', { name: /addback starten/i }).first(),
  ]

  for (const candidate of candidates) {
    try {
      if ((await candidate.count()) === 0) continue
      await candidate.scrollIntoViewIfNeeded()
      await Promise.all([
        page.waitForURL(/\/grows\/[^/]+\/addback/i, { timeout: 4500 }).catch(() => null),
        candidate.click({ timeout: 3000 }),
      ])
      if (/\/grows\/[^/]+\/addback/i.test(page.url())) return { opened: true, note: null }
    } catch {
      // nächster Kandidat
    }
  }

  return { opened: false, note: 'Addback-Flow wurde nicht automatisch geöffnet. Wahrscheinlich kein aktiver Grow oder kein eindeutiger Start-Link.' }
}

async function auditAddbackDeepFlow(page: import('@playwright/test').Page, viewport: ViewportCase, pageError: string | null) {
  const response = await page.goto('/addback', { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)

  const result = await tryClickFirstAddbackStart(page)
  await waitForAppIdle(page)

  const slug = result.opened ? 'addback-flow' : 'addback-flow-not-opened'
  const metrics = await collectPageMetrics(page)
  const fileName = `${viewport.name}-${viewport.width}x${viewport.height}-${slug}.png`
  await page.screenshot({ path: path.join(outputDir, fileName), fullPage: true })

  reportRows.push({
    viewport: `${viewport.name}-${viewport.width}x${viewport.height}`,
    route: result.opened ? '/grows/:growId/addback' : '/addback → Flow nicht geöffnet',
    url: page.url(),
    status: response?.status() ?? null,
    screenshot: fileName,
    bodyScrollWidth: metrics.bodyScrollWidth,
    documentScrollWidth: metrics.documentScrollWidth,
    innerWidth: metrics.innerWidth,
    horizontalOverflow: metrics.bodyScrollWidth > metrics.innerWidth || metrics.documentScrollWidth > metrics.innerWidth,
    heading: metrics.heading,
    note: result.note,
    pageError,
    bottomNavFindings: metrics.bottomNavFindings,
    touchTargetFindings: metrics.touchTargetFindings,
    safeAreaFindings: metrics.safeAreaFindings,
    navStructureFindings: metrics.navStructureFindings,
    tabletLayoutFindings: metrics.tabletLayoutFindings,
  })

  if (viewport.enforceMobileHardChecks && metrics.bottomNavFindings.length > 0) {
    const details = metrics.bottomNavFindings.slice(0, 5).map((item) => `${item.selector} "${item.text}" ${item.problem} bottom=${item.bottom} navTop=${item.bottomNavTop ?? 'n/a'} gap=${item.bottomNavGap ?? 'n/a'}`).join(' | ')
    throw new Error(`${fileName}: mobile bottom nav spacing issue: ${details}`)
  }
  if (viewport.enforceMobileHardChecks) {
    await assertMobileBottomNavDocked(page, fileName)
  }
}

function writeReports() {
  fs.mkdirSync(outputDir, { recursive: true })
  fs.writeFileSync(path.join(outputDir, 'visual-audit-report.json'), JSON.stringify({ results: reportRows, messages }, null, 2), 'utf8')

  const lines = [
    '# Grow OS Visual Audit',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
    '## Screenshots',
    '',
    '| Viewport | Route | Status | Overflow | Bottom Nav | Touch | Safe Area | Nav Structure | Tablet | Heading | Screenshot | Note | PageError |',
    '|---|---|---:|---|---:|---:|---:|---:|---:|---|---|---|---|',
    ...reportRows.map((row) => {
      const note = row.note ? row.note.replace(/\|/g, '\\|').slice(0, 220) : ''
      const error = row.pageError ? row.pageError.replace(/\|/g, '\\|').slice(0, 160) : ''
      return `| ${row.viewport} | ${row.route} | ${row.status ?? ''} | ${row.horizontalOverflow ? 'YES' : 'no'} | ${row.bottomNavFindings.length} | ${row.touchTargetFindings.length} | ${row.safeAreaFindings.length} | ${row.navStructureFindings.length} | ${row.tabletLayoutFindings.length} | ${row.heading ?? ''} | ${row.screenshot} | ${note} | ${error} |`
    }),
    '',
    '## Mobile / Tablet Findings',
    '',
    'Diese Findings sind vorbereitend und reportend. Sie aktivieren noch keine neuen harten Contract-Fails.',
    '',
    '| Viewport | Route | Kategorie | Problem | Details |',
    '|---|---|---|---|---|',
    ...reportRows.flatMap((row) => [
      ...row.touchTargetFindings.map((finding) => findingLine(row, 'touchTargetFindings', finding)),
      ...row.safeAreaFindings.map((finding) => findingLine(row, 'safeAreaFindings', finding)),
      ...row.navStructureFindings.map((finding) => findingLine(row, 'navStructureFindings', finding)),
      ...row.tabletLayoutFindings.map((finding) => findingLine(row, 'tabletLayoutFindings', finding)),
    ]),
    '',
    '## Addback Deep Flow',
    '',
    'Der Audit öffnet zusätzlich `/addback` und versucht den ersten sichtbaren Addback-Start-Link zu klicken.',
    '',
    '## Console / Network Hinweise',
    '',
    '| Typ | Ziel | Meldung | URL |',
    '|---|---|---|---|',
    ...messages.map((message) => `| ${message.type} | ${message.target} | ${message.text.replace(/\|/g, '\\|')} | ${message.url ?? ''} |`),
  ]

  fs.writeFileSync(path.join(outputDir, 'visual-audit-report.md'), lines.join('\n'), 'utf8')
}

function findingLine(row: ReportRow, category: string, finding: AuditFinding) {
  const detail = `${finding.selector} ${finding.text ? `"${finding.text}" ` : ''}${finding.details}`.replace(/\|/g, '\\|').slice(0, 260)
  return `| ${row.viewport} | ${row.route} | ${category} | ${finding.problem} | ${detail} |`
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
        const failure = request.failure()?.errorText ?? 'request failed'
        if (failure.includes('net::ERR_ABORTED')) return
        messages.push({ type: 'requestfailed', target: `${viewport.name}-${viewport.width}x${viewport.height}:${page.url()}`, text: failure, url: request.url() })
      })
    })

    test(`capture main routes ${viewport.name}`, async ({ page }) => {
      await ensureVisualAuditData(page.request)
      let pageError: string | null = null
      page.on('pageerror', (error) => { pageError = error.message })
      for (const route of routes) {
        await auditRoute(page, viewport, route, pageError)
        await assertRouteContract(page, route.slug)
        pageError = null
      }
    })

    test(`capture addback deep flow ${viewport.name}`, async ({ page }) => {
      await ensureVisualAuditData(page.request)
      let pageError: string | null = null
      page.on('pageerror', (error) => { pageError = error.message })
      await auditAddbackDeepFlow(page, viewport, pageError)
    })
  })
}

test.afterAll(() => {
  writeReports()
})

async function ensureVisualAuditData(request: import('@playwright/test').APIRequestContext) {
  const tents = await apiJson<AuditTent[]>(request, 'GET', '/api/settings/tents?includeArchived=true')
  const existing = tents.find((tent) => tent.name === 'E2E Visual Audit Empty Tent' && tent.status !== 'Archived')
  if (existing) return existing.id

  const created = await apiJson<AuditTent>(request, 'POST', '/api/settings/tents', {
    name: 'E2E Visual Audit Empty Tent',
    kind: 'Grow Tent',
    tentType: 'Production',
    status: 'Active',
    notes: 'Automatisch angelegte Testdaten fuer Playwright Visual Audit',
    displayOrder: 9100,
    accentColor: '#22c55e',
    widthCm: 80,
    depthCm: 80,
    tentHeightCm: 160,
    lightType: 'LED',
    lightWatt: 120,
    lightController: null,
    lightControllerEntityId: null,
    exhaustFanCount: 1,
    exhaustM3h: 200,
    circulationFanCount: 1,
    hvacController: null,
    hvacControllerEntityId: null,
    co2Available: false,
    cameraEntityId: null,
    sensors: [],
  })

  return created.id
}

async function apiJson<T>(
  request: import('@playwright/test').APIRequestContext,
  method: 'GET' | 'POST',
  pathName: string,
  body?: unknown,
) {
  const response = await request.fetch(pathName, {
    method,
    data: body,
    headers: body ? { 'content-type': 'application/json' } : undefined,
  })

  if (!response.ok()) {
    throw new Error(`${method} ${pathName} failed: ${response.status()} ${await response.text()}`)
  }

  return await response.json() as T
}

async function assertRouteContract(page: import('@playwright/test').Page, slug: string) {
  if (slug === 'settings') {
    await expect(page.getByRole('button', { name: /Vollbackup herunterladen/i })).toBeVisible()
    await expect(page.locator('.rc-file-input').first()).toBeVisible()
    const backupResponse = await page.request.post('/api/system/backup')
    expect(backupResponse.status(), 'Vollbackup endpoint must not return 500').toBe(201)
    const manifest = await backupResponse.json() as { downloadUrl?: string; fileName?: string; sizeBytes?: number }
    expect(manifest.downloadUrl, 'Backup manifest needs a download URL').toMatch(/^\/api\/system\/backup\/grow-os-backup-.*\.zip$/)
    expect(manifest.sizeBytes ?? 0, 'Backup ZIP must not be empty').toBeGreaterThan(0)
    const downloadResponse = await page.request.get(manifest.downloadUrl!)
    expect(downloadResponse.status(), 'Backup ZIP download must return 200').toBe(200)
    expect(downloadResponse.headers()['content-type'] ?? '').toContain('application/zip')
    expect((await downloadResponse.body()).length, 'Downloaded Backup ZIP must not be empty').toBeGreaterThan(0)
  }
  if (slug === 'release') {
    await expect(page.locator('.rc-file-input').first()).toBeVisible()
  }
  if (slug === 'connect') {
    await expect(page.getByRole('button', { name: /^(Addback|Messung|HA)$/i })).toHaveCount(0)
    await expect(page.getByText(/\/addback|\/messung|\/home-assistant/i)).toHaveCount(0)
  }
  if (slug === 'action' || slug === 'aufgaben') {
    await expect(page.locator('.rc-action-guide-card')).toHaveCount(4)
  }
  if (slug === 'wissen') {
    await expect(page.locator('[data-audit="knowledge-search"]')).toBeVisible()
    await expect(page.locator('[data-audit="knowledge-topic-nav"]')).toBeVisible()
    await expect(page.locator('[data-audit="knowledge-article"]')).toBeVisible()
    await expect(page.locator('.rc2-topic-grid')).toHaveCount(0)
  }
  if (slug === 'home-assistant') {
    await expect(page.locator('[data-audit="ha-connection-layout"]')).toBeVisible()
    await expect(page.locator('[data-audit="ha-connection-actions"]')).toBeVisible()
    await expect(page.locator('[data-audit="ha-camera-field-action"]')).toBeVisible()
    const layout = await page.locator('[data-audit="ha-connection-layout"]').evaluate((element) => {
      const rect = (element as HTMLElement).getBoundingClientRect()
      const actions = element.querySelector('[data-audit="ha-connection-actions"]') as HTMLElement | null
      const actionsRect = actions?.getBoundingClientRect() ?? null
      return {
        overflow: (element as HTMLElement).scrollWidth > (element as HTMLElement).clientWidth + 2,
        actionsOverlap: Boolean(actionsRect && actionsRect.top < rect.top),
      }
    })
    expect(layout.overflow, 'HA connection layout must not overflow').toBe(false)
    expect(layout.actionsOverlap, 'HA connection actions must not overlap inputs').toBe(false)
  }
  if (slug === 'hardware') {
    await page.locator('[data-audit="hardware-inventory-tab"]').click()
    await expect(page.locator('[data-audit="hardware-edit-form"]')).toBeVisible()
    const deleteButtons = page.locator('[data-audit="hardware-delete-button"]')
    if (await deleteButtons.count()) {
      await expect(deleteButtons.first()).toBeVisible()
    }
    await assertNoAsciiUmlautActions(page, slug)
  }
  if (slug === 'grows') {
    await expect(page.getByRole('heading', { name: /^Grows$/i })).toBeVisible()
    await expect(page.getByRole('link', { name: /Neuen Grow anlegen/i }).first()).toBeVisible()
    await expect(page.getByRole('link', { name: /Grow starten/i })).toHaveCount(0)
    const firstCard = page.locator('.grow-overview-card').first()
    if (await firstCard.count()) {
      await expect(firstCard.getByRole('link', { name: /^Öffnen$/i })).toBeVisible()
      await expect(firstCard.getByRole('link', { name: /^Bearbeiten$/i })).toBeVisible()
      await expect(firstCard.getByRole('button', { name: /^(Beenden|Löschen)$/i }).first()).toBeVisible()
    }
    await assertNoAsciiUmlautActions(page, slug)
  }
  if (slug === 'zelte') {
    await expect(page.locator('[data-audit="tent-delete-blocked"]')).toHaveCount(0)
    await expect(page.locator('[data-audit="tent-delete-button"]').first()).toBeVisible()
    const overflowingTentCards = await page.locator('.v1-tent-card').evaluateAll((cards) =>
      cards.filter((card) => {
        const html = card as HTMLElement
        return html.scrollWidth > html.clientWidth + 2
      }).length)
    expect(overflowingTentCards, 'Zelt cards must not overflow horizontally').toBe(0)
    const clippedMetricValues = await page.locator('.tent-metric-row dd').evaluateAll((values) =>
      values.filter((value) => {
        const html = value as HTMLElement
        const text = html.textContent ?? ''
        return text.includes('...') || text.includes('…') || html.scrollWidth > html.clientWidth + 2
      }).map((value) => value.textContent?.trim()))
    expect(clippedMetricValues, 'Zelt metric values must not be clipped or ellipsized').toEqual([])
    await assertNoAsciiUmlautActions(page, slug)
  }
  if (slug === 'hydro' || slug === 'hydro-new') {
    const previewProblems = await page.locator('[data-audit="hydro-preview"]').evaluateAll((previews) =>
      previews.filter((preview) => {
        const html = preview as HTMLElement
        const rect = html.getBoundingClientRect()
        const parent = html.closest('.v1-card, .v1-section') as HTMLElement | null
        const parentRect = parent?.getBoundingClientRect() ?? null
        return html.scrollWidth > html.clientWidth + 2
          || (parentRect != null && (rect.left < parentRect.left - 2 || rect.right > parentRect.right + 2))
      }).length)
    expect(previewProblems, 'Hydro preview must stay inside its card and not overflow horizontally').toBe(0)
    const clippedPreviewChildren = await page.locator('[data-audit="hydro-preview"]').evaluateAll((previews) =>
      previews.flatMap((preview) => {
        const previewRect = (preview as HTMLElement).getBoundingClientRect()
        return Array.from(preview.querySelectorAll('.rdwc-preview__site, .rdwc-preview__tank, .rdwc-preview__caption'))
          .filter((child) => {
            const rect = (child as HTMLElement).getBoundingClientRect()
            return rect.left < previewRect.left - 2
              || rect.right > previewRect.right + 2
              || rect.top < previewRect.top - 2
              || rect.bottom > previewRect.bottom + 2
          })
          .map((child) => {
            const rect = (child as HTMLElement).getBoundingClientRect()
            const previewStyle = window.getComputedStyle(preview as HTMLElement)
            const parent = child.parentElement as HTMLElement | null
            const parentStyle = parent ? window.getComputedStyle(parent) : null
            const parentRect = parent?.getBoundingClientRect()
            return `${child.textContent?.trim()} child=${Math.round(rect.left)},${Math.round(rect.top)},${Math.round(rect.right)},${Math.round(rect.bottom)} preview=${Math.round(previewRect.left)},${Math.round(previewRect.top)},${Math.round(previewRect.right)},${Math.round(previewRect.bottom)} previewStyle=${previewStyle.display}/${previewStyle.position}/${previewStyle.alignItems}/${previewStyle.justifyContent}/${previewStyle.marginTop}/${previewStyle.transform} parent=${parent?.className ?? 'none'} parentRect=${parentRect ? `${Math.round(parentRect.left)},${Math.round(parentRect.top)},${Math.round(parentRect.right)},${Math.round(parentRect.bottom)}` : 'none'} parentStyle=${parentStyle?.display}/${parentStyle?.position}/${parentStyle?.alignItems}/${parentStyle?.justifyContent}/${parentStyle?.marginTop}/${parentStyle?.transform}`
          })
      }))
    expect(clippedPreviewChildren, 'Hydro preview children must not be clipped inside the preview').toEqual([])
    await assertNoAsciiUmlautActions(page, slug)
  }
  if (slug === 'addback') {
    await expect(page.locator('[data-audit="addback-hub"]')).toBeVisible()
    await expect(page.locator('.v1-addback-flow-strip, [data-audit="addback-stepper"]')).toHaveCount(0)
    const overflowingLastFields = await page.locator('.v1-info').filter({ hasText: /^Letzter/ }).evaluateAll((elements) =>
      elements.filter((element) => {
        const value = element.querySelector('strong') as HTMLElement | null
        return Boolean(value && value.scrollWidth > value.clientWidth + 2)
      }).length)
    expect(overflowingLastFields, 'Addback Verlauf "Letzter" must not overflow its box').toBe(0)
  }
}

async function assertMobileBottomNavDocked(page: import('@playwright/test').Page, context: string) {
  const nav = await page.evaluate(() => {
    const element = document.querySelector('.v1-bottom-nav') as HTMLElement | null
    if (!element) return null
    const rect = element.getBoundingClientRect()
    const style = window.getComputedStyle(element)
    return {
      visible: style.display !== 'none' && rect.width > 1 && rect.height > 1,
      bottomGap: Math.abs(window.innerHeight - rect.bottom),
      backgroundColor: style.backgroundColor,
      paddingBottom: Number.parseFloat(style.paddingBottom || '0'),
    }
  })
  expect(nav, `${context}: mobile bottom nav must exist`).not.toBeNull()
  expect(nav!.visible, `${context}: mobile bottom nav must be visible`).toBe(true)
  expect(nav!.bottomGap, `${context}: mobile bottom nav must be docked to viewport bottom`).toBeLessThanOrEqual(1)
  expect(nav!.backgroundColor, `${context}: mobile bottom nav background must be opaque`).not.toMatch(/rgba\(0,\s*0,\s*0,\s*0\)|transparent/i)
  expect(nav!.paddingBottom, `${context}: mobile bottom nav must reserve safe-area padding`).toBeGreaterThanOrEqual(8)
}

async function assertNoAsciiUmlautActions(page: import('@playwright/test').Page, slug: string) {
  const offenders = await page.locator('button, a, [role="button"], [role="link"]').evaluateAll((items) =>
    items
      .map((item) => (item.textContent ?? '').trim().replace(/\s+/g, ' '))
      .filter((text) => /\b(Loeschen|Loescht|geloescht|endgueltig|Oeffnen|Zurueck|waehle|laedt|bestaetigen|moeglich|verknuepft)\b/i.test(text)))
  expect(offenders, `${slug}: visible actions must use German umlauts`).toEqual([])
}
