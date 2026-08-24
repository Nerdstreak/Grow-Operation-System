import { test, expect, type Page } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { darfUeberspringen } from './pflicht'
import { TEXTSEITEN } from './seiten'

/**
 * Kein englischer Enum-Wert steht roh auf dem Bildschirm.
 *
 * <b>Der Anlass.</b> Das wichtigste Formular der App — „Grow starten" — bot
 * am 24.08.2026 an: Samen-Typ <i>Feminized / Autoflower / Regular</i>,
 * Startmaterial <i>Seed / Clone</i>, Startpunkt <i>Germination / Seedling /
 * Veg / Flower / Flush</i>, Status <i>Planning / Running / Completed /
 * Aborted</i>. Dazu <i>Grid2x2</i>, <i>Production</i> und <i>External</i> auf
 * den Zelt- und Hydro-Karten. Insgesamt 29 rohe Werte über die Seiten verteilt.
 *
 * <b>Warum der vorhandene Test das nicht gesehen hat.</b>
 * <c>deutsche-woerter.node.test.ts</c> prüft, dass die <i>Tabelle</i>
 * vollständig ist — nicht, dass eine Seite sie <i>benutzt</i>. Genau die
 * Unterscheidung aus CLAUDE.md: eine Erwähnung ist keine Verwendung. Eine
 * Seite, die den Wert direkt ausgibt, kommt an der Tabelle vorbei, und der
 * Test bleibt grün.
 *
 * <b>Die Grundmenge</b> sind die Werte der String-Unions aus
 * <c>src/types/shared.ts</c> und <c>src/types/production.ts</c> — also die
 * Wahrheit aus dem Typ, keine abgetippte Liste.
 */

/**
 * Wörter, die auf Deutsch genau so heißen — mit Grund.
 *
 * <b>Kein Freibrief:</b> hier steht nur, was ein deutscher Text ohnehin
 * enthielte. Wer einen Wert hier einträgt, weil eine Seite ihn roh ausgibt,
 * hat den Test abgeschaltet statt den Fehler behoben.
 */
const ERLAUBT: Record<string, string> = {
  RDWC: 'Fachbegriff, auf Deutsch genauso — Recirculating Deep Water Culture',
  DWC: 'dasselbe für Deep Water Culture',
  Autoflower: 'die deutsche Sortenbezeichnung ist genau dieses Wort',
  Automatic: 'dasselbe — im deutschen Samenhandel heissen sie Automatics',
  Finish: 'in PHASEN_NAMEN bewusst „Finish" — im Grow-Deutsch etabliert',
  Problem: 'in FOTO_NAMEN bewusst „Problem" — ist auch ein deutsches Wort',
  Training: 'in FOTO_NAMEN bewusst „Training" — LST/HST heissen hier so',
  Offline: 'deutsches Lehnwort; der Knopf „Offline" ist die Handlung',
  System: 'deutsches Wort — steht in „Hydro-System", nicht als Enum-Wert',
  Top: 'steht in Geraetenamen wie „LED Top", nicht als Tankposition',
  Root: 'steht im Namen des Krankheitsbildes „Root Rot" im Wissen',
  Flush: 'steht im Titel des Ablaufs „Erntevorbereitung — Flush"',
  Flower: 'steht in SOP- und Programmnamen („Flower Fuel"), nicht als Phase',
  Normal: 'deutsches Wort',
  Info: 'deutsches Kurzwort',
}


/** Die Werte aller String-Unions aus den Typdateien. */
function enumWerte(): Map<string, string> {
  const werte = new Map<string, string>()

  for (const datei of ['../src/types/shared.ts', '../src/types/production.ts']) {
    const quelltext = readFileSync(new URL(datei, import.meta.url), 'utf8')

    for (const zeile of quelltext.split(/\r?\n/)) {
      const treffer = zeile.match(/export type (\w+) =(.*)$/)
      if (!treffer) continue

      for (const wert of treffer[2].matchAll(/'([^']+)'/g)) {
        if (!werte.has(wert[1])) werte.set(wert[1], treffer[1])
      }
    }
  }

  return werte
}

const WERTE = enumWerte()

test('die Grundmenge ist da', () => {
  // Ohne sie liefe jede Seitenpruefung null Mal durch und waere gruen.
  expect(WERTE.size).toBeGreaterThan(80)
  expect(WERTE.get('Feminized')).toBe('SeedType')
  expect(WERTE.get('Grid2x2')).toBe('HydroSetupLayoutType')

  // Und die Ausnahmeliste nennt nur Werte, die es wirklich gibt — sonst
  // schuetzt sie etwas, das niemand je sieht.
  const erfunden = Object.keys(ERLAUBT).filter((wert) => !WERTE.has(wert))
  expect(erfunden, `Diese Ausnahmen sind keine Enum-Werte: ${erfunden.join(', ')}`).toEqual([])
})

async function roheWerte(page: Page): Promise<string[]> {
  const text = await page.evaluate(() =>
    (document.querySelector('main') as HTMLElement | null)?.innerText || '')

  const gefunden: string[] = []
  for (const [wert, typ] of WERTE) {
    if (wert in ERLAUBT) continue
    if (!new RegExp(`(?<![\\w-])${wert}(?![\\w-])`).test(text)) continue
    gefunden.push(`${wert} (${typ})`)
  }

  return gefunden
}

for (const pfad of TEXTSEITEN) {
  test(`${pfad} — kein englischer Enum-Wert`, async ({ page }) => {
    const antwort = await page.goto(pfad, { waitUntil: 'networkidle' })
    darfUeberspringen(
      antwort == null || antwort.status() >= 400,
      `${pfad} antwortet nicht — laeuft die App unter GROW_OS_URL?`,
    )
    await page.waitForTimeout(400)

    const laenge = await page.evaluate(() =>
      ((document.querySelector('main') as HTMLElement | null)?.innerText || '').trim().length)
    darfUeberspringen(laenge < 200, `${pfad} zeigt fast nichts — vermutlich ein Ladezustand`)

    const roh = await roheWerte(page)
    expect(
      roh,
      `Diese Entwickler-Bezeichner stehen roh auf ${pfad}: ${roh.join(', ')}\n`
      + 'Deutsches Wort in src/deutsche-woerter.ts eintragen und die Seite darauf ziehen.',
    ).toEqual([])
  })
}
