import { describe, expect, it } from 'vitest'
import { buildJournalStream, streamTimeLabel } from './journal-stream'
import type { JournalEntryDto, PhotoAssetDto } from '../../types'

/**
 * Der Journal-Strom: was zusammengehört, steht zusammen — und nichts fällt raus.
 *
 * **Der Anlass (02.09.2026).** `journal-stream.ts` stand bei **0 %** Abdeckung.
 * Der Strom ist das Tagebuch des Nutzers; ein Foto, das dort nicht auftaucht,
 * ist für ihn verloren — es liegt zwar auf der Platte, aber es gibt keinen
 * zweiten Weg dorthin.
 *
 * Zwei Klassen kann diese Zusammenführung falsch machen:
 *
 * - **Ein Foto verschwindet**, weil es weder an seinen Eintrag gehängt noch als
 *   eigenes Element aufgenommen wurde.
 * - **Ein Foto erscheint doppelt** — einmal am Eintrag, einmal als eigenes
 *   Element. Dann zählt der Nutzer mehr Fotos, als er gemacht hat.
 */

let laufendeId = 0

function eintrag(teile: Partial<JournalEntryDto> = {}): JournalEntryDto {
  laufendeId += 1
  return {
    id: laufendeId,
    growId: 1,
    entryType: 'Note',
    title: 'Notiz ' + laufendeId,
    body: null,
    occurredAtUtc: '2026-09-01T10:00:00Z',
    measurementId: null,
    ...teile,
  } as JournalEntryDto
}

function foto(teile: Partial<PhotoAssetDto> = {}): PhotoAssetDto {
  laufendeId += 1
  return {
    id: laufendeId,
    growId: 1,
    measurementId: null,
    relativePath: '/uploads/1/bild.jpg',
    caption: null,
    tag: 'Overview',
    takenAtUtc: '2026-09-01T09:00:00Z',
    ...teile,
  } as PhotoAssetDto
}

describe('Der Journal-Strom', () => {
  it('verliert kein einziges Foto', () => {
    const fotos = [
      foto({ measurementId: 7 }),   // haengt an einem Eintrag
      foto({ measurementId: 99 }),  // haengt an einer Messung OHNE Eintrag
      foto({ measurementId: null }), // lose
    ]
    const strom = buildJournalStream([eintrag({ measurementId: 7 })], fotos)

    const gezeigt = strom.flatMap((element) => element.photos).map((p) => p.id).sort()

    // Mengenwaechter: ohne Fotos prueft der Vergleich nichts.
    expect(fotos.length).toBeGreaterThan(2)
    expect(gezeigt,
      'Ein Foto taucht im Strom nicht auf. Fuer den Nutzer ist es damit verloren — es liegt '
      + 'auf der Platte, aber es gibt keinen zweiten Weg dorthin.')
      .toEqual(fotos.map((f) => f.id).sort())
  })

  it('zeigt kein Foto zweimal', () => {
    const gehoertDazu = foto({ measurementId: 7 })
    const strom = buildJournalStream([eintrag({ measurementId: 7 })], [gehoertDazu])

    const wieOft = strom.flatMap((element) => element.photos).filter((p) => p.id === gehoertDazu.id).length

    expect(wieOft,
      'Dasselbe Foto steht zweimal im Strom — einmal am Eintrag, einmal als eigenes Element. '
      + 'Der Nutzer zaehlt dann mehr Fotos, als er gemacht hat.').toBe(1)
  })

  it('hängt Fotos an den Eintrag ihrer Messung', () => {
    const strom = buildJournalStream(
      [eintrag({ measurementId: 7, title: 'Messung vom Montag' })],
      [foto({ measurementId: 7 })])

    const messzeile = strom.find((element) => element.title === 'Messung vom Montag')
    expect(messzeile?.photos.length,
      'Das Foto haengt nicht an seiner Messung, sondern steht als eigene Zeile daneben — '
      + 'der Zusammenhang geht verloren.').toBe(1)
  })

  it('sortiert das Neueste nach oben', () => {
    const strom = buildJournalStream([
      eintrag({ occurredAtUtc: '2026-08-01T10:00:00Z', title: 'alt' }),
      eintrag({ occurredAtUtc: '2026-09-01T10:00:00Z', title: 'neu' }),
    ], [])

    expect(strom.map((e) => e.title)).toEqual(['neu', 'alt'])
  })

  it('gibt einer Messung ohne Eintrag eine eigene Zeile', () => {
    // Fotos aus der Automatik haben eine Messung, aber keinen Journal-Eintrag.
    const strom = buildJournalStream([], [foto({ measurementId: 42 }), foto({ measurementId: 42 })])

    expect(strom.length).toBe(1)
    expect(strom[0].photos.length).toBe(2)
    expect(strom[0].title, 'Bei zwei Fotos sollte die Anzahl dastehen.').toBe('2 Fotos')
  })

  it('kommt mit gar nichts zurecht', () => {
    expect(buildJournalStream([], [])).toEqual([])
  })
})

describe('Die Zeitspalte', () => {
  const jetzt = new Date(2026, 8, 2, 15, 0)

  it('sagt „Heute" für heute', () => {
    expect(streamTimeLabel(new Date(2026, 8, 2, 9, 30).toISOString(), jetzt).day).toBe('Heute')
  })

  it('sagt „Gestern" für gestern', () => {
    expect(streamTimeLabel(new Date(2026, 8, 1, 23, 59).toISOString(), jetzt).day).toBe('Gestern')
  })

  it('zeigt davor das Datum', () => {
    // Und zwar deutsch: Tag.Monat, nicht Monat/Tag.
    expect(streamTimeLabel(new Date(2026, 7, 28, 12, 0).toISOString(), jetzt).day).toBe('28.08.')
  })

  it('sagt auch bei einem Zeitpunkt in der Zukunft „Heute"', () => {
    // Eine zurueckdatierte Eingabe kann in der Zukunft liegen. „In -1 Tagen"
    // waere schlimmer als „Heute".
    expect(streamTimeLabel(new Date(2026, 8, 5, 9, 0).toISOString(), jetzt).day).toBe('Heute')
  })

  it('bricht bei einem unlesbaren Zeitpunkt nicht ab', () => {
    // Sonst reisst eine einzige kaputte Zeile das ganze Journal mit.
    expect(streamTimeLabel('kein Datum', jetzt)).toEqual({ day: '—', clock: '' })
  })
})
