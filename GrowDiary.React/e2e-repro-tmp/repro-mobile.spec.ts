import { test, expect } from '@playwright/test'

test('mobil: unbekannte Adresse', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('http://localhost:5076/gibtsnicht', { waitUntil: 'networkidle' })
  await page.waitForTimeout(1200)
  const mainText = await page.evaluate(() => (document.querySelector('main') as HTMLElement)?.innerText ?? '(kein main)')
  const links = await page.evaluate(() =>
    Array.from(document.querySelectorAll('a')).filter((a) => (a as HTMLElement).offsetParent !== null).map((a) => a.getAttribute('href')))
  console.log('MOBIL main.innerText:', JSON.stringify(mainText))
  console.log('MOBIL sichtbare Links:', JSON.stringify(links))
  await page.screenshot({ path: 'C:/Users/mkles/AppData/Local/Temp/claude/D--Grow-Operation-System-new/ca535cbd-9108-4901-aa5f-dfbeb293132c/scratchpad/shot_mobil.png', fullPage: true })
  expect(true).toBe(true)
})
