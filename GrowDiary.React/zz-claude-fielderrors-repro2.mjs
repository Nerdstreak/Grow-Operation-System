import { chromium } from '@playwright/test'

const BASE = 'http://localhost:5076'
const browser = await chromium.launch()
const page = await browser.newPage()
const responses = []
page.on('response', async (res) => {
  if (res.request().method() === 'POST' && res.url().includes('/measurements')) {
    try { responses.push(res.status() + ' ' + (await res.text())) } catch { /* egal */ }
  }
})

// Index 14 = "INPUT PH" (irrigationPh) — vom Live-Check NICHT abgedeckt.
await page.goto(`${BASE}/messung`, { waitUntil: 'networkidle' })
await page.waitForTimeout(1500)
const select = page.locator('[data-audit="measurement-section-context"] select').first()
await select.selectOption({ index: 0 })
await page.waitForTimeout(1200)
const numeric = page.locator('.rc2-measurement-grid input')
const n = await numeric.count()
for (let i = 0; i < n; i++) await numeric.nth(i).fill('')
await numeric.nth(14).fill('99')
await page.waitForTimeout(500)

const panel = (await page.locator('[data-audit="measurement-section-check"]').innerText()).replace(/\s+/g, ' ').trim()
await page.getByRole('button', { name: 'Messung speichern' }).click()
await page.waitForTimeout(2500)

const alertText = (await page.locator('.v1-alert').first().innerText()).replace(/\s+/g, ' ').trim()
const marked = await page.evaluate(() => {
  const out = []
  document.querySelectorAll('.rc2-measurement-grid input').forEach((el) => { if (el.className) out.push(el.className + '=' + el.value) })
  return out
})

console.log('=== Input pH 99 (kein Live-Check-Feld) ===')
console.log('Server   :', responses.join(' | '))
console.log('Panel    :', panel)
console.log('Markiert :', JSON.stringify(marked))
console.log('Meldung  :', alertText)
await page.screenshot({ path: 'zz-claude-fielderrors-inputph99.png' })
await browser.close()
