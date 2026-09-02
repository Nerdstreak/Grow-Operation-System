import { test, expect } from '@playwright/test'
import { readFileSync, readdirSync } from 'node:fs'

/**
 * Auch ein Formular **ohne** `<form>` braucht einen Rundweg oder einen Grund.
 *
 * **Der Anlass (02.09.2026).** `formular-rundweg.spec.ts` bildet seine
 * Grundmenge über `<form onSubmit>` — das steht dort auch so geschrieben, samt
 * dem Hinweis, dass Knöpfe ohne `<form>` nicht darunterfallen und deshalb
 * unten mit Grund stehen müssten.
 *
 * Nur greift diese Zählung je **Datei**. `HardwarePage.tsx` kommt über ihr
 * Geräteformular in die Grundmenge und besteht — während der Pflege-Bereich
 * daneben (`data-audit="care-form"`, ein `<div>` mit Knöpfen) weder einen
 * Rundweg noch einen Grund hatte. Genau dort trägt der Nutzer ein, dass er
 * kalibriert hat; genau dort wurde in beta.63 die Mehrpunkt-Kalibrierung
 * gebaut. Gefunden hat das der Prüfer, nicht die Zählung.
 *
 * **Diese Zählung geht je Block.** Grundmenge sind die
 * `data-audit="…form…"`-Marken im Quelltext — die Kennzeichnung, die dieses
 * Projekt ohnehin an jeden Eingabeblock schreibt. Für jede gibt es entweder
 * einen E2E-Fall, der sie anfasst, oder einen ausgeschriebenen Grund.
 */

const QUELLE = new URL('../src/', import.meta.url)
const E2E = new URL('.', import.meta.url)

/** Marken, die es gibt — aber bewusst ohne Rundweg. */
const OHNE_RUNDWEG: Record<string, string> = {
  'ac-test-form':
    'Schaltet an der ECHTEN Anlage des Nutzers: Stufe, Modus, Zeitplan gehen an einen AC-Infinity-Controller in einem Zelt mit Pflanzen. Ein Rundweg müsste dort etwas stellen. Geprüft wird stattdessen die reine Rechnung dahinter (AcTestSchuetztDieEchteAnlageTests, 9 Fälle) und lesend über GET.',
  'measurement-form-actions':
    'Kein eigener Block, sondern die Knopfleiste von measurement-form — dieselbe Absendung, schon abgedeckt.',
  'journal-photo-form':
    'Lädt eine Bilddatei hoch. Ein Rundweg müsste eine Datei mitbringen und liesse sie im Bestand liegen; die Schreibseite hängt an PhotoStorageService und ist dort geprüft (DerFotospeicherSchreibtNurBilderTests, 16 Fälle). Eigenes Stück, noch nicht gebaut.',
  'sorten-formular':
    'Legt eine Sorte an, die im ganzen Bestand hängt (Grows, Pflanzen, Pheno-Hunt). Ein Rundweg braucht erst einen Weg, sie wieder wegzuräumen, ohne Verweise zu brechen — dieselbe Frage, die beim Wegräumen der Hardware-Ausnahme einen echten Fehler zutage gefördert hat. Eigenes Stück.',
}

/** Alle .tsx unterhalb von src, rekursiv. */
function alleBauteile(ordner = QUELLE, pfad = ''): string[] {
  const raus: string[] = []
  for (const eintrag of readdirSync(ordner, { withFileTypes: true })) {
    if (eintrag.name === 'node_modules') continue
    if (eintrag.isDirectory()) {
      raus.push(...alleBauteile(new URL(eintrag.name + '/', ordner), pfad + eintrag.name + '/'))
    } else if (eintrag.name.endsWith('.tsx')) {
      raus.push(pfad + eintrag.name)
    }
  }
  return raus
}

/** Jede `data-audit`-Marke, deren Name auf ein Formular deutet. */
function formularMarken(): Map<string, string> {
  const raus = new Map<string, string>()
  for (const datei of alleBauteile()) {
    const inhalt = readFileSync(new URL(datei, QUELLE), 'utf8')
    for (const treffer of inhalt.matchAll(/data-audit="([a-z0-9-]*(?:form|formular)[a-z0-9-]*)"/g)) {
      raus.set(treffer[1], datei)
    }
  }
  return raus
}

/** Der gesamte Text der E2E-Mappe — dort steht, was angefasst wird. */
function e2eText(): string {
  return readdirSync(E2E)
    .filter((name) => name.endsWith('.spec.ts'))
    // Diese Datei NICHT mitlesen: sonst belegt sich jede Marke durch ihren
    // eigenen Eintrag in OHNE_RUNDWEG. Dieselbe Falle ist `routes-reachable`
    // schon einmal zugeschnappt.
    .filter((name) => name !== 'formularbloecke-vollstaendig.spec.ts')
    .map((name) => readFileSync(new URL(name, E2E), 'utf8'))
    .join('\n')
}

test.describe('Formularblöcke', () => {
  test('die Grundmenge wird überhaupt gelesen', () => {
    const marken = formularMarken()
    expect(
      marken.size,
      'Es wurde keine einzige data-audit-Formularmarke gefunden — dann prüft die Zählung nichts.',
    ).toBeGreaterThanOrEqual(8)
  })

  test('jede ausgenommene Marke gibt es wirklich', () => {
    const marken = formularMarken()
    for (const name of Object.keys(OHNE_RUNDWEG)) {
      expect([...marken.keys()], `„${name}" steht in OHNE_RUNDWEG, aber nirgends im Quelltext.`)
        .toContain(name)
    }
  })

  test('jeder Formularblock wird angefasst oder hat einen Grund', () => {
    const marken = formularMarken()
    const text = e2eText()
    const fehlend: string[] = []

    for (const [marke, datei] of marken) {
      if (OHNE_RUNDWEG[marke]) continue
      if (text.includes(marke)) continue
      fehlend.push(`${marke}  (${datei})`)
    }

    expect(
      fehlend,
      'Diese Eingabeblöcke fasst kein E2E-Fall an und keiner hat einen Grund:\n  '
      + fehlend.join('\n  ')
      + '\n\nEntweder ein Rundweg, der die Marke benutzt, oder ein ausgeschriebener Eintrag in '
      + 'OHNE_RUNDWEG. Ein Block ohne <form> ist für formular-rundweg.spec.ts unsichtbar — genau '
      + 'so ist der Pflege-Bereich der Hardware-Seite durchgerutscht.',
    ).toEqual([])
  })
})
