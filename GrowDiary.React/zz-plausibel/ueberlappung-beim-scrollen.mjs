/**
 * Was sich beim SCROLLEN übereinanderschiebt.
 *
 * <b>Warum es diese Prüfung braucht.</b> Alle bisherigen Messungen — Überlauf,
 * Kontrast, Textkasten, Tippziele — laufen bei Scrollstand 0. Ein klebendes
 * Element liegt dort sauber an seinem Platz und wandert erst über seine
 * Nachbarn, wenn man scrollt. Genau so ist die Kontext-Karte auf /messung
 * wochenlang unbemerkt über 22 Eingabefelder gerutscht: jede Prüfung sagte
 * „sauber", weil keine je gescrollt hat.
 *
 * <b>Und die zweite Lehre.</b> Die Regel dagegen stand seit Wochen im
 * Stylesheet und tat nichts — sie verlor gegen eine gleich starke Regel aus
 * einer später geladenen Datei. Die damalige Gegenprobe hat die REGEL geprüft
 * (per addStyleTag eingespielt, hängt am Dokumentende, gewinnt immer) und nicht
 * die DATEI. Diese Prüfung misst das gebaute Ergebnis im Browser — sie kann
 * diesen Unterschied gar nicht übersehen.
 */
import { chromium } from '@playwright/test'

const BASIS = process.env.GROW_OS ?? 'http://127.0.0.1:5076'
const BREITEN = [375, 1280, 1640]
const ROUTEN = [
  '/', '/aufgaben', '/addback', '/grows', '/grows/1', '/messung', '/messungen',
  '/diagnose', '/journal', '/sops', '/zelte', '/zelte/1', '/hydro', '/hydro/1',
  '/wissen', '/einkaufsliste', '/aushaerten', '/sensoren', '/sollwerte',
  '/wasser', '/dosierung', '/regeln', '/sorten', '/archiv', '/settings',
  '/start', '/handy', '/home-assistant', '/release',
]

/**
 * Zwei Kästen, die sich überdecken, obwohl keiner den anderen enthält.
 *
 * Nur wirklich sichtbare Überdeckung zählt: ein paar Pixel Anschnitt entstehen
 * durch Rundungen und negative Ränder (die Haarlinien-Technik der Kacheln
 * arbeitet absichtlich mit −1 px). Ab 24 px in beiden Richtungen liegt etwas
 * auf etwas.
 */
const PRUEFUNG = `(() => {
  const funde = []
  const kaesten = [...document.querySelectorAll('main *')].filter((el) => {
    const s = getComputedStyle(el)
    if (s.display === 'none' || s.visibility === 'hidden' || Number(s.opacity) === 0) return false
    // Was gar keine eigene Fläche zeichnet, kann auch nichts verdecken.
    if (s.backgroundColor === 'rgba(0, 0, 0, 0)' && s.borderTopWidth === '0px' && s.boxShadow === 'none') return false
    const r = el.getBoundingClientRect()
    return r.width > 160 && r.height > 40
  })

  const name = (el) => el.tagName.toLowerCase() + (typeof el.className === 'string' && el.className
    ? '.' + el.className.trim().split(/\\s+/).slice(0, 2).join('.') : '')

  for (let i = 0; i < kaesten.length; i++) {
    for (let j = i + 1; j < kaesten.length; j++) {
      const a = kaesten[i], b = kaesten[j]
      if (a.contains(b) || b.contains(a)) continue
      const ra = a.getBoundingClientRect(), rb = b.getBoundingClientRect()
      const hoch = Math.min(ra.bottom, rb.bottom) - Math.max(ra.top, rb.top)
      const quer = Math.min(ra.right, rb.right) - Math.max(ra.left, rb.left)
      if (hoch > 24 && quer > 24) {
        funde.push({ a: name(a), b: name(b), hoch: Math.round(hoch), quer: Math.round(quer) })
      }
    }
  }
  return [...new Map(funde.map((f) => [f.a + '|' + f.b, f])).values()]
})()`

const b = await chromium.launch()
let gesamt = 0

for (const breite of BREITEN) {
  const ctx = await b.newContext({ viewport: { width: breite, height: 900 }, locale: 'de-DE', isMobile: breite < 768, hasTouch: breite < 768 })
  const page = await ctx.newPage()
  const treffer = []

  for (const route of ROUTEN) {
    await page.goto(BASIS + route, { waitUntil: 'networkidle' }).catch(() => {})
    await page.waitForTimeout(400)

    const hoehe = await page.evaluate(() => document.documentElement.scrollHeight)
    // Fünf Haltepunkte über die Seite. Ein klebendes Element zeigt sich erst
    // in der Mitte — oben sitzt es an seinem Platz, unten ist es wieder am Rand
    // seines Elternkastens angekommen.
    const stellen = [0, 0.25, 0.5, 0.75, 1].map((t) => Math.round(t * Math.max(0, hoehe - 900)))

    for (const y of stellen) {
      await page.evaluate((pos) => window.scrollTo(0, pos), y)
      await page.waitForTimeout(140)
      const funde = await page.evaluate(PRUEFUNG).catch(() => [])
      for (const f of funde) {
        treffer.push(`${route} @${y}px  ${f.a} × ${f.b}  (${f.quer}×${f.hoch} px)`)
      }
    }
  }

  const einmalig = [...new Set(treffer)]
  gesamt += einmalig.length
  console.log(String(breite).padStart(5) + ' px  ' + (einmalig.length ? einmalig.length + ' Ueberdeckungen' : 'sauber'))
  for (const t of einmalig.slice(0, 10)) console.log('        ' + t)
  await ctx.close()
}

console.log('\nUeberdeckungen beim Scrollen: ' + gesamt)
await b.close()
