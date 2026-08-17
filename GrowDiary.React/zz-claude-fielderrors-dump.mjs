import { chromium } from '@playwright/test'
const BASE = 'http://localhost:5076'
const browser = await chromium.launch()
const page = await browser.newPage()
await page.goto(`${BASE}/messung`, { waitUntil: 'networkidle' })
await page.waitForTimeout(1500)
const select = page.locator('[data-audit="measurement-section-context"] select').first()
const options = await select.locator('option').allTextContents()
console.log('Grows:', options)
const target = options.find((o) => o.includes('Purple Lemonade')) ?? options[0]
await select.selectOption({ label: target })
await page.waitForTimeout(1200)

const info = await page.evaluate(() => {
  const out = []
  document.querySelectorAll('.rc2-measurement-grid input').forEach((el, i) => {
    const wrap = el.closest('label') || el.parentElement?.parentElement
    out.push({ i, label: (wrap?.innerText || '').replace(/\s+/g, ' ').trim().slice(0, 60), cls: wrap?.className, value: el.value })
  })
  return out
})
console.log(JSON.stringify(info, null, 1))
await browser.close()
