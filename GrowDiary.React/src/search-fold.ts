/**
 * Suchvergleich, der Umlaute nicht gegen den Nutzer arbeiten lässt.
 *
 * Die Navigationsziele hiessen frueher "Zelte & Raeume", damit ein einfacher
 * `includes` auch dann trifft, wenn jemand ohne Umlaut tippt. Das ist der
 * falsche Ort für den Kompromiss: die Beschriftung steht sichtbar in der
 * Navigation, der Vergleich nicht. Also stehen die Umlaute jetzt dort, wo sie
 * hingehören, und der Vergleich faltet sie — in beide Richtungen, weil "raume"
 * und "raeume" beide vorkommen und keine der beiden Schreibweisen die andere
 * als Teilzeichenkette enthält.
 */

const SIMPLE: Record<string, string> = { 'ä': 'a', 'ö': 'o', 'ü': 'u', 'ß': 'ss' }
const ASCII: Record<string, string> = { 'ä': 'ae', 'ö': 'oe', 'ü': 'ue', 'ß': 'ss' }

function fold(text: string, map: Record<string, string>): string {
  return text.toLowerCase().replace(/[äöüß]/g, (char) => map[char] ?? char)
}

/** True, wenn `term` in `haystack` vorkommt — Gross-/Kleinschreibung und Umlautschreibweise egal. */
export function matchesSearchTerm(haystack: string, term: string): boolean {
  const needle = term.trim()
  if (!needle) return false
  const forms = [haystack.toLowerCase(), fold(haystack, SIMPLE), fold(haystack, ASCII)]
  const needles = [needle.toLowerCase(), fold(needle, SIMPLE), fold(needle, ASCII)]
  return needles.some((candidate) => forms.some((form) => form.includes(candidate)))
}
