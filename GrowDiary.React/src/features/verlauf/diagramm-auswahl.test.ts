import { describe, expect, it } from 'vitest'
import { inZeichenflaeche, punktBeiX, zeitpunktText } from './diagramm-auswahl'

const punkte = [
  { t: '2026-09-01T06:00:00Z', v: 5.8 },
  { t: '2026-09-01T12:00:00Z', v: 6.1 },
  { t: '2026-09-01T18:00:00Z', v: 6.4 },
]

/** Eine einfache Skala: 06:00 → 0, 18:00 → 600. */
const xVon = (zeit: number) => {
  const von = new Date('2026-09-01T06:00:00Z').getTime()
  const bis = new Date('2026-09-01T18:00:00Z').getTime()
  return ((zeit - von) / (bis - von)) * 600
}

describe('Welchen Punkt hat der Nutzer angetippt', () => {
  it('trifft den Punkt, auf dem er steht', () => {
    expect(punktBeiX(punkte, 300, xVon)?.index).toBe(1)
  })

  it('nimmt den nächsten, wenn er danebentippt', () => {
    // 320 liegt naeher an 300 (Punkt 2) als an 600 (Punkt 3).
    expect(punktBeiX(punkte, 320, xVon)?.index).toBe(1)
    /* 460 liegt naeher an 600. NICHT 450: das waere von 300 und 600 gleich
       weit entfernt, und dann entscheidet die Reihenfolge — der Fall pruefte
       die Naehe gar nicht. */
    expect(punktBeiX(punkte, 460, xVon)?.index).toBe(2)
  })

  it('fällt an den Rändern auf den ersten und letzten', () => {
    expect(punktBeiX(punkte, -50, xVon)?.index).toBe(0)
    expect(punktBeiX(punkte, 9999, xVon)?.index).toBe(2)
  })

  it('ergibt ohne Punkte nichts', () => {
    expect(punktBeiX([], 100, xVon)).toBeNull()
  })
})

describe('Vom Klick auf die Zeichenfläche', () => {
  it('rechnet die Breite mit', () => {
    // Ein Kasten von 300 px Breite, das Bild ist 600 breit: die Mitte
    // (Klick bei 150 relativ) muss 300 ergeben.
    expect(inZeichenflaeche(150, 0, 300, 600)).toBeCloseTo(300, 5)
  })

  it('rechnet den linken Rand heraus', () => {
    // Derselbe Klick, aber der Kasten beginnt erst bei 100 px.
    expect(inZeichenflaeche(250, 100, 300, 600)).toBeCloseTo(300, 5)
  })

  it('stürzt bei einem Kasten ohne Breite nicht ab', () => {
    // Kommt vor, solange das Bauteil noch nicht gemessen wurde.
    expect(inZeichenflaeche(150, 0, 0, 600)).toBe(0)
  })
})

describe('Der Zeitpunkt unter dem Diagramm', () => {
  it('zeigt bei Tageswerten den Tag', () => {
    // „01.09." — den Punkt am Ende setzt Intl bei de-DE selbst.
    expect(zeitpunktText('2026-09-01T12:00:00Z', 'daily')).toMatch(/^\d{2}\.\d{2}\.?$/)
  })

  it('zeigt bei Rohwerten auch die Uhrzeit', () => {
    // Sonst stuenden sechs Punkte desselben Tages unter demselben Text.
    expect(zeitpunktText('2026-09-01T12:00:00Z', 'raw')).toMatch(/\d{2}:\d{2}/)
  })

  it('macht aus einem unlesbaren Datum keinen Unsinn', () => {
    expect(zeitpunktText('kein datum', 'raw')).toBe('')
  })
})
