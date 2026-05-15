import { test, type Page } from '@playwright/test'
import fs from 'node:fs/promises'
import path from 'node:path'

type RouteTarget = {
  path: string
  name: string
}

type ViewportTarget = {
  name: string
  width: number
  height: number
}

type RouteResult = {
  viewport: string
  route: string
  path: string
  screenshot: string
  httpStatus: number | null
  title: string
  heading: string
  innerWidth: number
  bodyScrollWidth: number
  documentScrollWidth: number
  hasHorizontalOverflow: boolean
  capturedAtUtc: string
  note?: string
}

type AuditMessage = {
  type: string
  target: string
  text: string
  url?: string
}

const routes: RouteTarget[] = [
  { path: '/', name: 'dashboard' },
  { path: '/addback', name: 'addback' },
  { path: '/action', name: 'action' },
  { path: '/zelte', name: 'zelte' },
  { path: '/hydro', name: 'hydro' },
  { path: '/home-assistant', name: 'home-assistant' },
  { path: '/grows/new', name: 'grow-starten' },
  { path: '/settings', name: 'settings' },
  { path: '/wissen', name: 'wissen' },
  { path: '/hardware', name: 'hardware' },
]

const viewports: ViewportTarget[] = [
  { name: 'desktop-1440x1000', width: 1440, height: 1000 },
  { name: 'mobile-390x844', width: 390, height: 844 },
]

const auditName = process.env.GROW_OS_AUDIT_NAME ?? 'visual-audit-current'
const auditRoot = path.resolve(process.cwd(), '..', 'artifacts', auditName)

test.describe.configure({ mode: 'serial' })

test('capture Grow OS visual audit screenshots', async ({ page }) => {
  await fs.rm(auditRoot, { recursive: true, force: true })
  await fs.mkdir(auditRoot, { recursive: true })

  const results: RouteResult[] = []
  const messages: AuditMessage[] = []
  let activeTarget = 'bootstrap'

  page.on('console', (message) => {
    if (message.type() === 'error' || message.type() === 'warning') {
      messages.push({
        type: `console:${message.type()}`,
        target: activeTarget,
        text: trimMessage(message.text()),
      })
    }
  })

  page.on('pageerror', (error) => {
    messages.push({
      type: 'pageerror',
      target: activeTarget,
      text: trimMessage(error.message),
    })
  })

  page.on('requestfailed', (request) => {
    const failure = request.failure()
    messages.push({
      type: 'requestfailed',
      target: activeTarget,
      text: trimMessage(failure?.errorText ?? 'request failed'),
      url: request.url(),
    })
  })

  for (const viewport of viewports) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height })

    for (const route of routes) {
      activeTarget = `${viewport.name}:${route.path}`
      const screenshotFileName = `${viewport.name}-${route.name}.png`
      const screenshotPath = path.join(auditRoot, screenshotFileName)
      let httpStatus: number | null = null
      let note: string | undefined

      try {
        const response = await page.goto(route.path, { waitUntil: 'domcontentloaded', timeout: 30_000 })
        httpStatus = response?.status() ?? null
        await page.waitForLoadState('networkidle', { timeout: 5_000 }).catch(() => undefined)
        await page.waitForTimeout(500)
      } catch (error) {
        note = error instanceof Error ? error.message : String(error)
      }

      const layout = await readLayout(page).catch((error) => ({
        title: 'n/a',
        heading: 'n/a',
        innerWidth: viewport.width,
        bodyScrollWidth: viewport.width,
        documentScrollWidth: viewport.width,
        hasHorizontalOverflow: false,
        note: error instanceof Error ? error.message : String(error),
      }))

      await page.screenshot({ path: screenshotPath, fullPage: true, animations: 'disabled' }).catch((error) => {
        note = error instanceof Error ? error.message : String(error)
      })

      results.push({
        viewport: viewport.name,
        route: route.name,
        path: route.path,
        screenshot: screenshotFileName,
        httpStatus,
        title: layout.title,
        heading: layout.heading,
        innerWidth: layout.innerWidth,
        bodyScrollWidth: layout.bodyScrollWidth,
        documentScrollWidth: layout.documentScrollWidth,
        hasHorizontalOverflow: layout.hasHorizontalOverflow,
        capturedAtUtc: new Date().toISOString(),
        note: note ?? layout.note,
      })
    }
  }

  await fs.writeFile(path.join(auditRoot, 'visual-audit-report.json'), `${JSON.stringify({ results, messages }, null, 2)}\n`, 'utf-8')
  await fs.writeFile(path.join(auditRoot, 'visual-audit-report.md'), buildMarkdownReport(results, messages), 'utf-8')
})

async function readLayout(page: Page) {
  return page.evaluate(() => {
    const heading = document.querySelector('h1')?.textContent?.trim() || document.querySelector('h2')?.textContent?.trim() || ''
    const bodyScrollWidth = document.body?.scrollWidth ?? 0
    const documentScrollWidth = document.documentElement?.scrollWidth ?? 0
    const innerWidth = window.innerWidth

    return {
      title: document.title,
      heading,
      innerWidth,
      bodyScrollWidth,
      documentScrollWidth,
      hasHorizontalOverflow: Math.max(bodyScrollWidth, documentScrollWidth) > innerWidth + 1,
    }
  })
}

function buildMarkdownReport(results: RouteResult[], messages: AuditMessage[]): string {
  const lines = [
    '# Grow OS Visual Audit',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
    '## Screenshots',
    '',
    '| Viewport | Route | HTTP | Overflow | Heading | Screenshot | Note |',
    '|---|---|---:|---|---|---|---|',
  ]

  for (const result of results) {
    lines.push(
      `| ${escapeCell(result.viewport)} | ${escapeCell(result.path)} | ${result.httpStatus ?? ''} | ${result.hasHorizontalOverflow ? 'YES' : 'no'} | ${escapeCell(result.heading)} | ${escapeCell(result.screenshot)} | ${escapeCell(result.note ?? '')} |`,
    )
  }

  lines.push('', '## Console / Network Hinweise', '')

  if (messages.length === 0) {
    lines.push('Keine Console-Warnungen, Console-Errors oder fehlgeschlagenen Requests protokolliert.')
  } else {
    lines.push('| Typ | Ziel | Meldung | URL |', '|---|---|---|---|')
    for (const message of messages.slice(0, 200)) {
      lines.push(`| ${escapeCell(message.type)} | ${escapeCell(message.target)} | ${escapeCell(message.text)} | ${escapeCell(message.url ?? '')} |`)
    }

    if (messages.length > 200) {
      lines.push('', `Weitere Meldungen gekürzt: ${messages.length - 200}`)
    }
  }

  lines.push('', '## Hinweise', '')
  lines.push('- Die Screenshots sind bewusst ein Audit-Artefakt, kein Approval-Test.')
  lines.push('- Ein Overflow-Eintrag bedeutet: `scrollWidth` war größer als `innerWidth`.')
  lines.push('- Fehlende Daten oder leere Zustände werden so sichtbar, wie die App sie lokal rendert.')

  return `${lines.join('\n')}\n`
}

function escapeCell(value: string): string {
  return value.replace(/\|/g, '\\|').replace(/\r?\n/g, ' ').trim()
}

function trimMessage(value: string): string {
  return value.replace(/\s+/g, ' ').trim().slice(0, 500)
}
