/**
 * Die Zahl muss den Schnitt überleben.
 *
 * Im Zeitstrahl wurde die Beschriftung gekürzt, weil die Balkenlänge die Dauer
 * ist und nicht breiter werden darf. Stand der Name vorn, fiel dabei genau die
 * Zahl weg, um die es geht („TROCKNE…" statt „Trocknen 10 T"). Der Test hält
 * fest, dass die Zahl vorn steht — sonst kommt das lautlos zurück.
 */
import { describe, expect, it } from 'vitest'
import { balkenText } from './phase-timeline'

describe('balkenText', () => {
  it('stellt die Dauer vor den Namen', () => {
    expect(balkenText('Trocknen 10 T', 10)).toBe('10 T Trocknen')
    expect(balkenText('Blüte 12/56', 56)).toBe('12/56 Blüte')
    expect(balkenText('Veg 22/28', 28)).toBe('22/28 Veg')
  })

  it('lässt Abschnitte ohne bekannte Dauer in Ruhe', () => {
    // Die werden gar nicht gekürzt (flex-grow: 0), und „— Keim" wäre seltsam.
    expect(balkenText('Keim —', 0)).toBe('Keim —')
    expect(balkenText('Veg —', 0)).toBe('Veg —')
  })

  it('lässt einteilige Beschriftungen unverändert', () => {
    expect(balkenText('Veg', 12)).toBe('Veg')
  })
})
