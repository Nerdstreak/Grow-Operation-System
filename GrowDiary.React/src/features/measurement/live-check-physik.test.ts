import { describe, expect, it } from 'vitest'
import { checkDraft } from './live-check-model'
import type { MetricPayload } from '../../types'

/**
 * Ein unmöglicher Wert ist keine Abweichung.
 *
 * <b>Der Anlass.</b> Wer 9000 in die Lufttemperatur tippte, bekam
 * „+8971 Lufttemperatur 9000 °C über 29 °C" — dieselbe Sprache wie für eine
 * etwas zu warme Kammer. Dass es diesen Wert nicht geben kann, stand nirgends;
 * die Sperre griff erst beim Speichern, und dann als Serverfehler.
 *
 * Der Nutzer hat es so gemeldet: „unlogische werte wird keine meldung
 * teilweise gegeben."
 */
describe('Live-Prüfung: physikalisch Unmögliches', () => {
  const metrics: MetricPayload[] = [
    { key: 'temperature', label: 'Temperatur', value: '24', unit: '°C', tone: 'default', hint: null, numericValue: 24, targetMin: 20, targetMax: 29 },
    { key: 'co2', label: 'CO₂', value: '420', unit: 'ppm', tone: 'default', hint: null, numericValue: 420, targetMin: null, targetMax: null },
  ]

  it('nennt 9000 °C beim Namen statt es als Abweichung zu buchen', () => {
    const funde = checkDraft({ airTemperatureC: '9000' }, metrics)
    const luft = funde.find((f) => f.field === 'airTemperatureC')

    expect(luft?.severity).toBe('crit')
    expect(luft?.text).toContain('das kann es nicht geben')
    // Kein Abstand zum Ziel: der Wert steht ausserhalb jeder Skala, eine
    // Differenz dazu waere eine erfundene Zahl.
    expect(luft?.delta).toBeNull()
    expect(luft?.text).not.toContain('über 29')
  })

  it('meldet auch, wo es gar kein Ziel gibt — der CO₂-Fall', () => {
    // Ohne Anreicherung hat CO₂ absichtlich kein Ziel. Vorher stieg die Pruefung
    // deshalb sofort aus, und -500 ppm blieben stumm — wochenlang auf einer
    // Kachel der Startseite.
    const funde = checkDraft({ co2Ppm: '-500' }, metrics)
    const co2 = funde.find((f) => f.field === 'co2Ppm')

    expect(co2?.severity).toBe('crit')
    expect(co2?.text).toContain('das kann es nicht geben')
  })

  it('sagt, was möglich wäre', () => {
    const funde = checkDraft({ reservoirPh: '99' }, metrics)
    expect(funde.find((f) => f.field === 'reservoirPh')?.hint).toContain('0 bis 14')
  })

  it('lässt normale Abweichungen normale Abweichungen bleiben', () => {
    // Die Physik-Sperre darf die eigentliche Pruefung nicht verdraengen.
    const funde = checkDraft({ airTemperatureC: '31' }, metrics)
    const luft = funde.find((f) => f.field === 'airTemperatureC')

    expect(luft?.text).toContain('über 29')
    expect(luft?.text).not.toContain('kann es nicht geben')
  })

  it('lässt Werte im Ziel in Ruhe', () => {
    const funde = checkDraft({ airTemperatureC: '24' }, metrics)
    expect(funde.find((f) => f.field === 'airTemperatureC')?.severity).toBe('ok')
  })
})
