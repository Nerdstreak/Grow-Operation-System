/* src/features/grow-detail/pflanzen-namen.ts
   Wie eine Pflanze heisst und in welchen Topf sie kommt.

   <b>Warum eine eigene Datei.</b> Die drei Funktionen standen in
   `GrowPlantsCard.tsx`. Eine Datei, die eine Komponente exportiert, soll nur
   Komponenten exportieren — sonst verliert Fast Refresh den Zustand bei jeder
   Aenderung, und der Linter meldet es. Inhaltlich gehoeren sie ohnehin hierher:
   das ist Logik, keine Darstellung, und sie laesst sich so einzeln pruefen.
*/

/**
 * Der Name einer Pflanze folgt ihrem TOPF, nicht der Anzahl.
 *
 * <b>Der Anlass (28.08.2026).</b> Gemeldet: „Der user hat eine pflanze
 * gelöscht und wieder hinzugefügt und da taucht diese doppelt auf."
 * Nachgestellt: vier Pflanzen, die dritte entfernt, eine neue angelegt —
 * heraus kam „Pflanze 4" auf Topf 4 UND „Pflanze 4" auf Topf 3.
 *
 * Der Name kam aus `plants.length + 1`, der Topf aus der ersten freien Lücke.
 * Nach einer Löschung laufen die beiden auseinander: drei Pflanzen ergeben
 * „Pflanze 4", und die gibt es schon.
 *
 * Ein Topf trägt eine Pflanze — seine Nummer ist also eindeutig, und das
 * macht sie zum besseren Namen. Zwei Nummern nebeneinander, die verschiedene
 * Dinge sagen („Pflanze 4" auf „Topf 3"), lassen den Leser ohnehin raten.
 */
export function pflanzenName(topf: number | null): string {
  return topf == null ? 'Pflanze ohne Topf' : `Pflanze ${topf}`
}

/**
 * Der nächste freie Topf ab 1 — dieselbe Zählung, die die Draufsicht an ihre
 * Sites zeichnet.
 */
export function naechsterFreierTopf(plants: ReadonlyArray<{ siteIndex: number | null }>): number {
  const belegt = new Set(plants.map((p) => p.siteIndex).filter((n): n is number => n != null))
  let frei = 1
  while (belegt.has(frei)) frei += 1
  return frei
}

/**
 * Stammt dieser Name von der App — oder hat ihn jemand selbst vergeben?
 *
 * Nur automatische Namen wandern beim Topfwechsel mit. „Mutter Nord" bleibt
 * stehen, wo sie steht; sonst überschriebe ein Topfwechsel eine Angabe, die
 * jemand mit Absicht gemacht hat.
 */
export function istAutomatischerName(label: string | null | undefined): boolean {
  if (label == null) return true
  return /^\s*(Pflanze|Topf)\s*(\d+)?\s*$/i.test(label) || label.trim() === 'Pflanze ohne Topf'
}
