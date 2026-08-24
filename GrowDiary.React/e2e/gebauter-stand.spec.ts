import { test, expect } from '@playwright/test'
import { existsSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { darfUeberspringen } from './pflicht'

/**
 * Die laufende App liefert das Bündel aus, das zuletzt gebaut wurde.
 *
 * <b>Der Anlass.</b> In der Nacht auf den 25.08.2026 habe ich eine halbe Stunde
 * gegen eine App gemessen, die noch aus einer früheren Sitzung lief. Daraufhin
 * kam <c>bauKennung</c> in <c>/api/system/backend-health</c> — <b>aber die
 * deckt nur das Backend.</b> Ein Prüfer hat es sofort gefunden: das Startskript
 * baut ausschliesslich mit <c>dotnet build</c>, und praktisch die ganze
 * sichtbare Arbeit liegt im Frontend. Die App lieferte ein Bündel von 02:48
 * aus, während zwei Quelldateien von 02:58 und 02:59 waren — und das Skript
 * meldete „gebaut und laufend sind dasselbe".
 *
 * <b>Was hier geprüft wird.</b> Die Seite nennt in ihrem <c>&lt;script&gt;</c>
 * eine Datei <c>index-&lt;hash&gt;.js</c>. Genau diese muss die jüngste im
 * Asset-Verzeichnis sein. Ist sie es nicht, misst jede Oberflächen-Prüfung
 * einen alten Stand — und meldet grün.
 *
 * <b>Warum das erst jetzt zuverlässig geht.</b> Vorher lagen dort 481 Dateien,
 * darunter 246 Bündel, weil niemand aufräumte. Seit <c>npm run build</c> das
 * Verzeichnis vorher leert, gibt es je Bau genau eines.
 */

const ASSETS = fileURLToPath(new URL('../../GrowDiary.Web/wwwroot/assets', import.meta.url))
const QUELLE = fileURLToPath(new URL('../src', import.meta.url))

/** Die juengste Datei, aus der das Buendel entsteht. */
function juengsteQuelle(ordner: string): { name: string, zeit: number } {
  let neueste = { name: '', zeit: 0 }

  for (const eintrag of readdirSync(ordner, { withFileTypes: true })) {
    const pfad = join(ordner, eintrag.name)

    if (eintrag.isDirectory()) {
      const tiefer = juengsteQuelle(pfad)
      if (tiefer.zeit > neueste.zeit) neueste = tiefer
      continue
    }

    // Nur, was ins Buendel wandert. Tests nicht.
    if (!/[.](ts|tsx|css)$/.test(eintrag.name)) continue
    if (/[.]test[.]tsx?$/.test(eintrag.name)) continue

    const zeit = statSync(pfad).mtimeMs
    if (zeit > neueste.zeit) neueste = { name: eintrag.name, zeit }
  }

  return neueste
}

test('die App liefert das zuletzt gebaute Buendel aus', async ({ page }) => {
  const antwort = await page.goto('/', { waitUntil: 'domcontentloaded' })
  darfUeberspringen(
    antwort == null || antwort.status() >= 400,
    'Die App antwortet nicht — laeuft sie unter GROW_OS_URL?',
  )

  darfUeberspringen(
    !existsSync(ASSETS),
    `${ASSETS} gibt es nicht — dann laeuft die Pruefung nicht gegen einen lokalen Bau.`,
  )

  const gebuendelt = readdirSync(ASSETS)
    .filter((name) => /^index-.*\.js$/.test(name))
    .map((name) => ({ name, zeit: statSync(join(ASSETS, name)).mtimeMs }))
    .sort((a, b) => b.zeit - a.zeit)

  // Mengenwaechter: ohne Buendel prueft der Vergleich unten nichts.
  darfUeberspringen(gebuendelt.length === 0, `In ${ASSETS} liegt kein index-*.js — wurde je gebaut?`)

  const ausgeliefert = await page.evaluate(() =>
    Array.from(document.querySelectorAll<HTMLScriptElement>('script[src]'))
      .map((el) => el.getAttribute('src') || '')
      .find((src) => /index-.*\.js/.test(src)) || '')

  expect(ausgeliefert, 'Die Seite bindet kein index-*.js ein.').not.toBe('')

  const juengstes = gebuendelt[0].name
  expect(
    ausgeliefert.endsWith(juengstes),
    `Die App liefert „${ausgeliefert}" aus, das juengste Buendel ist aber „${juengstes}".\n`
    + 'Es laeuft ein alter Frontend-Stand: `npm run build` ausfuehren und die App neu starten.\n'
    + `Weitere Buendel im Verzeichnis: ${gebuendelt.length}.`,
  ).toBe(true)

  // Und je Bau genau EIN Buendel: liegen dort mehrere, ist die Aufraeumung
  // aus `skripte/assets-leeren.mjs` nicht gelaufen, und „das juengste" ist
  // wieder eine Frage von Zeitstempeln statt eine Tatsache.
  expect(
    gebuendelt.length,
    `${gebuendelt.length} Buendel im Asset-Verzeichnis — es sollte genau eines sein.`,
  ).toBe(1)

  // Der eigentliche Fall: es wurde geaendert und NICHT gebaut. Dann liefert
  // die App brav das juengste Buendel aus — nur ist das aelter als die
  // Quelle, und jede Messung gilt einem Stand von vorhin.
  const quelle = juengsteQuelle(QUELLE)
  expect(quelle.zeit, `Keine Quelldatei unter ${QUELLE} gefunden.`).toBeGreaterThan(0)

  const alter = Math.round((quelle.zeit - gebuendelt[0].zeit) / 1000)
  expect(
    quelle.zeit <= gebuendelt[0].zeit,
    `Die Quelldatei ${quelle.name} ist ${alter} s neuer als das ausgelieferte Buendel.`
    + ' Die laufende App kennt diese Aenderung NICHT — npm run build fehlt.',
  ).toBe(true)
})
