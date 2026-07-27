import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const SRC = dirname(fileURLToPath(import.meta.url))

function alleDateien(ordner: string, treffer: string[] = []): string[] {
  for (const eintrag of readdirSync(ordner)) {
    const pfad = join(ordner, eintrag)
    if (statSync(pfad).isDirectory()) alleDateien(pfad, treffer)
    else if (/\.tsx?$/.test(eintrag) && !/\.test\.tsx?$/.test(eintrag)) treffer.push(pfad)
  }
  return treffer
}

/**
 * Jede Seite muss von irgendwo aus erreichbar sein.
 *
 * Beim Umbau der Zelte-Seite fiel ein „Öffnen"-Knopf weg. Die Route
 * `/zelte/:tentId` blieb, die Seite blieb, die Tests blieben grün — nur führte
 * kein Weg mehr hin. Damit waren die Lichtzeiten, die Zelt-Historie und die
 * Verwaltung von Setups und Pflanzen für den Nutzer verschwunden, obwohl im
 * Code alles vorhanden war. Genau das findet weder ein Typcheck noch ein
 * Klicktest, der die Adresse selbst kennt.
 */
describe('Erreichbarkeit der Routen', () => {
  const dateien = alleDateien(SRC)
  const quelltext = dateien.map((datei) => readFileSync(datei, 'utf8')).join('\n')
  const appTsx = readFileSync(join(SRC, 'App.tsx'), 'utf8')

  const routen = [...appTsx.matchAll(/<Route\s+path="([^"]+)"/g)]
    .map((treffer) => treffer[1])
    .filter((pfad) => pfad !== '*')

  /** Ziele, die bewusst nur über eine Weiterleitung oder einen Deep-Link gelten. */
  const nurUmleitung = new Set([
    '/live', '/action', '/messungen/new', '/einstellungen', '/zelte/new', '/grows/new', '/hydro/new',
  ])

  it('führt zu jeder Route mindestens ein Link oder Navigationseintrag', () => {
    const navigation = readFileSync(join(SRC, 'navigation.ts'), 'utf8')
    const unerreichbar: string[] = []

    for (const route of routen) {
      if (nurUmleitung.has(route)) continue
      // Aus `/grows/:growId/addback` wird der feste Anfang `/grows/` plus das
      // Endstück `/addback` — beides muss in irgendeinem Link vorkommen.
      const segmente = route.split('/').filter(Boolean)
      const feste = segmente.filter((segment) => !segment.startsWith(':'))
      if (feste.length === 0) continue

      const imNav = navigation.includes(`'/${feste[0]}'`)
      const alleTeileVerlinkt = feste.every((segment) => new RegExp(`[/\`"']${segment}\\b`).test(quelltext))
      // Der letzte feste Teil ist der aussagekräftigste: `/addback`, `/harvest`, `/setup`.
      const letzterTeil = feste[feste.length - 1]
      const zielVerlinkt = new RegExp(`to=[{"\`][^"\`}]*${letzterTeil}`).test(quelltext)

      if (!imNav && !zielVerlinkt && !alleTeileVerlinkt) unerreichbar.push(route)
    }

    expect(unerreichbar, `Diese Seiten sind von nirgendwo erreichbar:\n${unerreichbar.join('\n')}`).toEqual([])
  })

  it('verlinkt die Zelt-Detailseite, auf der Lichtzeiten und Pflanzen wohnen', () => {
    // Der konkrete Fall, der den Test ausgeloest hat — als eigene Zusicherung,
    // weil die allgemeine Regel ihn nur so lange faengt, wie sie scharf bleibt.
    const tentsPage = readFileSync(join(SRC, 'pages', 'TentsPage.tsx'), 'utf8')
    expect(tentsPage, 'Kein Link auf /zelte/:id in der Zelte-Übersicht').toMatch(/to=\{`\/zelte\/\$\{[^}]+\}`\}/)
  })
})
