/**
 * Deutsche Wörter für die Zustände und Typen einer Routine.
 *
 * <b>Wozu.</b> Auf der SOP-Seite standen die Entwickler-Bezeichner unverändert
 * auf dem Schirm: `Recurring`, `MultiDay`, `Active`, `Pending`, `InProgress`,
 * `Action`, `Measurement`, `Confirmation` — mitten in der deutschen Oberfläche,
 * auf genau der Seite, die man mit dem Telefon im Zelt aufhat. Dieselbe Klasse
 * Fehler wie die 65 rohen Symptom-Schlüssel im Wissen.
 *
 * <b>Was hier NICHT passiert.</b> Es wird nichts erfunden. Die Werte stammen aus
 * `SopInstanceStatus` und `SopStepInstanceStatus` im Backend sowie aus den
 * ausgelieferten Ablauf-Dateien (`knowledge-defaults/sops`); ein unbekannter
 * Wert wird durchgereicht, statt verschluckt zu werden — lieber ein englisches
 * Wort als ein leeres Feld.
 */

/** Zustand einer laufenden Routine (`SopInstanceStatus`). */
const INSTANZ_ZUSTAND: Record<string, string> = {
  Active: 'Läuft',
  Completed: 'Fertig',
  Cancelled: 'Abgebrochen',
}

/** Zustand eines einzelnen Schritts (`SopStepInstanceStatus`). */
const SCHRITT_ZUSTAND: Record<string, string> = {
  Pending: 'Offen',
  InProgress: 'Angefangen',
  Done: 'Erledigt',
  Skipped: 'Übersprungen',
}

/** Bauart der Routine — steht so in den ausgelieferten Ablauf-Dateien. */
const ABLAUF_ART: Record<string, string> = {
  Linear: 'Der Reihe nach',
  Recurring: 'Wiederkehrend',
  MultiDay: 'Über mehrere Tage',
}

/** Was ein Schritt von einem verlangt. */
const SCHRITT_ART: Record<string, string> = {
  Action: 'Handgriff',
  Measurement: 'Messung',
  Confirmation: 'Bestätigen',
  Photo: 'Foto',
  Wait: 'Warten',
  SubSop: 'Unterablauf',
}

const nachschlagen = (tabelle: Record<string, string>, wert: string | null | undefined): string =>
  (wert && tabelle[wert]) || wert || ''

export const instanzZustand = (wert?: string | null) => nachschlagen(INSTANZ_ZUSTAND, wert)
export const schrittZustand = (wert?: string | null) => nachschlagen(SCHRITT_ZUSTAND, wert)
export const ablaufArt = (wert?: string | null) => nachschlagen(ABLAUF_ART, wert)
export const schrittArt = (wert?: string | null) => nachschlagen(SCHRITT_ART, wert)

/** Nur für Tests: was übersetzt ist. */
export const SOP_WOERTER = { INSTANZ_ZUSTAND, SCHRITT_ZUSTAND, ABLAUF_ART, SCHRITT_ART }
