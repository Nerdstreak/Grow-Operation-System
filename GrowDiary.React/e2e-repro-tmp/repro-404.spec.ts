import { test, expect } from '@playwright/test'

const paths = [
  '/gibtsnicht',
  '/aushaerten/foo',
  '/sorten/99999',
  '/messung/99999',
  '/foo/bar/baz',
  '/grows/99999', // Gegenprobe: soll "Nicht gefunden" zeigen
  '/',            // Gegenprobe: bekannte Route
]

for (const p of paths) {
  test(`route ${p}`, async ({ page }) => {
    const consoleErrors: string[] = []
    page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()) })
    const resp = await page.goto(`http://localhost:5076${p}`, { waitUntil: 'networkidle' })
    await page.waitForTimeout(1500)
    const status = resp?.status()
    const mainText = await page.evaluate(() => {
      const m = document.querySelector('main')
      return m ? (m as HTMLElement).innerText : '(kein <main>)'
    })
    const mainHtmlLen = await page.evaluate(() => {
      const m = document.querySelector('main')
      return m ? m.innerHTML.length : -1
    })
    const bodyText = await page.evaluate(() => document.body.innerText)
    console.log('='.repeat(70))
    console.log(`PATH ${p}  HTTP ${status}  mainHtmlLen=${mainHtmlLen}`)
    console.log(`--- main.innerText ---\n${mainText}`)
    console.log(`--- body.innerText (erste 600) ---\n${bodyText.slice(0, 600)}`)
    console.log(`--- console errors: ${JSON.stringify(consoleErrors)}`)
    await page.screenshot({ path: `C:/Users/mkles/AppData/Local/Temp/claude/D--Grow-Operation-System-new/ca535cbd-9108-4901-aa5f-dfbeb293132c/scratchpad/shot${p.replace(/\//g, '_')}.png`, fullPage: true })
    expect(true).toBe(true)
  })
}
