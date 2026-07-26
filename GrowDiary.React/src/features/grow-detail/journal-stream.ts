import type { JournalEntryDto, PhotoAssetDto } from '../../types'

/**
 * Der Journal-Strom: alle Einträge, Messfotos und Ereignisse in EINER
 * zeitlich sortierten Liste, wie im Entwurf.
 *
 * Fotos hängen an Messungen. Referenziert ein Eintrag dieselbe Messung,
 * erscheinen die Fotos bei diesem Eintrag; übrige Fotos werden je Messung
 * zu einem eigenen FOTO-Element gebündelt, statt einzeln zu tropfen.
 */

export type StreamItem = {
  key: string
  at: string
  tag: string
  tone: 'accent' | 'warn' | 'info' | 'muted'
  title: string
  body: string | null
  photos: PhotoAssetDto[]
}

const TYPE_TAGS: Record<string, { tag: string; tone: StreamItem['tone'] }> = {
  Feeding: { tag: 'Addback', tone: 'info' },
  ReservoirChange: { tag: 'Wasserwechsel', tone: 'info' },
  Problem: { tag: 'Ereignis', tone: 'warn' },
  Solution: { tag: 'Massnahme', tone: 'accent' },
  Action: { tag: 'Aktion', tone: 'accent' },
  Training: { tag: 'Training', tone: 'accent' },
  Transplant: { tag: 'Umtopfen', tone: 'accent' },
  GerminationConfirmed: { tag: 'Meilenstein', tone: 'accent' },
  CloneRooted: { tag: 'Meilenstein', tone: 'accent' },
  FlipToFlower: { tag: 'Meilenstein', tone: 'accent' },
  Observation: { tag: 'Notiz', tone: 'muted' },
  Note: { tag: 'Notiz', tone: 'muted' },
}

export function buildJournalStream(entries: JournalEntryDto[], photos: PhotoAssetDto[]): StreamItem[] {
  const photosByMeasurement = new Map<number, PhotoAssetDto[]>()
  const loosePhotos: PhotoAssetDto[] = []
  for (const photo of photos) {
    if (photo.measurementId == null) {
      loosePhotos.push(photo)
      continue
    }
    const list = photosByMeasurement.get(photo.measurementId) ?? []
    list.push(photo)
    photosByMeasurement.set(photo.measurementId, list)
  }

  const claimed = new Set<number>()
  const items: StreamItem[] = entries.map((entry) => {
    const spec = entry.measurementId != null
      ? { tag: 'Messung', tone: 'accent' as const }
      : TYPE_TAGS[entry.entryType] ?? { tag: 'Notiz', tone: 'muted' as const }
    const attached = entry.measurementId != null ? photosByMeasurement.get(entry.measurementId) ?? [] : []
    if (entry.measurementId != null) claimed.add(entry.measurementId)
    return {
      key: `entry-${entry.id}`,
      at: entry.occurredAtUtc,
      tag: spec.tag,
      tone: spec.tone,
      title: entry.title ?? spec.tag,
      body: entry.body,
      photos: attached,
    }
  })

  // Fotos ohne eigenen Eintrag: ein Element je Messung, plus eines für lose.
  for (const [measurementId, list] of photosByMeasurement) {
    if (claimed.has(measurementId)) continue
    items.push(photoItem(`measurement-${measurementId}`, list))
  }
  if (loosePhotos.length > 0) items.push(photoItem('lose', loosePhotos))

  return items.sort((a, b) => b.at.localeCompare(a.at))
}

function photoItem(key: string, list: PhotoAssetDto[]): StreamItem {
  const newest = [...list].sort((a, b) => b.takenAtUtc.localeCompare(a.takenAtUtc))[0]
  return {
    key: `photos-${key}`,
    at: newest.takenAtUtc,
    tag: 'Foto',
    tone: 'accent',
    title: newest.caption ?? (list.length === 1 ? 'Foto' : `${list.length} Fotos`),
    body: null,
    photos: list,
  }
}

/** „HEUTE / 09:30" — Datum und Uhrzeit als zwei Zeilen der Zeitspalte. */
export function streamTimeLabel(iso: string, now = new Date()): { day: string; clock: string } {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return { day: '—', clock: '' }
  const clock = new Intl.DateTimeFormat('de-DE', { hour: '2-digit', minute: '2-digit' }).format(date)
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const diffDays = Math.floor((startOfToday - new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime()) / 86_400_000)
  if (diffDays <= 0) return { day: 'Heute', clock }
  if (diffDays === 1) return { day: 'Gestern', clock }
  return { day: new Intl.DateTimeFormat('de-DE', { day: '2-digit', month: '2-digit' }).format(date), clock }
}
