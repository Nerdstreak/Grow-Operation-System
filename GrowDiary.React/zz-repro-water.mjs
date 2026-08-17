import { chromium } from '@playwright/test'

const BASE = 'http://localhost:5076'
const MARK = 'ZZCLAUDE-WASSERQUELLE'

function field(page, label) {
  return page.locator(`label.v1-field:has(> span:text-is(${JSON.stringify(label)}))`)
}

const run = async () => {
  const browser = await chromium.launch()
  const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } })
  const posts = []
  page.on('response', async (res) => {
    if (res.request().method() === 'POST' && res.url().includes('/addback/logs')) {
      posts.push({ url: res.url(), status: res.status(), body: await res.text().catch(() => '') })
    }
  })

  await page.goto(`${BASE}/grows/1/addback`, { waitUntil: 'networkidle' })
  await page.waitForSelector('[data-audit="addback-flow"] .addback-step-panel', { timeout: 20000 })

  await field(page, 'Aktuelles Volumen').locator('input').fill('100')
  await field(page, 'EC aktuell').locator('input').fill('1,20')
  await field(page, 'pH aktuell').locator('input').fill('6,1')
  await field(page, 'Ziel-EC').locator('input').fill('1,80')
  await field(page, 'Addback-EC').locator('input').fill('3,00')

  const wasser = field(page, 'Wasser').locator('select')
  console.log('--- Wasser-Feld Optionen ---')
  console.log(await wasser.locator('option').allTextContents())
  await wasser.selectOption('Tap')
  console.log('gewaehlt:', await wasser.inputValue())

  await page.getByRole('button', { name: /Prüfen & Dosierung berechnen/ }).click()
  await page.waitForTimeout(1200)

  await field(page, 'EC nach Addback').locator('input').fill('1,78')
  await field(page, 'pH nach Addback').locator('input').fill('5,9')
  await field(page, 'Notizen').locator('textarea').fill(MARK)

  console.log('--- Raster "Prüfen & Speichern" VOR dem Speichern ---')
  console.log(await page.locator('.addback-review-grid').innerText())

  await page.getByRole('button', { name: /^Addback speichern$/ }).click()
  await page.waitForTimeout(2000)
  console.log('--- POST-Antworten ---')
  for (const p of posts) console.log(p.status, p.body.slice(0, 700))

  // Nach dem Speichern, ohne Reload
  console.log('--- Liste "Letzte Addbacks" direkt nach dem Speichern ---')
  console.log(await page.locator('[data-audit="addback-log-list"]').innerText())

  await page.reload({ waitUntil: 'networkidle' })
  await page.waitForSelector('[data-audit="addback-log-list"]', { timeout: 20000 })
  console.log('--- Liste "Letzte Addbacks" NACH Reload ---')
  console.log(await page.locator('[data-audit="addback-log-list"]').innerText())
  console.log('--- Raster "Prüfen & Speichern" NACH Reload ---')
  console.log(await page.locator('.addback-review-grid').innerText())
  console.log('--- Volltext der Seite: kommt "Leitungswasser"/"Osmose" irgendwo vor? ---')
  const text = await page.locator('body').innerText()
  for (const needle of ['Leitungswasser', 'Osmose', 'Mischung', 'Tap']) {
    const hits = [...text.matchAll(new RegExp(needle, 'g'))].length
    console.log(`${needle}: ${hits}x`)
  }
  await page.screenshot({ path: 'C:/Users/mkles/AppData/Local/Temp/claude/D--Grow-Operation-System-new/ca535cbd-9108-4901-aa5f-dfbeb293132c/scratchpad/assistent-verlauf.png', fullPage: true })

  // Hub
  await page.goto(`${BASE}/addback`, { waitUntil: 'networkidle' })
  await page.waitForSelector('[data-audit="addback-log-list"]', { timeout: 20000 })
  console.log('--- Hub: Verlauf ---')
  console.log(await page.locator('[data-audit="addback-log-list"]').innerText())

  await page.getByRole('button', { name: /Wechsel erfassen/ }).click()
  await page.waitForSelector('[data-audit="changeout-form"]', { timeout: 10000 })
  console.log('--- Wasserwechsel-Formular: Felder ---')
  console.log(await page.locator('[data-audit="changeout-form"]').locator('label.v1-field > span').allTextContents())
  console.log('--- Wasserwechsel-Liste ---')
  const list = page.locator('[data-audit="changeout-list"]')
  console.log((await list.count()) ? await list.innerText() : '(keine Eintraege)')
  await page.screenshot({ path: 'C:/Users/mkles/AppData/Local/Temp/claude/D--Grow-Operation-System-new/ca535cbd-9108-4901-aa5f-dfbeb293132c/scratchpad/hub-wechsel.png', fullPage: true })

  await browser.close()
}

run().catch((error) => { console.error(error); process.exit(1) })
