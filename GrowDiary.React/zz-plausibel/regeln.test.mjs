/**
 * Selbsttest der Regeln — ohne Browser, in einer Sekunde.
 *
 * Aufruf: node --test zz-plausibel/
 *
 * Warum es das gibt: eine Pruefung, die nie etwas meldet, sieht genauso aus
 * wie eine Pruefung, die nichts finden KANN. Der zweite Teil (die Ausnahmen)
 * ist genauso wichtig wie der erste — falsche Befunde kosten mehr Vertrauen
 * als uebersehene.
 */
import { strict as assert } from 'node:assert'
import { test } from 'node:test'
import { datumPruefen, platzhalterPruefen, spanne, zahlenLesen, zahlenPruefen, GRENZEN } from './regeln.mjs'

const kachel = (roh, etikett) => ({ roh, etikett, etikettQuelle: 'kachel' })
const prosa = (roh, etikett) => ({ roh, etikett, etikettQuelle: 'davor' })

test('die Grenzen werden wirklich gelesen — sonst ist alles still gruen', () => {
  assert.equal(GRENZEN.size >= 10, true)
  assert.deepEqual(spanne('co2'), { min: 0, max: 30000, quelle: 'PhysikalischeGrenzen["co2"]' })
})

test('deutsche Zahlen richtig lesen', () => {
  assert.deepEqual(zahlenLesen('99.999').map((z) => z.wert), [99999])
  assert.deepEqual(zahlenLesen('25,7 °C').map((z) => z.wert), [25.7])
  assert.deepEqual(zahlenLesen('1.234,56 ml').map((z) => z.wert), [1234.56])
  // Datum und Uhrzeit duerfen NICHT in Einzelzahlen zerfallen
  assert.deepEqual(zahlenLesen('01.01.2099, 10:00').map((z) => z.wert), [])
})

test('faengt die fuenf Werte, die wochenlang auf dem Bildschirm standen', () => {
  const faelle = [
    [kachel('-500ppm', 'CO₂'), 'Negativ, obwohl nicht möglich'],
    [kachel('5.000', 'Wasser'), 'Physikalisch unmöglich'],
    [kachel('9.000', 'Luft'), 'Physikalisch unmöglich'],
    [kachel('99.999', 'EC'), 'Physikalisch unmöglich'],
    [kachel('143 %', 'Feuchte'), 'Prozent über 100'],
  ]
  for (const [ablesung, regel] of faelle) {
    const funde = zahlenPruefen(ablesung)
    assert.equal(funde.length, 1, 'kein Befund fuer ' + ablesung.roh)
    assert.equal(funde[0].regel, regel)
  }
})

test('gewollte Faelle bleiben unbeanstandet — mit Grund', () => {
  // ORP darf negativ sein: reduzierende Loesung.
  assert.deepEqual(zahlenPruefen(kachel('-320 mV', 'ORP')), [])
  // Ein Temperaturunterschied ist negativ gemeint.
  assert.deepEqual(zahlenPruefen(kachel('-1 °C je Woche', 'Nachtabsenkung')), [])
  assert.deepEqual(zahlenPruefen(kachel('Δ -3 °C', 'DIF')), [])
  // Normale Werte.
  assert.deepEqual(zahlenPruefen(kachel('25,7 °C', 'Luft')), [])
  assert.deepEqual(zahlenPruefen(kachel('64 %', 'Feuchte')), [])
  assert.deepEqual(zahlenPruefen(kachel('1,50 mS/cm', 'EC')), [])
})

test('geratene Beschriftungen begruenden keine Zuordnung', () => {
  // Der Fehlgriff des ersten Laufs: "EC-Sonde … ~30 Monate" wurde zu EC 30.
  assert.deepEqual(zahlenPruefen(prosa('~30 Monate', 'EC-Sonde')), [])
  // Eine Flaeche ist keine Laenge — sonst waren 2.025 cm² ein Befund.
  assert.deepEqual(zahlenPruefen(prosa('2.025 cm²', 'Fläche/Pflanze')), [])
})

test('die Einheit darf hinter einer Spanne stehen', () => {
  assert.deepEqual(zahlenPruefen(prosa('Ziel 25,0–29,0 °C', 'Luft')), [])
  const f = zahlenPruefen(prosa('Ziel 25,0–9.000 °C', 'Luft'))
  assert.equal(f.length, 1)
  assert.equal(f[0].wert, 9000)
})

test('Platzhalter: was ueberall zaehlt und was nur allein', () => {
  assert.equal(platzhalterPruefen('Fuellstand NaN Liter'), 'NaN')
  assert.equal(platzhalterPruefen('Verbrauch [object Object]'), '[object Object]')
  assert.equal(platzhalterPruefen('Geerntet Invalid Date'), 'Invalid Date')
  assert.equal(platzhalterPruefen('Reservoir undefined Liter'), 'undefined')
  assert.equal(platzhalterPruefen('-1', true), '-1 als Ersatz für „unbekannt"')
  // Das Zelt heisst „AC Infinity Growzelt" — kein Rechenfehler.
  assert.equal(platzhalterPruefen('AC Infinity Growzelt', true), null)
  assert.equal(platzhalterPruefen('Infinity', true), 'Infinity')
  // „null" ist ein deutsches Wort.
  assert.equal(platzhalterPruefen('null Überlauf über zwölf Breiten', false), null)
})

test('Zeitangaben: Zukunft, Vergangenheit, Platzhalter', () => {
  const jetzt = new Date(2026, 7, 19)
  assert.equal(datumPruefen('01.01.2099, 10:00', jetzt)[0].regel, 'Zeitpunkt unglaubwürdig weit in der Zukunft')
  assert.equal(datumPruefen('01.01.1970', jetzt)[0].regel, 'Platzhalter-Datum')
  assert.equal(datumPruefen('12.03.1998', jetzt)[0].regel, 'Zeitpunkt unglaubwürdig weit in der Vergangenheit')
  // Ein geplanter Termin in der Zukunft ist kein Fehler.
  assert.deepEqual(datumPruefen('Ernte 12.11.2026', jetzt), [])
})
