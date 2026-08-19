import { expect, test, type Page } from '@playwright/test'
import { readFileSync } from 'node:fs'
import { darfUeberspringen } from './pflicht'

/**
 * Keine Seite im Menü steht im Demobestand leer da.
 *
 * <b>Der Anlass.</b> Der Nutzer: „Es fehlen wieder mock daten". Nachgegangen
 * bin ich dem mit einem Durchgang über ALLE Menüpunkte — und genau eine Seite
 * war wirklich leer: `/archiv` zeigte „Noch keine archivierten Grows". Damit
 * war die komplette Ernte- und Kostenrechnung (Summe, €/g, gebaut in beta.27)
 * auf dem Entwicklungsrechner nirgends zu sehen.
 *
 * <b>Warum das lange niemandem auffiel.</b> `DemoData` fälscht die
 * Home-Assistant-Seite — Sensoren, Verlauf, Dosierungen, Kamera. Die Grows
 * kommen aus der Datenbank, und die legt niemand an. Auf dem Rechner des
 * Entwicklers steht ein von Hand angelegter Grow, der nie fertig wurde; auf
 * jedem anderen Rechner steht gar nichts.
 *
 * <b>Warum eine Zählung.</b> Eine Liste „diese Seiten haben Daten" könnte
 * genau die Seite vergessen, für die niemand Daten angelegt hat — derselbe
 * blinde Fleck wie das, was sie prüfen soll. Die Grundmenge ist deshalb die
 * Navigation selbst.
 *
 * <b>Dieser Test braucht das Backend.</b> Die übrige E2E-Mappe läuft gegen
 * einen statischen Server ohne API — dort ist jede Seite leer, und zwar zu
 * Recht. Gegen die laufende App:
 *
 *   GROW_OS_URL=http://localhost:5076 npx playwright test demobestand
 */

/** Die Wege aus der Navigation lesen, nicht abtippen. */
function menueWege(): string[] {
  const quelle = readFileSync(new URL('../src/navigation.ts', import.meta.url), 'utf8')
  return [...quelle.matchAll(/to:\s*'([^']+)'/g)].map((m) => m[1])
}

/**
 * Wann gilt eine Seite als leer?
 *
 * <b>Nicht über Klassennamen.</b> Ein erster Anlauf suchte nach
 * `.empty-hint, .v1-empty, .rc2-empty` — und fand damit nur einen Teil: die
 * Leerzustände sind über `V1Empty`, `.empty-hint`, `.cu-hint` und schlichte
 * `<p>` in Panel-Rümpfen verteilt. Der Test war grün, ohne zu greifen.
 *
 * Geprüft wird deshalb, was der Nutzer sieht: steht auf der Seite eine
 * Leermeldung, UND hat die Seite sonst kaum Inhalt? Eine volle Seite mit einem
 * beiläufigen „Noch keine Angaben" in einer Zeile fällt damit nicht durch —
 * eine Seite, die nur aus der Leermeldung besteht, schon. Das Archiv hatte
 * 223 Zeichen.
 */
const LEER_MELDUNGEN = ['Noch keine', 'Noch nichts', 'Keine Daten', 'noch keine']
const INHALT_SCHWELLE = 900

async function leerBefund(seite: Page, weg: string) {
  await seite.goto(weg, { waitUntil: 'networkidle' })

  // Warten, bis wirklich nachgeladen ist. Eine erste Messung hat drei volle
  // Seiten als leer gemeldet — gemessen wurde vor dem Nachladen.
  await seite.waitForTimeout(1200)

  return seite.evaluate(({ meldungen, schwelle }) => {
    const main = document.querySelector('main') ?? document.body
    const text = (main as HTMLElement).innerText.trim()
    const treffer = meldungen.filter((m) => text.includes(m))
    return {
      leer: treffer.length > 0 && text.length < schwelle,
      zeichen: text.length,
      meldung: treffer[0] ?? null,
      auszug: text.replace(/\s+/g, ' ').slice(0, 160),
    }
  }, { meldungen: LEER_MELDUNGEN, schwelle: INHALT_SCHWELLE })
}

/**
 * Seiten, die im Demobestand leer sein DÜRFEN — jede mit Grund.
 *
 * Die Wege sind aus `navigation.ts` abgeschrieben. Ein falsch getippter Weg
 * würde hier stumm ins Leere greifen und die Ausnahme wirkungslos machen —
 * dagegen prüft `jeder ausgenommene Weg steht wirklich im Menü`.
 *
 * Leer heisst die Liste, weil zur Zeit keine Seite eine Ausnahme braucht.
 */
const GEWOLLT_LEER: Record<string, string> = {}

let backendDa = false

test.beforeAll(async ({ request }) => {
  try {
    backendDa = (await request.get('/api/grows')).ok()
  } catch {
    backendDa = false
  }
})

test.describe('Demobestand', () => {
  test('jeder ausgenommene Weg steht wirklich im Menü', () => {
    // Sonst schützt eine Ausnahme eine Seite, die es nicht gibt, während die
    // echte durchfällt. Genau so sind schon einmal drei erfundene Kennungen in
    // eine Ausnahmeliste geraten.
    const wege = menueWege()
    expect(wege.length, 'Die Navigation wurde nicht gelesen — der Test prüft nichts.').toBeGreaterThan(15)
    for (const weg of Object.keys(GEWOLLT_LEER)) {
      expect(wege, `${weg} steht in GEWOLLT_LEER, aber nicht im Menü.`).toContain(weg)
    }
  })

  test('die Prüfung greift überhaupt — mit geleerter Antwort', async ({ page }) => {
    darfUeberspringen(!backendDa, 'Kein Backend unter der Basis-Adresse — siehe Kopf der Datei.')

    // <b>Der Beweis, dass die Prüfung beisst.</b> Ohne ihn wäre nicht zu
    // unterscheiden, ob jede Seite Inhalt hat oder ob die Erkennung ins Leere
    // greift. Genau das war beim ersten Anlauf der Fall: er suchte nach
    // Klassennamen, die die Hälfte der Leerzustände nicht tragen, und war
    // deshalb grün.
    //
    // Hier wird die Archiv-Antwort auf der Leitung geleert — die Seite muss
    // dann als leer erkannt werden.
    await page.route('**/api/grows*', async (route) => {
      const url = route.request().url()
      if (url.includes('archived=true')) {
        await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
        return
      }
      await route.continue()
    })

    const befund = await leerBefund(page, '/archiv')
    expect(befund.leer,
      `Mit geleerter Archiv-Antwort meldet die Prüfung KEINEN Leerstand (${befund.zeichen} Zeichen: `
      + `„${befund.auszug}"). Damit wäre jedes grüne Ergebnis darunter wertlos.`)
      .toBe(true)
  })

  for (const weg of menueWege()) {
    test(`${weg} zeigt Inhalt`, async ({ page }) => {
      darfUeberspringen(!backendDa,
        'Kein Backend unter der Basis-Adresse — ohne API ist jede Seite leer, und der Test '
        + 'würde nur sein eigenes Fehlen messen. Gegen die laufende App: '
        + 'GROW_OS_URL=http://localhost:5076 npx playwright test demobestand')

      await page.setViewportSize({ width: 1440, height: 1000 })
      const befund = await leerBefund(page, weg)

      if (GEWOLLT_LEER[weg]) {
        test.info().annotations.push({ type: 'gewollt leer', description: GEWOLLT_LEER[weg] })
        return
      }

      expect(befund.leer,
        `${weg} steht im Demobestand leer da (${befund.zeichen} Zeichen, „${befund.meldung}"): `
        + `„${befund.auszug}". Wer die App zum ersten Mal öffnet, sieht dort nichts von dem, was `
        + 'gebaut ist. Entweder in DemoData etwas säen — oder mit Grund in GEWOLLT_LEER eintragen.')
        .toBe(false)
    })
  }
})
