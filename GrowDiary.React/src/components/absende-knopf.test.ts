import { describe, expect, it } from 'vitest'
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

/**
 * Jedes Formular braucht einen Knopf, der es auch absendet.
 *
 * <b>Der Fehler, der diesen Test erzwungen hat.</b> Auf der Messungen-Seite
 * stand ein Knopf „Messung speichern" in einem `<form onSubmit={…}>` — und tat
 * nichts. Grund: `V1Button` setzt ohne Angabe `type="button"`, und ein
 * button-Typ löst kein Absenden aus. Der Knopf sah richtig aus, war anklickbar,
 * zeigte keinen Fehler, und die Messung war weg.
 *
 * Das fällt weder beim Lesen noch beim Bauen auf — nur beim Klicken. Und weil
 * jedes Formular in dieser App denselben Baustein benutzt, kann es überall
 * wieder passieren.
 *
 * Der Test sucht deshalb im Quelltext: in jedem `<form>` mit `onSubmit` muss
 * mindestens ein Knopf mit `type="submit"` stehen. Knöpfe mit eigenem
 * `onClick` zählen nicht — die tun etwas anderes.
 */
describe('Formulare senden ab', () => {
  const wurzel = fileURLToPath(new URL('..', import.meta.url))

  function alleTsx(ordner: string): string[] {
    const raus: string[] = []
    for (const eintrag of readdirSync(ordner)) {
      const pfad = join(ordner, eintrag)
      if (statSync(pfad).isDirectory()) raus.push(...alleTsx(pfad))
      else if (eintrag.endsWith('.tsx')) raus.push(pfad)
    }
    return raus
  }

  it('hat in jedem onSubmit-Formular einen Knopf mit type="submit"', () => {
    const ohne: string[] = []

    for (const datei of alleTsx(wurzel)) {
      const text = readFileSync(datei, 'utf8')
      for (const treffer of text.matchAll(/<form[^>]*onSubmit[^>]*>([\s\S]*?)<\/form>/g)) {
        const block = treffer[1]

        // Nur im Knopf-Tag selbst suchen, nicht im ganzen Formular. Die erste
        // Fassung prüfte den gesamten Block — und blieb prompt grün, weil der
        // erklärende Kommentar über dem Knopf das Wort `type="submit"` enthält.
        // Ein Test, den ein Kommentar besänftigt, ist keiner.
        const knoepfe = [...block.matchAll(/<(V1Button|button)\b[^>]*?>/g)].map((m) => m[0])
        if (knoepfe.length === 0) continue
        if (knoepfe.some((k) => /type=["']submit["']/.test(k))) continue

        const zeile = text.slice(0, treffer.index).split('\n').length
        ohne.push(`${datei.replace(wurzel, '')}:${zeile} — ${knoepfe.length} Knopf/Knoepfe, keiner sendet ab`)
      }
    }

    expect(ohne, 'Formulare, deren Knoepfe nichts absenden:\n' + ohne.join('\n')).toEqual([])
  })
})
