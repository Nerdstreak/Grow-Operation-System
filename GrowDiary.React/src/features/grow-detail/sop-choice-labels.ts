/**
 * German wording for the branching questions.
 *
 * The knowledge files carry stable keys ("severity", "severe") so they stay diffable and
 * citable; the screen has to say what a grower would say. Anything without an entry falls
 * through to the raw value rather than being hidden.
 */
const CHOICE_LABELS: Record<string, string> = {
  severity: 'Befallsgrad',
  substrate: 'Substrat',
  waterSource: 'Ausgangswasser',
}

const OPTION_LABELS: Record<string, string> = {
  light: 'leicht befallen — Wurzeln überwiegend weiß',
  severe: 'stark befallen — braune, schleimige Wurzeln',
  rockwool: 'Steinwolle',
  easyplug: 'EasyPlug',
  jiffy: 'Jiffy',
  none: 'ohne Substratträger',
  ro: 'RO- / VE-Wasser',
  soft: 'Weichwasser',
}

const SUBJECT_LABELS: Record<string, { singular: string; plural: string }> = {
  plant: { singular: 'Pflanze', plural: 'Pflanzen' },
  cutting: { singular: 'Steckling', plural: 'Stecklinge' },
  bucket: { singular: 'Eimer', plural: 'Eimer' },
  module: { singular: 'Modul', plural: 'Module' },
}

export function choiceLabel(key: string): string {
  return CHOICE_LABELS[key] ?? key
}

export function optionLabel(value: string): string {
  return OPTION_LABELS[value] ?? value
}

export function subjectPlural(subject: string): string {
  return SUBJECT_LABELS[subject]?.plural ?? subject
}
