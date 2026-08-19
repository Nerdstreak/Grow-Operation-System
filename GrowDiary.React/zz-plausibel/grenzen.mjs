/**
 * Die physikalischen Grenzen — GELESEN, nicht abgetippt.
 *
 * Warum lesen statt abtippen: genau daran ist der Protokoll-Beurteiler
 * gescheitert. Die Zahlen standen nur im Sperr-Code, eine zweite Stelle
 * kannte sie nicht, und EC 99999 zaehlte dort als normale Abweichung mit.
 * Eine abgetippte Kopie in diesem Skript waere die dritte Stelle und der
 * naechste Widerspruch. Also: eine Tabelle, jetzt drei Leser.
 */
import { readFileSync } from 'node:fs'

export const QUELLE = new URL('../../GrowDiary.Web/Services/MeasurementSanityService.cs', import.meta.url)

export function grenzenLesen(quelle = QUELLE) {
  const text = readFileSync(quelle, 'utf8')
  const start = text.indexOf('PhysikalischeGrenzen')
  if (start < 0) throw new Error('PhysikalischeGrenzen steht nicht mehr in ' + quelle.pathname)
  const block = text.slice(start, text.indexOf('};', start))
  const grenzen = new Map()
  for (const treffer of block.matchAll(/\["([a-z0-9-]+)"\]\s*=\s*\(\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\)/g)) {
    grenzen.set(treffer[1], { min: Number(treffer[2]), max: Number(treffer[3]) })
  }

  // Der Selbsttest, den MessfelderVollstaendigTests.cs vormacht: eine
  // Zaehlung, die nichts SIEHT, ist leer und trotzdem gruen. Wenn jemand die
  // Tabelle umbaut und dieser Regex ins Leere greift, meldet dieses Skript
  // ab morgen null Befunde — und niemand merkt es.
  const pflicht = ['ph', 'ec', 'water-temp', 'air-temp', 'humidity', 'co2', 'orp', 'ppfd', 'do', 'vpd']
  const fehlend = pflicht.filter((k) => !grenzen.has(k))
  if (fehlend.length) {
    throw new Error('Grenzen nicht gelesen (Tabelle umgebaut?). Es fehlen: ' + fehlend.join(', '))
  }
  return grenzen
}

/**
 * Groessen, die die C#-Tabelle nicht kennt, weil sie keine Messwerte sind —
 * sie stehen aber auf dem Bildschirm und koennen genauso unmoeglich werden.
 * Hier mit Grund, damit niemand raten muss.
 */
export const WEITERE_GRENZEN = new Map([
  ['prozent', { min: 0, max: 100, grund: 'Ein Anteil ueber 100 % ist kein Anteil mehr.' }],
  ['menge-ml', { min: 0, max: 200000, grund: 'Giess- und Runoffmengen; 200 l waeren schon ein Tanklaster.' }],
  ['menge-l', { min: 0, max: 5000, grund: 'Reservoir- und Top-Off-Liter.' }],
  ['laenge-cm', { min: 0, max: 500, grund: 'Hoehe und Wasserstand; ueber 5 m ist kein Zelt mehr.' }],
  ['anzahl', { min: 0, max: 100000, grund: 'Zaehler und Stueckzahlen sind nie negativ.' }],
  ['dauer', { min: 0, max: 100000, grund: 'Eine Dauer laeuft nicht rueckwaerts.' }],
  ['strom-w', { min: 0, max: 100000, grund: 'Leistungsaufnahme.' }],
])

/**
 * Ausdruecklich AUSGENOMMEN — mit Grund, wie es die Menue-Pruefung vormacht.
 * Ein Eintrag ohne Grund ist beim naechsten Audit selbst ein Befund.
 */
export const AUSNAHMEN = new Map([
  ['orp', 'ORP darf negativ sein: eine reduzierende Loesung liefert Minuswerte. Die Tabelle laesst deshalb -1000 zu.'],
  ['temperatur-differenz', 'Ein Temperaturunterschied (DIF, Nachtabsenkung, Rampe) ist negativ gemeint — "-1 °C je Woche" ist der Sollwert, kein Fehler.'],
  ['veraenderung', 'Trend- und Differenzangaben mit Vorzeichen (+/-, Delta, "gegenueber") duerfen negativ sein und ueber 100 % gehen.'],
  ['geld', 'Preise pruefe ich nicht auf Plausibilitaet — was Duenger kostet, weiss dieses Skript nicht.'],
  ['datum-geplant', 'Geplante Termine liegen selbstverstaendlich in der Zukunft. Beanstandet wird erst, was mehr als fuenf Jahre voraus liegt.'],
])
