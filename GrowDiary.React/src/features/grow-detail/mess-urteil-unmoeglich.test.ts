import { describe, expect, it } from 'vitest'
import type { MeasurementAssessmentDto, MeasurementAssessmentReportDto, MetricAssessmentDto } from '../../types'
import { NACHKOMMASTELLEN, bilanzKurz, bilanzSatz, urteilKlasse, urteilSatz, urteilZeichen } from './mess-urteil'

/**
 * Unmögliche Werte müssen IM PROTOKOLL zu sehen sein, nicht nur im Formular.
 *
 * <b>Der Anlass.</b> Im Messprotokoll standen `EC 99.999` und `9.000 °C` in
 * ganz normalen Zeilen — ohne Zeichen, ohne Farbe, ohne Wort. Der Grund: das
 * Backend gab beiden das Urteil `NoTarget`, dasselbe wie „für diese Phase gibt
 * es kein Zielband". Das eine heißt „unauffällig", das andere „das Messgerät
 * stimmt nicht" — gleich gezeichnet hieß: unsichtbar.
 *
 * Gefunden hat es der Nutzer: „unlogische werte wird keine meldung teilweise
 * gegeben". Die Live-Prüfung beim Eintippen hatte die Grenzen da schon; das
 * Protokoll, in dem er den ganzen Grow beurteilt, nicht.
 */
describe('Unmögliche Werte im Protokoll', () => {
  const wert = (metric: string, value: number, verdict: MetricAssessmentDto['verdict'], note = ''): MetricAssessmentDto => ({
    metric, label: metric, value, unit: '°C', targetMin: null, targetMax: null, verdict, note,
  })

  const zeile = (metrics: MetricAssessmentDto[], excluded = false): MeasurementAssessmentDto => ({
    measurementId: 1, takenAt: '2026-08-18T10:00:00', storedStage: 'Veg', computedStage: null,
    source: 'Manual', excluded, excludedReason: null, metrics,
  })

  it('trägt ein eigenes Zeichen — Farbe allein ist keine Auskunft', () => {
    expect(urteilZeichen('Impossible')).toBe('⚠')
    // Und es ist ein ANDERES als „über dem Ziel": ein Grund am Messgerät ist
    // kein Grund an der Pflanze.
    expect(urteilZeichen('Impossible')).not.toBe(urteilZeichen('Above'))
  })

  it('bekommt eine eigene Klasse, nicht die von „daneben“', () => {
    expect(urteilKlasse('Impossible')).toContain('is-unmoeglich')
    expect(urteilKlasse('Impossible')).not.toContain('is-bad')
    // Gegenprobe: die alte Klasse gibt es weiter für echte Abweichungen.
    expect(urteilKlasse('Above')).toContain('is-bad')
  })

  it('sagt im Klartext, dass es den Wert nicht geben kann', () => {
    const satz = urteilSatz(wert('air-temp', 9000, 'Impossible', 'Physikalisch nicht möglich (-20–60 °C).'))
    expect(satz).toContain('kann es nicht geben')
    // Deutsch mit Tausenderpunkt — dieselbe Schreibweise wie in der Zelle.
    expect(satz).toContain('9.000')
  })

  it('steht in der Zeile am Telefon — auch wenn die Zeile ausgeschlossen ist', () => {
    // DAS war die Lücke. Eine Zeile mit unplausiblem Zeitpunkt UND einem
    // unmöglichen Wert sagte gar nichts, weil `excluded` sofort abbrach.
    expect(bilanzKurz(zeile([wert('ec', 99999, 'Impossible')], true))).toBe('ec unmöglich')
    expect(bilanzKurz(zeile([wert('air-temp', 9000, 'Impossible')]))).toBe('air-temp unmöglich')
  })

  it('nennt Unmögliches neben den echten Abweichungen, nicht statt ihrer', () => {
    const gemischt = zeile([
      wert('air-temp', 9000, 'Impossible'),
      wert('ph', 4.1, 'Below'),
      wert('ec', 1.5, 'InTarget'),
    ])
    // Beides muss dastehen: der Messgerätefehler UND der Befund am Grow.
    expect(bilanzKurz(gemischt)).toBe('air-temp unmöglich · 1 von 2 daneben')
  })

  it('verzerrt die Zählung „daneben“ nicht', () => {
    // Ein unmöglicher Wert darf nicht als Abweichung mitzählen — sonst sähe ein
    // Tippfehler aus wie ein Problem an der Pflanze.
    const nurUnmoeglich = zeile([wert('ec', 99999, 'Impossible'), wert('ph', 5.9, 'InTarget')])
    expect(bilanzKurz(nurUnmoeglich)).toBe('ec unmöglich · alle im Ziel')

    // Bei mehreren wird gezaehlt statt aufgezaehlt — die Zeile ist am Telefon
    // eine Zeile.
    const zwei = zeile([wert('ec', 99999, 'Impossible'), wert('air-temp', 9000, 'Impossible')])
    expect(bilanzKurz(zwei)).toBe('2 Werte unmöglich')
  })

  it('steht in der Bilanz über dem Protokoll', () => {
    const bericht: MeasurementAssessmentReportDto = {
      measurementCount: 19, excludedCount: 2, checkedValueCount: 32, inTargetCount: 19,
      offTargetCount: 13, impossibleCount: 2, profileId: 'custom:1', profileLabel: 'eigenes Profil',
      measurements: [],
    }
    expect(bilanzSatz(bericht)).toContain('2 unmöglich')
    // Getrennt von „daneben" genannt, nicht dazugezählt.
    expect(bilanzSatz(bericht)).toContain('13 daneben')
  })

  it('liest dieselbe Zahl vor, die in der Zelle steht', () => {
    // Die Zelle zeigte „5,95", der Vorlese-Satz sagte „5,953333333333333" —
    // aus einem Durchschnitt über drei Sensorwerte. Zwei Wahrheiten für eine
    // Zahl, gefunden erst beim LESEN der gerenderten Seite.
    const satz = urteilSatz(wert('ph', 5.953333333333333, 'InTarget', 'dein Grenzwert 5,8–6,2.'))
    expect(satz).toContain('5,95')
    expect(satz).not.toContain('5,953')

    // Und je Größe die richtige Anzahl: Temperaturen eine Stelle, Feuchte keine.
    expect(urteilSatz(wert('air-temp', 24.53333333333333, 'NoTarget'))).toContain('24,5')
    expect(urteilSatz(wert('humidity', 58.633333333333326, 'NoTarget'))).toContain('59')
    expect(urteilSatz(wert('humidity', 58.633333333333326, 'NoTarget'))).not.toContain('58,6')
  })

  it('kennt jede Messgröße, die das Backend erzeugt', () => {
    // Die Kennungen stammen aus MeasurementAssessmentService. Fehlt eine, fällt
    // sie stumm auf eine Stelle zurück — bei pH wäre das falsch gerundet.
    for (const metrik of ['ph', 'ec', 'water-temp', 'air-temp', 'humidity', 'vpd', 'orp', 'do', 'co2', 'ppfd']) {
      expect(NACHKOMMASTELLEN, `${metrik} fehlt in NACHKOMMASTELLEN`).toHaveProperty(metrik)
    }
  })

  it('beginnt den Satz nach dem Punkt groß', () => {
    // Gelesen stand da „im Ziel. dein Grenzwert 5,8–6,2." — die Notiz ist im
    // Backend ein Halbsatz, hier steht ein Punkt davor.
    expect(urteilSatz(wert('ph', 5.9, 'InTarget', 'dein Grenzwert 5,8–6,2.'))).toContain('. Dein Grenzwert')
  })

  it('schweigt, wenn es nichts Unmögliches gibt', () => {
    // Gegenprobe: die Warnung darf nicht überall auftauchen.
    const sauber: MeasurementAssessmentReportDto = {
      measurementCount: 5, excludedCount: 0, checkedValueCount: 20, inTargetCount: 20,
      offTargetCount: 0, impossibleCount: 0, profileId: 'rdwc-default', profileLabel: 'rdwc-default',
      measurements: [],
    }
    expect(bilanzSatz(sauber)).not.toContain('unmöglich')
    expect(bilanzKurz(zeile([wert('ph', 5.9, 'InTarget')]))).toBe('alle im Ziel')
  })
})
