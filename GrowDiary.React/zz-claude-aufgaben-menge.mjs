import { chromium } from '@playwright/test'

const BASE = process.env.BASE ?? 'http://localhost:5076'

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1400, height: 1200 } })
await page.goto(`${BASE}/aufgaben`, { waitUntil: 'networkidle' })
await page.waitForTimeout(2500)

const result = await page.evaluate(() => {
  const termine = document.querySelector('[data-audit="af-termine"]')
  const rows = termine ? [...termine.querySelectorAll('.af-row')] : []
  // Sidebar / nav badge: find any nav link mentioning Aufgaben
  const navTexts = [...document.querySelectorAll('a, nav *')]
    .map((el) => (el.textContent || '').trim())
    .filter((t) => /Aufgaben/.test(t) && t.length < 40)
  return {
    panelHeader: termine?.querySelector('.ls-panel-meta')?.textContent?.trim() ?? null,
    rowCount: rows.length,
    rowTitles: rows.map((r) => r.querySelector('.af-title')?.textContent?.trim() ?? '?'),
    navTexts: [...new Set(navTexts)],
    bodyHasSkeleton: !!document.querySelector('[class*="skeleton" i]'),
  }
})

console.log(JSON.stringify(result, null, 2))

// Is there any "mehr anzeigen" / pagination control anywhere on the page?
const moreControls = await page.evaluate(() => {
  const all = [...document.querySelectorAll('button, a')]
  return all
    .map((el) => (el.textContent || '').trim())
    .filter((t) => /mehr|weitere|alle|nächste|naechste|seite|more/i.test(t))
})
console.log('possible "more" controls:', JSON.stringify(moreControls))

await page.screenshot({ path: process.env.SHOT ?? 'zz-aufgaben-repro.png', fullPage: true })
await browser.close()
