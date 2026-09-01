/**
 * Formaudit — was auf dem Schirm falsch geschrieben steht.
 *
 * Sucht nach Formfehlern, die die vorhandenen Prüfungen NICHT sehen:
 * `deutsche-zahlen` findet den englischen Punkt, `kontrast` die Lesbarkeit,
 * `handy-zuschnitt` den Überlauf. Keine davon merkt, wenn „undefined" auf dem
 * Schirm steht, ein ISO-Zeitstempel durchschlägt oder eine Zahl mit sieben
 * Nachkommastellen dasteht.
 *
 * Läuft über alle Seiten aus `e2e/seiten.ts`, in beiden Themen.
 *
 *   node zz-plausibel/formaudit.mjs
 */
import { chromium } from '@playwright/test'
import { readFileSync, writeFileSync } from 'node:fs'

const BASIS = process.env.GROW_OS_URL || 'http://localhost:5076'

/** Die Seitenliste aus derselben Quelle wie die E2E-Mappe. */
function seiten() {
  const quelle = readFileSync(new URL('../e2e/seiten.ts', import.meta.url), 'utf8')
  const ausListe = [...quelle.matchAll(/^\s*'(\/[^']*)',/gm)].map((m) => m[1])
  const navigation = readFileSync(new URL('../src/navigation.ts', import.meta.url), 'utf8')
  const ausMenue = [...navigation.matchAll(/\{\s*to:\s*'([^']+)'/g)].map((m) => m[1])
  return [...new Set([...ausMenue, ...ausListe])]
}

/** Die Suchmuster — je ein belegbarer Formfehler. */
const MUSTER = [
  {
    name: 'Platzhalter aus dem Code',
    // NICHT "Infinity": "AC-Infinity" ist ein Herstellername, und der Bindestrich
    // ist eine Wortgrenze. Der erste Lauf meldete zehn Treffer, alle davon.
    regex: /(?<![-\w])(undefined|NaN|\[object Object\])(?![-\w])/,
    warum: 'Ein Wert, den die Oberflaeche nicht hatte, steht roh auf dem Schirm.',
  },
  {
    name: 'Gerader Bindestrich als Gedankenstrich',
    regex: /\S \- \S/,
    warum: 'Das Projekt schreibt den Gedankenstrich als Halbgeviert, nicht als Bindestrich.',
  },
  {
    name: 'Drei Punkte statt Auslassungszeichen',
    regex: /\w\.\.\./,
    warum: 'Drei Punkte statt eines Auslassungszeichens \u2014 im selben Satz sieht man beide.',
  },
  {
    name: 'ISO-Zeitstempel',
    regex: /\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/,
    warum: 'Ein Zeitstempel aus der Schnittstelle ist ungeformt durchgeschlagen.',
  },
  {
    name: 'ISO-Datum',
    regex: /(?<![\d.])\d{4}-\d{2}-\d{2}(?![\d-])/,
    warum: 'Ein Datum steht englisch da; in Deutschland heisst das 01.09.2026.',
  },
  {
    name: 'Zahl mit zu vielen Nachkommastellen',
    regex: /(?<![\w.])\d+,\d{4,}(?![\d])/,
    warum: 'Eine Rechnung ist ungerundet durchgereicht worden.',
  },
  {
    name: 'Doppeltes Leerzeichen',
    regex: /\S {2,}\S/,
    warum: 'Zwei Textstuecke sind ohne Ruecksicht aneinandergehaengt worden.',
  },
  {
    name: 'Leerzeichen vor Satzzeichen',
    regex: /\s[,.;:!?](\s|$)/,
    warum: 'Ein Satzzeichen steht abgetrennt — meist eine leere Einsetzung davor.',
  },
  {
    name: 'Doppelte Einheit',
    regex: /\b(\d+(?:,\d+)?)\s*(%|°C|mS\/cm|ppm|L|mV)\s+\2\b/,
    warum: 'Die Einheit steht zweimal.',
  },
  {
    name: 'Leere Klammer',
    regex: /\(\s*\)|\[\s*\]/,
    warum: 'Eine Klammer ohne Inhalt — die Einsetzung darin war leer.',
  },
]

/** Text, den ein Muster zu Recht enthaelt — mit Grund. */
/* AUSDRUECKLICH NICHT gesucht: das Paar „…" mit geradem Schlusszeichen.
   Der erste Lauf meldete es als Formfehler. Es steht aber 241-mal in 124
   Dateien und ist damit der durchgehende Haus-Stil dieses Projekts — auch in
   CLAUDE.md und in jedem Kommentar. Typographisch waere " richtig; das
   nachts ueber 241 Stellen zu aendern waere ein grosser Eingriff ohne Not und
   keine Entscheidung, die ich allein treffe. Wer es umstellen will, macht es
   in einem eigenen Durchgang. */
const ERLAUBT = [
  { seite: null, muster: 'ISO-Zeitstempel', text: /sensor\.|switch\.|number\./, warum: 'Entitaets-Kennungen aus Home Assistant.' },
]

async function sammle(seite) {
  return seite.evaluate(() => {
    const raus = []
    const lauf = document.createTreeWalker(document.querySelector('main') ?? document.body, NodeFilter.SHOW_TEXT)
    for (let knoten = lauf.nextNode(); knoten; knoten = lauf.nextNode()) {
      const text = (knoten.textContent || '').replace(/ /g, ' ')
      if (!text.trim()) continue
      const el = knoten.parentElement
      if (!el) continue
      const stil = getComputedStyle(el)
      if (stil.display === 'none' || stil.visibility === 'hidden') continue
      // Kennungen und Quelltext stehen bewusst so da.
      let kennung = false
      for (let n = el; n && n !== document.body; n = n.parentElement) {
        const tag = n.tagName.toLowerCase()
        if (tag === 'code' || tag === 'pre' || tag === 'kbd') { kennung = true; break }
        if (/entity|kennung|mono|url|version/i.test(n.getAttribute('class') || '')) { kennung = true; break }
      }
      if (kennung) continue
      raus.push({ text: text.slice(0, 160), tag: el.tagName.toLowerCase(), klasse: (el.getAttribute('class') || '').slice(0, 40) })
    }
    return raus
  })
}

const browser = await chromium.launch()
const funde = []

for (const thema of ['dark', 'light']) {
  const kontext = await browser.newContext({ baseURL: BASIS, viewport: { width: 1280, height: 900 } })
  const seite = await kontext.newPage()

  for (const pfad of seiten()) {
    try {
      const antwort = await seite.goto(pfad, { waitUntil: 'networkidle', timeout: 20000 })
      if (!antwort || antwort.status() >= 400) continue
      await seite.evaluate((t) => document.documentElement.setAttribute('data-theme', t), thema)
      await seite.waitForTimeout(350)

      const texte = await sammle(seite)
      for (const eintrag of texte) {
        for (const muster of MUSTER) {
          if (!muster.regex.test(eintrag.text)) continue
          if (ERLAUBT.some((a) => a.muster === muster.name && a.text.test(eintrag.text))) continue
          funde.push({ thema, pfad, muster: muster.name, warum: muster.warum, ...eintrag })
        }
      }
    } catch (fehler) {
      funde.push({ thema, pfad, muster: 'Seite nicht ladbar', warum: String(fehler).slice(0, 120), text: '', tag: '', klasse: '' })
    }
  }

  await kontext.close()
}

await browser.close()

const nachMuster = new Map()
for (const f of funde) nachMuster.set(f.muster, (nachMuster.get(f.muster) ?? 0) + 1)

console.log(`\n${funde.length} Funde ueber ${seiten().length} Seiten x 2 Themen\n`)
for (const [muster, anzahl] of [...nachMuster].sort((a, b) => b[1] - a[1])) {
  console.log(`  ${String(anzahl).padStart(4)}  ${muster}`)
}
console.log('')

// Je Muster die ersten Beispiele.
for (const [muster] of nachMuster) {
  const beispiele = funde.filter((f) => f.muster === muster).slice(0, 4)
  console.log(`--- ${muster} ---`)
  for (const b of beispiele) console.log(`  ${b.pfad} (${b.thema}) <${b.tag}> „${b.text.trim().slice(0, 110)}"`)
  console.log('')
}

writeFileSync(new URL('zz-formaudit.json', import.meta.url), JSON.stringify(funde, null, 1), 'utf8')
console.log(`Vollstaendig in zz-plausibel/zz-formaudit.json`)
