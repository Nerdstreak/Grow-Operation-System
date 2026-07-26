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
    expect(index).toMatch(/@layer\s+tokens\s*,\s*primitives\s*,\s*features\s*;/)
  })
})
