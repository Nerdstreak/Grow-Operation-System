/**
 * Die Regeln. Reine Funktionen auf abgelesenem Text — kein Browser noetig,
 * damit man sie einzeln nachrechnen kann (siehe regeln.node.test.mjs).
 */
import { AUSNAHMEN, WEITERE_GRENZEN, grenzenLesen } from './grenzen.mjs'

export const GRENZEN = grenzenLesen()

/**
 * Einheit am Bildschirm -> Groesse. Reihenfolge zaehlt: das Laengste zuerst,
 * sonst schluckt "cm" das "cm²" und aus einer Flaeche wird eine Laenge.
 */
const EINHEITEN = [
  ['µmol/m²/s', 'ppfd'], ['umol/m²/s', 'ppfd'], ['umol/m2/s', 'ppfd'], ['µmol', 'ppfd'],
  ['mS/cm', 'ec'], ['µS/cm', 'ec-mikro'], ['uS/cm', 'ec-mikro'],
  ['mg/L', 'do'], ['mg/l', 'do'],
  ['kPa', 'vpd'], ['mV', 'orp'], ['ppm', 'co2'],
  ['°dH', 'haerte'], ['°C', 'temperatur'],
  ['cm²', 'flaeche'], ['m²', 'flaeche'],
  ['kWh', 'strom-w'], ['Wh', 'strom-w'],
  ['%', 'prozent'],
  ['ml/min', 'menge-ml'], ['ml', 'menge-ml'],
  ['Liter', 'menge-l'], ['l/min', 'menge-l'], ['L', 'menge-l'],
  ['pH', 'ph'],
  ['cm', 'laenge-cm'], ['mm', 'laenge-cm'],
  ['Tagen', 'dauer'], ['Tage', 'dauer'], ['Tag', 'dauer'], ['Std', 'dauer'], ['Wochen', 'dauer'],
]

/**
 * Beschriftung -> Groesse, wenn keine Einheit dransteht.
 *
 * Nur fuer Beschriftungen, die die Oberflaeche ZUSICHERT: das Etikett einer
 * Kachel, ein Spaltenkopf, ein Formularfeld. Der Text, der zufaellig davor
 * steht, zaehlt hier NICHT — sonst wird aus „EC-Sonde … ~30 Monate" ein
 * EC-Wert von 30 und der Bericht ist Schrott.
 */
const ETIKETTEN = [
  [/co.?[2₂]/i, 'co2'],
  [/wassertemp|^wasser$/i, 'water-temp'],
  [/lufttemp|^luft$/i, 'air-temp'],
  [/feuchte|\brf\b|humid/i, 'humidity'],
  [/^ph$|ph-wert|^ph\b/i, 'ph'],
  [/^ec$|^ec\b|leitwert/i, 'ec'],
  [/orp|redox/i, 'orp'],
  [/vpd/i, 'vpd'],
  [/ppfd|lichtstärke|lichtstaerke/i, 'ppfd'],
  [/sauerstoff|gelöster|geloester|^do$|^o₂$/i, 'do'],
]

/** Beschriftungsquellen, denen die Zuordnung glauben darf. */
export const HARTE_ETIKETTEN = new Set(['kachel', 'spalte', 'feld'])

/**
 * Woran man erkennt, dass eine Zahl eine VERAENDERUNG ist und nicht ein Wert.
 * Solche Zahlen duerfen negativ sein und ueber 100 % gehen — siehe AUSNAHMEN.
 */
const VERAENDERUNG = /[+±Δ]|delta|differenz|unterschied|absenkung|\bdif\b|rampe|je woche|gegenüber|gegenueber|trend|abweichung|toleranz|spanne|schwankung|karenz/i

/** Zahlen, die gar keine Messwerte sind. */
const KEINE_MESSZAHL = /version|beta\.|build|traceid|^#\d|\bpx\b/i

export function zahlenLesen(text) {
  // Datum und Uhrzeit zuerst ausblenden, sonst zerlegt der Zahl-Regex sie
  // in 01, 08, 2026, 10, 00 und meldet Unsinn.
  const ohneZeit = text
    .replace(/\d{1,2}\.\d{1,2}\.(\d{4})?/g, (m) => ' '.repeat(m.length))
    .replace(/\d{1,2}:\d{2}(:\d{2})?/g, (m) => ' '.repeat(m.length))
  const treffer = []
  const muster = /(?<![\d.,:])([+-]?\d{1,3}(?:\.\d{3})+(?:,\d+)?|[+-]?\d+(?:,\d+)?)(?![\d,:])/g
  let t
  while ((t = muster.exec(ohneZeit))) {
    const roh = t[1]
    const wert = Number(roh.replace(/\./g, '').replace(',', '.'))
    if (!Number.isFinite(wert)) continue
    treffer.push({ roh, wert, hinter: text.slice(t.index + roh.length, t.index + roh.length + 22) })
  }
  return treffer
}

/**
 * Die Einheit steht nicht immer direkt hinter der Zahl: „Ziel 25,0–29,0 °C"
 * und „(~400–500 ppm)" haengen sie hinter das ZWEITE Ende der Spanne. Also
 * darf zwischen Zahl und Einheit noch eine zweite Zahl und Zeichensetzung
 * liegen — aber nichts anderes, sonst zieht das Skript Einheiten heran, die
 * zu einem ganz anderen Wert gehoeren.
 */
function einheitSuchen(hinter) {
  for (let laenge = 0; laenge <= hinter.length; laenge++) {
    const zwischen = hinter.slice(0, laenge)
    if (!/^[\s\d.,~()–—-]*$/.test(zwischen)) break
    const rest = hinter.slice(laenge)
    for (const [einheit, groesse] of EINHEITEN) {
      if (rest.startsWith(einheit)) return { groesse, quelle: 'Einheit "' + einheit + '"' }
    }
  }
  return null
}

export function groesseBestimmen(hinter, etikett, etikettQuelle) {
  const ueberEinheit = einheitSuchen(hinter)
  if (ueberEinheit) return ueberEinheit
  if (etikett && HARTE_ETIKETTEN.has(etikettQuelle)) {
    for (const [muster, groesse] of ETIKETTEN) {
      if (muster.test(etikett)) return { groesse, quelle: 'Beschriftung "' + etikett + '"' }
    }
  }
  return { groesse: null, quelle: null }
}

export function spanne(groesse) {
  if (groesse === 'temperatur') {
    // Ein blankes °C sagt nicht, ob Luft oder Wasser gemeint ist. Also die
    // weitere der beiden Spannen nehmen — lieber einen Wert durchlassen als
    // einen falschen Befund erfinden.
    const l = GRENZEN.get('air-temp'), w = GRENZEN.get('water-temp')
    return { min: Math.min(l.min, w.min), max: Math.max(l.max, w.max), quelle: 'air-temp ∪ water-temp' }
  }
  if (groesse === 'ec-mikro') {
    const e = GRENZEN.get('ec')
    return { min: e.min * 1000, max: e.max * 1000, quelle: 'ec × 1000' }
  }
  if (GRENZEN.has(groesse)) return { ...GRENZEN.get(groesse), quelle: 'PhysikalischeGrenzen["' + groesse + '"]' }
  if (WEITERE_GRENZEN.has(groesse)) return { ...WEITERE_GRENZEN.get(groesse), quelle: 'WEITERE_GRENZEN["' + groesse + '"]' }
  return null   // flaeche, haerte, geld: dieses Skript kennt dafuer keine Wahrheit
}

/** Regel 1-3: Physik, Minuszeichen, Prozent ueber hundert. */
export function zahlenPruefen(ablesung) {
  const funde = []
  if (KEINE_MESSZAHL.test(ablesung.roh)) return funde
  const istVeraenderung = VERAENDERUNG.test(ablesung.roh) || VERAENDERUNG.test(ablesung.etikett ?? '')
  for (const zahl of zahlenLesen(ablesung.roh)) {
    const { groesse, quelle } = groesseBestimmen(zahl.hinter, ablesung.etikett, ablesung.etikettQuelle)
    if (!groesse) continue
    if (groesse === 'temperatur' && istVeraenderung) continue         // AUSNAHMEN: temperatur-differenz
    if (groesse === 'orp' && zahl.wert < 0 && zahl.wert >= -1000) continue // AUSNAHMEN: orp
    if (istVeraenderung && zahl.wert < 0) continue                     // AUSNAHMEN: veraenderung
    const s = spanne(groesse)
    if (!s) continue
    if (zahl.wert < s.min || zahl.wert > s.max) {
      const regel = groesse === 'prozent' && zahl.wert > 100 ? 'Prozent über 100'
        : zahl.wert < s.min && s.min >= 0 ? 'Negativ, obwohl nicht möglich'
        : 'Physikalisch unmöglich'
      funde.push({
        regel, groesse, wert: zahl.wert, gezeigt: zahl.roh,
        erwartet: s.min + '–' + s.max, herkunft: s.quelle, erkannt: quelle,
      })
    }
  }
  return funde
}

/**
 * Regel 4: Platzhalter, die als Wert durchgehen.
 *
 * NaN, [object Object], Invalid Date und undefined kommen in deutscher Prosa
 * nicht vor — die darf man ueberall suchen. Bei zwei Woertern geht das nicht:
 * „Infinity" steckt im Zeltnamen „AC Infinity Growzelt", und „null" ist ein
 * deutsches Wort. Die beiden zaehlen nur, wenn sie ALLEIN in einem Wertknoten
 * stehen. Sonst meldet die Pruefung den Herstellernamen des Zelts.
 */
const PLATZHALTER_UEBERALL = [
  [/\bNaN\b/, 'NaN'],
  [/\[object Object\]/, '[object Object]'],
  [/Invalid Date/, 'Invalid Date'],
  [/(^|[\s:>(=])undefined([\s<),.]|$)/, 'undefined'],
]
const PLATZHALTER_ALLEIN = [
  [/^[-−+]?Infinity$|^[-−+]?∞$/, 'Infinity'],
  [/^null$/, 'null'],
  [/^[-−]1$/, '-1 als Ersatz für „unbekannt"'],
]
export function platzhalterPruefen(text, istWertknoten = false) {
  for (const [muster, name] of PLATZHALTER_UEBERALL) if (muster.test(text)) return name
  if (istWertknoten) {
    const blank = text.trim()
    for (const [muster, name] of PLATZHALTER_ALLEIN) if (muster.test(blank)) return name
  }
  return null
}

/**
 * Regel 5: Zeitangaben.
 *
 * Geplante Termine liegen selbstverstaendlich in der Zukunft (Ernte,
 * naechster Wasserwechsel). Beanstandet wird deshalb erst, was mehr als
 * fuenf Jahre voraus liegt — in einem Grow-Tagebuch plant niemand so weit.
 * Nach hinten ist die Grenze 2010: aelter als die App kann nichts sein.
 */
export function datumPruefen(text, jetzt = new Date()) {
  const funde = []
  for (const t of text.matchAll(/\b(\d{1,2})\.(\d{1,2})\.(\d{4})\b/g)) {
    const d = new Date(Number(t[3]), Number(t[2]) - 1, Number(t[1]))
    const jahre = (d - jetzt) / (365.25 * 24 * 3600 * 1000)
    if (t[3] === '0001' || t[3] === '1970') funde.push({ regel: 'Platzhalter-Datum', gezeigt: t[0], erwartet: 'ein echtes Datum' })
    else if (jahre > 5) funde.push({ regel: 'Zeitpunkt unglaubwürdig weit in der Zukunft', gezeigt: t[0], erwartet: 'höchstens 5 Jahre voraus' })
    else if (Number(t[3]) < 2010) funde.push({ regel: 'Zeitpunkt unglaubwürdig weit in der Vergangenheit', gezeigt: t[0], erwartet: 'nicht vor 2010' })
  }
  return funde
}

export { AUSNAHMEN }
