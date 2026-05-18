import { test, type APIRequestContext } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

const repoRoot = path.resolve(process.cwd(), '..')
const outputDir = path.join(repoRoot, 'artifacts', 'workflow-audit-current')
const backendUrl = (process.env.GROW_OS_BACKEND_URL ?? 'http://127.0.0.1:5076').replace(/\/$/, '')
const workflowTentName = 'E2E Workflow Audit Zelt'
const workflowHydroName = 'E2E Workflow Audit RDWC'

type LayoutFinding = {
  tag: string
  selector: string
  text: string
  horizontalClip: boolean
  outOfViewport: boolean
  coveredByBottomNav: boolean
  width: number
  left: number
  right: number
  top: number
  bottom: number
  scrollWidth: number
  clientWidth: number
}

type WorkflowStepReport = {
  name: string
  path: string
  url: string
  heading: string | null
  activeWizardStep: number | null
  bodyOverflow: boolean
  documentOverflow: boolean
  hardOffenders: LayoutFinding[]
  clipWarnings: LayoutFinding[]
}

type WorkflowTent = {
  id: number
  name: string
  status?: string | null
}

type WorkflowHydroSetup = {
  id: number
  tentId: number | null
  name: string
  status?: string | null
}

const workflowReports: WorkflowStepReport[] = []

test.beforeAll(() => {
  fs.rmSync(outputDir, { recursive: true, force: true })
  fs.mkdirSync(outputDir, { recursive: true })
})

test.afterAll(() => {
  fs.mkdirSync(outputDir, { recursive: true })
  fs.writeFileSync(path.join(outputDir, 'workflow-audit-report.json'), JSON.stringify({ generatedAtUtc: new Date().toISOString(), results: workflowReports }, null, 2), 'utf8')

  const lines = [
    '# Grow OS Workflow Audit',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
    '| Step | Path | Heading | Wizard | Body Overflow | Document Overflow | Hard Offenders | Clip Warnings |',
    '|---|---|---|---:|---|---|---:|---:|',
    ...workflowReports.map((row) => `| ${row.name} | ${row.path} | ${row.heading ?? ''} | ${row.activeWizardStep ?? ''} | ${row.bodyOverflow ? 'YES' : 'no'} | ${row.documentOverflow ? 'YES' : 'no'} | ${row.hardOffenders.length} | ${row.clipWarnings.length} |`),
  ]
  fs.writeFileSync(path.join(outputDir, 'workflow-audit-report.md'), lines.join('\n'), 'utf8')
})

test.describe.configure({ mode: 'serial' })

test.describe('workflow audit mobile', () => {
  test.beforeEach(async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })
  })

  test('walk focused create workflows without saving', async ({ page }) => {
    await auditRoute(page, '/zelte/new', 'mobile-zelt-new')
    await fillIfVisible(page, 'input[placeholder="Hauptzelt"]', `E2E Zelt ${Date.now()}`)
    await screenshotAndLayout(page, 'mobile-zelt-new-filled')

    await auditRoute(page, '/hydro/new', 'mobile-hydro-step-1')
    await fillIfVisible(page, 'input[placeholder="RDWC 4-Site"]', `E2E RDWC ${Date.now()}`)
    await selectFirstNonEmptyOption(page, 'select')
    await expectWizardStep(page, 1, 'hydro initial')
    await clickNextAndExpectStep(page, 2, 'hydro step 2')
    await screenshotAndLayout(page, 'mobile-hydro-step-2')
    await clickNextAndExpectStep(page, 3, 'hydro step 3')
    await screenshotAndLayout(page, 'mobile-hydro-step-3')
    await clickNextAndExpectStep(page, 4, 'hydro step 4')
    await screenshotAndLayout(page, 'mobile-hydro-step-4')
    await clickNextAndExpectStep(page, 5, 'hydro step 5')
    await screenshotAndLayout(page, 'mobile-hydro-step-5')

    await ensureHydroSetupForWorkflowAudit(page.request)
    await auditRoute(page, '/grows/new', 'mobile-grow-step-1')
    await fillIfVisible(page, 'input[placeholder="Purple Lemonade RDWC"]', `E2E Grow ${Date.now()}`)
    await expectWizardStep(page, 1, 'grow initial')

    await clickNextAndExpectStep(page, 2, 'grow step 2')
    await selectGrowTent(page)
    await screenshotAndLayout(page, 'mobile-grow-step-2')

    await clickNextAndExpectStep(page, 3, 'grow step 3')
    await selectGrowHydro(page)
    await screenshotAndLayout(page, 'mobile-grow-step-3')

    await clickNextAndExpectStep(page, 4, 'grow step 4')
    await screenshotAndLayout(page, 'mobile-grow-step-4')

    await clickNextAndExpectStep(page, 5, 'grow step 5')
    await selectProgramIfPresent(page)
    await screenshotAndLayout(page, 'mobile-grow-step-5')

    await clickNextAndExpectStep(page, 6, 'grow step 6')
    await screenshotAndLayout(page, 'mobile-grow-step-6')
  })
})

test.describe('workflow audit desktop', () => {
  test.beforeEach(async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 })
  })

  test('walk admin and mapping workflows', async ({ page }) => {
    await auditRoute(page, '/aufgaben', 'desktop-aufgaben')
    await auditRoute(page, '/home-assistant', 'desktop-ha-setup')
    await auditRoute(page, '/wissen', 'desktop-knowledge')
    await auditRoute(page, '/release', 'desktop-release')
    await auditRoute(page, '/settings', 'desktop-settings')
    await auditRoute(page, '/connect', 'desktop-connect')
  })
})

async function ensureHydroSetupForWorkflowAudit(request: APIRequestContext) {
  const [tents, hydroSetups] = await Promise.all([
    apiJson<WorkflowTent[]>(request, 'GET', '/api/settings/tents?includeArchived=true'),
    apiJson<WorkflowHydroSetup[]>(request, 'GET', '/api/hydro-setups?includeArchived=true'),
  ])

  const activeTents = tents.filter((tent) => tent.status !== 'Archived')
  const activeTentIds = new Set(activeTents.map((tent) => tent.id))
  const reusableHydro = hydroSetups.find((setup) => setup.status !== 'Archived' && setup.tentId != null && activeTentIds.has(setup.tentId))
  if (reusableHydro) return

  let tent = activeTents.find((item) => item.name === workflowTentName)
  if (!tent) {
    tent = await apiJson<WorkflowTent>(request, 'POST', '/api/settings/tents', {
      name: workflowTentName,
      kind: 'Grow Tent',
      tentType: 'Production',
      status: 'Active',
      notes: 'Automatisch angelegte Testdaten fuer Playwright Workflow Audit',
      displayOrder: 9001,
      accentColor: '#22c55e',
      widthCm: 120,
      depthCm: 120,
      tentHeightCm: 200,
      lightType: 'LED',
      lightWatt: 400,
      lightController: null,
      lightControllerEntityId: null,
      exhaustFanCount: 1,
      exhaustM3h: 400,
      circulationFanCount: 2,
      hvacController: null,
      hvacControllerEntityId: null,
      co2Available: false,
      cameraEntityId: null,
      sensors: [],
    })
  }

  const hasHydroForTent = hydroSetups.some((setup) => setup.status !== 'Archived' && setup.tentId === tent.id)
  if (hasHydroForTent) return

  await apiJson<WorkflowHydroSetup>(request, 'POST', '/api/hydro-setups', {
    tentId: tent.id,
    name: workflowHydroName,
    hydroStyle: 'RDWC',
    potCount: 4,
    potSizeLiters: 19,
    reservoirLiters: 60,
    layoutType: 'Grid2x2',
    reservoirPosition: 'Left',
    hasCirculationPump: true,
    circulationPumpNotes: null,
    hasAirPump: true,
    airPumpNotes: null,
    airStoneCount: 4,
    hasChiller: false,
    hasUvSterilizer: false,
    notes: 'Automatisch angelegte Testdaten fuer Playwright Workflow Audit',
    displayOrder: 9001,
  })
}

async function apiJson<T>(request: APIRequestContext, method: 'GET' | 'POST', pathName: string, data?: unknown): Promise<T> {
  const response = await request.fetch(`${backendUrl}${pathName}`, {
    method,
    data,
    headers: data == null ? undefined : { 'Content-Type': 'application/json' },
  })

  if (!response.ok()) {
    const body = await response.text().catch(() => '')
    throw new Error(`Workflow-Audit Testdaten API fehlgeschlagen: ${method} ${pathName} -> ${response.status()} ${body}`)
  }

  return await response.json() as T
}

async function auditRoute(page: import('@playwright/test').Page, url: string, name: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  await screenshotAndLayout(page, name)
}

async function waitForAppIdle(page: import('@playwright/test').Page) {
  await page.waitForTimeout(850)
  try {
    await page.waitForLoadState('networkidle', { timeout: 2500 })
  } catch {
    // Live-/HA-Requests dürfen laufen.
  }
}

async function fillIfVisible(page: import('@playwright/test').Page, selector: string, value: string) {
  const locator = page.locator(selector).first()
  if ((await locator.count()) === 0) return

  try {
    if (await locator.isVisible({ timeout: 1000 })) {
      await locator.fill(value, { timeout: 1200 })
    }
  } catch {
    // Optionaler Workflow-Schritt. Der Audit soll die Seite prüfen, nicht Testdaten erzwingen.
  }
}

async function selectFirstNonEmptyOption(page: import('@playwright/test').Page, selector: string) {
  const locator = page.locator(selector).first()
  if ((await locator.count()) === 0) return

  const values = await locator.locator('option').evaluateAll((options) => options.map((option) => (option as HTMLOptionElement).value).filter(Boolean))
  if (values.length === 0) {
    throw new Error(`Keine auswählbare Option für ${selector}. Für diesen Workflow fehlen Testdaten.`)
  }

  await locator.selectOption(values[0], { timeout: 1200 })
}

async function clickNextAndExpectStep(page: import('@playwright/test').Page, expectedStep: number, label: string) {
  const next = page.getByRole('button', { name: /^Weiter$/i }).first()
  if ((await next.count()) === 0) {
    throw new Error(`${label}: Weiter-Button nicht gefunden.`)
  }

  if (!(await next.isVisible({ timeout: 1500 })) || !(await next.isEnabled({ timeout: 1500 }))) {
    throw new Error(`${label}: Weiter-Button ist nicht sichtbar oder nicht aktiv.`)
  }

  await next.click({ timeout: 2000 })
  await waitForAppIdle(page)
  await expectWizardStep(page, expectedStep, label)
}

async function expectWizardStep(page: import('@playwright/test').Page, expectedStep: number, label: string) {
  const activeStep = await getActiveWizardStep(page)
  if (activeStep !== expectedStep) {
    const alertText = await page.locator('.v1-alert, [role="alert"]').first().textContent({ timeout: 500 }).catch(() => null)
    throw new Error(`${label}: Wizard ist nicht auf Schritt ${expectedStep}, sondern auf Schritt ${activeStep ?? 'unbekannt'}.${alertText ? ` Hinweis: ${alertText.trim()}` : ''}`)
  }
}

async function getActiveWizardStep(page: import('@playwright/test').Page) {
  return await page.evaluate(() => {
    const steps = Array.from(document.querySelectorAll('.v1-wizard-step'))
    const activeIndex = steps.findIndex((element) => element.classList.contains('active'))
    return activeIndex >= 0 ? activeIndex + 1 : null
  })
}

async function selectGrowTent(page: import('@playwright/test').Page) {
  const cards = page.locator('.grow-select-card')
  const count = await cards.count()
  if (count === 0) {
    throw new Error('Grow Schritt Zelt: keine Zelt-Karte gefunden. Für den Workflow braucht die DB mindestens ein Zelt.')
  }

  for (let i = 0; i < count; i += 1) {
    const card = cards.nth(i)
    const text = (await card.textContent()) ?? ''
    if (!/0\s*Hydro/i.test(text)) {
      await card.click({ timeout: 1500 })
      await waitForAppIdle(page)
      return
    }
  }

  await cards.first().click({ timeout: 1500 })
  await waitForAppIdle(page)
}

async function selectGrowHydro(page: import('@playwright/test').Page) {
  const cards = page.locator('.grow-select-card')
  const count = await cards.count()
  if (count === 0) {
    throw new Error('Grow Schritt Hydro: kein Hydro-Setup gefunden. Wähle im Testdatenstand ein Zelt mit aktivem Hydro-Setup oder lege vorher ein Setup an.')
  }

  await cards.first().click({ timeout: 1500 })
  await waitForAppIdle(page)
}

async function selectProgramIfPresent(page: import('@playwright/test').Page) {
  const cards = page.locator('.program-card')
  const count = await cards.count()
  if (count > 0) {
    await cards.first().click({ timeout: 1500 })
    await waitForAppIdle(page)
    return
  }

  const customInput = page.locator('input[placeholder*="eigene"], input[placeholder*="Mischung"]').first()
  if ((await customInput.count()) > 0 && await customInput.isVisible({ timeout: 800 })) {
    await customInput.fill('E2E Programm', { timeout: 1200 })
  }
}

async function screenshotAndLayout(page: import('@playwright/test').Page, name: string) {
  const result = await page.evaluate(() => {
    function selectorFor(element: Element) {
      if (element.id) return `#${element.id}`
      const className = Array.from(element.classList).slice(0, 3).join('.')
      return `${element.tagName.toLowerCase()}${className ? `.${className}` : ''}`
    }

    function isBottomNavCriticalElement(element: HTMLElement) {
      const style = window.getComputedStyle(element)
      return (
        style.position === 'fixed' ||
        style.position === 'sticky' ||
        Boolean(element.closest('.sticky-actions'))
      )
    }

    const bottomNav = document.querySelector('.v1-bottom-nav') as HTMLElement | null
    const bottomNavRect = bottomNav?.getBoundingClientRect() ?? null

    const candidates = Array.from(document.querySelectorAll('button, a, input, select, textarea, .v1-tab, .v1-wizard-step, .v1-card, .v1-section'))
      .map((element) => {
        const html = element as HTMLElement
        const rect = html.getBoundingClientRect()
        const style = window.getComputedStyle(html)
        const visible = style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1 && rect.bottom > 0 && rect.top < window.innerHeight
        const horizontalClip = html.scrollWidth > html.clientWidth + 8
        const outOfViewport = rect.left < -4 || rect.right > window.innerWidth + 4
        const insideBottomNav = Boolean(html.closest('.v1-bottom-nav'))
        const insideMobileMore = Boolean(html.closest('.v1-mobile-more-panel'))
        const criticalForBottomNav = isBottomNavCriticalElement(html)
        const coveredByBottomNav = Boolean(
          bottomNavRect &&
          criticalForBottomNav &&
          !insideBottomNav &&
          !insideMobileMore &&
          rect.bottom > bottomNavRect.top + 4 &&
          rect.top < bottomNavRect.bottom - 4,
        )

        return {
          tag: html.tagName,
          selector: selectorFor(html),
          text: (html.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 100),
          visible,
          horizontalClip,
          outOfViewport,
          coveredByBottomNav,
          criticalForBottomNav,
          width: Math.round(rect.width),
          left: Math.round(rect.left),
          right: Math.round(rect.right),
          top: Math.round(rect.top),
          bottom: Math.round(rect.bottom),
          scrollWidth: html.scrollWidth,
          clientWidth: html.clientWidth,
          role: html.getAttribute('role'),
          className: html.className,
        }
      })
      .filter((item) => item.visible)

    const hardOffenders = candidates
      .filter((item) => item.outOfViewport || item.coveredByBottomNav)
      .filter((item) => !String(item.className).includes('v1-bottom-nav'))
      .filter((item) => !String(item.className).includes('v1-mobile-more-panel'))
      .filter((item) => item.tag !== 'SECTION' && !String(item.className).includes('v1-section'))

    const clipWarnings = candidates
      .filter((item) => item.horizontalClip)
      .filter((item) => !String(item.className).includes('v1-tabs'))
      .filter((item) => item.tag !== 'INPUT' && item.tag !== 'TEXTAREA')

    const heading =
      document.querySelector('h1')?.textContent?.trim() ??
      document.querySelector('[data-audit-title]')?.textContent?.trim() ??
      null

    const steps = Array.from(document.querySelectorAll('.v1-wizard-step'))
    const activeIndex = steps.findIndex((element) => element.classList.contains('active'))

    return {
      path: window.location.pathname,
      url: window.location.href,
      heading,
      activeWizardStep: activeIndex >= 0 ? activeIndex + 1 : null,
      bodyScrollWidth: document.body.scrollWidth,
      documentScrollWidth: document.documentElement.scrollWidth,
      innerWidth: window.innerWidth,
      bodyOverflow: document.body.scrollWidth > window.innerWidth + 4,
      documentOverflow: document.documentElement.scrollWidth > window.innerWidth + 4,
      hardOffenders,
      clipWarnings,
    }
  })

  workflowReports.push({
    name,
    path: result.path,
    url: result.url,
    heading: result.heading,
    activeWizardStep: result.activeWizardStep,
    bodyOverflow: result.bodyOverflow,
    documentOverflow: result.documentOverflow,
    hardOffenders: result.hardOffenders,
    clipWarnings: result.clipWarnings,
  })

  fs.writeFileSync(path.join(outputDir, `${name}.json`), JSON.stringify(result, null, 2), 'utf8')
  await page.screenshot({ path: path.join(outputDir, `${name}.png`), fullPage: true })

  if (result.bodyOverflow || result.documentOverflow) {
    throw new Error(`${name}: horizontal overflow on ${result.path} body=${result.bodyScrollWidth} document=${result.documentScrollWidth} viewport=${result.innerWidth}`)
  }

  const hardOffenders = result.hardOffenders as LayoutFinding[]
  if (hardOffenders.length > 0) {
    const details = hardOffenders.slice(0, 5).map((item) => {
      const problem = item.coveredByBottomNav ? 'coveredByBottomNav' : 'outsideViewport'
      return `${item.selector} "${item.text}" ${problem} left=${item.left} right=${item.right} top=${item.top} bottom=${item.bottom}`
    }).join(' | ')
    throw new Error(`${name}: visible elements blocked or outside viewport: ${details}`)
  }
}
