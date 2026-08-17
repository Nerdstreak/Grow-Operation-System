import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

/**
 * Die eine Regel, die jede Seite auf dem Telefon zusammenhält.
 *
 * Dieselbe Falle wie beim Live-Bildschirm, nur eine Ebene höher: `.v1-page`
 * ist ein Grid mit einer Spalte, und Grid-Kinder haben `min-width: auto` —
 * ihre Untergrenze ist die min-content-Breite ihres Inhalts. Ein einziges
 * breites Kind zieht damit die ganze Seite über den Schirm hinaus, und weil
 * es die Seite selbst ist, die zu breit wird, hilft kein Umbruch weiter unten.
 *
 * Gemessen auf der Home-Assistant-Seite: 423 px Spalte bei 351 px Platz. Es
 * reichte ein langer Entitäten-Name. Auf `/grows` traf es die Zeitachse.
 *
 * Warum ein Quelltext-Test: die E2E-Suite läuft ohne Backend — die Seiten,
 * auf denen es auffiel, zeigen dort nur ihren Ladezustand. Vgl. die
 * ausführliche Begründung in `features/live/schutzregeln.test.ts`.
 */
describe('Schutzregel der Seitenbreite', () => {
  const css = readFileSync(fileURLToPath(new URL('./primitives.css', import.meta.url)), 'utf8')

  it('erlaubt den Kindern jeder Seite zu schrumpfen', () => {
    expect(css).toMatch(/\.v1-page\s*>\s*\*\s*\{[^}]*min-width:\s*0/)
  })
})
