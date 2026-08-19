import type { GrowStage, PhotoTag, ValueOrigin } from './types'

/**
 * Deutsche Namen für die Enum-Werte, die auf dem Bildschirm landen.
 *
 * <b>Wozu.</b> In Auswahlfeldern standen die Entwickler-Bezeichner roh da:
 * „Seedling", „Overview", „HomeAssistant". Dieselbe Klasse Fehler wie die 65
 * rohen Symptom-Schlüssel im Wissen und wie „Ec" statt „EC" in der Diagnose —
 * nur an einer Stelle, an der man sie auch noch anklicken muss.
 *
 * <b>Warum hier und nicht je Seite.</b> Die Listen standen viermal im
 * Quelltext: zweimal `GrowStage` und zweimal `PhotoTag`, dazu acht fest
 * getippte `<option>`-Zeilen im Messformular, die keine Tabelle je erwischt
 * hätte. Vier Kopien einer Liste sind vier Gelegenheiten, eine zu vergessen.
 *
 * <b>Unbekanntes wird durchgereicht</b>, nicht verschluckt: ein englisches Wort
 * ist besser als ein leeres Feld. Die Zählung in
 * `deutsche-woerter.node.test.ts` sorgt dafür, dass es dabei nicht bleibt.
 */

/** Die Phasen eines Laufs, in ihrer natürlichen Reihenfolge. */
export const PHASEN: GrowStage[] = ['Seedling', 'Clone', 'Veg', 'Transition', 'Flower', 'Finish', 'Dry', 'Cure']

const PHASEN_NAMEN: Record<GrowStage, string> = {
  Seedling: 'Sämling',
  Clone: 'Steckling',
  Veg: 'Wachstum',
  Transition: 'Übergang',
  Flower: 'Blüte',
  Finish: 'Finish',
  Dry: 'Trocknen',
  Cure: 'Aushärten',
}

/** Die Kennzeichnungen für Fotos. */
export const FOTO_TAGS: PhotoTag[] = ['Overview', 'Canopy', 'Leaf', 'Root', 'Training', 'Flower', 'Problem', 'Comparison', 'Other']

const FOTO_NAMEN: Record<PhotoTag, string> = {
  Overview: 'Übersicht',
  Canopy: 'Blätterdach',
  Leaf: 'Blatt',
  Root: 'Wurzeln',
  Training: 'Training',
  Flower: 'Blüte',
  Problem: 'Problem',
  Comparison: 'Vergleich',
  Other: 'Sonstiges',
}

/**
 * Woher ein Wert stammt.
 *
 * <b>Achtung bei der Beschriftung.</b> `HomeAssistant` heißt „aus Home
 * Assistant" und NICHT „von der Automatik erzeugt": das Feld lässt sich im
 * Bearbeiten-Formular von Hand auf diesen Wert setzen. Dass in der heutigen
 * Datenbank beides zusammenfällt, ist ein Zufall der Daten.
 */
const HERKUNFT_NAMEN: Record<ValueOrigin, string> = {
  Manual: 'von Hand',
  HomeAssistant: 'aus Home Assistant',
  Imported: 'importiert',
  Derived: 'berechnet',
}

/** „Veg" wird „Wachstum". Unbekanntes bleibt, wie es ist. */
export function phaseName(wert: string | null | undefined): string {
  if (!wert) return ''
  return PHASEN_NAMEN[wert as GrowStage] ?? wert
}

/** „Overview" wird „Übersicht". */
export function fotoTagName(wert: string | null | undefined): string {
  if (!wert) return ''
  return FOTO_NAMEN[wert as PhotoTag] ?? wert
}

/** „HomeAssistant" wird „aus Home Assistant". */
export function herkunftName(wert: string | null | undefined): string {
  if (!wert) return ''
  return HERKUNFT_NAMEN[wert as ValueOrigin] ?? wert
}

/** Nur für die Zählung: was übersetzt ist. */
export const WOERTERBUECHER = {
  phase: PHASEN_NAMEN,
  fotoTag: FOTO_NAMEN,
  herkunft: HERKUNFT_NAMEN,
}
