/**
 * Visualaudit — was auf dem Schirm nicht stimmt.
 *
 * Über alle Seiten aus `e2e/seiten.ts`, in beiden Themen, über drei Breiten.
 * Gemessen wird vier Mal:
 *
 * 1. **Abgeschnittener Text** — nicht der Element-Kasten, sondern der
 *    Textinhalt (`Range.getClientRects()`). Ein `nowrap`-Element bleibt mit
 *    seinem Kasten brav in der Spalte, während die Buchstaben abgeschnitten
 *    werden.
 * 2. **Überlappung** — zwei Elemente mit Text, deren Kästen sich schneiden.
 * 3. **Überlauf über den Seitenrand.**
 * 4. **Bedienelemente unter der Tippgröße.**
 *
 *   node zz-plausibel/visualaudit.mjs
 */
import { chromium } from '@playwright/test'
import { readFileSync, writeFileSync } from 'node:fs'

const BASIS = process.env.GROW_OS_URL || 'http://localhost:5076'
const BREITEN = [360, 768, 1280]
const TIPPGROESSE = 44

function seiten() {
  const quelle = readFileSync(new URL('../e2e/seiten.ts', import.meta.url), 'utf8')
  const ausListe = [...quelle.matchAll(/^\s*'(\/[^']*)',/gm)].map((m) => m[1])
  const navigation = readFileSync(new URL('../src/navigation.ts', import.meta.url), 'utf8')
  const ausMenue = [...navigation.matchAll(/\{\s*to:\s*'([^']+)'/g)].map((m) => m[1])
  return [...new Set([...ausMenue, ...ausListe])]
}

async function messen(seite, breite) {
  return seite.evaluate((tippgroesse) => {
    const raus = []
    const wurzel = document.querySelector('main') ?? document.body

    /* Sichtbar heisst: ein Mensch sieht es.
       Nicht nur display/visibility — auch die uebliche Konstruktion fuer
       Vorleseprogramme zaehlt nicht (position:absolute, 1px hoch, overflow
       hidden, clip-path: inset(50%)). Deren Kinder melden volle Kaesten, und
       der erste Lauf hielt sie fuer 28 Ueberlappungen und 198 abgeschnittene
       Texte. Am laufenden Stand nachgesehen: der Kartenaufbau ist sauber. */
    const sichtbar = (el) => {
      for (let n = el; n && n !== document.body; n = n.parentElement) {
        const s = getComputedStyle(n)
        if (s.display === 'none' || s.visibility === 'hidden' || Number(s.opacity) === 0) return false
        if (s.clipPath !== 'none' && s.clipPath !== '') return false
        if (n !== el) {
          const kn = n.getBoundingClientRect()
          // Ein Vorfahr von 1px Hoehe mit overflow:hidden zeigt nichts.
          if (kn.height <= 2 && s.overflow !== 'visible') return false
        }
      }
      const k = el.getBoundingClientRect()
      return k.width > 0 && k.height > 0
    }

    const kurz = (el) => {
      const t = (el.textContent || '').trim().replace(/\s+/g, ' ')
      return t.slice(0, 70)
    }
    const wo = (el) => `${el.tagName.toLowerCase()}${el.className && typeof el.className === 'string' ? '.' + el.className.split(/\s+/).slice(0, 2).join('.') : ''}`

    // ---------- 1. Abgeschnittener Text ----------
    const lauf = document.createTreeWalker(wurzel, NodeFilter.SHOW_TEXT)
    for (let knoten = lauf.nextNode(); knoten; knoten = lauf.nextNode()) {
      const text = (knoten.textContent || '').trim()
      if (text.length < 2) continue
      const el = knoten.parentElement
      if (!el || !sichtbar(el)) continue

      const bereich = document.createRange()
      bereich.selectNodeContents(knoten)
      const kaesten = [...bereich.getClientRects()]
      if (kaesten.length === 0) continue

      const kasten = el.getBoundingClientRect()
      const stil = getComputedStyle(el)
      // Nur wo nichts wegrollen kann: ein eigener Rollbereich ist Absicht.
      if (stil.overflowX === 'auto' || stil.overflowX === 'scroll') continue
      /* Text nur fuer Vorleseprogramme ist ABSICHTLICH geklippt (1px-Kasten
         mit clip-path). Der erste Lauf meldete 198 solche Stellen als
         "abgeschnitten" — alle davon. */
      if (el.closest('.sr-only') || stil.clipPath !== 'none' || stil.clip !== 'auto') continue
      // Und was in einem Rollbereich liegt, ist nicht abgeschnitten.
      let rollbar = false
      for (let n = el.parentElement; n && n !== document.body; n = n.parentElement) {
        const s2 = getComputedStyle(n)
        if (s2.overflowX === 'auto' || s2.overflowX === 'scroll') { rollbar = true; break }
      }
      if (rollbar) continue

      const rechts = Math.max(...kaesten.map((k) => k.right))
      const links = Math.min(...kaesten.map((k) => k.left))
      const ueber = Math.max(rechts - kasten.right, kasten.left - links)
      if (ueber > 1.5) {
        raus.push({ art: 'Text abgeschnitten', px: Math.round(ueber), wo: wo(el), text: text.slice(0, 70) })
      }
    }

    // ---------- 2. Ueberlauf ueber den Seitenrand ----------
    const seitenbreite = document.documentElement.clientWidth
    for (const el of wurzel.querySelectorAll('*')) {
      if (!sichtbar(el)) continue
      const k = el.getBoundingClientRect()
      if (k.right <= seitenbreite + 1) continue
      /* Wer in einem eigenen Rollbereich liegt, ragt nicht ueber die SEITE —
         er rollt in seinem Kasten. Der erste Lauf meldete 580 Stellen, fast
         alle aus den Tabellen mit `overflow-x: auto`. Was wirklich ueber den
         Seitenrand geht, findet handy-zuschnitt.spec.ts ohnehin. */
      let rollbar = false
      for (let n = el.parentElement; n && n !== document.body; n = n.parentElement) {
        const s2 = getComputedStyle(n)
        if (s2.overflowX === 'auto' || s2.overflowX === 'scroll') { rollbar = true; break }
      }
      if (rollbar) continue
      raus.push({ art: 'ragt ueber den Seitenrand', px: Math.round(k.right - seitenbreite), wo: wo(el), text: kurz(el) })
    }

    /* Die Tippgroesse misst e2e/touch-targets.spec.ts — und zwar besser:
       ueber die TREFFERFLAECHE (elementFromPoint) statt ueber die Kastenhoehe,
       mit Handy-Nachbildung und mit den dokumentierten Ausnahmen. Eine zweite,
       gruebere Fassung daneben meldete 2924 Stellen, die dort zu Recht
       durchgehen. */

    // ---------- 4. Ueberlappung ----------
    const kandidaten = [...wurzel.querySelectorAll('h1,h2,h3,h4,p,span,strong,label,button,td,th')]
      .filter((el) => sichtbar(el) && (el.textContent || '').trim().length > 1)
      .filter((el) => ![...el.children].some((k) => (k.textContent || '').trim().length > 1))
      .slice(0, 400)

    /* Verglichen werden ZEILENKAESTEN, nicht Umrisse.
       Ein umbrechendes `span` im Fliesstext hat einen Umriss ueber beide
       Zeilen — der ueberdeckt dann jeden Nachbarn in derselben Zeile. Auf
       /sops meldete der erste Lauf so vier Ueberlappungen zwischen zwei
       Angaben, die in Wahrheit brav nebeneinanderstehen. */
    const zeilen = (el) => [...el.getClientRects()].filter((k) => k.width > 0 && k.height > 0)

    for (let i = 0; i < kandidaten.length; i += 1) {
      const aZeilen = zeilen(kandidaten[i])
      for (let j = i + 1; j < kandidaten.length; j += 1) {
        if (kandidaten[i].contains(kandidaten[j]) || kandidaten[j].contains(kandidaten[i])) continue
        const bZeilen = zeilen(kandidaten[j])
        let breit = 0
        let hoch = 0
        for (const a of aZeilen) {
          for (const b of bZeilen) {
            const w = Math.min(a.right, b.right) - Math.max(a.left, b.left)
            const h = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top)
            if (w > 3 && h > 3 && Math.min(w, h) > Math.min(breit, hoch)) { breit = w; hoch = h }
          }
        }
        if (breit > 3 && hoch > 3) {
          raus.push({
            art: 'Ueberlappung',
            px: Math.round(Math.min(breit, hoch)),
            wo: `${wo(kandidaten[i])} / ${wo(kandidaten[j])}`,
            text: `${kurz(kandidaten[i])} || ${kurz(kandidaten[j])}`,
          })
        }
      }
    }

    return raus
  }, TIPPGROESSE)
}

const browser = await chromium.launch()
const funde = []
const liste = seiten()

for (const thema of ['dark', 'light']) {
  for (const breite of BREITEN) {
    const kontext = await browser.newContext({ baseURL: BASIS, viewport: { width: breite, height: 900 } })
    const seite = await kontext.newPage()

    for (const pfad of liste) {
      try {
        const antwort = await seite.goto(pfad, { waitUntil: 'networkidle', timeout: 20000 })
        if (!antwort || antwort.status() >= 400) continue
        await seite.evaluate((t) => document.documentElement.setAttribute('data-theme', t), thema)
        await seite.waitForTimeout(300)
        for (const f of await messen(seite, breite)) funde.push({ thema, breite, pfad, ...f })
      } catch {
        // Eine Seite, die nicht laedt, meldet die E2E-Mappe.
      }
    }

    await kontext.close()
  }
}

await browser.close()

const nachArt = new Map()
for (const f of funde) nachArt.set(f.art, (nachArt.get(f.art) ?? 0) + 1)

console.log(`\n${funde.length} Funde ueber ${liste.length} Seiten x 2 Themen x ${BREITEN.length} Breiten\n`)
for (const [art, anzahl] of [...nachArt].sort((a, b) => b[1] - a[1])) {
  console.log(`  ${String(anzahl).padStart(5)}  ${art}`)
}

for (const [art] of nachArt) {
  const alle = funde.filter((f) => f.art === art).sort((a, b) => b.px - a.px)
  console.log(`\n--- ${art} (${alle.length}), die schlimmsten ---`)
  for (const b of alle.slice(0, 8)) {
    console.log(`  ${String(b.px).padStart(4)}px  ${b.pfad} @${b.breite} ${b.thema}  ${b.wo}  „${b.text}"`)
  }
}

writeFileSync(new URL('zz-visualaudit.json', import.meta.url), JSON.stringify(funde, null, 1), 'utf8')
console.log(`\nVollstaendig in zz-plausibel/zz-visualaudit.json`)
