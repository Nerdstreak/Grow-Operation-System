import { test, expect } from '@playwright/test'

// Schrift, die man nicht lesen kann.
//
// Zweimal an einem Tag ist derselbe Fehler durchgekommen: eine Farbe fest im
// Stylesheet verdrahtet, weil sie in der dunklen Ansicht gut aussah — und in
// der hellen stand dann dunkler Text auf dunklem Grund (gemessener Kontrast
// 1,0 bis 1,2, also praktisch unsichtbar). Aufgefallen ist es beide Male nur
// durch Zufall, weil ein Screenshot in der anderen Ansicht entstand.
//
// Deshalb misst dieser Test beide Ansichten. Er prüft NICHT die
// Gestaltungsvorgaben — die Schwelle liegt bewusst tief bei 3,0. Alles
// darunter ist kein Geschmacksfall mehr, sondern unleserlich.
const ROUTEN = [
  '/', '/messung', '/addback', '/aufgaben',
  '/grows', '/diagnose', '/journal', '/sorten', '/aushaerten', '/archiv',
  '/dosierung', '/sensoren', '/regeln', '/sollwerte',
  '/zelte', '/hydro', '/wasser', '/home-assistant', '/handy',
  '/wissen', '/einkaufsliste', '/berater', '/einstellungen',
  // Detailseiten: hier stehen die laufende Phase, die Messkacheln und die
  // Ablauf-Schritte — Bausteine, die es auf keiner Uebersichtsseite gibt. Ohne
  // sie blieb ein ganzer Teil der App fuer diese Pruefung unsichtbar, und drei
  // Funde des Desktop-Audits konnte sie deshalb nicht finden.
  '/grows/1', '/zelte/1', '/hydro/1', '/messungen', '/sops', '/release', '/start',
]

// Die Liste muss etwas hergeben. Ohne diese Zeile laeuft die Schleife
// darunter bei einer leeren Liste null Mal durch — und der Testlauf meldet
// gruen, obwohl er nichts geprueft hat. Genau diese Falle hat in diesem
// Projekt schon zugeschlagen.
if (ROUTEN.length < 20) throw new Error(`Diese Pruefung sieht nur ${ROUTEN.length} Seiten — sie wuerde nichts messen.`)

/**
 * Die Schwelle — seit 2026-08-17 der WCAG-AA-Wert statt der alten 3,0.
 *
 * Die 3,0 waren als Notbremse gedacht: „darunter ist es nicht mehr lesbar".
 * Damit blieb aber ein ganzes Feld unbeachtet, in dem Text zwar erkennbar,
 * aber mühsam ist — und genau dort lagen die Funde eines Durchgangs durch die
 * laufende App: jeder Link in einer Tabelle bei 3,39, die Zelt-Beschriftungen
 * bei 4,26, der Notfall-Knopf bei 3,82. Nach deren Korrektur misst die App auf
 * allen Seiten in beiden Ansichten sauber, also ist der strengere Wert
 * haltbar.
 */
const SCHWELLE = 4.5

/**
 * Der tatsächlich gemalte Kontrast eines Textknotens.
 *
 * Der Knackpunkt sind halbtransparente Flächen: ein Abzeichen mit
 * `rgba(…, 0.10)` über einer Karte ist NICHT diese Farbe, sondern die
 * Mischung. Wer das ignoriert, misst Unsinn — eine erste Fassung dieser
 * Prüfung meldete deshalb lauter Fehlalarme auf Stellen, die in Wahrheit gut
 * lesbar sind.
 */
const MESSUNG = `() => {
  // Farben vom Browser umrechnen lassen, statt sie mit einem Regex zu zerlegen.
  // Grund: die App benutzt inzwischen auch oklch() — aus
  // "oklch(0.55 0.18 27.4 / 0.04)" liest ein Zahlen-Regex 0.55/0.18/27.4 und
  // haelt das fuer RGB. Das Ergebnis ist frei erfunden, in beide Richtungen:
  // eine erste Fassung dieser Pruefung meldete so einen Kontrast von 2,79 an
  // einer Stelle, die in Wahrheit bei 5,9 lag. Ueber eine 1x1-Leinwand malt der
  // Browser jede Farbschreibweise korrekt und mischt die Deckkraft gleich mit.
  const c = document.createElement('canvas'); c.width = c.height = 1
  const ctx = c.getContext('2d', { willReadFrequently: true })
  const alsRgb = (farbe, unter) => {
    ctx.clearRect(0, 0, 1, 1)
    ctx.fillStyle = 'rgb(' + unter[0] + ',' + unter[1] + ',' + unter[2] + ')'
    ctx.fillRect(0, 0, 1, 1)
    ctx.fillStyle = farbe
    ctx.fillRect(0, 0, 1, 1)
    const d = ctx.getImageData(0, 0, 1, 1).data
    return [d[0], d[1], d[2]]
  }
  const zahl = (c) => (c.match(/[\\d.]+/g) || []).map(Number)
  const flaeche = (el) => {
    const schichten = []
    for (let e = el; e; e = e.parentElement) {
      const bg = getComputedStyle(e).backgroundColor
      if (bg && bg !== 'rgba(0, 0, 0, 0)' && bg !== 'transparent') schichten.unshift(bg)
    }
    // Ueberlappende Geschwister, die NICHT Vorfahren sind: der Fuellbalken der
    // laufenden Phase liegt als eigenes Element ueber der Beschriftung, gehoert
    // aber keinem gemeinsamen Ast an — ueber die Elternkette allein war er
    // unsichtbar, und genau dort lagen 3,93:1 im hellen Thema.
    //
    // Nur Elemente, die im Dokument SPAETER kommen (die also darueber malen),
    // und nur solche, die den Textkasten wirklich ueberdecken. Sonst gibt es
    // Fehlalarme bei jedem beliebigen Nachbarn.
    const r = el.getBoundingClientRect()
    if (r.width > 0 && r.height > 0) {
      for (const o of document.querySelectorAll('body *')) {
        if (o === el || o.contains(el) || el.contains(o)) continue
        if (!(el.compareDocumentPosition(o) & Node.DOCUMENT_POSITION_FOLLOWING)) continue
        const os = getComputedStyle(o)
        const bg = os.backgroundColor
        if (!bg || bg === 'rgba(0, 0, 0, 0)' || bg === 'transparent') continue
        if (os.visibility === 'hidden' || os.display === 'none') continue
        const q = o.getBoundingClientRect()
        if (q.left <= r.left && q.right >= r.right && q.top <= r.top && q.bottom >= r.bottom) {
          schichten.push(bg)
        }
      }
    }
    let unten = [255, 255, 255]
    for (const s of schichten) unten = alsRgb(s, unten)
    return unten
  }
  const lum = ([r, g, b]) => {
    const f = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4) }
    return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)
  }
  const funde = []
  // ALLES im Koerper, nicht nur main/nav/header. Die frueheren drei Selektoren
  // waren schon eine Reparatur (die Navigation lag ausserhalb von <main>), aber
  // immer noch eine Aufzaehlung — und was nicht aufgezaehlt ist, wird nicht
  // gemessen. Skripte und Stildateien fallen unten ueber die Sichtbarkeit weg.
  for (const el of document.querySelectorAll('body *')) {
    // Der EIGENE Text des Elements, nicht der seiner Kinder.
    //
    // Vorher wurde jedes Element mit Kindern uebersprungen. Genau so ist der
    // Zaehler im aktiven Menuepunkt durchgerutscht: der Menuepunkt traegt
    // seinen Namen SELBST und daneben ein Kind mit der Zahl — also hat ihn die
    // Pruefung wegen des Kindes gar nicht erst angesehen.
    const eigen = [...el.childNodes]
      .filter((k) => k.nodeType === 3)
      .map((k) => k.textContent)
      .join('')
      .trim()
    if (!eigen) continue
    const s = getComputedStyle(el)
    if (s.visibility === 'hidden' || s.display === 'none' || Number(s.opacity) < 0.3) continue
    const [, , , ta = 1] = zahl(s.color)
    if (ta < 0.3) continue
    const grund = flaeche(el)
    const vorne = alsRgb(s.color, grund)
    const l1 = lum(vorne), l2 = lum(grund)
    const k = (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05)
    // Grosse Schrift darf nach WCAG bei 3,0 bleiben — sie ist auch mit
    // weniger Kontrast noch gut zu lesen.
    const px = parseFloat(s.fontSize)
    const gross = px >= 24 || (px >= 18.66 && Number(s.fontWeight) >= 700)
    if (k < (gross ? 3 : ${SCHWELLE})) funde.push((el.className || el.tagName) + ' — „' + eigen.slice(0, 40) + '" — Kontrast ' + k.toFixed(2))
  }
  return [...new Set(funde)]
}`

/**
 * Bausteine, die eine eigene Tonfarbe tragen.
 *
 * Der Routen-Durchgang unten sieht nur, was ohne Backend rendert — und leere
 * Seiten haben keine Abzeichen. Genau dort saß der Fehler: mit wieder
 * eingebautem Fehler lief die Prüfung grün durch. Deshalb werden die
 * betroffenen Klassen hier zusätzlich in eine echte Seite eingesetzt und in
 * ihrer normalen Umgebung gemessen (Karte innerhalb `.v1-page`).
 *
 * Kommt eine neue tonführende Klasse dazu, gehört sie hier hinein.
 */
const BAUSTEINE = `
  <div class="v1-page"><div class="card"><div class="card-header">
    <span class="card-title">Titel</span><span class="text-muted">Zusatz</span>
  </div><div style="padding:14px 18px">
    <span class="badge badge-ok">Info</span>
    <span class="badge badge-warn">Warnung</span>
    <span class="badge badge-danger">Kritisch</span>
    <span class="badge badge-neutral">Neutral</span>
    <span class="dz-pump-state">bereit</span>
    <span class="dz-pump-state is-blocked">gesperrt</span>
    <div class="tn-group"><div class="tn-group-label">Klima</div>
      <div class="tn-row"><span>Lufttemperatur</span><strong class="is-faint">nicht gemappt</strong></div></div>
    <span class="cu-alter">Abgelesen vor 2 h</span>
    <div class="cu-ampel is-warn"><span class="cu-alter">Abgelesen gerade eben</span>
      <strong class="cu-befund">63,5 % — über dem Fenster</strong><p>Länger lüften.</p>
      <small>Quelle: budtrainer.com</small></div>
    <span class="kb-tag is-accent">Akzent</span>
    <span class="kb-tag is-warn">Warnung</span>
    <span class="kb-tag is-info">Hinweis</span>
    <span class="kb-tag is-muted">Leise</span>
    <span class="kb-tag is-danger">Notfall</span>
    <div class="tl-title">Zeilentitel</div>
    <div class="tl-sub">Zeilenzusatz</div>
    <div class="empty-hint">Nichts vorhanden</div>
    <div class="section-label">Abschnitt</div>
    <a class="btn">Knopf</a>
    <a class="btn btn-primary">Hauptknopf</a>
  </div></div></div>`

for (const schema of ['light', 'dark'] as const) {
  test.describe(`Lesbarkeit in der ${schema === 'light' ? 'hellen' : 'dunklen'} Ansicht`, () => {
    test.use({ colorScheme: schema })

    test('die tonfuehrenden Bausteine sind lesbar', async ({ page }) => {
      await page.goto('/')
      await page.waitForLoadState('networkidle')
      await page.evaluate((markup) => {
        const halter = document.createElement('div')
        halter.innerHTML = markup
        document.querySelector('main')!.appendChild(halter)
      }, BAUSTEINE)

      const funde: string[] = await page.evaluate(`(${MESSUNG})()`)

      expect(funde, `Unlesbare Bausteine (${schema}):\n` + funde.join('\n')).toEqual([])
    })

    for (const route of ROUTEN) {
      test(`${route} hat lesbare Schrift`, async ({ page }) => {
        await page.goto(route)
        await page.waitForLoadState('networkidle')
        await page.waitForTimeout(400)

        // Als Ausdruck aufrufen, nicht nur reichen: `page.evaluate` mit einem
        // String wertet ihn als Ausdruck aus — ohne die Klammern kaeme die
        // Funktion selbst zurueck und nie ihr Ergebnis.
        const funde: string[] = await page.evaluate(`(${MESSUNG})()`)

        expect(funde, `Unlesbare Schrift auf ${route} (${schema}):\n` + funde.join('\n')).toEqual([])
      })
    }
  })
}

/**
 * Dasselbe noch einmal am Handy — denn genau dort ist der Fehler ein drittes
 * Mal durchgekommen.
 *
 * Der Durchgang oben laeuft in Desktop-Breite. Die Hauptnavigation am Handy
 * (`.v1-mobile-nav`) existiert erst unterhalb des Umbruchs und war damit fuer
 * die Pruefung schlicht unsichtbar: ihr Balken trug ein festverdrahtetes
 * Dunkelgruen, waehrend die Beschriftungen dem Thema folgten — in der hellen
 * Ansicht gemessene 1,15, also unlesbar. Auf der Flaeche, die im
 * Home-Assistant-Handy als Erstes erscheint.
 */
for (const schema of ['light', 'dark'] as const) {
  test.describe(`Lesbarkeit am Handy in der ${schema === 'light' ? 'hellen' : 'dunklen'} Ansicht`, () => {
    test.use({ colorScheme: schema, viewport: { width: 390, height: 844 } })

    for (const route of ['/', '/aufgaben', '/messungen']) {
      test(`${route} ist am Handy lesbar`, async ({ page }) => {
        await page.goto(route)
        await page.waitForLoadState('networkidle')
        await page.waitForTimeout(400)

        const funde: string[] = await page.evaluate(`(${MESSUNG})()`)

        expect(funde, `Unlesbare Schrift am Handy auf ${route} (${schema}):\n` + funde.join('\n')).toEqual([])
      })
    }
  })
}
