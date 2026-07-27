import { describe, expect, it } from 'vitest'
import { buildScore } from './live-model'
import type { MetricPayload, TentDto } from '../../types'

/**
 * Der Grow-Score und seine Ampel.
 *
 * Die Bewertung („Stabil / Beobachten / Kritisch") stand schon immer im
 * Ergebnis — nur der Ring auf dem Bildschirm hat sie ignoriert und leuchtete
 * grundsätzlich grün. Bei 40 Punkten stand daneben „Kritisch" und der Kreis
 * war trotzdem grün. Diese Tests halten die Schwellen fest, an denen die
 * Farbe kippt.
 */
const zelt = { id: 1, name: 'Testzelt' } as TentDto

function metrik(key: string, wert: number, min: number | null, max: number | null): MetricPayload {
  return {
    key, label: key, value: String(wert), unit: null, tone: 'default', hint: null,
    numericValue: wert, targetMin: min, targetMax: max,
  }
}

/** Sechs Messwerte im Ziel — sonst zieht der Sensor-Abschlag den Score. */
function sechsImZiel(): MetricPayload[] {
  return [
    metrik('reservoir-ph', 6, 5.8, 6.2),
    metrik('reservoir-ec', 1.6, 1.5, 1.8),
    metrik('temperature', 25, 24, 27),
    metrik('humidity', 60, 55, 70),
    metrik('vpd', 1.1, 1.0, 1.3),
    metrik('reservoir-temp', 19, 18, 21),
  ]
}

describe('buildScore', () => {
  it('bleibt ohne Zelt und ohne Messwerte neutral', () => {
    expect(buildScore([], null).tone).toBe('neutral')
    expect(buildScore([], zelt)).toMatchObject({ value: 0, tone: 'neutral', label: 'Einrichten' })
  })

  it('nennt alles im Zielband stabil und grün', () => {
    const score = buildScore(sechsImZiel(), zelt)

    expect(score.value).toBe(100)
    expect(score.tone).toBe('ok')
    expect(score.label).toBe('Stabil')
  })

  it('bleibt bei einer knappen Abweichung noch stabil', () => {
    const werte = sechsImZiel()
    // Knapp daneben: weniger als eine Zielbandbreite -> 10 Punkte Abzug.
    werte[0] = metrik('reservoir-ph', 6.3, 5.8, 6.2)

    const score = buildScore(werte, zelt)

    expect(score.value).toBe(90)
    expect(score.tone).toBe('ok')
  })

  it('kippt auf Beobachten, sobald ein Wert weit daneben liegt', () => {
    const werte = sechsImZiel()
    // Mehr als eine Bandbreite daneben -> 20 Punkte Abzug.
    werte[0] = metrik('reservoir-ph', 8, 5.8, 6.2)

    const score = buildScore(werte, zelt)

    expect(score.value).toBe(80)
    expect(score.tone).toBe('warn')
    expect(score.label).toBe('Beobachten')
  })

  it('wird kritisch, sobald mehrere Werte weit daneben liegen', () => {
    const werte = sechsImZiel()
    // Jeweils deutlich mehr als eine Bandbreite daneben -> 20 Punkte je Wert.
    werte[0] = metrik('reservoir-ph', 8, 5.8, 6.2)
    werte[1] = metrik('reservoir-ec', 4, 1.5, 1.8)
    werte[2] = metrik('temperature', 40, 24, 27)

    const score = buildScore(werte, zelt)

    expect(score.value).toBe(40)
    expect(score.tone).toBe('critical')
    expect(score.label).toBe('Kritisch')
  })

  it('beschönigt fehlende Sensoren nicht', () => {
    // Nur zwei brauchbare Werte: vier fehlende Sensoren kosten je 8 Punkte.
    const score = buildScore(sechsImZiel().slice(0, 2), zelt)

    expect(score.value).toBe(68)
    expect(score.tone).toBe('warn')
  })

  it('hält die Ampelgrenzen bei 82 und 55', () => {
    // Direkt an den Grenzen geprüft, damit ein verschobener Vergleich auffällt.
    const werte = sechsImZiel()

    // Zwei weit daneben -> 60: noch Beobachten.
    werte[0] = metrik('reservoir-ph', 8, 5.8, 6.2)
    werte[1] = metrik('reservoir-ec', 4, 1.5, 1.8)
    expect(buildScore([...werte], zelt)).toMatchObject({ value: 60, tone: 'warn' })

    // Drei weit daneben -> 40: kritisch.
    werte[2] = metrik('temperature', 40, 24, 27)
    expect(buildScore([...werte], zelt)).toMatchObject({ value: 40, tone: 'critical' })
  })
})
