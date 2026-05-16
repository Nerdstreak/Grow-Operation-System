import { test } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

const repoRoot = path.resolve(process.cwd(), '..')
const outputDir = path.join(repoRoot, 'artifacts', 'workflow-audit-current')

type LayoutFinding = {
  tag: string
  selector: string
  text: string
  horizontalClip: boolean
  outOfViewport: boolean
  width: number
  left: number
  right: number
  scrollWidth: number
  clientWidth: number
}

type WorkflowStepReport = {
  name: string
  path: string
  url: string
  heading: string | null
  bodyOverflow: boolean
  documentOverflow: boolean
  hardOffenders: LayoutFinding[]
  clipWarnings: LayoutFinding[]
}

const workflowReports: WorkflowStepReport[] = []

test.beforeAll(() => {
  fs.rmSync(outputDir, { recursive: true, force: true })
  fs.mkdirSync(outputDir, { recursive: true })
})

test.afterAll(() => {
  const reportPath = path.join(outputDir, 'workflow-audit-report.json')
  fs.writeFileSync(reportPath, JSON.stringify({ generatedAtUtc: new Date().toISOString(), results: workflowReports }, null, 2), 'utf8')

  const lines = [
    '# Grow OS Workflow Audit',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
    '| Step | Path | Heading | Body Overflow | Document Overflow | Hard Offenders | Clip Warnings |',
    '|---|---|---|---|---|---:|---:|',
    ...workflowReports.map((row) => `| ${row.name} | ${row.path} | ${row.heading ?? ''} | ${row.bodyOverflow ? 'YES' : 'no'} | ${row.documentOverflow ? 'YES' : 'no'} | ${row.hardOffenders.length} | ${row.clipWarnings.length} |`),
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

    await auditRoute(page, '/hydro/new', 'mobile-hydro-new')
    await fillIfVisible(page, 'input[placeholder="RDWC 4-Site"]', `E2E RDWC ${Date.now()}`)
    await selectFirstNonEmptyOption(page, 'select')
    await goWizardStep(page, 2, /Weiter/i)
    await screenshotAndLayout(page, 'mobile-hydro-step-2')
    await goWizardStep(page, 3, /Weiter/i)
    await screenshotAndLayout(page, 'mobile-hydro-step-3')
    await goWizardStep(page, 4, /Weiter/i)
    await screenshotAndLayout(page, 'mobile-hydro-step-4')
    await goWizardStep(page, 5, /Weiter/i)
    await screenshotAndLayout(page, 'mobile-hydro-step-5')

    await auditRoute(page, '/grows/new', 'mobile-grow-new')
    await fillIfVisible(page, 'input[placeholder="Purple Lemonade RDWC"]', `E2E Grow ${Date.now()}`)
    await goWizardStep(page, 2, /Weiter/i)
    await screenshotAndLayout(page, 'mobile-grow-step-2')
    await goWizardStep(page, 3, /Weiter/i)
    await screenshotAndLayout(page, 'mobile-grow-step-3')
    await goWizardStep(page, 4, /Weiter/i)
    await screenshotAndLayout(page, 'mobile-grow-step-4')
    await goWizardStep(page, 5, /Weiter/i)
    await screenshotAndLayout(page, 'mobile-grow-step-5')
    await goWizardStep(page, 6, /Weiter/i)
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

  try {
    const values = await locator.locator('option').evaluateAll((options) => options.map((option) => (option as HTMLOptionElement).value).filter(Boolean))
    if (values.length > 0) {
      await locator.selectOption(values[0], { timeout: 1200 })
    }
  } catch {
    // Keine auswählbaren Daten vorhanden.
  }
}

async function goWizardStep(page: import('@playwright/test').Page, stepNumber: number, fallbackButton: RegExp) {
  const directStep = page.locator('.v1-wizard-step').nth(stepNumber - 1)
  try {
    if ((await directStep.count()) > 0 && await directStep.isVisible({ timeout: 600 }) && await directStep.isEnabled({ timeout: 600 })) {
      await directStep.click({ timeout: 1200 })
      await waitForAppIdle(page)
      return
    }
  } catch {
    // Fallback auf Weiter.
  }

  await clickButtonIfPossible(page, fallbackButton)
}

async function clickButtonIfPossible(page: import('@playwright/test').Page, name: RegExp) {
  const locator = page.getByRole('button', { name }).first()
  if ((await locator.count()) === 0) return

  try {
    if (await locator.isVisible({ timeout: 1000 }) && await locator.isEnabled({ timeout: 1000 })) {
      await locator.click({ timeout: 1500 })
      await waitForAppIdle(page)
    }
  } catch {
    // Validierung kann den Wizard bewusst auf dem aktuellen Schritt halten.
  }
}

async function screenshotAndLayout(page: import('@playwright/test').Page, name: string) {
  const result = await page.evaluate(() => {
    function selectorFor(element: Element) {
      if (element.id) return `#${element.id}`
      const className = Array.from(element.classList).slice(0, 3).join('.')
      return `${element.tagName.toLowerCase()}${className ? `.${className}` : ''}`
    }

    const candidates = Array.from(document.querySelectorAll('button, a, input, select, textarea, .v1-tab, .v1-wizard-step, .v1-card, .v1-section'))
      .map((element) => {
        const html = element as HTMLElement
        const rect = html.getBoundingClientRect()
        const style = window.getComputedStyle(html)
        const visible = style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1 && rect.bottom > 0 && rect.top < window.innerHeight + 1400
        const horizontalClip = html.scrollWidth > html.clientWidth + 8
        const outOfViewport = rect.left < -4 || rect.right > window.innerWidth + 4

        return {
          tag: html.tagName,
          selector: selectorFor(html),
          text: (html.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 100),
          visible,
          horizontalClip,
          outOfViewport,
          width: Math.round(rect.width),
          left: Math.round(rect.left),
          right: Math.round(rect.right),
          scrollWidth: html.scrollWidth,
          clientWidth: html.clientWidth,
          role: html.getAttribute('role'),
          className: html.className,
        }
      })
      .filter((item) => item.visible)

    const hardOffenders = candidates
      .filter((item) => item.outOfViewport)
      .filter((item) => !String(item.className).includes('v1-bottom-nav'))
      .filter((item) => !String(item.className).includes('v1-mobile-more-panel'))

    const clipWarnings = candidates
      .filter((item) => item.horizontalClip)
      .filter((item) => !String(item.className).includes('v1-tabs'))
      .filter((item) => item.tag !== 'INPUT' && item.tag !== 'TEXTAREA')

    const heading =
      document.querySelector('h1')?.textContent?.trim() ??
      document.querySelector('[data-audit-title]')?.textContent?.trim() ??
      null

    return {
      path: window.location.pathname,
      url: window.location.href,
      heading,
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
    const details = hardOffenders.slice(0, 5).map((item) => `${item.selector} "${item.text}" left=${item.left} right=${item.right}`).join(' | ')
    throw new Error(`${name}: visible elements outside viewport: ${details}`)
  }
}
