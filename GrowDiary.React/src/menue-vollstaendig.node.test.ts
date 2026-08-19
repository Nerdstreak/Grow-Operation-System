import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { navGroups } from './navigation'

/**
 * Jede eigenständige Seite muss im Menü stehen.
 *
 * <b>Wozu.</b> Zweimal ist in diesem Projekt eine fertig gebaute Funktion
 * vermisst worden, weil man sie nicht finden konnte: die Einkaufsliste
 * (beta.42) und zuletzt das Messprotokoll unter `/messungen`. Beide waren
 * vollständig da. Beide standen in keiner Menügruppe. Und weil die Suche ihre
 * Einträge aus genau diesen Gruppen baut (`navigation.ts`), waren sie damit
 * auch nicht suchbar — „Messungen", „Sensorwerte", „gemessen" lieferten alle
 * nichts.
 *
 * <b>Warum der vorhandene Test es nicht gefunden hat.</b>
 * `routes-reachable.node.test.ts` sucht im gesamten Quelltext nach dem
 * Routennamen. `GrowDetailPage.tsx` enthält einen Reiter-Link auf
 * `/messungen` — Treffer, Test grün, obwohl der einzige Weg dorthin ein
 * Reiter tief in einem einzelnen Grow war. Ein Link IRGENDWO ist eben nicht
 * dasselbe wie ein Weg im Menü.
 *
 * <b>Warum genau diese Seiten.</b> `GrowScopedSectionPage` ist laut ihrem
 * eigenen Kommentar die Bauform für eigenständige Seiten, die sich ihren Grow
 * selbst holen. Eine solche Seite nur über einen Reiter erreichbar zu machen
 * ist immer ein Versehen, nie Absicht.
 */
describe('Menü-Vollständigkeit', () => {
  const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')

  /**
   * Bewusst nicht im Menü — mit ausgeschriebenem Grund.
   *
   * Wer hier etwas einträgt, schreibt dazu, warum. Ein Eintrag ohne Grund ist
   * beim nächsten Audit ein Befund.
   */
  const gewollteAusnahmen = new Map([
    // Laufende Routinen wohnen bei den Aufgaben — dort arbeitet man sie ab.
    // Der Menüpunkt „SOPs & Bibliothek" führt bewusst zum Wissen, nicht hierher.
    ['/sops', 'Laufende Routinen stehen auf der Aufgabenseite, das Menü führt zur Bibliothek'],
  ])

  const routen = [...app.matchAll(/<Route path="([^"]+)" element={<GrowScopedSectionPage/g)].map((t) => t[1])
  const imMenue = new Set(navGroups.flatMap((gruppe) => gruppe.items.map((punkt) => punkt.to)))

  it('findet überhaupt Routen dieser Bauform', () => {
    // Sonst prüft der Test hier gar nichts und ist trotzdem grün — die Falle,
    // in die sein Vorgänger gelaufen ist.
    expect(routen.length).toBeGreaterThan(3)
  })

  for (const route of routen) {
    const grund = gewollteAusnahmen.get(route)
    if (grund) {
      it(`${route} steht mit Grund nicht im Menü: ${grund}`, () => {
        expect(imMenue.has(route)).toBe(false)
      })
      continue
    }

    it(`${route} steht im Menü`, () => {
      expect(imMenue.has(route), `${route} ist eine eigenständige Seite, steht aber in keiner Menügruppe — damit ist sie auch nicht suchbar.`).toBe(true)
    })
  }
})
