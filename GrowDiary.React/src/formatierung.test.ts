import { describe, expect, it } from 'vitest'
import { classNames, formatNumber, formatSeverityLabel, haWert, toLocalInputValue } from './utils'

/**
 * Was auf dem Schirm steht — Komma, Einheit, Ortszeit.
 *
 * **Der Anlass (02.09.2026).** `src/utils.ts` stand bei **15,8 %** Abdeckung.
 * Dabei laufen drei belegte Fehlerklassen dieses Projekts genau hier durch:
 *
 * - **Der Dezimalpunkt.** An den Diagramm-Achsen stand „5.80". Der
 *   Backend-Test dafür (`DeutscheZahlenTests.cs`) sieht Texte aus dem Backend —
 *   eine Zahl, die im Browser aus einer JavaScript-Zahl entsteht, kommt dort
 *   nie vorbei.
 * - **Home Assistant liefert Zahlen immer mit Punkt.** „5.78", „1.02", „17.8" —
 *   nicht nur im Testbetrieb, sondern in jeder echten Anlage. Roh ausgegeben
 *   stand das an drei Stellen auf dem Schirm.
 * - **Ortszeit gegen UTC.** Ein `datetime-local`-Feld erwartet Ortszeit ohne
 *   Zeitzone. Wer dort einen UTC-Wandwert hineinschreibt, verschiebt jeden
 *   Zeitpunkt um den Versatz — in diesem Projekt schon belegt.
 */

describe('Zahlen auf Deutsch', () => {
  it('schreibt das Komma', () => {
    expect(formatNumber(5.8), 'An den Diagramm-Achsen stand deshalb einmal „5.80".').toBe('5,8')
  })

  it('rundet auf die gewünschte Stellenzahl', () => {
    expect(formatNumber(5.789, 2)).toBe('5,79')
    expect(formatNumber(5.789, 0)).toBe('6')
  })

  it('hängt keine Nullen an, die niemand getippt hat', () => {
    // minimumFractionDigits: 0 — „1" bleibt „1" und wird nicht zu „1,0".
    expect(formatNumber(1)).toBe('1')
  })

  it('macht aus fehlend einen Gedankenstrich, keine Null', () => {
    // Der teure Unterschied: „0" heisst gemessen und null, „–" heisst nicht
    // gemessen. Stuende hier eine Null, urteilte der Waechter darueber.
    expect(formatNumber(null)).toBe('–')
    expect(formatNumber(undefined)).toBe('–')
    expect(formatNumber(Number.NaN)).toBe('–')
  })

  it('schreibt auch grosse Zahlen deutsch', () => {
    // Tausenderpunkt statt Komma — die englische Fassung schriebe „1,234.5".
    expect(formatNumber(1234.5)).toBe('1.234,5')
  })
})

describe('Werte aus Home Assistant', () => {
  it('macht aus dem Punkt ein Komma', () => {
    expect(haWert('5.78'), 'Home Assistant liefert IMMER Punkte — auch in einer echten Anlage.')
      .toBe('5,78')
  })

  it('hängt die Einheit an', () => {
    expect(haWert('1.02', 'mS/cm')).toBe('1,02 mS/cm')
  })

  it('zeigt kleine Werte genauer als grosse', () => {
    // Unter 10 zwei Stellen (pH 5,78 braucht sie), ab 10 eine (17,8 °C reicht).
    expect(haWert('5.784')).toBe('5,78')
    expect(haWert('17.84')).toBe('17,8')
  })

  it('lässt Text Text sein', () => {
    // `on`, `off`, `unavailable` sind keine Zahlen und werden nicht angefasst.
    expect(haWert('unavailable')).toBe('unavailable')
    expect(haWert('on')).toBe('on')
  })

  it('macht aus leer nichts — und nicht null', () => {
    // `null` heisst „nichts anzuzeigen"; die Kachel zeigt dann ihren eigenen
    // Ersatztext. Eine leere Zeichenkette waere ein leeres Feld auf dem Schirm.
    expect(haWert(null)).toBeNull()
    expect(haWert('')).toBeNull()
  })
})

describe('Ortszeit für ein datetime-local-Feld', () => {
  it('schreibt die Ortszeit, nicht UTC', () => {
    // Ein Zeitpunkt, der in MESZ und UTC verschieden aussieht.
    const zeitpunkt = new Date(2026, 6, 15, 14, 30)
    const text = toLocalInputValue(zeitpunkt)

    expect(text, `„${text}" traegt nicht die Ortszeit 14:30. Ein datetime-local-Feld erwartet `
      + 'Ortszeit ohne Zeitzone — ein UTC-Wandwert verschiebt jeden Zeitpunkt um den Versatz.')
      .toBe('2026-07-15T14:30')
  })

  it('hat genau die Form, die das Feld annimmt', () => {
    // Sekunden oder ein „Z" am Ende, und der Browser lehnt den Wert stumm ab:
    // das Feld bleibt leer, und der Nutzer merkt es erst beim Speichern.
    expect(toLocalInputValue(new Date(2026, 0, 2, 3, 4))).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/)
  })

  it('füllt einstellige Werte auf', () => {
    expect(toLocalInputValue(new Date(2026, 0, 2, 3, 4))).toBe('2026-01-02T03:04')
  })
})

describe('Stufen auf Deutsch', () => {
  it('übersetzt alle vier Typen', () => {
    expect(formatSeverityLabel('Info')).toBe('Hinweis')
    expect(formatSeverityLabel('Warning')).toBe('Warnung')
    expect(formatSeverityLabel('Critical')).toBe('Kritisch')
    expect(formatSeverityLabel('Normal')).toBe('Normal')
    expect(formatSeverityLabel('Medium')).toBe('Mittel')
  })

  it('lässt Unbekanntes stehen, statt es zu verschlucken', () => {
    // Ein roher Wert auf dem Schirm ist haesslich, aber sichtbar — und
    // e2e/rohe-enums.spec.ts findet ihn an der laufenden App. Ein leeres Feld
    // waere schlimmer: dann fehlt die Stufe ganz.
    expect(formatSeverityLabel('Erfunden')).toBe('Erfunden')
  })

  it('macht aus fehlend einen Gedankenstrich', () => {
    expect(formatSeverityLabel(null)).toBe('–')
    expect(formatSeverityLabel('')).toBe('–')
  })
})

describe('classNames', () => {
  it('lässt Falsches weg', () => {
    expect(classNames('a', false, null, undefined, 'b')).toBe('a b')
  })

  it('gibt bei nichts eine leere Zeichenkette', () => {
    // `class=""` ist harmlos; `class="undefined"` traefe eine CSS-Regel, die es
    // nicht gibt — und faellt niemandem auf.
    expect(classNames(false, null)).toBe('')
  })
})
