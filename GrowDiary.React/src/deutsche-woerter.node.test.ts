import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { FOTO_TAGS, PHASEN, WOERTERBUECHER, fotoTagName, herkunftName, phaseName } from './deutsche-woerter'

/**
 * Kein Entwickler-Bezeichner steht roh auf dem Bildschirm.
 *
 * <b>Der Anlass.</b> In vier Auswahlfeldern standen die englischen Enum-Werte
 * unübersetzt da — „Seedling", „Overview", „HomeAssistant" —, dazu acht fest
 * getippte `<option>`-Zeilen im Messformular, die keine Übersetzungstabelle je
 * erwischt hätte. Dieselbe Klasse wie die 65 rohen Symptom-Schlüssel im Wissen
 * und „Ec" statt „EC" in der Diagnose.
 *
 * <b>Warum eine Zählung.</b> Eine Liste der übersetzten Werte kann nur an dem
 * scheitern, was schon draufsteht. Diese Prüfung geht über die Enum-Werte im
 * Typ und verlangt für jeden eine Übersetzung — ein neuer Wert fällt sofort auf.
 */
describe('Deutsche Wörter', () => {
  const shared = readFileSync(new URL('./types/shared.ts', import.meta.url), 'utf8')

  /**
   * Die Werte einer String-Union aus der Typdatei — die Wahrheit steht dort,
   * nicht in einer zweiten Liste hier.
   */
  function werteVon(name: string): string[] {
    const treffer = shared.match(new RegExp(`export type ${name} =([^\\n]*(?:\\n\\s*\\|[^\\n]*)*)`))
    if (!treffer) throw new Error(`Typ ${name} nicht in shared.ts gefunden — der Test würde nichts prüfen.`)
    return [...treffer[1].matchAll(/'([^']+)'/g)].map((m) => m[1])
  }

  it('findet die Typen überhaupt', () => {
    // Sonst läuft jede Schleife darunter null Mal und der Test ist grün,
    // ohne etwas geprüft zu haben.
    expect(werteVon('GrowStage').length).toBeGreaterThanOrEqual(8)
    expect(werteVon('PhotoTag').length).toBeGreaterThanOrEqual(9)
    expect(werteVon('ValueOrigin').length).toBeGreaterThanOrEqual(4)
  })

  for (const [typ, woerterbuch] of [
    ['GrowStage', WOERTERBUECHER.phase],
    ['PhotoTag', WOERTERBUECHER.fotoTag],
    ['ValueOrigin', WOERTERBUECHER.herkunft],
  ] as const) {
    it(`jeder Wert von ${typ} hat ein deutsches Wort`, () => {
      const fehlend = werteVon(typ).filter((wert) => !(wert in woerterbuch))
      expect(fehlend, `Ohne deutsches Wort in deutsche-woerter.ts: ${fehlend.join(', ')}`).toEqual([])
    })
  }

  it('die angebotenen Listen sind vollständig', () => {
    // Die Auswahlfelder bauen ihre Einträge aus PHASEN und FOTO_TAGS. Fehlt dort
    // ein Wert, kann man ihn nicht mehr auswählen — ein stiller Funktionsverlust.
    expect([...PHASEN].sort()).toEqual([...werteVon('GrowStage')].sort())
    expect([...FOTO_TAGS].sort()).toEqual([...werteVon('PhotoTag')].sort())
  })

  it('übersetzt, was es kennt', () => {
    expect(phaseName('Veg')).toBe('Wachstum')
    expect(fotoTagName('Overview')).toBe('Übersicht')
    expect(herkunftName('HomeAssistant')).toBe('aus Home Assistant')
  })

  it('reicht Unbekanntes durch, statt es zu verschlucken', () => {
    // Ein englisches Wort ist besser als ein leeres Feld.
    expect(phaseName('Etwas Neues')).toBe('Etwas Neues')
    expect(phaseName(null)).toBe('')
  })

  it('nennt HomeAssistant nicht „Automatik“', () => {
    // Das Feld laesst sich im Bearbeiten-Formular von Hand auf diesen Wert
    // setzen. „Automatik“ waere also eine Behauptung ueber die Herkunft, die die
    // Daten nicht decken.
    expect(herkunftName('HomeAssistant')).not.toContain('Automatik')
  })
})
