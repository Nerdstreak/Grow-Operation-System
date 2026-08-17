import { chromium } from '@playwright/test'

const BASE = 'http://localhost:5076'
const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1400, height: 1000 } })

// 1) Detailseite des abgebrochenen Grows
await page.goto(`${BASE}/grows/33`, { waitUntil: 'networkidle' })
await page.waitForTimeout(1500)
const detailText = await page.locator('body').innerText()
console.log('--- /grows/33 Auszug ---')
console.log(detailText.split('\n').slice(0, 30).join('\n'))
const tabHref = await page.locator('a.gd-tab', { hasText: 'Messungen' }).getAttribute('href')
console.log('Messungen-Reiter href:', tabHref)

// 2) Reiter klicken
await page.locator('a.gd-tab', { hasText: 'Messungen' }).click()
await page.waitForURL(/messungen/)
await page.waitForTimeout(2000)
console.log('URL nach Klick:', page.url())

const sel = page.locator('.v1-scope-picker select')
console.log('select.value        :', await sel.inputValue())
console.log('select angezeigt    :', await sel.evaluate(el => el.selectedIndex >= 0 ? el.options[el.selectedIndex].text : '(keine)'))
console.log('select.selectedIndex:', await sel.evaluate(el => el.selectedIndex))
console.log('Optionen            :', await sel.evaluate(el => [...el.options].map(o => `${o.value}:${o.text}`)))
console.log('gs-back Links       :', await page.locator('a.gs-back').count())
console.log('Links zu /grows/33  :', await page.locator('a[href*="/grows/33"]').count())

const body = await page.locator('body').innerText()
const idx = body.indexOf('Messungen')
console.log('--- /messungen Auszug ---')
console.log(body.slice(idx, idx + 900))

await page.screenshot({ path: 'C:/Users/mkles/AppData/Local/Temp/claude/D--Grow-Operation-System-new/ca535cbd-9108-4901-aa5f-dfbeb293132c/scratchpad/messungen-33.png', fullPage: false })

// 3) Gegenprobe: laufender Grow 1
await page.goto(`${BASE}/messungen?growId=1`, { waitUntil: 'networkidle' })
await page.waitForTimeout(1500)
console.log('--- Gegenprobe growId=1 ---')
console.log('select.value :', await page.locator('.v1-scope-picker select').inputValue())
console.log('gs-back      :', await page.locator('a.gs-back').count())

// 4) Die anderen gemeldeten Seiten
for (const p of ['journal', 'diagnose', 'sops']) {
  await page.goto(`${BASE}/${p}?growId=33`, { waitUntil: 'networkidle' })
  await page.waitForTimeout(1200)
  const s = page.locator('.v1-scope-picker select')
  console.log(`/${p}?growId=33 -> value=${await s.inputValue()} angezeigt="${await s.evaluate(el => el.selectedIndex >= 0 ? el.options[el.selectedIndex].text : '(keine)')}" back=${await page.locator('a.gs-back').count()}`)
}

await browser.close()
