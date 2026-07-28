import qrcode from 'qrcode-generator'

/**
 * QR-Code als SVG-Pfad.
 *
 * Warum eine Bibliothek und nicht selbst gerechnet: ein QR-Code ist
 * Reed-Solomon-Fehlerkorrektur plus acht Maskierungsvarianten, und ein Fehler
 * darin fällt hier niemandem auf — das Bild sieht immer aus wie ein QR-Code.
 * Auffallen würde es erst dem Nutzer, dessen Handy nichts erkennt.
 * `qrcode-generator` ist winzig, hat keine eigenen Abhängigkeiten und ist seit
 * Jahren im Einsatz. Gezeichnet wird trotzdem selbst, damit der Code die Farben
 * der App erbt statt ein starres schwarzes PNG zu sein.
 */

/** Fehlerkorrektur M: verträgt ~15 % Verlust. Genug für einen Bildschirm, ohne den Code unnötig dicht zu machen. */
const ERROR_CORRECTION = 'M'

/** Ruhezone in Modulen. Vier ist das Minimum der Spezifikation — weniger, und Scanner finden die Kanten nicht. */
export const QUIET_ZONE = 4

export type QrMatrix = {
  /** Kantenlänge in Modulen, ohne Ruhezone. */
  size: number
  /** true = dunkles Modul. */
  isDark: (row: number, column: number) => boolean
}

export function buildMatrix(text: string): QrMatrix {
  // typeNumber 0 heisst: die kleinste Version waehlen, in die der Text passt.
  const qr = qrcode(0, ERROR_CORRECTION)
  qr.addData(text)
  qr.make()

  return { size: qr.getModuleCount(), isDark: (row, column) => qr.isDark(row, column) }
}

/**
 * Die dunklen Module als ein einziger SVG-Pfad.
 *
 * Ein Pfad statt tausend `<rect>`: bei Version 6 sind das über 1700 Knoten, und
 * die Seite wird spürbar träge, obwohl das Bild sich nie ändert.
 */
export function toPathData(matrix: QrMatrix): string {
  const parts: string[] = []
  for (let row = 0; row < matrix.size; row++) {
    for (let column = 0; column < matrix.size; column++) {
      if (matrix.isDark(row, column)) {
        parts.push(`M${column + QUIET_ZONE} ${row + QUIET_ZONE}h1v1h-1z`)
      }
    }
  }
  return parts.join('')
}

/** Kantenlänge des fertigen Bildes in Modulen, Ruhezone eingerechnet. */
export function totalSize(matrix: QrMatrix): number {
  return matrix.size + QUIET_ZONE * 2
}
