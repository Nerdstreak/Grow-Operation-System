import { describe, expect, it } from 'vitest'
import { aktiveRegeln, speicherbareRegeln, vertauschteGrenzen, type Grenzwertzeile } from './grenzwerte-modell'

/**
 * Ein abgewählter Grenzwert wird pausiert, nicht gelöscht.
 *
 * <b>Der Anlass (01.09.2026).</b> Die Seite schickte nur die Zeilen mit Haken,
 * und der Server ersetzt beim Speichern den ganzen Satz. Wer den Haken
 * herausnahm, verlor seine Zahlen — mit der Meldung „gespeichert" und den
 * Zahlen weiter sichtbar im Formular. Erst beim nächsten Aufruf der Seite fiel
 * es auf.
 */

const zeile = (teil: Partial<Grenzwertzeile> = {}): Grenzwertzeile =>
  ({ min: '', max: '', cooldown: '30', enabled: true, ...teil })

describe('speicherbareRegeln', () => {
  it('schickt eine abgewaehlte Zeile MIT — nur eben pausiert', () => {
    const regeln = speicherbareRegeln(['reservoir-ph'], {
      'reservoir-ph': zeile({ min: '5,8', max: '6,3', enabled: false }),
    })

    expect(regeln, 'Die abgewaehlte Zeile faellt aus dem Aufruf — der Server ersetzt den '
      + 'ganzen Satz und loescht damit die Grenzen.').toHaveLength(1)
    expect(regeln[0].enabled).toBe(false)
    expect(regeln[0].minValue).toBe(5.8)
    expect(regeln[0].maxValue).toBe(6.3)
  })

  it('laesst eine Zeile ganz ohne Grenzen weg', () => {
    // Ohne Grenze ist nichts zu speichern — das lehnt der Server ohnehin ab.
    expect(speicherbareRegeln(['reservoir-ph'], { 'reservoir-ph': zeile() })).toHaveLength(0)
  })

  it('eine Grenze genuegt', () => {
    expect(speicherbareRegeln(['x'], { x: zeile({ max: '30' }) })).toHaveLength(1)
    expect(speicherbareRegeln(['x'], { x: zeile({ min: '18' }) })).toHaveLength(1)
  })

  it('liest deutsche Kommazahlen', () => {
    expect(speicherbareRegeln(['x'], { x: zeile({ min: '5,85' }) })[0].minValue).toBe(5.85)
  })

  it('faengt eine unbrauchbare Schonfrist ab', () => {
    // Math.max(1, …): 0 Minuten hiesse „bei jedem Messwert melden".
    expect(speicherbareRegeln(['x'], { x: zeile({ min: '1', cooldown: '0' }) })[0].cooldownMinutes).toBe(1)
    expect(speicherbareRegeln(['x'], { x: zeile({ min: '1', cooldown: '' }) })[0].cooldownMinutes).toBe(30)
  })
})

describe('aktiveRegeln', () => {
  it('zaehlt nur die, die wirklich wachen', () => {
    const regeln = speicherbareRegeln(['a', 'b'], {
      a: zeile({ min: '1', enabled: true }),
      b: zeile({ min: '2', enabled: false }),
    })

    expect(regeln).toHaveLength(2)
    expect(aktiveRegeln(regeln)).toBe(1)
  })
})

describe('vertauschteGrenzen', () => {
  it('meldet ein Paar, dessen Untergrenze ueber der Obergrenze liegt', () => {
    /* Bei min 22 / max 18 rechnet der Server `wert < min ? unten : wert > max ?
       oben : im Rahmen` — bei 20 °C greift die erste Bedingung, und die Regel
       meldet dauerhaft „zu kalt", obwohl 20 zwischen den Zahlen liegt. */
    const regeln = speicherbareRegeln(['reservoir-temp'], {
      'reservoir-temp': zeile({ min: '22', max: '18' }),
    })

    expect(vertauschteGrenzen(regeln)).toEqual(['reservoir-temp'])
  })

  it('schweigt bei gleichen Grenzen und bei nur einer', () => {
    expect(vertauschteGrenzen(speicherbareRegeln(['x'], { x: zeile({ min: '20', max: '20' }) }))).toEqual([])
    expect(vertauschteGrenzen(speicherbareRegeln(['x'], { x: zeile({ min: '20' }) }))).toEqual([])
  })
})
