import { describe, expect, it } from 'vitest'
import {
  buildHardwareRows,
  countBy,
  dueLabel,
  filterHardwareRows,
  statusLabel,
} from './hardware-table-model'
import type { CalibrationEventDto, HardwareItemDto, MaintenanceEventDto, TentDto } from '../../types'

const NOW = new Date('2026-07-26T12:00:00Z')

function item(over: Partial<HardwareItemDto> & { id: number; name: string }): HardwareItemDto {
  return {
    category: 'Sensor', status: 'Active', criticality: 'Medium',
    tentId: null, setupId: null, hydroSetupId: null, growId: null,
    wearTemplateId: null, tentSensorId: null, haEntityId: null,
    manufacturer: null, model: null, serialNumber: null,
    installedAtUtc: null, retiredAtUtc: null, expectedLifespanDays: null,
    inspectionIntervalDays: null, calibrationIntervalDays: null, notes: null,
    createdAtUtc: NOW.toISOString(), updatedAtUtc: NOW.toISOString(),
    ...over,
  }
}

function maintenance(over: Partial<MaintenanceEventDto> & { id: number; hardwareItemId: number }): MaintenanceEventDto {
  return {
    eventType: 'Inspection', status: 'Planned', result: 'Pending', title: 'Wartung',
    description: null, dueAtUtc: null, performedAtUtc: null, nextDueAtUtc: null,
    growTaskId: null, sopInstanceId: null, notes: null,
    createdAtUtc: NOW.toISOString(), updatedAtUtc: NOW.toISOString(),
    ...over,
  } as MaintenanceEventDto
}

function calibration(over: Partial<CalibrationEventDto> & { id: number; hardwareItemId: number }): CalibrationEventDto {
  return {
    calibrationType: 'Ph', status: 'Planned', result: 'Pending', title: 'Kalibrierung',
    referenceSolution: null, referenceValue: null, beforeValue: null, afterValue: null,
    temperatureC: null, dueAtUtc: null, performedAtUtc: null, nextDueAtUtc: null,
    growTaskId: null, notes: null,
    createdAtUtc: NOW.toISOString(), updatedAtUtc: NOW.toISOString(),
    ...over,
  } as CalibrationEventDto
}

const tents: TentDto[] = [{ id: 1, name: 'Hauptzelt' } as TentDto]

describe('buildHardwareRows', () => {
  it('hängt den Zeltnamen an, statt nur die Id zu führen', () => {
    const rows = buildHardwareRows([item({ id: 1, name: 'pH', tentId: 1 })], tents, [], [], NOW)
    expect(rows[0].tentName).toBe('Hauptzelt')
  })

  it('lässt den Zeltnamen leer, wenn das Zelt nicht mehr existiert', () => {
    const rows = buildHardwareRows([item({ id: 1, name: 'pH', tentId: 99 })], tents, [], [], NOW)
    expect(rows[0].tentName).toBeNull()
  })

  it('nimmt von zwei offenen Terminen den früheren', () => {
    const rows = buildHardwareRows(
      [item({ id: 1, name: 'pH' })],
      tents,
      [maintenance({ id: 1, hardwareItemId: 1, title: 'Membran', dueAtUtc: '2026-08-10T00:00:00Z' })],
      [calibration({ id: 1, hardwareItemId: 1, title: 'Zweipunkt', dueAtUtc: '2026-07-30T00:00:00Z' })],
      NOW,
    )
    expect(rows[0].nextCare).toMatchObject({ kind: 'Kalibrierung', title: 'Zweipunkt' })
    expect(rows[0].dueInDays).toBe(3)
  })

  it('lässt einen datierten Termin gegen einen undatierten gewinnen', () => {
    // Sonst verdeckt eine Aufgabe ohne Datum den Termin, der übermorgen ansteht.
    const rows = buildHardwareRows(
      [item({ id: 1, name: 'pH' })],
      tents,
      [maintenance({ id: 1, hardwareItemId: 1, title: 'irgendwann', dueAtUtc: null })],
      [calibration({ id: 1, hardwareItemId: 1, title: 'Zweipunkt', dueAtUtc: '2026-07-28T00:00:00Z' })],
      NOW,
    )
    expect(rows[0].nextCare?.title).toBe('Zweipunkt')
  })

  it('ignoriert erledigte Termine', () => {
    const rows = buildHardwareRows(
      [item({ id: 1, name: 'pH' })],
      tents,
      [maintenance({ id: 1, hardwareItemId: 1, status: 'Completed', dueAtUtc: '2026-07-27T00:00:00Z' })],
      [],
      NOW,
    )
    expect(rows[0].nextCare).toBeNull()
  })

  it('erkennt Überfälligkeit', () => {
    const rows = buildHardwareRows(
      [item({ id: 1, name: 'pH' })],
      tents,
      [maintenance({ id: 1, hardwareItemId: 1, dueAtUtc: '2026-07-24T12:00:00Z' })],
      [],
      NOW,
    )
    expect(rows[0].overdue).toBe(true)
    expect(rows[0].dueInDays).toBe(-2)
  })

  it('stellt nach oben, was Aufmerksamkeit braucht', () => {
    const rows = buildHardwareRows(
      [
        item({ id: 1, name: 'Ruhig' }),
        item({ id: 2, name: 'Ausgemustert', status: 'Retired' }),
        item({ id: 3, name: 'Offline', status: 'Offline' }),
        item({ id: 4, name: 'Ueberfaellig' }),
      ],
      tents,
      [maintenance({ id: 1, hardwareItemId: 4, dueAtUtc: '2026-07-20T00:00:00Z' })],
      [],
      NOW,
    )
    expect(rows.map((row) => row.item.name)).toEqual(['Ueberfaellig', 'Offline', 'Ruhig', 'Ausgemustert'])
  })
})

describe('filterHardwareRows', () => {
  const rows = buildHardwareRows(
    [
      item({ id: 1, name: 'pH-Sonde', deviceKind: 'FixedSensor', haEntityId: 'sensor.ph' }),
      item({ id: 2, name: 'Handmessgeraet', deviceKind: 'HandheldMeter' }),
      item({ id: 3, name: 'Umwaelzpumpe', deviceKind: 'Equipment', category: 'Pumpe' }),
      item({ id: 4, name: 'Alter Sensor ohne Art', haEntityId: 'sensor.alt' }),
    ],
    tents,
    [maintenance({ id: 1, hardwareItemId: 3, dueAtUtc: '2026-07-30T00:00:00Z' })],
    [],
    NOW,
  )

  it('zählt Sensoren inklusive der Altdatensätze ohne deviceKind', () => {
    expect(filterHardwareRows(rows, 'sensoren').map((r) => r.item.id).sort()).toEqual([1, 2, 4])
  })

  it('trennt Geräte davon ab', () => {
    expect(filterHardwareRows(rows, 'geraete').map((r) => r.item.id)).toEqual([3])
  })

  it('zeigt unter Pflege nur, was einen offenen Termin hat', () => {
    expect(filterHardwareRows(rows, 'pflege').map((r) => r.item.id)).toEqual([3])
  })

  it('liefert Zähler für jeden Filter', () => {
    expect(countBy(rows)).toMatchObject({ alle: 4, sensoren: 3, geraete: 1, pflege: 1 })
  })
})

describe('Beschriftungen', () => {
  it('schreibt den Status aus, statt den Enum-Namen zu zeigen', () => {
    expect(statusLabel('MaintenanceDue')).toBe('Wartung fällig')
    expect(statusLabel('Active')).toBe('aktiv')
  })

  it('sagt die Frist in Worten', () => {
    expect(dueLabel(0)).toBe('heute')
    expect(dueLabel(1)).toBe('morgen')
    expect(dueLabel(5)).toBe('in 5 Tagen')
    expect(dueLabel(-1)).toBe('seit 1 Tag überfällig')
    expect(dueLabel(-3)).toBe('seit 3 Tagen überfällig')
    expect(dueLabel(null)).toBe('ohne Termin')
  })
})
