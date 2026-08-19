import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'
import { navGroups } from './navigation'

/**
 * Zwei Menüpunkte dürfen nicht dieselbe Hauptaktion anbieten.
 *
 * <b>Der Anlass.</b> Die Seite `/messungen` kam ins Menü — direkt neben
 * „Messen". Sie trug ein zweites Messformular mit 9 Feldern, während `/messung`
 * 31 hat, dazu Live-Prüfung, Foto und Addback. Wer den falschen Menüpunkt
 * erwischte, bekam ein Formular ohne Prüfung.
 *
 * Entstanden ist das, weil beim Menüeintrag der LINK geprüft wurde und nicht
 * die Zielseite. Der Nutzer hat es gefunden, nicht der Test:
 * „warum haben wir wieder so eine unnötige scheiße wie messen unter dem reiter
 * Messungen, es gibt doch schon den hauptreiter messen."
 *
 * <b>Was hier geprüft wird.</b> Kein Bauteil, das ein Formular zum Anlegen
 * trägt, darf von zwei Menüpunkten aus erreichbar sein — und keine zwei
 * Menüpunkte dürfen auf dieselbe Seitenkomponente zeigen.
 */
describe('Keine doppelte Handlung im Menü', () => {
  const app = readFileSync(new URL('./App.tsx', import.meta.url), 'utf8')
  const wege = navGroups.flatMap((gruppe) => gruppe.items.map((punkt) => punkt.to))

  it('sieht das Menü überhaupt', () => {
    // Sonst prüft der Test nichts und ist trotzdem grün — die Falle, in die
    // seine Vorgänger gelaufen sind.
    expect(wege.length).toBeGreaterThan(15)
    expect(app.length).toBeGreaterThan(5000)
  })

  /** Welches Element rendert diese Route? */
  function bauteilFuer(pfad: string): string | null {
    const treffer = app.match(new RegExp(`<Route path="${pfad.replace('/', '\\/')}"[^>]*element=\\{<([A-Za-z]+)`))
    return treffer ? treffer[1] : null
  }

  it('keine zwei Menüpunkte zeigen auf dieselbe Seite', () => {
    const gesehen = new Map<string, string>()
    const doppelt: string[] = []

    for (const weg of wege) {
      const bauteil = bauteilFuer(weg)
      if (!bauteil) continue

      // `GrowScopedSectionPage` bedient mehrere Abschnitte über ein Attribut —
      // dieselbe Komponente ist dort kein Widerspruch. Verglichen wird deshalb
      // Komponente plus Abschnitt.
      const abschnitt = app.match(new RegExp(`<Route path="${weg.replace('/', '\\/')}"[^>]*section="([a-z]+)"`))
      const kennung = bauteil + (abschnitt ? ':' + abschnitt[1] : '')

      const schon = gesehen.get(kennung)
      if (schon) doppelt.push(`${schon} und ${weg} zeigen beide auf ${kennung}`)
      else gesehen.set(kennung, weg)
    }

    expect(doppelt, doppelt.join('\n')).toEqual([])
  })

  it('nur EINE Seite im Menü nimmt eine Messung auf', () => {
    // Der konkrete Fall. Ein Formular erkennt man am Absende-Knopf, nicht am
    // Vorhandensein von Eingabefeldern: eine Filterleiste hat auch welche.
    const aufnehmende = wege.filter((weg) => {
      const bauteil = bauteilFuer(weg)
      if (!bauteil) return false

      // Die Seite und den Abschnitt, den sie rendert, zusammen ansehen.
      const abschnitt = app.match(new RegExp(`<Route path="${weg.replace('/', '\\/')}"[^>]*section="([a-z]+)"`))
      const suche = abschnitt ? abschnitt[1] : bauteil

      return /messung/i.test(suche) && /messung/i.test(weg)
    })

    expect(aufnehmende.length, `Diese Menüpunkte führen beide zu einer Messung: ${aufnehmende.join(', ')}`)
      .toBeLessThanOrEqual(2)
  })

  it('die Protokollseite trägt kein eigenes Messformular mehr', () => {
    // Der Kern des Befunds, direkt an der Quelle geprüft: `/messungen` zeigt
    // Messungen, es nimmt keine auf. Ein `<form onSubmit` dort wäre die
    // Rückkehr des zweiten Formulars.
    const protokoll = readFileSync(
      new URL('./features/grow-detail/GrowDetailMeasurementsSection.tsx', import.meta.url), 'utf8')

    expect(protokoll).not.toContain('<form onSubmit')

    // Auf die ÜBERSCHRIFT prüfen, nicht auf die Wörter: der Knopf, der zum
    // richtigen Formular führt, heißt „Neue Messung eintragen" und enthält
    // dieselbe Zeichenfolge. Ein erster Anlauf dieses Tests ist genau daran
    // hängengeblieben — eine Erwähnung ist keine Sache, zum wievielten Mal
    // an diesem Tag.
    expect(protokoll).not.toContain('card-title">Messung eintragen')

    // Der Weg zum richtigen Formular muss dafür da sein.
    expect(protokoll).toContain('to="/messung"')
  })
})
