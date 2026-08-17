import { chromium } from '@playwright/test'

const BASE = 'http://localhost:5076'
const log = (...a) => console.log(...a)

const browser = await chromium.launch()
const page = await browser.newPage()

const responses = []
page.on('response', async (res) => {
  if (res.request().method() === 'POST' && res.url().includes('/measurements')) {
    let body = ''
    try { body = await res.text() } catch { body = '<unlesbar>' }
    responses.push({ status: res.status(), body })
  }
})

// Feld-Indizes im .rc2-measurement-grid (aus dem Dump):
// 1 = Luftfeuchte, 5 = pH (reservoirPh), 6 = EC (reservoirEc)
async function versuch(index, wert, name) {
  responses.length = 0
  await page.goto(`${BASE}/messung`, { waitUntil: 'networkidle' })
  await page.waitForTimeout(1200)

  const select = page.locator('[data-audit="measurement-section-context"] select').first()
  const options = await select.locator('option').allTextContents()
  const target = options.find((o) => o.includes('Purple Lemonade')) ?? options[0]
  await select.selectOption({ label: target })
  await page.waitForTimeout(1200)

  const numeric = page.locator('.rc2-measurement-grid input')
  const n = await numeric.count()
  for (let i = 0; i < n; i++) await numeric.nth(i).fill('')
  await numeric.nth(index).fill(wert)
  await page.waitForTimeout(600)

  let panel = '<kein Panel>'
  try {
    panel = (await page.locator('[data-audit="measurement-section-check"]').innerText()).replace(/\s+/g, ' ').trim()
  } catch { /* egal */ }

  await page.getByRole('button', { name: 'Messung speichern' }).click()
  await page.waitForTimeout(2500)

  const alerts = page.locator('.v1-alert')
  const count = await alerts.count()
  const alertText = count > 0
    ? (await alerts.first().innerText()).replace(/\s+/g, ' ').trim()
    : '<keine Meldung>'

  // Rot markierte Felder?
  const marked = await page.evaluate(() => {
    const out = []
    document.querySelectorAll('.rc2-measurement-grid input').forEach((el) => {
      if (el.className) out.push({ cls: el.className, val: el.value })
    })
    return out
  })

  log('')
  log('=== ' + name + ' ===')
  log('Server-Antwort  :', responses.map((r) => `${r.status} ${r.body}`).join(' | ') || '<kein POST>')
  log('Live-Check      :', panel)
  log('Markierte Felder:', JSON.stringify(marked))
  log('Meldungskasten  :', alertText)
  log('URL danach      :', page.url())

  await page.screenshot({ path: `zz-claude-fielderrors-${name}.png` })
}

try {
  await versuch(5, '99', 'ph99')
  await versuch(6, '-5', 'ec-minus5')
  await versuch(1, '500', 'rh500')
} finally {
  await browser.close()
}
