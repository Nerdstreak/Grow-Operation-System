import { test, expect } from '@playwright/test'

/**
 * Keine Seite darf mit einem Fehler in der Konsole oder einem gescheiterten
 * Request aufmachen.
 *
 * Beides sieht man beim Durchklicken nicht: ein React-Warnung über doppelte
 * Keys, ein 500er auf einem Nebenpfad, ein Bild, das nie ankommt — die Seite
 * sieht trotzdem richtig aus und ist es nicht. Dieser Test schaut dorthin,
 * wo man beim Ansehen nicht hinschaut.
 *
 * Erlaubt sind nur Fehler, die zum Testaufbau gehören und nicht zur App:
 * Home Assistant ist hier nicht erreichbar, also scheitern Kamerabilder und
 * Entity-Abrufe — das ist der dokumentierte Offline-Fall.
 */
const ROUTEN = [
  '/', '/messung', '/addback', '/aufgaben',
  '/grows', '/grows/1', '/grows/new', '/diagnose', '/journal', '/sorten', '/archiv',
  '/zelte', '/zelte/1', '/hydro', '/sensoren', '/regeln', '/home-assistant',
  '/wissen', '/settings', '/start', '/release',
  '/regeln?tab=automatik', '/regeln?tab=push', '/regeln?tab=ki',
]

/** Was am Testaufbau liegt, nicht an der App. */
function istAufbauRauschen(text: string): boolean {
  return /\/api\/live\/tents\/\d+\/camera/.test(text)
    || /\/api\/home-assistant\/entities/.test(text)
    || /favicon/.test(text)
}

for (const route of ROUTEN) {
  test(`ohne Fehler: ${route}`, async ({ page }) => {
    const konsole: string[] = []
    const netz: string[] = []

    page.on('console', (msg) => {
      if (msg.type() !== 'error') return
      // „Failed to load resource" nennt die URL nicht im Text, sondern nur in
      // der Herkunft — ohne die liesse sich Aufbau-Rauschen nicht aussortieren.
      const text = `${msg.text()} ${msg.location()?.url ?? ''}`.trim()
      if (!istAufbauRauschen(text)) konsole.push(text)
    })
    page.on('pageerror', (error) => konsole.push(`pageerror: ${error.message}`))
    page.on('response', (response) => {
      if (response.status() < 400) return
      const text = `${response.status()} ${response.url()}`
      if (!istAufbauRauschen(text)) netz.push(text)
    })

    await page.goto(route, { waitUntil: 'networkidle' })
    // Kurz nachlaufen lassen: Effekte holen nach dem ersten Anstrich nach.
    await page.waitForTimeout(400)

    expect(konsole, `Konsolenfehler auf ${route}:\n${konsole.join('\n')}`).toEqual([])
    expect(netz, `Gescheiterte Requests auf ${route}:\n${netz.join('\n')}`).toEqual([])
  })
}
