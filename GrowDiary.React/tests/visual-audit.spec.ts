import { expect, test } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

type ViewportCase = {
  name: string
  width: number
  height: number
  category: 'desktop' | 'phone' | 'tablet'
  enforceShellHardChecks?: boolean
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

type AuditHydroSetup = {
  id: number
  name: string
  tentId?: number | null
  status?: string | null
}

type AuditGrowSummary = {
  id: number
  name: string
  status?: string | null
  systemId?: number | null
  setupId?: number | null
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
  liveDashboardFindings: AuditFinding[]
  addbackFindings: AuditFinding[]
  measurementFindings: AuditFinding[]
  measurementFormGroups: number | null
  measurementFieldsAboveFold: number | null
  growsFindings: AuditFinding[]
  growsCardsAboveFold: number | null
  growsActionBarHeight: number | null
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
  { name: 'android-small', width: 360, height: 800, category: 'phone', enforceShellHardChecks: true },
  { name: 'iphone-se', width: 375, height: 667, category: 'phone', enforceShellHardChecks: true },
  { name: 'mobile', width: 390, height: 844, category: 'phone', enforceShellHardChecks: true },
  { name: 'iphone17-near', width: 393, height: 852, category: 'phone', enforceShellHardChecks: true },
  { name: 'phone-plus', width: 430, height: 932, category: 'phone', enforceShellHardChecks: true },
  { name: 'ipad-portrait', width: 768, height: 1024, category: 'tablet', enforceShellHardChecks: true },
  { name: 'ipad-landscape', width: 1024, height: 768, category: 'tablet', enforceShellHardChecks: true },
  { name: 'ipad-air-portrait', width: 820, height: 1180, category: 'tablet' },
  { name: 'ipad-air-landscape', width: 1180, height: 820, category: 'tablet' },
]

const shellOverflowSlugs = new Set([
  'dashboard',
  'addback',
  'messung',
  'grows',
  'grow-new',
  'zelte',
  'hydro',
  'hardware',
  'home-assistant',
])

const visualAuditTentName = 'E2E Visual Audit Empty Tent'
const visualAuditHydroName = 'E2E Visual Audit RDWC'
const visualAuditGrowName = 'E2E Visual Audit Grow'

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

    const liveScreen = document.querySelector('[data-audit="live-screen"]') as HTMLElement | null
    const liveDashboardFindings = liveScreen
      ? [
        (() => {
          const camera = document.querySelector('[data-audit="live-camera"]') as HTMLElement | null
          if (!camera) return null
          const rect = camera.getBoundingClientRect()
          return { selector: '[data-audit="live-camera"]', text: '', problem: 'cameraBlockHeight', details: `${Math.round(rect.height)}px` }
        })(),
        (() => {
          const foldBottom = isPhone ? window.innerHeight : Math.min(window.innerHeight, 1024)
          const cards = Array.from(liveScreen.querySelectorAll('.v1-card, .v1-section, [data-audit^="live-"]'))
          const visibleAboveFold = cards.filter((item) => {
            const rect = (item as HTMLElement).getBoundingClientRect()
            return rect.top < foldBottom && rect.bottom > 0 && rect.width > 1 && rect.height > 1
          }).length
          return { selector: '[data-audit="live-screen"]', text: '', problem: 'visibleCardsAboveFold', details: String(visibleAboveFold) }
        })(),
        isTablet ? { selector: '[data-audit="live-screen"]', text: '', problem: 'tabletLiveLayout', details: `viewport=${window.innerWidth}x${window.innerHeight}` } : null,
      ].filter((item): item is AuditFinding => item !== null)
      : []

    const addbackSurface = document.querySelector('[data-audit="addback-hub"], [data-audit="addback-flow"]') as HTMLElement | null
    const addbackFindings = addbackSurface
      ? [
        (() => {
          const cards = Array.from(addbackSurface.querySelectorAll('.v1-card, .v1-section, [data-audit^="addback-"]'))
          const foldBottom = isPhone ? window.innerHeight : Math.min(window.innerHeight, 1024)
          const visibleAboveFold = cards.filter((item) => {
            const rect = (item as HTMLElement).getBoundingClientRect()
            return rect.top < foldBottom && rect.bottom > 0 && rect.width > 1 && rect.height > 1
          }).length
          return { selector: '[data-audit^="addback-"]', text: '', problem: 'addbackCardsAboveFold', details: String(visibleAboveFold) }
        })(),
        (() => {
          const stepper = document.querySelector('[data-audit="addback-mobile-stepper"], [data-audit="addback-stepper"]') as HTMLElement | null
          if (!stepper) return null
          const rect = stepper.getBoundingClientRect()
          return { selector: '[data-audit="addback-stepper"]', text: '', problem: 'addbackStepperHeight', details: `${Math.round(rect.height)}px` }
        })(),
        isTablet ? { selector: '[data-audit^="addback-"]', text: '', problem: 'tabletAddbackLayout', details: `viewport=${window.innerWidth}x${window.innerHeight}` } : null,
      ].filter((item): item is AuditFinding => item !== null)
      : []

    const measurementForm = document.querySelector('[data-audit="measurement-form"]') as HTMLElement | null
    const measurementEmpty = document.querySelector('[data-audit="measurement-empty-state"]') as HTMLElement | null
    const measurementSave = document.querySelector('[data-audit="measurement-save-actions"]') as HTMLElement | null
    const measurementFileInput = measurementForm?.querySelector('.rc-file-input') as HTMLElement | null
    const measurementGroups = measurementForm ? Array.from(measurementForm.querySelectorAll('[data-audit="measurement-group"]')) : []
    const measurementControls = measurementForm
      ? Array.from(measurementForm.querySelectorAll('input, select, textarea, button, a'))
        .map((element) => {
          const html = element as HTMLElement
          const rect = html.getBoundingClientRect()
          const style = window.getComputedStyle(html)
          return {
            tag: html.tagName.toLowerCase(),
            selector: selectorFor(html),
            text: (html.textContent ?? html.getAttribute('aria-label') ?? '').trim().replace(/\s+/g, ' ').slice(0, 100),
            top: rect.top,
            bottom: rect.bottom,
            width: Math.round(rect.width),
            height: Math.round(rect.height),
            visible: style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1,
          }
        })
        .filter((item) => item.visible)
      : []
    const measurementSaveRect = measurementSave?.getBoundingClientRect() ?? null
    const measurementFileRect = measurementFileInput?.getBoundingClientRect() ?? null
    const measurementFindings = [
      isPhone && !measurementForm && measurementEmpty && !/Noch kein Grow für Messungen/i.test(measurementEmpty.textContent ?? '')
        ? { selector: '[data-audit="measurement-empty-state"]', text: (measurementEmpty.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 120), problem: 'measurementEmptyStateText', details: 'Expected short fresh-install empty state.' }
        : null,
      isPhone && !measurementForm && measurementEmpty && !Array.from(measurementEmpty.querySelectorAll('a, button')).some((item) => /Grow anlegen/i.test(item.textContent ?? ''))
        ? { selector: '[data-audit="measurement-empty-state"]', text: '', problem: 'measurementEmptyStateMissingGrowCta', details: 'Expected Grow anlegen CTA.' }
        : null,
      isPhone && measurementForm && measurementGroups.length < 4
        ? { selector: '[data-audit="measurement-group"]', text: '', problem: 'measurementGroupCountLow', details: String(measurementGroups.length) }
        : null,
      isPhone && measurementForm && !measurementSave
        ? { selector: '[data-audit="measurement-save-actions"]', text: '', problem: 'measurementSaveMissing', details: 'Expected save actions.' }
        : null,
      isPhone && measurementSaveRect && bottomNavRect && measurementSaveRect.bottom > bottomNavRect.top + 4 && measurementSaveRect.top < bottomNavRect.bottom - 4
        ? { selector: '[data-audit="measurement-save-actions"]', text: '', problem: 'measurementSaveUnderBottomNav', details: `saveBottom=${Math.round(measurementSaveRect.bottom)} navTop=${Math.round(bottomNavRect.top)}` }
        : null,
      isPhone && measurementFileRect && measurementFileRect.width > window.innerWidth
        ? { selector: '.rc-file-input', text: '', problem: 'measurementFileInputTooWide', details: `fileWidth=${Math.round(measurementFileRect.width)} viewport=${window.innerWidth}` }
        : null,
      ...measurementControls
        .filter((control) => {
          const isInput = control.tag === 'input' || control.tag === 'select' || control.tag === 'textarea'
          const minHeight = isInput ? 48 : 44
          return isPhone && control.height < minHeight
        })
        .map((control) => ({
          selector: control.selector,
          text: control.text,
          problem: 'measurementControlTooSmall',
          details: `${control.tag} ${control.width}x${control.height}`,
        })),
      isTablet && measurementForm
        ? { selector: '[data-audit="measurement-form"]', text: '', problem: 'tabletMeasurementLayout', details: `groups=${measurementGroups.length} fieldsAboveFold=${measurementControls.filter((control) => ['input', 'select', 'textarea'].includes(control.tag) && control.top < Math.min(window.innerHeight, 1024) && control.bottom > 0).length}` }
      : null,
    ].filter((item): item is AuditFinding => item !== null)

    const growsOverview = document.querySelector('[data-audit="grows-overview"]') as HTMLElement | null
    const growsEmpty = document.querySelector('[data-audit="grows-empty-state"]') as HTMLElement | null
    const growsCards = Array.from(document.querySelectorAll('.grow-overview-card')) as HTMLElement[]
    const firstGrowActions = document.querySelector('[data-audit="grow-list-actions"]') as HTMLElement | null
    const firstGrowActionsRect = firstGrowActions?.getBoundingClientRect() ?? null
    const growActionControls = Array.from(document.querySelectorAll('[data-audit="grow-list-actions"] a, [data-audit="grow-list-actions"] button'))
      .map((element) => {
        const html = element as HTMLElement
        const rect = html.getBoundingClientRect()
        const style = window.getComputedStyle(html)
        return {
          selector: selectorFor(html),
          text: (html.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 100),
          width: Math.round(rect.width),
          height: Math.round(rect.height),
          visible: style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1,
        }
      })
      .filter((item) => item.visible)
    const growDetail = document.querySelector('[data-audit="grow-detail"]') as HTMLElement | null
    const growDetailActions = document.querySelector('[data-audit="grow-detail-actions"]') as HTMLElement | null
    const growWizard = document.querySelector('[data-audit="grow-wizard"]') as HTMLElement | null
    const growsFindings = [
      isPhone && /\/grows(?:$|[?#])/i.test(window.location.pathname) && !growsOverview && !growsEmpty
        ? { selector: '[data-audit="grows-overview"]', text: '', problem: 'growsSurfaceMissing', details: 'Expected grows overview or empty state.' }
        : null,
      isPhone && growsEmpty && !Array.from(growsEmpty.querySelectorAll('a, button')).some((item) => /Neuen Grow anlegen/i.test(item.textContent ?? ''))
        ? { selector: '[data-audit="grows-empty-state"]', text: '', problem: 'growsEmptyMissingCreateCta', details: 'Expected Neuen Grow anlegen CTA.' }
        : null,
      isPhone && growsOverview && growsCards.length === 0
        ? { selector: '[data-audit="grows-overview"]', text: '', problem: 'growsCardMissing', details: 'Expected at least one grow card when overview is visible.' }
        : null,
      ...growActionControls
        .filter((control) => isPhone && (control.width < 44 || control.height < 44))
        .map((control) => ({
          selector: control.selector,
          text: control.text,
          problem: 'growActionTouchTargetBelow44px',
          details: `${control.width}x${control.height}`,
        })),
      isPhone && /\/grows\/\d+(?:$|[?#])/i.test(window.location.pathname) && !growDetail
        ? { selector: '[data-audit="grow-detail"]', text: '', problem: 'growDetailMissingHook', details: 'Expected mobile grow detail surface.' }
        : null,
      isPhone && growDetail && !growDetailActions
        ? { selector: '[data-audit="grow-detail-actions"]', text: '', problem: 'growDetailActionsMissing', details: 'Expected detail actions.' }
        : null,
      isPhone && /\/grows\/new(?:$|[?#])/i.test(window.location.pathname) && !growWizard
        ? { selector: '[data-audit="grow-wizard"]', text: '', problem: 'growWizardMissingHook', details: 'Expected grow wizard audit hook.' }
        : null,
      isTablet && /\/grows(?:$|[?#])/i.test(window.location.pathname)
        ? { selector: '[data-audit="grows-overview"]', text: '', problem: 'tabletGrowsLayout', details: `cardsAboveFold=${growsCards.filter((card) => { const rect = card.getBoundingClientRect(); return rect.top < Math.min(window.innerHeight, 1024) && rect.bottom > 0 }).length}` }
        : null,
    ].filter((item): item is AuditFinding => item !== null)

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
      liveDashboardFindings,
      addbackFindings,
      measurementFindings,
      measurementFormGroups: measurementForm ? measurementGroups.length : null,
      measurementFieldsAboveFold: measurementForm
        ? measurementControls.filter((control) => ['input', 'select', 'textarea'].includes(control.tag) && control.top < (isPhone ? window.innerHeight : Math.min(window.innerHeight, 1024)) && control.bottom > 0).length
        : null,
      growsFindings,
      growsCardsAboveFold: growsOverview
        ? growsCards.filter((card) => { const rect = card.getBoundingClientRect(); return rect.top < (isPhone ? window.innerHeight : Math.min(window.innerHeight, 1024)) && rect.bottom > 0 }).length
        : null,
      growsActionBarHeight: firstGrowActionsRect ? Math.round(firstGrowActionsRect.height) : null,
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
  copyRouteScreenshotAlias(viewport, route.slug, fileName)

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
    liveDashboardFindings: metrics.liveDashboardFindings,
    addbackFindings: metrics.addbackFindings,
    measurementFindings: metrics.measurementFindings,
    measurementFormGroups: metrics.measurementFormGroups,
    measurementFieldsAboveFold: metrics.measurementFieldsAboveFold,
    growsFindings: metrics.growsFindings,
    growsCardsAboveFold: metrics.growsCardsAboveFold,
    growsActionBarHeight: metrics.growsActionBarHeight,
  })

  if (viewport.enforceShellHardChecks && shellOverflowSlugs.has(route.slug)) {
    expect(
      metrics.bodyScrollWidth > metrics.innerWidth || metrics.documentScrollWidth > metrics.innerWidth,
      `${fileName}: shell route must not overflow horizontally body=${metrics.bodyScrollWidth} document=${metrics.documentScrollWidth} viewport=${metrics.innerWidth}`,
    ).toBe(false)
  }
  const coveredByBottomNav = metrics.bottomNavFindings.filter((item) => item.problem === 'coveredByBottomNav')
  if (viewport.category === 'phone' && viewport.enforceShellHardChecks && coveredByBottomNav.length > 0) {
    const details = coveredByBottomNav.slice(0, 5).map((item) => `${item.selector} "${item.text}" ${item.problem} bottom=${item.bottom} navTop=${item.bottomNavTop ?? 'n/a'} gap=${item.bottomNavGap ?? 'n/a'}`).join(' | ')
    throw new Error(`${fileName}: mobile bottom nav spacing issue: ${details}`)
  }
  if (viewport.category === 'phone' && viewport.enforceShellHardChecks) {
    await assertMobileShellContract(page, fileName)
    await assertAddbackFlowMobileContract(page, fileName)
    await assertMeasurementMobileContract(page, fileName)
    await assertGrowsMobileContract(page, fileName)
    await assertGrowWizardMobileContract(page, fileName)
  }
  if (viewport.category === 'tablet' && viewport.enforceShellHardChecks) {
    await assertTabletShellContract(page, fileName)
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

async function mockEmptyGrows(page: import('@playwright/test').Page) {
  await page.route('**/api/grows?archived=false', (route) => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }))
  await page.route('**/api/grows?archived=true', (route) => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }))
  await page.route('**/api/hydro-setups?includeArchived=true', (route) => route.fulfill({ status: 200, contentType: 'application/json', body: '[]' }))
}

async function unmockEmptyGrows(page: import('@playwright/test').Page) {
  await page.unroute('**/api/grows?archived=false')
  await page.unroute('**/api/grows?archived=true')
  await page.unroute('**/api/hydro-setups?includeArchived=true')
}

async function tryOpenFirstGrowDetail(page: import('@playwright/test').Page) {
  await page.goto('/grows', { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  const firstOpen = page.locator('[data-audit="grows-overview"] [data-audit="grow-list-actions"] a').filter({ hasText: /^Öffnen$/ }).first()
  if ((await firstOpen.count()) === 0) return false
  await firstOpen.scrollIntoViewIfNeeded()
  await Promise.all([
    page.waitForURL(/\/grows\/\d+$/i, { timeout: 4500 }).catch(() => null),
    firstOpen.click({ timeout: 3000 }),
  ])
  return /\/grows\/\d+$/i.test(page.url())
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
    liveDashboardFindings: metrics.liveDashboardFindings,
    addbackFindings: metrics.addbackFindings,
    measurementFindings: metrics.measurementFindings,
    measurementFormGroups: metrics.measurementFormGroups,
    measurementFieldsAboveFold: metrics.measurementFieldsAboveFold,
    growsFindings: metrics.growsFindings,
    growsCardsAboveFold: metrics.growsCardsAboveFold,
    growsActionBarHeight: metrics.growsActionBarHeight,
  })

  const coveredByBottomNav = metrics.bottomNavFindings.filter((item) => item.problem === 'coveredByBottomNav')
  if (viewport.category === 'phone' && viewport.enforceShellHardChecks && coveredByBottomNav.length > 0) {
    const details = coveredByBottomNav.slice(0, 5).map((item) => `${item.selector} "${item.text}" ${item.problem} bottom=${item.bottom} navTop=${item.bottomNavTop ?? 'n/a'} gap=${item.bottomNavGap ?? 'n/a'}`).join(' | ')
    throw new Error(`${fileName}: mobile bottom nav spacing issue: ${details}`)
  }
  if (viewport.category === 'phone' && viewport.enforceShellHardChecks) {
    await assertMobileShellContract(page, fileName)
    await assertAddbackFlowMobileContract(page, fileName)
    await assertMeasurementMobileContract(page, fileName)
    await assertGrowsMobileContract(page, fileName)
    await assertGrowWizardMobileContract(page, fileName)
  }
  if (viewport.category === 'tablet' && viewport.enforceShellHardChecks) {
    await assertTabletShellContract(page, fileName)
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
    '| Viewport | Route | Status | Overflow | Bottom Nav | Touch | Safe Area | Nav Structure | Tablet | Messung | Grows | Grow-Karten above fold | Grow-Aktionshöhe | Gruppen | Felder above fold | Heading | Screenshot | Note | PageError |',
    '|---|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|---|---|',
    ...reportRows.map((row) => {
      const note = row.note ? row.note.replace(/\|/g, '\\|').slice(0, 220) : ''
      const error = row.pageError ? row.pageError.replace(/\|/g, '\\|').slice(0, 160) : ''
      return `| ${row.viewport} | ${row.route} | ${row.status ?? ''} | ${row.horizontalOverflow ? 'YES' : 'no'} | ${row.bottomNavFindings.length} | ${row.touchTargetFindings.length} | ${row.safeAreaFindings.length} | ${row.navStructureFindings.length} | ${row.tabletLayoutFindings.length} | ${row.measurementFindings.length} | ${row.growsFindings.length} | ${row.growsCardsAboveFold ?? ''} | ${row.growsActionBarHeight ?? ''} | ${row.measurementFormGroups ?? ''} | ${row.measurementFieldsAboveFold ?? ''} | ${row.heading ?? ''} | ${row.screenshot} | ${note} | ${error} |`
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
      ...row.liveDashboardFindings.map((finding) => findingLine(row, 'liveDashboardFindings', finding)),
      ...row.addbackFindings.map((finding) => findingLine(row, 'addbackFindings', finding)),
      ...row.measurementFindings.map((finding) => findingLine(row, 'measurementFindings', finding)),
      ...row.growsFindings.map((finding) => findingLine(row, 'growsFindings', finding)),
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

function copyRouteScreenshotAlias(viewport: ViewportCase, slug: string, fileName: string) {
  const prefix = viewport.category === 'phone' ? 'mobile' : viewport.category === 'tablet' ? 'tablet' : null
  if (!prefix) return
  const aliasSlug = slug === 'messung' || slug === 'grows' || slug === 'grow-new' ? slug : null
  if (!aliasSlug) return
  const alias = `${prefix}-${viewport.width}x${viewport.height}-${aliasSlug}.png`
  if (alias === fileName) return
  fs.copyFileSync(path.join(outputDir, fileName), path.join(outputDir, alias))
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
        const useEmptyGrowsMock = route.slug === 'addback' || route.slug === 'hydro' || route.slug === 'messung'
        if (useEmptyGrowsMock) await mockEmptyGrows(page)
        try {
          await auditRoute(page, viewport, route, pageError)
          if (viewport.enforceShellHardChecks || viewport.category === 'desktop') {
            await assertRouteContract(page, route.slug)
          }
        } finally {
          if (useEmptyGrowsMock) await unmockEmptyGrows(page)
        }
        pageError = null
      }
    })

    test(`capture addback deep flow ${viewport.name}`, async ({ page }) => {
      await ensureVisualAuditData(page.request)
      let pageError: string | null = null
      page.on('pageerror', (error) => { pageError = error.message })
      await mockEmptyGrows(page)
      try {
        await auditAddbackDeepFlow(page, viewport, pageError)
      } finally {
        await unmockEmptyGrows(page)
      }
    })

    test(`capture grows fresh install ${viewport.name}`, async ({ page }) => {
      if (viewport.category !== 'phone') return
      await mockEmptyGrows(page)
      const response = await page.goto('/grows', { waitUntil: 'domcontentloaded' })
      await waitForAppIdle(page)
      expect(response?.status() ?? null, `${viewport.name}: /grows fresh install loads`).toBe(200)
      await assertMobileShellContract(page, `${viewport.name}: grows fresh install shell`)
      await assertGrowsMobileContract(page, `${viewport.name}: grows fresh install`)
      const fileName = `mobile-${viewport.width}x${viewport.height}-grows-empty.png`
      await page.screenshot({ path: path.join(outputDir, fileName), fullPage: true })
    })

    test(`capture grow detail when available ${viewport.name}`, async ({ page }) => {
      await ensureVisualAuditData(page.request)
      const opened = await tryOpenFirstGrowDetail(page)
      if (!opened) return
      await waitForAppIdle(page)
      await assertGrowDetailMobileContract(page, `${viewport.name}: grow detail`)
      const prefix = viewport.category === 'phone' ? 'mobile' : viewport.category === 'tablet' ? 'tablet' : null
      if (prefix) {
        await page.screenshot({ path: path.join(outputDir, `${prefix}-${viewport.width}x${viewport.height}-grow-detail.png`), fullPage: true })
      }
    })
  })
}

test.afterAll(() => {
  writeReports()
})

async function ensureVisualAuditData(request: import('@playwright/test').APIRequestContext) {
  const tents = await apiJson<AuditTent[]>(request, 'GET', '/api/settings/tents?includeArchived=true')
  const existingTent = tents.find((tent) => tent.name === visualAuditTentName && tent.status !== 'Archived')
  const tent = existingTent ?? await apiJson<AuditTent>(request, 'POST', '/api/settings/tents', {
    name: visualAuditTentName,
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

  const hydroSetups = await apiJson<AuditHydroSetup[]>(request, 'GET', '/api/hydro-setups?includeArchived=true')
  const hydro = hydroSetups.find((setup) => setup.name === visualAuditHydroName && setup.status !== 'Archived' && (setup.tentId == null || setup.tentId === tent.id))
    ?? await apiJson<AuditHydroSetup>(request, 'POST', '/api/hydro-setups', {
      tentId: tent.id,
      name: visualAuditHydroName,
      hydroStyle: 'RDWC',
      potCount: 2,
      potSizeLiters: 19,
      reservoirLiters: 45,
      layoutType: 'Row',
      reservoirPosition: 'Left',
      hasCirculationPump: true,
      circulationPumpNotes: null,
      hasAirPump: true,
      airPumpNotes: null,
      airStoneCount: 2,
      hasChiller: false,
      hasUvSterilizer: false,
      notes: 'Automatisch angelegte Testdaten fuer Playwright Visual Audit',
      displayOrder: 9101,
    })

  const grows = await apiJson<AuditGrowSummary[]>(request, 'GET', '/api/grows?archived=false')
  const existingGrow = grows.find((grow) => grow.name === visualAuditGrowName && (grow.systemId === hydro.id || grow.setupId === hydro.id))
  if (existingGrow) return existingGrow.id

  const createdGrow = await apiJson<AuditGrowSummary>(request, 'POST', '/api/grows', {
    name: visualAuditGrowName,
    tentId: hydro.tentId ?? tent.id,
    systemId: hydro.id,
    setupId: null,
    hydroStyle: 'RDWC',
    startDate: '2026-01-03',
    status: 'Running',
    environment: 'Indoor',
    seedType: 'Feminized',
    startMaterial: 'Seed',
    waterSource: 'RO',
    strain: 'Audit Kush',
    breeder: 'Audit',
  })

  return createdGrow.id
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
    await assertGrowsMobileContract(page, slug)
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
    await assertAddbackHubMobileContract(page, slug)
  }
  if (slug === 'dashboard') {
    await assertLiveMobileContract(page, slug)
  }
  if (slug === 'messung') {
    await assertMeasurementMobileContract(page, slug)
  }
  if (slug === 'grow-new') {
    await assertGrowWizardMobileContract(page, slug)
  }

  await assertNoAsciiUmlautUiText(page, slug)
}

async function assertAddbackHubMobileContract(page: import('@playwright/test').Page, slug: string) {
  const isPhone = await page.evaluate(() => window.innerWidth < 768)
  if (!isPhone) return

  await expect(page.locator('[data-audit="addback-hub"]'), `${slug}: Addback hub audit hook`).toBeVisible()
  await expect(page.locator('[data-audit="addback-stepper"], .v1-addback-flow-strip'), `${slug}: hub must not render workflow stepper`).toHaveCount(0)

  const hasStartCta = await page.getByRole('link', { name: /Addback starten/i }).count()
  const emptyState = page.locator('[data-audit="addback-empty-state"]')
  if (await emptyState.isVisible().catch(() => false)) {
    await expect(emptyState).toContainText(/Kein aktiver Hydro-Grow/i)
    await expect(emptyState.getByRole('link', { name: /Grow anlegen|Hydro öffnen/i }).first()).toBeVisible()
  } else {
    expect(hasStartCta, `${slug}: Addback hub needs visible Addback start CTA when data exists`).toBeGreaterThan(0)
  }

  const actions = await page.locator('[data-audit="addback-hub"] a, [data-audit="addback-hub"] button').evaluateAll((items) =>
    items
      .map((item) => {
        const html = item as HTMLElement
        const rect = html.getBoundingClientRect()
        const style = window.getComputedStyle(html)
        return {
          text: (html.textContent ?? '').trim().replace(/\s+/g, ' '),
          width: Math.round(rect.width),
          height: Math.round(rect.height),
          visible: style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1,
        }
      })
      .filter((item) => item.visible))
  expect(actions.length, `${slug}: Addback hub must expose visible actions`).toBeGreaterThan(0)
  for (const action of actions) {
    expect(action.width, `${slug}: Addback action "${action.text}" touch width`).toBeGreaterThanOrEqual(44)
    expect(action.height, `${slug}: Addback action "${action.text}" touch height`).toBeGreaterThanOrEqual(44)
  }

  const overflow = await page.locator('[data-audit="addback-hub"] .v1-info, [data-audit="addback-log-list"] *').evaluateAll((items) =>
    items
      .filter((item) => {
        const html = item as HTMLElement
        const rect = html.getBoundingClientRect()
        const style = window.getComputedStyle(html)
        return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && html.scrollWidth > html.clientWidth + 2
      })
      .map((item) => (item.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 120)))
  expect(overflow, `${slug}: Addback Verlauf/values must not overflow cards`).toEqual([])
}

async function assertAddbackFlowMobileContract(page: import('@playwright/test').Page, context: string) {
  const isPhone = await page.evaluate(() => window.innerWidth < 768)
  if (!isPhone) return
  if (!/\/grows\/[^/]+\/addback/i.test(page.url())) return

  await expect(page.locator('[data-audit="addback-flow"]'), `${context}: Addback flow audit hook`).toBeVisible()
  await expect(page.locator('[data-audit="addback-mobile-stepper"]'), `${context}: compact mobile stepper`).toBeVisible()
  const stepperHeight = await page.locator('[data-audit="addback-mobile-stepper"]').evaluate((element) => Math.round((element as HTMLElement).getBoundingClientRect().height))
  expect(stepperHeight, `${context}: mobile stepper must stay compact`).toBeLessThanOrEqual(96)

  const controls = await page.locator('[data-audit="addback-flow"] input, [data-audit="addback-flow"] select, [data-audit="addback-flow"] textarea, [data-audit="addback-flow"] button, [data-audit="addback-flow"] a').evaluateAll((items) =>
    items.map((item) => {
      const html = item as HTMLElement
      const rect = html.getBoundingClientRect()
      const style = window.getComputedStyle(html)
      return {
        tag: html.tagName.toLowerCase(),
        text: (html.textContent ?? html.getAttribute('aria-label') ?? '').trim().replace(/\s+/g, ' '),
        width: Math.round(rect.width),
        height: Math.round(rect.height),
        visible: style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1,
      }
    }).filter((item) => item.visible))
  for (const control of controls) {
    const minHeight = control.tag === 'input' || control.tag === 'select' || control.tag === 'textarea' ? 48 : 44
    expect(control.width, `${context}: Addback control "${control.text || control.tag}" touch width`).toBeGreaterThanOrEqual(44)
    expect(control.height, `${context}: Addback control "${control.text || control.tag}" height`).toBeGreaterThanOrEqual(minHeight)
  }
}

async function assertLiveMobileContract(page: import('@playwright/test').Page, slug: string) {
  await expect(page.locator('[data-audit="live-screen"]'), `${slug}: Live screen audit hook`).toBeVisible()

  const isPhone = await page.evaluate(() => window.innerWidth < 768)
  if (!isPhone) return

  const emptyState = page.locator('[data-audit="live-empty-state"]')
  if (await emptyState.isVisible().catch(() => false)) {
    await expect(emptyState).toContainText(/Noch kein aktiver Grow/i)
    await expect(emptyState.getByRole('link', { name: /Grow anlegen/i })).toBeVisible()
    const ctas = await emptyState.locator('a, button').evaluateAll((items) =>
      items.map((item) => {
        const html = item as HTMLElement
        const rect = html.getBoundingClientRect()
        return { text: (html.textContent ?? '').trim().replace(/\s+/g, ' '), width: Math.round(rect.width), height: Math.round(rect.height) }
      }))
    expect(ctas.length, `${slug}: fresh install empty state must expose setup CTAs`).toBeGreaterThan(0)
    for (const cta of ctas) {
      expect(cta.width, `${slug}: empty-state CTA "${cta.text}" touch width`).toBeGreaterThanOrEqual(44)
      expect(cta.height, `${slug}: empty-state CTA "${cta.text}" touch height`).toBeGreaterThanOrEqual(44)
    }
    return
  }

  await expect(page.locator('[data-audit="live-status-card"]'), `${slug}: phone status card first`).toBeVisible()
  await expect(page.locator('[data-audit="live-climate-card"]'), `${slug}: phone climate card`).toBeVisible()
  await expect(page.locator('[data-audit="live-sensor-card"]'), `${slug}: phone sensor card`).toBeVisible()
  await expect(page.locator('[data-audit="live-quick-actions"]'), `${slug}: phone quick actions`).toBeVisible()

  const order = await page.evaluate(() => {
    const names = ['live-status-card', 'live-climate-card', 'live-sensor-card', 'live-camera', 'live-quick-actions']
    return names
      .map((name) => {
        const element = document.querySelector(`[data-audit="${name}"]`) as HTMLElement | null
        const rect = element?.getBoundingClientRect() ?? null
        return rect && rect.width > 1 && rect.height > 1 ? { name, top: Math.round(rect.top) } : null
      })
      .filter((item): item is { name: string; top: number } => item !== null)
  })
  const statusTop = order.find((item) => item.name === 'live-status-card')?.top ?? 0
  const climateTop = order.find((item) => item.name === 'live-climate-card')?.top ?? 0
  const sensorTop = order.find((item) => item.name === 'live-sensor-card')?.top ?? 0
  expect(statusTop, `${slug}: status card must be before climate`).toBeLessThanOrEqual(climateTop)
  expect(climateTop, `${slug}: climate card must be before sensor`).toBeLessThanOrEqual(sensorTop)

  const actions = await page.locator('[data-audit="live-quick-actions"] a, [data-audit="live-quick-actions"] button').evaluateAll((items) =>
    items
      .map((item) => {
        const html = item as HTMLElement
        const rect = html.getBoundingClientRect()
        const style = window.getComputedStyle(html)
        return {
          text: (html.textContent ?? '').trim().replace(/\s+/g, ' '),
          width: Math.round(rect.width),
          height: Math.round(rect.height),
          visible: style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1,
        }
      })
      .filter((item) => item.visible))
  expect(actions.length, `${slug}: quick actions must contain visible actions`).toBeGreaterThan(0)
  for (const action of actions) {
    expect(action.width, `${slug}: live action "${action.text}" touch width`).toBeGreaterThanOrEqual(44)
    expect(action.height, `${slug}: live action "${action.text}" touch height`).toBeGreaterThanOrEqual(44)
  }

}

async function assertMeasurementMobileContract(page: import('@playwright/test').Page, context: string) {
  const isPhone = await page.evaluate(() => window.innerWidth < 768)
  if (!isPhone) return
  if (!/\/messung(?:$|[?#])/i.test(page.url())) return

  await expect(page.getByRole('heading', { name: /Messung erfassen/i }), `${context}: /messung page heading`).toBeVisible()

  const emptyState = page.locator('[data-audit="measurement-empty-state"]')
  if (await emptyState.isVisible().catch(() => false)) {
    await expect(emptyState, `${context}: fresh install empty title`).toContainText(/Noch kein Grow für Messungen/i)
    await expect(emptyState.getByRole('link', { name: /Grow anlegen/i }), `${context}: fresh install Grow CTA`).toBeVisible()
    await expect(page.locator('[data-audit="measurement-form"]'), `${context}: no broken form without grow context`).toHaveCount(0)
    const ctas = await emptyState.locator('a, button').evaluateAll((items) =>
      items.map((item) => {
        const html = item as HTMLElement
        const rect = html.getBoundingClientRect()
        return { text: (html.textContent ?? '').trim().replace(/\s+/g, ' '), width: Math.round(rect.width), height: Math.round(rect.height) }
      }))
    for (const cta of ctas) {
      expect(cta.width, `${context}: empty-state CTA "${cta.text}" touch width`).toBeGreaterThanOrEqual(44)
      expect(cta.height, `${context}: empty-state CTA "${cta.text}" touch height`).toBeGreaterThanOrEqual(44)
    }
    return
  }

  const form = page.locator('[data-audit="measurement-form"]')
  await expect(form, `${context}: measurement form or empty state must render`).toBeVisible()
  await expect(page.locator('[data-audit="measurement-save-actions"]'), `${context}: save actions must render`).toBeVisible()

  const groups = await page.locator('[data-audit="measurement-form"] [data-audit="measurement-group"]').count()
  expect(groups, `${context}: measurement form group count`).toBeGreaterThanOrEqual(4)

  const controls = await page.locator('[data-audit="measurement-form"] input, [data-audit="measurement-form"] select, [data-audit="measurement-form"] textarea, [data-audit="measurement-form"] button, [data-audit="measurement-form"] a').evaluateAll((items) =>
    items.map((item) => {
      const html = item as HTMLElement
      const rect = html.getBoundingClientRect()
      const style = window.getComputedStyle(html)
      return {
        tag: html.tagName.toLowerCase(),
        text: (html.textContent ?? html.getAttribute('aria-label') ?? '').trim().replace(/\s+/g, ' '),
        width: Math.round(rect.width),
        height: Math.round(rect.height),
        visible: style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1,
      }
    }).filter((item) => item.visible))

  for (const control of controls) {
    const minHeight = control.tag === 'input' || control.tag === 'select' || control.tag === 'textarea' ? 48 : 44
    expect(control.width, `${context}: measurement control "${control.text || control.tag}" touch width`).toBeGreaterThanOrEqual(44)
    expect(control.height, `${context}: measurement control "${control.text || control.tag}" height`).toBeGreaterThanOrEqual(minHeight)
  }

  const saveLayout = await page.locator('[data-audit="measurement-save-actions"]').evaluate((element) => {
    const rect = (element as HTMLElement).getBoundingClientRect()
    const bottomNav = document.querySelector('.v1-bottom-nav') as HTMLElement | null
    const navRect = bottomNav?.getBoundingClientRect() ?? null
    return {
      visible: rect.width > 1 && rect.height > 1 && rect.bottom > 0 && rect.top < window.innerHeight,
      bottom: Math.round(rect.bottom),
      navTop: navRect ? Math.round(navRect.top) : window.innerHeight,
    }
  })
  expect(saveLayout.visible, `${context}: save button must be visible`).toBe(true)
  expect(saveLayout.bottom, `${context}: save button must stay above bottom nav`).toBeLessThanOrEqual(saveLayout.navTop - 4)

  const fileInputWidths = await page.locator('[data-audit="measurement-form"] .rc-file-input').evaluateAll((items) =>
    items.map((item) => {
      const rect = (item as HTMLElement).getBoundingClientRect()
      return { width: Math.round(rect.width), viewport: window.innerWidth }
    }))
  expect(fileInputWidths.length, `${context}: photo/file input must be present`).toBeGreaterThan(0)
  for (const fileInput of fileInputWidths) {
    expect(fileInput.width, `${context}: photo/file input must not exceed viewport`).toBeLessThanOrEqual(fileInput.viewport)
  }
}

async function assertGrowsMobileContract(page: import('@playwright/test').Page, context: string) {
  const isPhone = await page.evaluate(() => window.innerWidth < 768)
  if (!/\/grows(?:$|[?#])/i.test(page.url())) return

  await expect(page.getByRole('heading', { name: /^Grows$/i }), `${context}: /grows heading`).toBeVisible()
  await expect(page.getByRole('link', { name: /Neuen Grow anlegen/i }).first(), `${context}: create grow CTA`).toBeVisible()
  await expect(page.getByRole('link', { name: /Grow starten/i }), `${context}: Grows overview must not link primary nav directly into wizard wording`).toHaveCount(0)

  if (!isPhone) return

  const emptyState = page.locator('[data-audit="grows-empty-state"]')
  if (await emptyState.isVisible().catch(() => false)) {
    await expect(emptyState, `${context}: empty state text`).toContainText(/Noch kein Grow/i)
    await expect(emptyState.getByRole('link', { name: /Neuen Grow anlegen/i }), `${context}: empty create CTA`).toBeVisible()
    await expect(page.locator('[data-audit="grows-overview"] .grow-overview-card'), `${context}: empty state must not render grow cards`).toHaveCount(0)
    const ctas = await emptyState.locator('a, button').evaluateAll((items) =>
      items.map((item) => {
        const html = item as HTMLElement
        const rect = html.getBoundingClientRect()
        return { text: (html.textContent ?? '').trim().replace(/\s+/g, ' '), width: Math.round(rect.width), height: Math.round(rect.height) }
      }))
    for (const cta of ctas) {
      expect(cta.width, `${context}: empty CTA "${cta.text}" touch width`).toBeGreaterThanOrEqual(44)
      expect(cta.height, `${context}: empty CTA "${cta.text}" touch height`).toBeGreaterThanOrEqual(44)
    }
    return
  }

  const firstCard = page.locator('[data-audit="grows-overview"] .grow-overview-card').first()
  await expect(firstCard, `${context}: grow card visible`).toBeVisible()
  await expect(firstCard.getByRole('link', { name: /^Öffnen$/i }), `${context}: Öffnen action`).toBeVisible()
  await expect(firstCard.getByRole('link', { name: /^Bearbeiten$/i }), `${context}: Bearbeiten action`).toBeVisible()
  await expect(firstCard.getByRole('button', { name: /^Beenden$/i }), `${context}: Beenden action`).toBeVisible()
  await expect(firstCard.getByRole('button', { name: /^Löschen$/i }), `${context}: Löschen action`).toBeVisible()

  const actions = await firstCard.locator('[data-audit="grow-list-actions"] a, [data-audit="grow-list-actions"] button').evaluateAll((items) =>
    items.map((item) => {
      const html = item as HTMLElement
      const rect = html.getBoundingClientRect()
      const style = window.getComputedStyle(html)
      return {
        text: (html.textContent ?? '').trim().replace(/\s+/g, ' '),
        width: Math.round(rect.width),
        height: Math.round(rect.height),
        visible: style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1,
      }
    }).filter((item) => item.visible))
  for (const action of actions) {
    expect(action.width, `${context}: grow action "${action.text}" touch width`).toBeGreaterThanOrEqual(44)
    expect(action.height, `${context}: grow action "${action.text}" touch height`).toBeGreaterThanOrEqual(44)
  }
}

async function assertGrowDetailMobileContract(page: import('@playwright/test').Page, context: string) {
  const isPhone = await page.evaluate(() => window.innerWidth < 768)
  if (!isPhone) return
  if (!/\/grows\/\d+(?:$|[?#])/i.test(page.url())) return

  const detail = page.locator('[data-audit="grow-detail"]')
  await expect(detail, `${context}: grow detail audit hook`).toBeVisible()
  await expect(page.locator('[data-audit="grow-detail-summary"]'), `${context}: detail summary`).toBeVisible()
  await expect(page.locator('[data-audit="grow-detail-actions"]'), `${context}: detail actions`).toBeVisible()
  await expect(page.locator('[data-audit="grow-detail-actions"]').getByRole('link', { name: /Bearbeiten/i }), `${context}: edit action`).toBeVisible()
  await expect(page.locator('[data-audit="grow-detail-actions"]').getByRole('button', { name: /^(Beenden|Löschen)$/i }).first(), `${context}: destructive actions`).toBeVisible()

  const actions = await page.locator('[data-audit="grow-detail-actions"] a, [data-audit="grow-detail-actions"] button').evaluateAll((items) =>
    items.map((item) => {
      const html = item as HTMLElement
      const rect = html.getBoundingClientRect()
      return { text: (html.textContent ?? '').trim().replace(/\s+/g, ' '), width: Math.round(rect.width), height: Math.round(rect.height) }
    }))
  for (const action of actions) {
    expect(action.width, `${context}: detail action "${action.text}" touch width`).toBeGreaterThanOrEqual(44)
    expect(action.height, `${context}: detail action "${action.text}" touch height`).toBeGreaterThanOrEqual(44)
  }
}

async function assertGrowWizardMobileContract(page: import('@playwright/test').Page, context: string) {
  const isPhone = await page.evaluate(() => window.innerWidth < 768)
  if (!isPhone) return
  if (!/\/grows\/new(?:$|[?#])/i.test(page.url()) && !/\/grows\/\d+\/setup(?:$|[?#])/i.test(page.url())) return

  await expect(page.locator('[data-audit="grow-wizard"]'), `${context}: grow wizard audit hook`).toBeVisible()
  const steps = await page.locator('[data-audit="grow-wizard"] .v1-wizard-step').count()
  expect(steps, `${context}: grow wizard step count must stay complete`).toBe(6)
  await expect(page.locator('[data-audit="grow-wizard-actions"]'), `${context}: grow wizard actions`).toBeVisible()
  const actions = await page.locator('[data-audit="grow-wizard-actions"] button, [data-audit="grow-wizard-actions"] a').evaluateAll((items) =>
    items.map((item) => {
      const html = item as HTMLElement
      const rect = html.getBoundingClientRect()
      return { text: (html.textContent ?? '').trim().replace(/\s+/g, ' '), width: Math.round(rect.width), height: Math.round(rect.height) }
    }))
  for (const action of actions) {
    expect(action.width, `${context}: wizard action "${action.text}" touch width`).toBeGreaterThanOrEqual(44)
    expect(action.height, `${context}: wizard action "${action.text}" touch height`).toBeGreaterThanOrEqual(44)
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

async function assertMobileShellContract(page: import('@playwright/test').Page, context: string) {
  await assertMobileBottomNavDocked(page, context)

  const shell = await page.evaluate(() => {
    const topbar = document.querySelector('.v1-mobile-topbar') as HTMLElement | null
    const bottomNav = document.querySelector('.v1-bottom-nav') as HTMLElement | null
    const moreButton = document.querySelector('.v1-mobile-more-button') as HTMLElement | null
    const bottomItems = Array.from(document.querySelectorAll('.v1-bottom-nav a, .v1-bottom-nav button')).map((element) => {
      const html = element as HTMLElement
      const rect = html.getBoundingClientRect()
      return {
        text: (html.textContent ?? '').trim().replace(/\s+/g, ' '),
        width: Math.round(rect.width),
        height: Math.round(rect.height),
      }
    })
    const topbarRect = topbar?.getBoundingClientRect() ?? null
    const moreButtonRect = moreButton?.getBoundingClientRect() ?? null
    const bottomNavRect = bottomNav?.getBoundingClientRect() ?? null
    const bottomNavStyle = bottomNav ? window.getComputedStyle(bottomNav) : null

    const topbarChildTops = Array.from(topbar?.children ?? [])
      .map((child) => child.getBoundingClientRect().top)
      .filter(Number.isFinite)

    return {
      topbarTop: topbarRect ? Math.round(topbarRect.top) : null,
      topbarFirstContentTop: topbarChildTops.length > 0 ? Math.round(Math.min(...topbarChildTops)) : null,
      moreButtonWidth: moreButtonRect ? Math.round(moreButtonRect.width) : 0,
      moreButtonHeight: moreButtonRect ? Math.round(moreButtonRect.height) : 0,
      bottomItems,
      bottomGap: bottomNavRect ? Math.round(Math.abs(window.innerHeight - bottomNavRect.bottom)) : null,
      bottomBackground: bottomNavStyle?.backgroundColor ?? '',
    }
  })

  expect(shell.topbarTop, `${context}: mobile header must exist`).not.toBeNull()
  expect(shell.topbarFirstContentTop, `${context}: mobile header content must exist`).not.toBeNull()
  expect(shell.bottomItems.map((item) => item.text), `${context}: phone bottom nav labels`).toEqual(['Live', 'Addback', 'Messung', 'Grows'])
  expect(shell.bottomItems.map((item) => item.text), `${context}: phone bottom nav must not contain secondary setup items`).not.toEqual(expect.arrayContaining(['Zelte', 'Hydro']))
  expect(shell.topbarFirstContentTop, `${context}: mobile header content must not touch y=0`).toBeGreaterThan(0)
  expect(shell.moreButtonWidth, `${context}: more button touch width`).toBeGreaterThanOrEqual(44)
  expect(shell.moreButtonHeight, `${context}: more button touch height`).toBeGreaterThanOrEqual(44)
  for (const item of shell.bottomItems) {
    expect(item.width, `${context}: bottom nav item "${item.text}" touch width`).toBeGreaterThanOrEqual(44)
    expect(item.height, `${context}: bottom nav item "${item.text}" touch height`).toBeGreaterThanOrEqual(44)
  }
  expect(shell.bottomGap, `${context}: mobile bottom nav must be docked`).toBeLessThanOrEqual(1)
  expect(shell.bottomBackground, `${context}: mobile bottom nav background must be opaque`).not.toMatch(/rgba\(0,\s*0,\s*0,\s*0\)|transparent/i)

  await page.locator('.v1-mobile-more-button').click()
  await expect(page.locator('[data-audit="mobile-more-menu"]')).toBeVisible()
  await expect(page.locator('[data-audit="mobile-more-group-setup"]')).toBeVisible()
  await expect(page.locator('[data-audit="mobile-more-group-integration"]')).toBeVisible()
  await expect(page.locator('[data-audit="mobile-more-group-system"]')).toBeVisible()
  for (const label of ['Zelte', 'Hydro', 'Sensoren', 'Home Assistant', 'Gerät verbinden', 'Wissen', 'Einstellungen', 'Release']) {
    await expect(page.locator('[data-audit="mobile-more-menu"]').getByRole('link', { name: label })).toBeVisible()
  }
  const panel = await page.locator('[data-audit="mobile-more-menu"]').evaluate((element) => {
    const html = element as HTMLElement
    const rect = html.getBoundingClientRect()
    const bottomNav = document.querySelector('.v1-bottom-nav') as HTMLElement | null
    const bottomNavRect = bottomNav?.getBoundingClientRect() ?? null
    return {
      top: Math.round(rect.top),
      bottom: Math.round(rect.bottom),
      bottomNavTop: bottomNavRect ? Math.round(bottomNavRect.top) : window.innerHeight,
      scrollable: html.scrollHeight >= html.clientHeight,
    }
  })
  expect(panel.top, `${context}: more menu must not touch notch/top edge`).toBeGreaterThan(0)
  expect(panel.bottom, `${context}: more menu must stay above bottom nav`).toBeLessThanOrEqual(panel.bottomNavTop)
  expect(panel.scrollable, `${context}: more menu must be scroll-capable`).toBe(true)
  await page.locator('.v1-mobile-more-button').click()
  await expect(page.locator('[data-audit="mobile-more-menu"]')).toHaveCount(0)
}

async function assertTabletShellContract(page: import('@playwright/test').Page, context: string) {
  const shell = await page.evaluate(() => {
    const desktopNav = document.querySelector('.v1-desktop-nav') as HTMLElement | null
    const bottomNav = document.querySelector('.v1-bottom-nav') as HTMLElement | null
    const desktopRect = desktopNav?.getBoundingClientRect() ?? null
    const bottomRect = bottomNav?.getBoundingClientRect() ?? null
    const desktopStyle = desktopNav ? window.getComputedStyle(desktopNav) : null
    const bottomStyle = bottomNav ? window.getComputedStyle(bottomNav) : null
    return {
      desktopNavVisible: Boolean(desktopStyle && desktopStyle.display !== 'none' && desktopRect && desktopRect.width > 1 && desktopRect.height > 1),
      bottomNavVisible: Boolean(bottomStyle && bottomStyle.display !== 'none' && bottomRect && bottomRect.width > 1 && bottomRect.height > 1),
      desktopNavWidth: desktopRect ? Math.round(desktopRect.width) : 0,
    }
  })

  expect(shell.desktopNavVisible, `${context}: tablet should use sidebar/adaptive navigation, not phone-only shell`).toBe(true)
  expect(shell.desktopNavWidth, `${context}: tablet sidebar must remain touch-friendly`).toBeGreaterThanOrEqual(220)
  expect(shell.bottomNavVisible, `${context}: tablet must not force phone bottom nav`).toBe(false)
}

async function assertNoAsciiUmlautActions(page: import('@playwright/test').Page, slug: string) {
  const offenders = await page.locator('button, a, [role="button"], [role="link"]').evaluateAll((items) =>
    items
      .map((item) => (item.textContent ?? '').trim().replace(/\s+/g, ' '))
      .filter((text) => /\b(Loeschen|Loescht|geloescht|endgueltig|Oeffnen|Zurueck|waehle|laedt|bestaetigen|moeglich|verknuepft)\b/i.test(text)))
  expect(offenders, `${slug}: visible actions must use German umlauts`).toEqual([])
}

async function assertNoAsciiUmlautUiText(page: import('@playwright/test').Page, slug: string) {
  const offenders = await page
    .locator('button, a, label, h1, h2, h3, h4, h5, h6, [role="button"], [role="link"], [role="alert"], .v1-empty, .v1-stat, .v1-info, .panel-card-title, .rc-file-input')
    .evaluateAll((items) => {
      const asciiUmlautPattern = /\b(?:Oeffnen|oeffnen|Loeschen|loeschen|Loesung|loesung|Loescht|loescht|geloescht|Zurueck|zurueck|gewaehlt|ausgewaehlt|waehlen|waehle|Geraete|geraete|muessen|koennen|fuer|ueber|spaeter|moeglich|verknuepft|ungueltig|bestaetigen|laedt|Naehrstoff|naehrstoff|Naehrloesung|naehrloesung|ergaenzen|Ergaenzen|verfuegbar|Spruenge|Stabilitaet|beruecksichtigen|Sofortmassnahmen|Verschleiss|haengen|gehoeren|staerkere|auswaehlbar)\b/i
      return items
        .map((item) => {
          const html = item as HTMLElement
          const rect = html.getBoundingClientRect()
          const style = window.getComputedStyle(html)
          const visible = style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1
          if (!visible || html.closest('[aria-hidden="true"]')) return ''
          return (html.textContent ?? '').trim().replace(/\s+/g, ' ')
        })
        .filter((text) => text && asciiUmlautPattern.test(text))
    })

  expect(offenders, `${slug}: visible UI text must use German umlauts`).toEqual([])
}
