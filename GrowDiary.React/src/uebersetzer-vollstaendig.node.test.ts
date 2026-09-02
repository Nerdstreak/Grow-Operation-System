import { describe, expect, it } from 'vitest'
import * as woerter from './deutsche-woerter'

/**
 * Jeder Übersetzer verhält sich gleich — auch der, den es morgen erst gibt.
 *
 * **Der Anlass (02.09.2026).** `deutsche-woerter.ts` stand bei **51 %**
 * Abdeckung. Die vorhandene Zählung (`deutsche-woerter.node.test.ts`) prüft,
 * dass jedes **Wörterbuch** vollständig ist — nicht, dass die **Funktionen**
 * darum herum sich richtig verhalten.
 *
 * Das sind zwei verschiedene Fehler:
 *
 * | Fehler | wer ihn fängt |
 * |---|---|
 * | ein Enum-Wert ohne deutsches Wort | die Wörterbuch-Zählung |
 * | ein unbekannter Wert wird **verschluckt** | diese hier |
 * | `null` wird zu „null" oder „undefined" auf dem Schirm | diese hier |
 *
 * **Warum eine Zählung und nicht fünfzehn Tests.** Es gibt heute fünfzehn
 * Übersetzer. Eine handgeschriebene Liste kann nur an dem scheitern, was schon
 * draufsteht; der sechzehnte käme still ungeprüft dazu. Diese Prüfung findet
 * ihn über die Ausfuhren des Moduls.
 */

/** Jede ausgeführte Funktion, deren Name auf „Name" endet. */
function alleUebersetzer(): Array<[string, (wert: string | null | undefined) => string]> {
  return Object.entries(woerter)
    .filter((eintrag): eintrag is [string, (wert: string | null | undefined) => string] =>
      typeof eintrag[1] === 'function' && /Name$/.test(eintrag[0]))
}

describe('Die Übersetzer', () => {
  it('werden überhaupt gefunden', () => {
    // Mengenwächter: ohne Grundmenge liefe alles darunter null Mal durch.
    expect(
      alleUebersetzer().length,
      'Keine Übersetzer gefunden — dann prüft diese Datei nichts. Heissen sie nicht mehr „…Name"?',
    ).toBeGreaterThanOrEqual(15)
  })

  for (const [name, uebersetzer] of alleUebersetzer()) {
    describe(name, () => {
      it('macht aus nichts nichts — kein „null" auf dem Schirm', () => {
        for (const leer of [null, undefined, '']) {
          const heraus = uebersetzer(leer)
          expect(
            heraus === '' || heraus === '–',
            `${name}(${JSON.stringify(leer)}) ergibt „${heraus}". Auf dem Schirm stünde dann `
            + 'dieses Wort statt eines leeren Feldes.',
          ).toBe(true)
        }
      })

      it('verschluckt einen unbekannten Wert nicht', () => {
        // Ein roher Wert ist haesslich, aber SICHTBAR — e2e/rohe-enums.spec.ts
        // findet ihn an der laufenden App. Ein leeres Feld waere schlimmer:
        // dann fehlt die Angabe ganz, und niemand merkt, dass etwas fehlt.
        const heraus = uebersetzer('EinWertDenNiemandKennt')

        expect(
          heraus,
          `${name} macht aus einem unbekannten Wert „${heraus}". Ein roher Wert faellt auf und `
          + 'wird gefunden; ein leeres Feld faellt niemandem auf.',
        ).toBe('EinWertDenNiemandKennt')
      })

      it('gibt immer eine Zeichenkette zurück', () => {
        // Sonst stünde `undefined` im JSX — genau der Fall, den
        // e2e/form-und-bild.spec.ts als „Platzhalter aus dem Code" meldet.
        for (const wert of [null, undefined, '', 'Unbekannt']) {
          expect(typeof uebersetzer(wert), `${name}(${JSON.stringify(wert)}) gibt keine Zeichenkette.`)
            .toBe('string')
        }
      })
    })
  }
})
