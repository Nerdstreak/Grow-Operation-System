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
  '/', '/messungen', '/diagnose', '/sops', '/journal', '/sorten',
  '/aufgaben', '/dosierung', '/regeln', '/zelte', '/hydro',
  '/sensoren', '/sollwerte', '/archiv', '/wissen', '/settings',
]

/** Unter dieser Schwelle ist Text nicht mehr lesbar, egal wie er gemeint war. */
const SCHWELLE = 3.0

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
  const zahl = (c) => (c.match(/[\\d.]+/g) || []).map(Number)
  const flaeche = (el) => {
    const schichten = []
    for (let e = el; e; e = e.parentElement) {
      const [r, g, b, a = 1] = zahl(getComputedStyle(e).backgroundColor)
      if (a > 0) schichten.push([r, g, b, a])
      if (a === 1) break
    }
    let [r, g, b] = schichten.pop() || [255, 255, 255]
    while (schichten.length) {
      const [nr, ng, nb, na] = schichten.pop()
      r = nr * na + r * (1 - na); g = ng * na + g * (1 - na); b = nb * na + b * (1 - na)
    }
    return [r, g, b]
  }
  const lum = ([r, g, b]) => {
    const f = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4) }
    return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)
  }
  const funde = []
  // Navigation und Kopfzeile MUESSEN mit hinein: sie standen ausserhalb von
  // <main> und waren dadurch fuer diese Pruefung nie sichtbar — genau dort ist
  // der Fehler dann ein drittes Mal aufgetreten.
  for (const el of document.querySelectorAll('main *, nav *, header *')) {
    if (el.children.length || !el.textContent.trim()) continue
    const s = getComputedStyle(el)
    if (s.visibility === 'hidden' || s.display === 'none' || Number(s.opacity) < 0.3) continue
    const [tr, tg, tb, ta = 1] = zahl(s.color)
    if (ta < 0.3) continue
    const grund = flaeche(el)
    const vorne = [tr * ta + grund[0] * (1 - ta), tg * ta + grund[1] * (1 - ta), tb * ta + grund[2] * (1 - ta)]
    const l1 = lum(vorne), l2 = lum(grund)
    const k = (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05)
    if (k < ${SCHWELLE}) funde.push((el.className || el.tagName) + ' — „' + el.textContent.trim().slice(0, 40) + '" — Kontrast ' + k.toFixed(2))
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
    <span class="badge badge-warn">Kritisch</span>
    <span class="badge badge-neutral">Neutral</span>
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
