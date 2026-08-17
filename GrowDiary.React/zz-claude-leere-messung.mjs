import { chromium } from '@playwright/test'

const BASE = 'http://localhost:5076'
const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1500, height: 1100 } })

const calls = []
page.on('request', (r) => {
  if (r.url().includes('/measurements') && r.method() === 'POST') {
    calls.push({ dir: 'REQ', url: r.url().replace(BASE, ''), body: r.postData() })
  }
})
page.on('response', async (r) => {
  if (r.url().includes('/measurements') && r.request().method() === 'POST') {
    let body = ''
    try { body = await r.text() } catch {}
    calls.push({ dir: 'RES', status: r.status(), body: body.slice(0, 1500) })
  }
})

// Dialoge (confirm/alert) protokollieren statt automatisch zu schliessen
const dialogs = []
page.on('dialog', async (d) => {
  dialogs.push({ type: d.type(), message: d.message() })
  await d.accept()
})

await page.goto(`${BASE}/messung`, { waitUntil: 'networkidle' })
await page.waitForTimeout(2500)

const growSelect = page.locator('[data-audit="measurement-section-context"] select').first()
console.log('Grow gewaehlt:', await growSelect.inputValue())

const badge = page.locator('[data-audit="measurement-section-context"] .v1-badge').first()
console.log('Abzeichen VORHER:', (await badge.innerText()).trim())

// Alle Zahlenfelder im Formular auflisten und leeren
const inputs = page.locator('form.ms-layout input[inputmode="decimal"]')
const n = await inputs.count()
let befuellt = 0
for (let i = 0; i < n; i += 1) {
  const v = await inputs.nth(i).inputValue()
  if (v.trim() !== '') befuellt += 1
}
console.log(`Zahlenfelder: ${n}, davon vorbefuellt: ${befuellt}`)

for (let i = 0; i < n; i += 1) {
  await inputs.nth(i).fill('')
}
await page.waitForTimeout(600)

// Notiz leer lassen, Lösungswechsel aus
const notiz = page.locator('form.ms-layout textarea').first()
console.log('Notiz-Inhalt:', JSON.stringify(await notiz.inputValue()))

console.log('Abzeichen NACHHER:', (await badge.innerText()).trim())

await page.screenshot({ path: 'zz-claude-leere-messung-vorher.png', fullPage: true })

const urlVorher = page.url()
await page.getByRole('button', { name: 'Messung speichern' }).click()
await page.waitForTimeout(3000)

console.log('---- Netzwerk ----')
for (const c of calls) console.log(JSON.stringify(c))
console.log('---- Dialoge ----', JSON.stringify(dialogs))
console.log('URL vorher:', urlVorher.replace(BASE, ''))
console.log('URL nachher:', page.url().replace(BASE, ''))

await page.screenshot({ path: 'zz-claude-leere-messung-nachher.png', fullPage: true })

await browser.close()
