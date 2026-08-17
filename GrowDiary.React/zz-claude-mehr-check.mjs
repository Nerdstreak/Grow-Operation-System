import { chromium } from '@playwright/test'
const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1400, height: 1200 } })
await page.goto('http://localhost:5076/aufgaben', { waitUntil: 'networkidle' })
await page.waitForTimeout(2000)
const info = await page.evaluate(() => {
  const els = [...document.querySelectorAll('button,a')].filter(e => /^Mehr$/i.test((e.textContent||'').trim()))
  return els.map(e => ({ tag: e.tagName, cls: e.className, html: e.outerHTML.slice(0,200),
    inTermine: !!e.closest('[data-audit="af-termine"]'),
    parentAudit: e.closest('[data-audit]')?.getAttribute('data-audit') ?? null,
    inNav: !!e.closest('nav') }))
})
console.log('Mehr elements:', JSON.stringify(info, null, 2))
// click it and see if more task rows appear
const before = await page.locator('[data-audit="af-termine"] .af-row').count()
try { await page.getByRole('button', { name: /^Mehr$/i }).first().click({ timeout: 3000 }) }
catch { try { await page.getByRole('link', { name: /^Mehr$/i }).first().click({ timeout: 3000 }) } catch(e) { console.log('click failed', e.message) } }
await page.waitForTimeout(1500)
const after = await page.locator('[data-audit="af-termine"] .af-row').count()
console.log('termine rows before click:', before, ' after click:', after, ' url now:', page.url())
await browser.close()
