import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { stichwort, UEBERSETZTE_STICHWOERTER } from './stichwoerter'

/**
 * Kein Entwickler-Bezeichner in der Oberfläche.
 *
 * Die Erreger- und Maßnahmen-Dateien verweisen auf Symptom-Schlüssel, zu denen
 * es keinen eigenen Eintrag gibt. Die Wissensseite zeigte sie roh — bei
 * „Bakterielle Fäule" stand `slimy-roots-foul-smell` mitten im deutschen Text,
 * an 69 Stellen.
 *
 * Dieser Test liest die ausgelieferte Wissensbasis und prüft, dass für jeden
 * solchen Schlüssel eine Übersetzung vorliegt. Kommt ein Erreger oder eine
 * Maßnahme dazu, die ein neues Stichwort nennt, fällt er — und zwar bevor der
 * Schlüssel beim Nutzer landet.
 */
describe('Stichwörter der Wissensbasis', () => {
  const basis = fileURLToPath(new URL('../../../../GrowDiary.Web/wwwroot/knowledge-defaults', import.meta.url))

  function lade(ordner: string): Array<Record<string, unknown>> {
    try {
      return readdirSync(join(basis, ordner))
        .filter((datei) => datei.endsWith('.json'))
        .map((datei) => JSON.parse(readFileSync(join(basis, ordner, datei), 'utf8')))
    } catch {
      return []
    }
  }

  const ordner = ['symptoms', 'treatments', 'sops', 'pathogens', 'setpoints', 'nutrient-programs', 'wear']
  const alle = ordner.flatMap(lade)
  const vorhandeneIds = new Set(alle.map((eintrag) => String(eintrag.id ?? '')))

  /** Jeder Verweis, der in der Wissensbasis ins Leere zeigt. */
  const ohneEintrag = [...new Set(alle.flatMap((eintrag) =>
    ['symptoms', 'targetSymptoms', 'suggestedTreatmentIds', 'suggestedSopIds', 'preventiveSopId', 'treatmentSopId']
      .flatMap((feld) => {
        const wert = eintrag[feld]
        const liste = typeof wert === 'string' ? [wert] : Array.isArray(wert) ? wert : []
        return liste.filter((x): x is string => typeof x === 'string' && x !== '' && !vorhandeneIds.has(x))
      })))]

  it('findet die Wissensbasis überhaupt', () => {
    // Ohne diese Prüfung wäre der Test unten trivial grün, sobald der Pfad
    // nicht mehr stimmt — und das ist genau die Sorte Test, die nichts hält.
    expect(alle.length, 'keine Wissensdateien gefunden — stimmt der Pfad noch?').toBeGreaterThan(50)
  })

  it('hat für jedes verwaiste Stichwort einen deutschen Begriff', () => {
    const fehlend = ohneEintrag.filter((id) => !UEBERSETZTE_STICHWOERTER.includes(id))

    expect(fehlend, 'Diese Stichwörter würden roh in der Oberfläche stehen:\n' + fehlend.join('\n'))
      .toEqual([])
  })

  it('macht auch einen unbekannten Schlüssel lesbar', () => {
    // Der Rückfall darf nie einen Bindestrich-Bezeichner durchreichen.
    expect(stichwort('irgendwas-ganz-neues')).toBe('Irgendwas ganz neues')
    expect(stichwort('slimy-roots-foul-smell')).toBe('Schleimige Wurzeln, fauliger Geruch')
  })
})
