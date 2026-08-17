import { chromium } from '@playwright/test'

const BASE = 'http://localhost:5076'
const browser = await chromium.launch()
const page = await browser.newPage()

const nonGet = []
page.on('request', (r) => { if (r.method() !== 'GET') nonGet.push(`${r.method()} ${r.url()}`) })

await page.goto(`${BASE}/messungen?growId=1`, { waitUntil: 'networkidle' })
await page.getByText('Messung eintragen').waitFor({ timeout: 15000 })

await page.locator('input[placeholder="5.8"]').fill('6.11')
await page.locator('input[placeholder="1.6"]').fill('1.71')

const btn = page.getByRole('button', { name: /Messung speichern/ })

// EINZIGE Aenderung: type="button" -> type="submit"
await btn.evaluate((el) => el.setAttribute('type', 'submit'))
console.log('Knopf type jetzt:', await btn.getAttribute('type'))

nonGet.length = 0
await btn.click()
await page.waitForTimeout(2500)
console.log('Netzwerk nach Klick (nur type geaendert):', nonGet.length ? nonGet.join(', ') : '(KEINE Anfrage)')

const bodyText = await page.locator('body').innerText()
console.log('Rueckmeldung "Messung gespeichert" sichtbar:', bodyText.includes('Messung gespeichert'))
console.log('pH-Feld nach Klick (leer = zurueckgesetzt):', JSON.stringify(await page.locator('input[placeholder="5.8"]').inputValue()))

await browser.close()
