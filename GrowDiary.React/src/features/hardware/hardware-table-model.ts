import type {
  CalibrationEventDto,
  HardwareDeviceKind,
  HardwareItemDto,
  HardwareItemStatus,
  MaintenanceEventDto,
  TentDto,
} from '../../types'

/**
 * Eine Zeile der Sensor- und Geräteliste.
 *
 * Die Seite hatte vier Tabs — Status, Inventar, Wartung, Mapping — und dieselbe
 * pH-Sonde stand in dreien davon. Wer wissen wollte, ob sie online ist *und* wann
 * sie zuletzt kalibriert wurde, musste zwischen zwei Tabs hin und her klicken und
 * die Namen im Kopf abgleichen. Deshalb: eine Zeile pro Gerät, alles darin.
 */
export type HardwareRow = {
  item: HardwareItemDto
  tentName: string | null
  /** Die nächste fällige Pflege — Wartung oder Kalibrierung, je nachdem was früher kommt. */
  /** `eventId` traegt den offenen Termin — ohne ihn liesse er sich nicht abhaken. */
  nextCare: { eventId: number; kind: 'Wartung' | 'Kalibrierung'; title: string; dueAtUtc: string | null } | null
  /** Tage bis zur Fälligkeit; negativ heißt überfällig. Ohne Termin null. */
  dueInDays: number | null
  overdue: boolean
}

export type HardwareFilter = 'alle' | 'sensoren' | 'geraete' | 'pflege' | 'problem'

const SENSOR_KINDS: HardwareDeviceKind[] = ['FixedSensor', 'HandheldMeter']

function isSensor(item: HardwareItemDto): boolean {
  if (item.deviceKind) return SENSOR_KINDS.includes(item.deviceKind)
  // Ältere Datensätze haben kein deviceKind — dort verrät die HA-Zuordnung den Sensor.
  return Boolean(item.haEntityId || item.metricType)
}

function daysUntil(dueAtUtc: string | null, now: Date): number | null {
  if (!dueAtUtc) return null
  const due = new Date(dueAtUtc).getTime()
  if (Number.isNaN(due)) return null
  return Math.floor((due - now.getTime()) / 86_400_000)
}

/**
 * Baut die Zeilen. Offene Termine sind solche, die noch nicht erledigt sind —
 * `status === 'Planned'`; alles andere ist Vergangenheit und gehört nicht in die
 * Spalte "nächste Pflege".
 */
export function buildHardwareRows(
  hardware: HardwareItemDto[],
  tents: TentDto[],
  maintenance: MaintenanceEventDto[],
  calibration: CalibrationEventDto[],
  now: Date = new Date(),
): HardwareRow[] {
  const tentName = new Map(tents.map((tent) => [tent.id, tent.name]))

  const care = new Map<number, HardwareRow['nextCare']>()
  const consider = (
    hardwareItemId: number,
    eventId: number,
    kind: 'Wartung' | 'Kalibrierung',
    title: string,
    dueAtUtc: string | null,
  ) => {
    const current = care.get(hardwareItemId)
    // Ein Eintrag ohne Datum ist immer noch besser als gar keiner, verliert aber
    // gegen jeden datierten — sonst verdeckt eine undatierte Aufgabe den Termin,
    // der morgen ansteht.
    if (current) {
      if (!dueAtUtc) return
      if (current.dueAtUtc && current.dueAtUtc <= dueAtUtc) return
    }
    care.set(hardwareItemId, { eventId, kind, title, dueAtUtc })
  }

  for (const event of maintenance) {
    if (event.status === 'Planned') consider(event.hardwareItemId, event.id, 'Wartung', event.title, event.dueAtUtc)
  }
  for (const event of calibration) {
    if (event.status === 'Planned') consider(event.hardwareItemId, event.id, 'Kalibrierung', event.title, event.dueAtUtc)
  }

  return hardware
    .map((item) => {
      const nextCare = care.get(item.id) ?? null
      const dueInDays = daysUntil(nextCare?.dueAtUtc ?? null, now)
      return {
        item,
        tentName: item.tentId == null ? null : tentName.get(item.tentId) ?? null,
        nextCare,
        dueInDays,
        overdue: dueInDays != null && dueInDays < 0,
      }
    })
    .sort(compareRows)
}

/** Was Aufmerksamkeit braucht, steht oben: überfällig, dann bald fällig, dann der Rest. */
function compareRows(a: HardwareRow, b: HardwareRow): number {
  const weight = (row: HardwareRow) => {
    if (row.overdue) return 0
    if (row.item.status === 'Offline') return 1
    if (row.item.status === 'MaintenanceDue') return 2
    if (row.dueInDays != null) return 3
    if (row.item.status === 'Retired') return 5
    return 4
  }
  const byWeight = weight(a) - weight(b)
  if (byWeight !== 0) return byWeight
  if (a.dueInDays != null && b.dueInDays != null && a.dueInDays !== b.dueInDays) return a.dueInDays - b.dueInDays
  return a.item.name.localeCompare(b.item.name)
}

export function filterHardwareRows(rows: HardwareRow[], filter: HardwareFilter): HardwareRow[] {
  switch (filter) {
    case 'sensoren': return rows.filter((row) => isSensor(row.item))
    case 'geraete': return rows.filter((row) => !isSensor(row.item))
    case 'pflege': return rows.filter((row) => row.nextCare != null)
    case 'problem': return rows.filter((row) => row.overdue || row.item.status === 'Offline' || row.item.status === 'MaintenanceDue')
    default: return rows
  }
}

export function countBy(rows: HardwareRow[]): Record<HardwareFilter, number> {
  return {
    alle: rows.length,
    sensoren: filterHardwareRows(rows, 'sensoren').length,
    geraete: filterHardwareRows(rows, 'geraete').length,
    pflege: filterHardwareRows(rows, 'pflege').length,
    problem: filterHardwareRows(rows, 'problem').length,
  }
}

/**
 * Wie ein Sensor sich meldet, in Worten statt in Enum-Namen. Vorher stand im
 * Status-Badge wörtlich `MaintenanceDue`.
 */
export function statusLabel(status: HardwareItemStatus): string {
  switch (status) {
    case 'Active': return 'aktiv'
    case 'Offline': return 'offline'
    case 'MaintenanceDue': return 'Wartung fällig'
    case 'Retired': return 'ausgemustert'
    default: return status
  }
}

export function statusTone(status: HardwareItemStatus): 'ok' | 'warn' | 'critical' | 'neutral' {
  switch (status) {
    case 'Active': return 'ok'
    case 'MaintenanceDue': return 'warn'
    case 'Offline': return 'critical'
    default: return 'neutral'
  }
}

/** „in 3 Tagen", „heute", „seit 2 Tagen überfällig" — statt eines nackten Datums. */
export function dueLabel(dueInDays: number | null): string {
  if (dueInDays == null) return 'ohne Termin'
  if (dueInDays < 0) return `seit ${Math.abs(dueInDays)} ${Math.abs(dueInDays) === 1 ? 'Tag' : 'Tagen'} überfällig`
  if (dueInDays === 0) return 'heute'
  if (dueInDays === 1) return 'morgen'
  return `in ${dueInDays} Tagen`
}
