/** Ableitungen aus den Erntegewichten. */

function parseNullableNumber(value: string): number | null {
  const trimmed = value.trim()
  if (!trimmed) return null
  // Deutsche Eingabe: 21,5 ist dasselbe wie 21.5.
  const parsed = Number(trimmed.replace(',', '.'))
  return Number.isNaN(parsed) ? null : parsed
}

/** Trockenausbeute in Prozent — üblich sind 20–25 %. */
export function summariseYield(wetWeightG: string, dryWeightG: string): { text: string } | null {
  const wet = parseNullableNumber(wetWeightG)
  const dry = parseNullableNumber(dryWeightG)
  if (wet == null || dry == null || wet <= 0 || dry <= 0) return null
  const percent = (dry / wet) * 100
  // Über 100 % ist keine Ausbeute, sondern ein Zahlendreher — das zu sagen ist
  // hilfreicher, als eine unmögliche Zahl auszugeben.
  if (dry > wet) return { text: 'Trockengewicht über Frischgewicht — vermutlich vertauscht.' }
  return { text: `Trockenausbeute ${percent.toFixed(1)} % (${dry} g von ${wet} g)` }
}
