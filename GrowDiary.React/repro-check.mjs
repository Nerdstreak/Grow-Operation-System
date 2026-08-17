import { chromium } from '@playwright/test'

const BASE = 'http://localhost:5076'

const countMeasurements = async () => {
  const res = await fetch(`${BASE}/api/grows/1/measurements`)
  const json = await res.json()
  return Array.isArray(json) ? json.length : JSON.stringify(json).slice(0, 200)
}

const before = await countMeasurements()
console.log('Messungen VORHER:', before)

const browser = await chromium.launch()
const page = await browser.newPage()

const nonGet = []
page.on('request', (r) => {
  if (r.method() !== 'GET') nonGet.push(`${r.method()} ${r.url()}`)
})
const consoleErrors = []
page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()) })
page.on('pageerror', (e) => consoleErrors.push('PAGEERROR ' + e.message))

await page.goto(`${BASE}/messungen?growId=1`, { waitUntil: 'networkidle' })

// Warten bis das Formular wirklich da ist
await page.getByText('Messung eintragen').waitFor({ timeout: 15000 })

// Felder ausfuellen wie der Pruefer
const phInput = page.locator('input[placeholder="5.8"]')
const ecInput = page.locator('input[placeholder="1.6"]')
await phInput.fill('6.0')
await ecInput.fill('1.7')

const btn = page.getByRole('button', { name: /Messung speichern/ })
console.log('Knopf sichtbar:', await btn.isVisible(), '| aktiv:', await btn.isEnabled())
console.log('Knopf type-Attribut:', await btn.getAttribute('type'))

// Ist der Knopf im Formular? Und wie viele submit-Knoepfe hat das Formular?
const formInfo = await btn.evaluate((el) => {
  const form = el.closest('form')
  return {
    imFormular: !!form,
    hatFormAttribut: el.getAttribute('form'),
    submitKnoepfeImFormular: form ? form.querySelectorAll('button[type=submit], input[type=submit], button:not([type])').length : -1,
    formularHatOnSubmit: !!form,
  }
})
console.log('Form-Info:', JSON.stringify(formInfo))

nonGet.length = 0
await btn.click()
await page.waitForTimeout(2500)

console.log('Netzwerk (ausser GET) nach Klick:', nonGet.length ? nonGet.join(', ') : '(KEINE Anfrage)')

// Stehen die Werte noch im Formular?
console.log('pH-Feld nach Klick:', await phInput.inputValue(), '| EC-Feld:', await ecInput.inputValue())

const bodyText = await page.locator('body').innerText()
console.log('Rueckmeldung "gespeichert" sichtbar:', bodyText.includes('gespeichert'))
console.log('Fehlermeldung sichtbar:', bodyText.includes('Fehler'))

// Gegenprobe: Enter im pH-Feld
nonGet.length = 0
await phInput.click()
await phInput.press('Enter')
await page.waitForTimeout(1500)
console.log('Netzwerk nach ENTER im pH-Feld:', nonGet.length ? nonGet.join(', ') : '(KEINE Anfrage)')

// Gegenprobe: kuenstlich submit ausloesen -> feuert der Handler ueberhaupt?
nonGet.length = 0
await page.evaluate(() => {
  const form = document.querySelector('form')
  form.requestSubmit ? form.requestSubmit() : form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
})
await page.waitForTimeout(2500)
console.log('Netzwerk nach kuenstlichem requestSubmit:', nonGet.length ? nonGet.join(', ') : '(KEINE Anfrage)')

console.log('Konsolenfehler:', consoleErrors.length ? consoleErrors.join(' | ') : '(keine)')

await browser.close()

const after = await countMeasurements()
console.log('Messungen NACHHER:', after)
