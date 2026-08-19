/**
 * Plausibilitaets-Pruefung: jede Zahl auf dem Bildschirm gegen die Wirklichkeit.
 *
 * <b>Wozu.</b> Bisher sahen alle Pruefungen auf Ueberlauf, Kontrast, Layout und
 * Erreichbarkeit. CO2 = -500 ppm rendert tadellos, hat vollen Kontrast, laeuft
 * nirgends ueber — und ist trotzdem falsch. Diese Linse hat gefehlt.
 *
 * <b>Wie.</b> Ueber alle Routen der App (aus App.tsx GELESEN, nicht gepflegt),
 * in zwei Breiten. Auf jeder Seite werden die innersten Textknoten mit Ziffern
 * abgelesen, die Zahl darin nach ihrer Einheit oder ihrer Beschriftung einer
 * Groesse zugeordnet und gegen die Grenzen gehalten, die in
 * MeasurementSanityService.PhysikalischeGrenzen stehen.
 *
 * Aufruf:  node zz-plausibel/pruefe-zahlen.mjs [--json datei] [--nur /route] [--gegenprobe]
 */
import { chromium } from '@playwright/test'
import { readFileSync, writeFileSync } from 'node:fs'
import { datumPruefen, platzhalterPruefen, zahlenPruefen, GRENZEN, AUSNAHMEN } from './regeln.mjs'

const BASIS = 'http://127.0.0.1:5076'
const args = process.argv.slice(2)
const nurRoute = args.includes('--nur') ? args[args.indexOf('--nur') + 1] : null
const jsonZiel = args.includes('--json') ? args[args.indexOf('--json') + 1] : null
/** Gegenprobe: schiebt vor dem Ablesen unmoegliche Werte in die Seite. */
const gegenprobe = args.includes('--gegenprobe')

// ---------------------------------------------------------------- Routen
// Wie die Menue-Pruefung: die Liste wird gelesen, nicht gepflegt. Eine
// handgepflegte Routenliste ist genau die Sorte Liste, die veraltet und dann
// eine Seite jahrelang ungeprueft laesst.
const app = readFileSync(new URL('../src/App.tsx', import.meta.url), 'utf8')
const rohRouten = [...app.matchAll(/<Route path="([^"]+)"/g)].map((t) => t[1])
if (rohRouten.length < 20) throw new Error('Routen nicht gelesen — App.tsx umgebaut? Gefunden: ' + rohRouten.length)

const holen = async (pfad) => {
  try { return await (await fetch(BASIS + pfad)).json() } catch { return [] }
}
const grows = await holen('/api/grows')
const growId = grows[0]?.id ?? 1
const messungen = await holen('/api/grows/' + growId + '/measurements')
const pumpen = await holen('/api/dosing/pumps')

const ERSATZ = {
  ':growId': [String(growId)],
  ':measurementId': (Array.isArray(messungen) ? messungen : []).slice(0, 8).map((m) => String(m.id)),
  ':tentId': [String(grows[0]?.tentId ?? 1)],
  ':setupId': [String(grows[0]?.systemId ?? 1)],
  ':id': [String(grows[0]?.systemId ?? 1)],
  ':pumpId': [String(pumpen[0]?.id ?? 1)],
}
function ausfuellen(route) {
  let liste = [route]
  for (const [platzhalter, werte] of Object.entries(ERSATZ)) {
    liste = liste.flatMap((r) => (r.includes(platzhalter) ? werte.map((w) => r.replace(platzhalter, w)) : [r]))
  }
  return liste.filter((r) => !r.includes(':'))
}
let ROUTEN = [...new Set(rohRouten.flatMap(ausfuellen))]
if (nurRoute) ROUTEN = ROUTEN.filter((r) => r === nurRoute)

// ---------------------------------------------------------------- Ablesen
const ABLESEN = () => {
  const sichtbar = (el) => {
    const r = el.getBoundingClientRect()
    if (r.width < 1 || r.height < 1) return false
    for (let e = el; e; e = e.parentElement) {
      const s = getComputedStyle(e)
      if (s.display === 'none' || s.visibility === 'hidden' || Number(s.opacity) < 0.1) return false
    }
    return true
  }
  const pfad = (el) => {
    const t = []
    for (let e = el; e && t.length < 4; e = e.parentElement) {
      const cn = typeof e.className === 'string' && e.className ? '.' + e.className.trim().split(/\s+/).slice(0, 2).join('.') : ''
      t.unshift(e.tagName.toLowerCase() + cn)
    }
    return t.join(' > ')
  }
  const imVorleser = (el) => !!el.closest('.sr-only')
  /**
   * Ziffern OHNE die Vorlese-Texte. Genau hier lag der erste Fehlgriff dieses
   * Skripts: eine beurteilte Zelle enthaelt ein <span class="sr-only">, in dem
   * die App den Wert nochmal ausschreibt. Weil dieses Kind Ziffern hat, galt
   * die Zelle nicht mehr als innerster Knoten — und ausgerechnet die Spalten
   * mit Urteil (pH, EC, Wassertemperatur) wurden nie abgelesen.
   */
  const ziffernOhneVorleser = (el) => {
    for (const n of el.childNodes) {
      if (n.nodeType === 3) { if (/\d/.test(n.textContent)) return true; continue }
      if (n.nodeType !== 1 || n.classList?.contains('sr-only')) continue
      if (ziffernOhneVorleser(n)) return true
    }
    return false
  }
  const sichtbarerText = (el) => {
    let t = ''
    for (const n of el.childNodes) {
      if (n.nodeType === 3) t += n.textContent
      else if (n.nodeType === 1 && !n.classList?.contains('sr-only')) t += sichtbarerText(n)
    }
    return t
  }

  /**
   * Beschriftung MIT Herkunft. Nur Kachel, Spaltenkopf und Formularfeld sind
   * zugesichert; „der Text davor" ist geraten und darf keine Zuordnung
   * begruenden (siehe HARTE_ETIKETTEN in regeln.mjs).
   */
  const etikettVon = (el) => {
    const kachel = el.closest('.gos-metric')
    if (kachel) {
      const l = kachel.querySelector('.gos-metric-label')
      if (l) return { etikett: l.textContent.trim(), quelle: 'kachel' }
    }
    const zelle = el.closest('.co-td')
    if (zelle) {
      const zeile = zelle.parentElement
      const zellen = [...zeile.children].filter((c) => c.classList.contains('co-td'))
      const i = zellen.indexOf(zelle)
      const koepfe = [...(zeile.parentElement?.children ?? [])].filter((c) => c.classList.contains('co-th'))
      if (i >= 0 && koepfe[i]) return { etikett: koepfe[i].textContent.trim(), quelle: 'spalte' }
    }
    const feld = el.closest('label')
    if (feld) {
      const nur = [...feld.childNodes].filter((n) => n.nodeType === 3).map((n) => n.textContent).join('').trim()
      if (nur) return { etikett: nur.slice(0, 40), quelle: 'feld' }
    }
    for (let e = el.previousElementSibling; e; e = e.previousElementSibling) {
      const t = (e.textContent || '').trim()
      if (t && t.length <= 30 && !/\d/.test(t)) return { etikett: t, quelle: 'davor' }
    }
    const kopf = el.closest('[class*="card"], [class*="panel"], section')?.querySelector('h1,h2,h3,h4,.card-title,legend')
    return kopf ? { etikett: kopf.textContent.trim().slice(0, 40), quelle: 'ueberschrift' } : { etikett: null, quelle: null }
  }

  const ablesungen = []
  const texte = []
  const kacheln = []
  const zaehler = []
  const vorleserTexte = []

  for (const el of document.querySelectorAll('body *')) {
    if (['SCRIPT', 'STYLE', 'SVG', 'PATH'].includes(el.tagName)) continue
    const voll = (el.textContent || '').trim()
    if (!voll) continue
    if (imVorleser(el)) { if (el.classList.contains('sr-only')) vorleserTexte.push(voll); continue }
    if (!sichtbar(el)) continue
    if (voll.length <= 400) texte.push({ roh: voll, pfad: pfad(el) })
    if (!ziffernOhneVorleser(el)) continue
    // Nur der INNERSTE Knoten mit Ziffern — sonst zaehlt jede Zahl viermal.
    if ([...el.children].some((c) => !c.classList?.contains('sr-only') && ziffernOhneVorleser(c))) continue
    const roh = sichtbarerText(el).trim()
    if (!roh || roh.length > 80) continue
    const e = etikettVon(el)
    ablesungen.push({ roh, pfad: pfad(el), etikett: e.etikett, etikettQuelle: e.quelle, wertknoten: true })
  }

  // Formularfelder: was in einem Eingabefeld steht, steht auch auf dem
  // Bildschirm. CO2 = -500 wohnt genau dort — im Messformular, nicht in einer
  // Kachel. textContent sieht das nicht.
  // V1Field baut <label class="v1-field"><span>CO2</span><input><small>ppm</small></label>
  // — die Beschriftung steht im span, die Einheit im small. Nachgesehen in
  // src/components/v1.tsx, Zeile 137-145, nicht geraten.
  for (const el of document.querySelectorAll('input, textarea')) {
    if (!sichtbar(el) || el.type === 'password' || el.type === 'hidden' || el.type === 'checkbox') continue
    const wert = el.value ?? ''
    if (!wert.trim() || !/\d/.test(wert)) continue
    const feld = el.closest('label')
    const etikett = feld?.querySelector('span')?.textContent?.trim() ?? null
    const einheit = feld?.querySelector('small')?.textContent?.trim() ?? ''
    ablesungen.push({
      roh: (wert + ' ' + einheit).trim(), pfad: pfad(el),
      etikett, etikettQuelle: etikett ? 'feld' : null, wertknoten: true, ausFeld: true,
    })
  }

  // Kacheln beider Bauformen: .gos-metric (Live-Dashboard) und .v1-stat
  // (Zelt-, Grow- und Hydro-Seiten). Verglichen wird nur INNERHALB desselben
  // Abschnitts — zwei Zelte duerfen selbstverstaendlich verschiedene
  // Lufttemperaturen zeigen.
  const abschnitt = (el) => pfad(el.closest('section, article, [class*="panel"]') ?? document.body)
  for (const k of document.querySelectorAll('.gos-metric')) {
    if (!sichtbar(k)) continue
    const l = k.querySelector('.gos-metric-label')
    const w = k.querySelector('.gos-metric-value')
    if (l && w) kacheln.push({ etikett: l.textContent.trim(), wert: w.textContent.trim(), abschnitt: abschnitt(k), pfad: pfad(k) })
  }
  for (const k of document.querySelectorAll('.v1-stat')) {
    if (!sichtbar(k)) continue
    const l = k.querySelector('span')
    const w = k.querySelector('strong')
    if (l && w) kacheln.push({ etikett: l.textContent.trim(), wert: w.textContent.trim(), abschnitt: abschnitt(k), pfad: pfad(k) })
  }

  // Zielbaender: "Ziel 0,90–1,10 mS/cm". Steht die kleinere Zahl rechts, ist
  // das Fenster verdreht und keine Ampel davor kann noch stimmen.
  const baender = []
  for (const el of document.querySelectorAll('body *')) {
    const eigen = [...el.childNodes].filter((n) => n.nodeType === 3).map((n) => n.textContent).join('').trim()
    if (!/^(Ziel|Sollwert|Zielbereich|Arbeitsbereich)\b/.test(eigen) || !sichtbar(el)) continue
    const t = eigen.match(/(-?\d+(?:[.,]\d+)?)\s*[–—-]\s*(-?\d+(?:[.,]\d+)?)/)
    if (t) baender.push({ roh: eigen, von: Number(t[1].replace(',', '.')), bis: Number(t[2].replace(',', '.')), pfad: pfad(el) })
  }

  // Zaehler gegen Zeilen. Zwei Einschraenkungen, beide aus einem Fehlgriff
  // des ersten Laufs gelernt:
  //  1. Der Zaehler muss ALLEIN im Knoten stehen ("15 von 19"). Steckt er in
  //     einem Satz ("… · 1 von 4 daneben"), meint er etwas anderes als die
  //     Zeilen darunter.
  //  2. Gezaehlt wird nur INNERHALB der Karte, und die Navigation zaehlt nie
  //     mit — sonst kommen 23 Menuepunkte als "Zeilen" heraus.
  const ZEILEN = '.gd-mess-zeile, .timeline-item, tbody tr, li, [class*="-row"], [class*="-item"]'
  for (const el of document.querySelectorAll('body *')) {
    const eigen = [...el.childNodes].filter((n) => n.nodeType === 3).map((n) => n.textContent).join('').trim()
    const t = eigen.match(/^(\d+)\s+von\s+(\d+)\b/)
    if (!t || !sichtbar(el)) continue
    const karte = el.closest('.ls-panel, [class*="card"], article, section') ?? el.parentElement
    if (!karte || karte.closest('nav')) continue
    const gruppen = new Map()
    for (const z of karte.querySelectorAll(ZEILEN)) {
      const key = (typeof z.className === 'string' ? z.className : '') || z.tagName
      // `display: contents` (die Messzeilen!) hat KEINE Box — getBoundingClientRect
      // liefert 0 und der Zaehlvergleich waere still ausgefallen.
      const sichtbareZeile = z.checkVisibility() || (getComputedStyle(z).display === 'contents' && [...z.children].some((c) => c.checkVisibility()))
      if (!sichtbareZeile || z.closest('nav') || /nav|tab|menu/i.test(key)) continue
      gruppen.set(key, (gruppen.get(key) ?? 0) + 1)
    }
    const beste = [...gruppen.entries()].sort((a, b) => b[1] - a[1])[0]
    zaehler.push({ roh: eigen, gezeigt: Number(t[1]), gesamt: Number(t[2]), zeilen: beste ? beste[1] : 0, zeilenKlasse: beste ? beste[0] : '—', pfad: pfad(el) })
  }

  return { ablesungen, texte, kacheln, zaehler, baender, vorleserTexte }
}

const GEGENPROBE = () => {
  const box = document.createElement('div')
  box.innerHTML =
    '<div class="gos-metric"><span class="gos-metric-label">Luft</span>'
    + '<div class="gos-metric-value">9.000<span class="unit">°C</span></div></div>'
    + '<div class="gos-metric"><span class="gos-metric-label">CO₂</span>'
    + '<div class="gos-metric-value">-500<span class="unit">ppm</span></div></div>'
    + '<div class="gos-metric"><span class="gos-metric-label">Feuchte</span>'
    + '<div class="gos-metric-value">143<span class="unit">%</span></div></div>'
    + '<div class="gos-metric"><span class="gos-metric-label">EC</span>'
    + '<div class="gos-metric-value">99.999<span class="unit">mS/cm</span></div></div>'
    + '<div class="gos-metric"><span class="gos-metric-label">Luft</span>'
    + '<div class="gos-metric-value">21,0<span class="unit">°C</span></div></div>'
    + '<div>Ernte 01.01.2099</div><div>Fuellstand NaN Liter</div><div>-1</div>'
    + '<div>Verbrauch [object Object]</div><div>Reservoir undefined Liter</div>'
    + '<div>Letzte Ernte 01.01.1970</div><div>Restlaufzeit -14 Tage</div>'
    + '<div>Ziel 6,2–5,8</div>'
    + '<section class="ls-panel"><span>27 von 40</span>'
    + '<div class="co-row">a</div><div class="co-row">b</div><div class="co-row">c</div>'
    + '<div class="co-row">d</div><div class="co-row">e</div><div class="co-row">f</div>'
    + '<div class="co-row">g</div><div class="co-row">h</div></section>'
  box.style.cssText = 'position:fixed;top:0;left:0;width:460px;height:700px;overflow:auto;z-index:99999;background:#000;color:#fff'
  document.body.appendChild(box)
}

// ---------------------------------------------------------------- Lauf
const browser = await chromium.launch()
const funde = []
const gesehen = { seiten: 0, ablesungen: 0 }

for (const [breiteName, viewport] of [['Desktop 1440', { width: 1440, height: 900 }], ['Handy 390', { width: 390, height: 844 }]]) {
  const ctx = await browser.newContext({ viewport, locale: 'de-DE', colorScheme: 'dark' })
  const page = await ctx.newPage()
  for (const route of ROUTEN) {
    await page.goto(BASIS + route, { waitUntil: 'networkidle' }).catch(() => {})
    await page.waitForTimeout(1400)
    // Gegenprobe: unmoegliche Werte hineinschreiben und schauen, ob die
    // Pruefung zubeisst. Eine Pruefung, die nie etwas meldet, ist wertlos,
    // solange das niemand gezeigt hat.
    if (gegenprobe) { await page.evaluate(GEGENPROBE); await page.waitForTimeout(150) }
    let ernte
    try { ernte = await page.evaluate(ABLESEN) } catch (e) {
      funde.push({ breite: breiteName, route, regel: 'Seite nicht lesbar', gezeigt: String(e).slice(0, 120) })
      continue
    }
    gesehen.seiten++
    gesehen.ablesungen += ernte.ablesungen.length
    const appErklaert = ernte.vorleserTexte.filter((t) => /Physikalisch nicht möglich|unplausib/i.test(t))

    for (const a of ernte.ablesungen) {
      for (const f of zahlenPruefen(a)) {
        const kommentiert = appErklaert.some((t) => t.includes(String(f.wert)) || t.includes(f.gezeigt))
        funde.push({ breite: breiteName, route, ...f, text: a.roh, etikett: a.etikett, pfad: a.pfad, appKommentiert: kommentiert })
      }
      const p = platzhalterPruefen(a.roh, true)
      if (p) funde.push({ breite: breiteName, route, regel: 'Platzhalter als Wert', gezeigt: p, text: a.roh, etikett: a.etikett, pfad: a.pfad })
      for (const d of datumPruefen(a.roh)) funde.push({ breite: breiteName, route, ...d, text: a.roh, etikett: a.etikett, pfad: a.pfad })
    }
    for (const t of ernte.texte) {
      const p = platzhalterPruefen(t.roh, false)
      if (p) funde.push({ breite: breiteName, route, regel: 'Platzhalter als Wert', gezeigt: p, text: t.roh.slice(0, 90), pfad: t.pfad })
    }
    for (const z of ernte.zaehler) {
      if (z.gezeigt > z.gesamt) funde.push({ breite: breiteName, route, regel: 'Zähler größer als Gesamtzahl', gezeigt: z.roh, erwartet: 'links ≤ rechts', pfad: z.pfad })
      else if (z.zeilen && z.zeilen !== z.gezeigt) funde.push({ breite: breiteName, route, regel: 'Zähler passt nicht zu den angezeigten Zeilen', gezeigt: z.roh + ' → ' + z.zeilen + ' Zeilen (' + z.zeilenKlasse + ')', erwartet: z.gezeigt + ' Zeilen', pfad: z.pfad })
    }
    const proEtikett = new Map()
    for (const k of ernte.kacheln) {
      const w = k.wert.replace(/[^\d,.-]/g, '')
      if (!w) continue
      const schl = k.abschnitt + ' ‖ ' + k.etikett
      if (!proEtikett.has(schl)) proEtikett.set(schl, new Set())
      proEtikett.get(schl).add(w)
    }
    for (const [schl, werte] of proEtikett) {
      if (werte.size > 1) funde.push({ breite: breiteName, route, regel: 'Dieselbe Größe, zwei Werte auf einer Seite', gezeigt: schl.split(' ‖ ')[1] + ': ' + [...werte].join(' vs. '), pfad: schl.split(' ‖ ')[0] })
    }
    for (const b of ernte.baender ?? []) {
      if (b.von > b.bis) funde.push({ breite: breiteName, route, regel: 'Zielfenster verdreht (Untergrenze über Obergrenze)', gezeigt: b.roh, erwartet: 'links ≤ rechts', pfad: b.pfad })
    }
  }
  await ctx.close()
}
await browser.close()

// ---------------------------------------------------------------- Bericht
const schluessel = (f) => [f.route, f.regel, f.gezeigt ?? '', f.text ?? '', f.etikett ?? ''].join('|')
const einmalig = [...new Map(funde.map((f) => [schluessel(f), f])).values()]
console.log('Grenzen gelesen aus MeasurementSanityService.PhysikalischeGrenzen: ' + [...GRENZEN].map(([k, v]) => k + ' ' + v.min + '…' + v.max).join(', '))
console.log('Ausdruecklich ausgenommen: ' + [...AUSNAHMEN.keys()].join(', '))
console.log('Seiten: ' + gesehen.seiten + ' · abgelesene Textknoten mit Ziffern: ' + gesehen.ablesungen + ' · Routen: ' + ROUTEN.length)
console.log('')
const nachRegel = new Map()
for (const f of einmalig) nachRegel.set(f.regel, [...(nachRegel.get(f.regel) ?? []), f])
for (const [regel, liste] of [...nachRegel].sort((a, b) => b[1].length - a[1].length)) {
  console.log('### ' + regel + '  (' + liste.length + ')')
  for (const f of liste) {
    console.log('  ' + f.route.padEnd(34) + ' | ' + String(f.gezeigt ?? '').slice(0, 46).padEnd(46)
      + ' | erwartet ' + String(f.erwartet ?? '—').padEnd(14)
      + ' | ' + (f.groesse ?? '') + (f.herkunft ? ' aus ' + f.herkunft : '') + (f.appKommentiert ? ' [App kommentiert es selbst]' : ''))
    if (f.text && f.text !== f.gezeigt) console.log('      Text: "' + f.text.slice(0, 70) + '"  Beschriftung: ' + (f.etikett ?? '—') + '  ' + (f.pfad ?? ''))
  }
  console.log('')
}
if (!einmalig.length) console.log('KEIN BEFUND. Jetzt die Gegenprobe: node zz-plausibel/pruefe-zahlen.mjs --gegenprobe')
if (jsonZiel) writeFileSync(new URL(jsonZiel, import.meta.url), JSON.stringify({ gesehen, funde: einmalig }, null, 1))
