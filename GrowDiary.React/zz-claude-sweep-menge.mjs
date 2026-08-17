import { chromium } from '@playwright/test'
const routes = ['/aufgaben','/journal?growId=1','/grows/1','/grows','/sops?growId=1','/dashboard','/']
const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1400, height: 1400 } })
for (const r of routes) {
  try {
    await page.goto('http://localhost:5076' + r, { waitUntil: 'networkidle' })
    await page.waitForTimeout(2200)
    const txt = await page.evaluate(() => document.body.innerText)
    const found = [1,2,3,4,5,6,7,8].filter(n => txt.includes(`ZZTEST-Menge ${n}`))
    console.log(r.padEnd(22), '-> ZZTEST-Menge visible:', found.length ? found.join(',') : 'NONE')
  } catch (e) { console.log(r.padEnd(22), '-> error', e.message.slice(0,80)) }
}
await browser.close()
