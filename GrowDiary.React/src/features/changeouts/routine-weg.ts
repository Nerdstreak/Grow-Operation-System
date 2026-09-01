/**
 * Wo eine fällige Routine erledigt wird — und wie der Knopf dorthin heisst.
 *
 * <b>Der Anlass (31.08.2026).</b> Gemeldet: „der User findet den Wasserwechsel
 * nicht wirklich." Vier Stellen in der App mahnen ihn an — „Heute fällig" auf
 * der Live-Seite, „Fällige Routinen" am Handy, die Risiko-Karten, der Trend —
 * und <b>keine</b> führte zum Eintragen. Der Knopf hiess überall „Öffnen" und
 * ging zur Aufgaben- oder Grow-Seite, wo das Formular auch nicht steht.
 *
 * <b>Eine Mahnung, die nicht zum Tun führt, ist eine Sackgasse.</b> Deshalb
 * steht der Weg hier einmal statt in jeder Ansicht neu — sonst haben in vier
 * Wochen drei Ansichten den Weg und die vierte nicht.
 */
export type RoutineWeg = { to: string; aktion: string }

/**
 * Der direkte Weg für Routinen, die eine eigene Seite haben. Alles andere gibt
 * <code>null</code> zurück — dort bleibt es beim bisherigen Ziel.
 */
export function wegZurRoutine(sopId: string): RoutineWeg | null {
  switch (sopId) {
    // Die Seite, die es seit dem 31.08.2026 gibt. Sie führt genau die Handlung,
    // die die Mahnung verlangt: den Wechsel eintragen — auch nachträglich.
    case 'weekly-water-change':
      return { to: '/wasserwechsel', aktion: 'Eintragen' }
    default:
      return null
  }
}
