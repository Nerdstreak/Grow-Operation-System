/**
 * Das Asset-Verzeichnis vor jedem Bau leeren.
 *
 * **Warum.** Vite schreibt je Bau eine neue `index-<hash>.js` und räumt die
 * alte nicht weg — nach ein paar Wochen lagen dort 481 Dateien, darunter 246
 * Bündel. Das ist nicht nur unordentlich: bei der Frage „welches Bündel liefert
 * die App gerade aus?" gibt es dann 246 Kandidaten, und eine Prüfung, die den
 * jüngsten sucht, kann sich vertun.
 *
 * `emptyOutDir` von Vite kann das nicht übernehmen: `outDir` ist
 * `../GrowDiary.Web/wwwroot`, und dort liegen auch die mitgelieferten
 * Wissensdateien. Geleert wird deshalb nur `assets/` — dort steht
 * ausschliesslich, was dieser Bau selbst erzeugt.
 */
import { existsSync, readdirSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

// `new URL(...).pathname` liefert unter Windows einen Pfad mit fuehrendem
// Schraegstrich vor dem Laufwerksbuchstaben, und `existsSync` findet damit
// nichts. Der erste Anlauf hat deshalb still "kein Verzeichnis" gemeldet und
// alle 481 Dateien liegen lassen — eine Aufraeumung, die nicht aufraeumt.
const ORDNER = fileURLToPath(new URL('../../GrowDiary.Web/wwwroot/assets', import.meta.url))

if (!existsSync(ORDNER)) {
  console.log(`assets-leeren: ${ORDNER} gibt es nicht — nichts zu tun.`)
  process.exit(0)
}

const dateien = readdirSync(ORDNER)
for (const name of dateien) rmSync(join(ORDNER, name), { force: true })
console.log(`assets-leeren: ${dateien.length} Dateien entfernt.`)
