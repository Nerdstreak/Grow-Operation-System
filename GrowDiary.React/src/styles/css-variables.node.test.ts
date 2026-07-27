import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

/**
 * Wacht darüber, dass keine benutzte CSS-Variable undefiniert ist.
 *
 * `background: var(--v1-surface)` mit undefiniertem `--v1-surface` ist keine
 * Zeile, die halb wirkt — die ganze Deklaration wird ungültig, still. Genau so
 * sind beim Umbau drei Regeln in rc2-overrides ausgefallen, ohne dass Build,
 * Lint oder der Baseline-Vergleich etwas gemerkt hätten: die Seite blieb ja
 * strukturell heil, nur der Hintergrund fehlte.
 *
 * `var(--x, fallback)` ist ausgenommen — dort ist das Fehlen eingeplant.
 */

const SRC = resolve(dirname(fileURLToPath(import.meta.url)), '..')

/**
 * Die Dateien, die tatsächlich geladen werden — über index.css und über TSX-Importe.
 *
 * Absichtlich nicht "alle .css unter src": genau diese Verwechslung war der Fehler,
 * den der Test absichert. hydro.css lag zwei Phasen lang da, ohne importiert zu sein.
 */
function loadedCssFiles(): string[] {
  const found = new Set<string>()

  const indexCss = join(SRC, 'index.css')
  found.add(indexCss)
  for (const match of readFileSync(indexCss, 'utf8').matchAll(/@import\s+'([^']+)'/g)) {
    found.add(resolve(SRC, match[1]))
  }

  for (const file of readdirSync(SRC, { recursive: true, encoding: 'utf8' })) {
    if (!file.endsWith('.tsx') && !file.endsWith('.ts')) continue
    const full = join(SRC, file)
    for (const match of readFileSync(full, 'utf8').matchAll(/import\s+'(\.[^']+\.css)'/g)) {
      found.add(resolve(dirname(full), match[1]))
    }
  }

  return [...found]
}

describe('CSS-Variablen', () => {
  const files = loadedCssFiles()

  it('lädt überhaupt die erwarteten Stylesheets', () => {
    expect(files.length).toBeGreaterThan(10)
  })

  it('lässt keine Stylesheet-Datei liegen, die niemand importiert', () => {
    // Eine nicht importierte .css-Datei sieht im Editor genauso aus wie eine
    // geladene. hydro.css lag so zwei Phasen lang da, während die Hydro-Seite
    // einspaltig stapelte — Build und Lint hatten dazu nichts zu sagen.
    const loaded = new Set(files.map((file) => resolve(file)))
    const orphans = readdirSync(SRC, { recursive: true, encoding: 'utf8' })
      .filter((file) => file.endsWith('.css'))
      .map((file) => join(SRC, file))
      .filter((file) => !loaded.has(resolve(file)))
      .map((file) => file.slice(SRC.length + 1).replace(/\\/g, '/'))

    expect(orphans, `Nicht importierte Stylesheets:\n${orphans.join('\n')}`).toEqual([])
  })

  it('benutzt keine Variable ohne Definition und ohne Fallback', () => {
    const defined = new Set<string>()
    const used = new Map<string, { file: string; hasFallback: boolean }>()

    for (const file of files) {
      const text = readFileSync(file, 'utf8')
      for (const match of text.matchAll(/(--[a-zA-Z0-9_-]+)\s*:/g)) defined.add(match[1])
      for (const match of text.matchAll(/var\(\s*(--[a-zA-Z0-9_-]+)\s*(,)?/g)) {
        const previous = used.get(match[1])
        // Ein Fallback an *einer* Stelle rettet die andere nicht — deshalb zählt
        // die strengste Verwendung.
        if (!previous || (previous.hasFallback && !match[2])) {
          used.set(match[1], { file: file.slice(SRC.length + 1), hasFallback: Boolean(match[2]) })
        }
      }
    }

    const missing = [...used.entries()]
      .filter(([name, use]) => !defined.has(name) && !use.hasFallback)
      .map(([name, use]) => `${name} (in ${use.file.replace(/\\/g, '/')})`)

    expect(missing, `Undefinierte CSS-Variablen ohne Fallback:\n${missing.join('\n')}`).toEqual([])
  })

  it('setzt die Schichtreihenfolge ausdrücklich, statt sie der Bundle-Reihenfolge zu überlassen', () => {
    const index = readFileSync(join(SRC, 'index.css'), 'utf8')
    expect(index).toMatch(/@layer\s+reset\s*,\s*tokens\s*,\s*primitives\s*,\s*features\s*;/)
  })

  it('hält den Reset in seiner eigenen, verlierenden Schicht', () => {
    // Ungeschichtet schlug `*, *::before, *::after { padding: 0 }` jede Regel in
    // @layer primitives — Schicht vor Spezifität. Damit war jedes padding aus
    // primitives.css und shell.css wirkungslos, und die Seitenleiste klebte am
    // Fensterrand. Ein Reset muss die Kaskade eröffnen, nicht beenden.
    const reset = readFileSync(join(SRC, 'styles', 'reset.css'), 'utf8')
    expect(reset, 'reset.css muss in @layer reset stehen').toMatch(/@layer\s+reset\s*\{/)
  })

  it('lässt kein Stylesheet ungeschichtet, das die Primitive überstimmen würde', () => {
    // Ungeschichtet heisst: gewinnt gegen alle Schichten. Fuer conventions und
    // widgets ist das gewollt, fuer die alten Nummern-Dateien geduldet, bis sie
    // aufgeloest sind. Der Test haelt den Bestand fest — eine NEUE ungeschichtete
    // Datei ist fast immer der Fehler, den reset.css hier vorgemacht hat.
    const erlaubt = new Set([
      'index.css',
      'conventions.css', 'widgets.css', 'primitives-rc2.css',
      // Abbauliste, siehe index.css. 10-grow-wizard-legacy.css ist geschichtet
      // und steht deshalb NICHT mehr hier: seine ungeschichteten
      // `html, body, #root { height: 100% }` haben den App-Rahmen auf eine
      // Bildschirmhoehe gedeckelt und die klebende Seitenleiste ausgehebelt.
      '30-live-home.css', '70-addback-assistant.css',
      '80-grow-wizard-final.css', '90-operations.css',
    ])
    const ungeschichtet = files
      .filter((file) => file.includes('styles') || file.endsWith('index.css'))
      .filter((file) => !readFileSync(file, 'utf8').includes('@layer'))
      .map((file) => file.split(/[\\/]/).pop() ?? file)
      .filter((name) => !erlaubt.has(name))

    expect(ungeschichtet, `Ungeschichtete Stylesheets:\n${ungeschichtet.join('\n')}`).toEqual([])
  })
})
