import { test, expect } from '@playwright/test'
import { darfUeberspringen } from './pflicht'

/**
 * NACHEINANDER, nicht parallel.
 *
 * Alle Faelle in dieser Datei aendern die Pflanzen desselben Grows am
 * laufenden Stand — Topf, Sorte, Anzahl. `fullyParallel` liesse sie
 * gleichzeitig laufen; dann raeumt der eine auf, waehrend der andere
 * misst, und das Ergebnis haengt am Zufall. Ein Test, dessen Ausgang vom
 * Zeitpunkt abhaengt, hat nichts geprueft.
 */
test.describe.configure({ mode: 'serial' })

/**
 * Je Topf eine eigene Sorte — und kein Feld geht dabei verloren.
 *
 * <b>Der Anlass.</b> Ein Nutzer fährt im RDWC in jedem Topf eine andere Sorte
 * und konnte das nicht angeben. Die Sorte je Pflanze gab es längst; es fehlte
 * der <i>Ort</i> — und die Auffindbarkeit.
 *
 * <b>Warum hier auch der zweite Klick zählt.</b> Das PUT auf eine Pflanze
 * überschreibt <b>alle</b> Felder. Die alte Fassung zählte sie von Hand auf;
 * ein neues Feld wäre dabei stillschweigend genullt worden, sobald jemand nur
 * die Sorte wechselt. Genau diese Klasse — „speichern, dann NOCHMAL speichern"
 * aus CLAUDE.md — prüft dieser Fall: erst die Sorte ändern, dann den Topf, und
 * nach jedem Schritt nachsehen, ob das andere Feld noch steht.
 */
test('je Pflanze Sorte UND Topf — beides überlebt die Änderung des anderen', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(),
    'Kein Backend — ohne Pflanzen gibt es nichts zu ändern.')

  const stand = async () => (await request.get('/api/plants?growId=1')).json()

  const vorher = await stand()
  darfUeberspringen(vorher.length < 2,
    'Weniger als zwei Pflanzen im Bestand — Demobestand.cs sollte vier anlegen.')

  // Die Grundmenge muss den Fall wirklich zeigen: mehrere Sorten, eigene Töpfe.
  const sorten = new Set(vorher.map((p: { strainId: number | null }) => p.strainId))
  expect(sorten.size, 'Alle Pflanzen tragen dieselbe Sorte — der Fall ist unsichtbar.')
    .toBeGreaterThan(1)
  expect(vorher.every((p: { siteIndex: number | null }) => p.siteIndex != null),
    'Nicht jede Pflanze hat einen Topf.').toBe(true)

  await page.goto('/grows/1', { waitUntil: 'networkidle' })
  const karte = page.locator('[data-audit="grow-plants"]')
  await expect(karte).toBeVisible()
  await karte.scrollIntoViewIfNeeded()

  const zweite = vorher[1]
  const andereSorte = vorher.find(
    (p: { strainId: number | null }) => p.strainId !== zweite.strainId)
  expect(andereSorte, 'Keine zweite Sorte gefunden.').toBeTruthy()

  try {
    // --- Schritt 1: die SORTE ändern. Der Topf muss stehen bleiben.
    await karte.locator('select').nth(1).selectOption(String(andereSorte.strainId))
    await page.waitForTimeout(900)

    const nachSorte = (await stand()).find((p: { id: number }) => p.id === zweite.id)
    expect(nachSorte.strainId, 'Die Sorte wurde nicht übernommen.').toBe(andereSorte.strainId)
    expect(nachSorte.siteIndex,
      `Beim Sortenwechsel ging der Topf verloren (war ${zweite.siteIndex}, ist ${nachSorte.siteIndex}). `
      + 'Das PUT überschreibt alle Felder — es muss das ganze DTO mitschicken.')
      .toBe(zweite.siteIndex)

    // --- Schritt 2, erschwert: jetzt den TOPF ändern. Die Sorte muss stehen.
    //
    // Seit dem 25.08.2026 gibt es nur Töpfe, die das System auch hat, und
    // jeden höchstens einmal. Ein frei erfundener Topf 9 wird abgelehnt —
    // richtig so. Also wird erst einer frei gemacht.
    const dritte = vorher.find(
      (p: { id: number, siteIndex: number | null }) => p.id !== zweite.id && p.siteIndex != null)
    expect(dritte, 'Keine zweite Pflanze mit Topf gefunden.').toBeTruthy()
    await request.put(`/api/plants/${dritte.id}`, { data: { ...dritte, siteIndex: null } })

    const neuerTopf = dritte.siteIndex
    await karte.locator('.gp-topf input').nth(1).fill(String(neuerTopf))
    await karte.locator('.gp-topf input').nth(1).blur()
    await page.waitForTimeout(900)

    const nachTopf = (await stand()).find((p: { id: number }) => p.id === zweite.id)
    expect(nachTopf.siteIndex, 'Der Topf wurde nicht übernommen.').toBe(neuerTopf)
    expect(nachTopf.strainId,
      'Beim Topfwechsel ging die Sorte verloren — dieselbe Falle, andere Richtung.')
      .toBe(andereSorte.strainId)

    await request.put(`/api/plants/${zweite.id}`, { data: { ...zweite, siteIndex: null } })
    await request.put(`/api/plants/${dritte.id}`, { data: dritte })
  } finally {
    // Aufräumen: der Demobestand bleibt, wie er war. Ein Rundweg, der Spuren
    // hinterlässt, verschmutzt jede spätere Messung (belegt, beta.51).
    await request.put(`/api/plants/${zweite.id}`, { data: zweite })
    const zurueck = (await stand()).find((p: { id: number }) => p.id === zweite.id)
    expect(zurueck.strainId, 'Aufräumen fehlgeschlagen.').toBe(zweite.strainId)
    expect(zurueck.siteIndex, 'Aufräumen fehlgeschlagen.').toBe(zweite.siteIndex)
  }
})

test('der Grow-Überblick behauptet bei mehreren Sorten keine einzelne', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(), 'Kein Backend — siehe oben.')

  const pflanzen = await (await request.get('/api/plants?growId=1')).json()
  const sorten = new Set(
    pflanzen.map((p: { strainName: string | null }) => p.strainName).filter(Boolean))
  darfUeberspringen(sorten.size < 2, 'Nur eine Sorte im Bestand — dann prüft dieser Fall nichts.')

  await page.goto('/grows/1', { waitUntil: 'networkidle' })
  await page.waitForTimeout(600)

  const kachel = await page.locator('[data-audit="grow-detail-summary"]').innerText()
  expect(kachel,
    `Die Kachel nennt eine einzelne Sorte, obwohl ${sorten.size} im Zelt stehen: ${kachel.slice(0, 120)}`)
    .toContain('gemischt')
})

/**
 * Nicht mehr Pflanzen als Töpfe — und ein Weg zurück.
 *
 * <b>Der Anlass (25.08.2026).</b> „Du kannst mehr Sorten angeben, als es Töpfe
 * gibt." Am laufenden Stand belegt: acht Pflanzen in einem Vier-Topf-System,
 * Topf 1 doppelt, ein Topf 999 — jedes Mal HTTP 201. Dazu gab es keinen
 * Löschweg: wer eine zu viel anlegte, behielt sie.
 *
 * <b>Warum die Oberfläche und nicht nur die API.</b> Ein gesperrter Knopf ohne
 * Grund ist ein kaputter Knopf, und eine Ablehnung, die „Eingaben konnten
 * nicht validiert werden" sagt, hilft niemandem. Beides sieht nur ein Lauf am
 * gerenderten Stand.
 */
test('sind alle Töpfe belegt, sagt die Karte es — und das Entfernen macht wieder Platz', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(), 'Kein Backend.')

  const grow = await (await request.get('/api/grows/1')).json()
  darfUeberspringen(grow.systemId == null, 'Der Grow hängt an keinem Hydro-System.')
  const system = await (await request.get(`/api/hydro-setups/${grow.systemId}`)).json()
  const toepfe: number = system.potCount
  darfUeberspringen(!toepfe, 'Das System kennt keine Töpfe.')

  const stand = async () => (await request.get('/api/plants?growId=1')).json()
  const vorher = await stand()
  darfUeberspringen(vorher.length !== toepfe,
    `Der Bestand hat ${vorher.length} Pflanzen bei ${toepfe} Töpfen — dann prüft der Fall etwas anderes.`)

  await page.goto('/grows/1', { waitUntil: 'networkidle' })
  const karte = page.locator('[data-audit="grow-plants"]')
  await karte.scrollIntoViewIfNeeded()

  // 1. Voll heisst voll — und der Grund steht daneben.
  await expect(karte.locator('.gp-bilanz')).toContainText(`${toepfe} von ${toepfe}`)
  await expect(karte.locator('.gp-neu button')).toBeDisabled()
  await expect(karte.locator('.gp-voll'),
    'Der Knopf ist gesperrt, aber niemand sagt warum.').toBeVisible()

  // 2. Ein belegter Topf wird abgelehnt — mit einem Satz, der den Topf nennt.
  const felder = karte.locator('.gp-topf input')
  await felder.nth(1).fill('1')
  await felder.nth(1).blur()
  await expect(karte.locator('.gp-fehler').first()).toContainText('Topf 1')
  await expect(karte.locator('.gp-fehler').first(),
    'Der generische Satz kommt beim Nutzer an statt der Begründung.')
    .not.toContainText('validiert werden')
  expect((await stand()).filter((p: { siteIndex: number }) => p.siteIndex === 1).length,
    'Der doppelte Topf wurde gespeichert.').toBe(1)

  // 3. Entfernen macht Platz — an einer Pflanze, die dieser Lauf selbst
  //    angelegt hat. Eine Demopflanze zu löschen und neu anzulegen gäbe ihr
  //    eine neue Id; alles, was daran hängt (Pheno-Bogen, Abstammung), wäre ab.
  //    Deshalb wird kurz ein Topf dazugegeben statt einer weggenommen.
  let angelegt: number | null = null
  try {
    await request.put(`/api/hydro-setups/${grow.systemId}`, {
      data: { ...system, potCount: toepfe + 1 },
    })
    await page.reload({ waitUntil: 'networkidle' })
    await karte.scrollIntoViewIfNeeded()
    await expect(karte.locator('.gp-neu button'),
      'Ein Topf mehr, und der Knopf lädt trotzdem nicht ein.').toBeEnabled()

    await karte.locator('.gp-neu button').click()
    await expect.poll(async () => (await stand()).length).toBe(toepfe + 1)
    angelegt = (await stand()).find(
      (p: { id: number }) => !vorher.some((v: { id: number }) => v.id === p.id))?.id ?? null
    expect(angelegt, 'Die neue Pflanze ist nicht auffindbar.').toBeTruthy()

    // Wieder voll, wieder gesperrt.
    await expect(karte.locator('.gp-neu button')).toBeDisabled()

    // Und jetzt weg damit — über den Knopf, mit Rückfrage.
    page.once('dialog', (dialog) => void dialog.accept())
    await karte.locator('.gp-weg').last().click()
    await expect.poll(async () => (await stand()).length).toBe(toepfe)
  } finally {
    if (angelegt != null) await request.delete(`/api/plants/${angelegt}`)
    await request.put(`/api/hydro-setups/${grow.systemId}`, { data: system })

    // Nachprüfen, dass wirklich nichts liegen bleibt — dieselben Ids wie vorher.
    const zurueck = await stand()
    expect(zurueck.map((p: { id: number }) => p.id).sort(),
      'Aufräumen fehlgeschlagen — der Bestand hat andere Pflanzen als vorher.')
      .toEqual(vorher.map((p: { id: number }) => p.id).sort())
    const system2 = await (await request.get(`/api/hydro-setups/${grow.systemId}`)).json()
    expect(system2.potCount, 'Die Topfzahl wurde nicht zurückgesetzt.').toBe(toepfe)
  }
})

/**
 * Nach dem Entfernen und Neuanlegen heisst keine Pflanze wie eine andere.
 *
 * <b>Der Anlass (28.08.2026).</b> Gemeldet: „Der user hat eine pflanze
 * gelöscht und wieder hinzugefügt und da taucht diese doppelt auf."
 * Nachgestellt am laufenden Stand:
 * <pre>
 *   Ausgangslage:       Pflanze 1@Topf1 | Pflanze 2@Topf2 | Pflanze 3@Topf3 | Pflanze 4@Topf4
 *   nach dem Entfernen: Pflanze 1@Topf1 | Pflanze 2@Topf2 | Pflanze 4@Topf4
 *   nach dem Anlegen:   Pflanze 1@Topf1 | Pflanze 2@Topf2 | Pflanze 4@Topf4 | Pflanze 4@Topf3
 * </pre>
 *
 * <b>Die Ursache.</b> Der Name kam aus der ANZAHL
 * (<code>Pflanze ${plants.length + 1}</code>), der Topf aus der ersten freien
 * Lücke. Nach einer Löschung laufen die beiden auseinander: drei Pflanzen
 * ergeben „Pflanze 4", und die gibt es schon.
 *
 * <b>Die Regel.</b> Der Name folgt dem TOPF, nicht der Anzahl. Ein Topf trägt
 * eine Pflanze, also ist seine Nummer eindeutig — und wenn der Topf wechselt,
 * zieht der Name mit. Genau das hat der Nutzer verlangt: „dass er automatisch
 * durchzählt und wenn sich was ändert, er die Zahl automatisch zieht".
 */
test('entfernen und neu anlegen gibt keiner Pflanze den Namen einer anderen', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(), 'Kein Backend.')

  const grow = await (await request.get('/api/grows/1')).json()
  darfUeberspringen(grow.systemId == null, 'Der Grow hängt an keinem Hydro-System.')

  const stand = async () => (await request.get('/api/plants?growId=1')).json() as
    Promise<Array<{ id: number; label: string; siteIndex: number | null; strainId: number | null }>>

  const vorher = await stand()
  expect(vorher.length, 'Zu wenige Pflanzen — der Fall braucht mindestens drei, damit '
    + 'nach dem Entfernen eine Lücke IN DER MITTE entsteht.').toBeGreaterThanOrEqual(3)

  await page.goto('/grows/1', { waitUntil: 'networkidle' })
  const karte = page.locator('[data-audit="grow-plants"]')
  await karte.scrollIntoViewIfNeeded()

  let angelegt: number | null = null
  try {
    /* Die MITTLERE entfernen, nicht die letzte: nur dann entsteht eine Lücke,
       und nur dann laufen Anzahl und Topfnummer auseinander. Hätte der Fall
       die letzte genommen, wäre er zufällig grün geblieben. */
    const mitte = Math.floor(vorher.length / 2)
    page.once('dialog', (dialog) => void dialog.accept())
    await karte.locator('.gp-weg').nth(mitte).click()
    await expect.poll(async () => (await stand()).length).toBe(vorher.length - 1)

    await karte.locator('.gp-neu button').click()
    await expect.poll(async () => (await stand()).length).toBe(vorher.length)

    const nachher = await stand()
    angelegt = nachher.find((p) => !vorher.some((v) => v.id === p.id))?.id ?? null
    expect(angelegt, 'Die neue Pflanze ist nicht auffindbar.').toBeTruthy()

    const namen = nachher.map((p) => p.label)
    const doppelt = namen.filter((n, i) => namen.indexOf(n) !== i)
    expect(doppelt, `Diese Namen kommen mehrfach vor: ${[...new Set(doppelt)].join(', ')}\n`
      + nachher.map((p) => `  ${p.label} auf Topf ${p.siteIndex}`).join('\n')).toEqual([])

    /* Und der Name PASST zum Topf. Zwei Nummern nebeneinander, die
       verschiedene Dinge sagen, sind schlimmer als eine falsche: „Pflanze 4"
       auf „Topf 3" lässt den Leser raten, welche gilt. */
    const unpassend = nachher.filter((p) =>
      p.siteIndex != null && !p.label.includes(String(p.siteIndex)))
    expect(unpassend, 'Name und Topfnummer widersprechen sich:\n'
      + unpassend.map((p) => `  „${p.label}" sitzt auf Topf ${p.siteIndex}`).join('\n'))
      .toEqual([])
  } finally {
    if (angelegt != null) await request.delete(`/api/plants/${angelegt}`)
    /* Die entfernte Pflanze wieder anlegen — mit ihren Feldern, damit der
       Bestand danach dieselbe MENGE hat. Die Id ist eine andere; das lässt
       sich nicht vermeiden, wenn ein Fall das Löschen prüft. */
    const fehlend = vorher.filter(async () => true)
    const jetzt = await stand()
    for (const p of fehlend) {
      if (jetzt.some((j) => j.id === p.id)) continue
      await request.post('/api/plants', {
        data: {
          growId: 1, strainId: p.strainId, label: p.label,
          plantRole: 'Production', plantStatus: 'Active', siteIndex: p.siteIndex,
        },
      })
    }
  }
})

/**
 * Der Topf wird GEWÄHLT, nicht getippt — und steht nur einmal da.
 *
 * <b>Der Anlass (28.08.2026).</b> Gemeldet: „Die Topf durchzählung ist
 * fehlerhaft und die Prüfung ob der Topf belegt ist ist etwas komisch, kannst
 * du das angenehmer und verständlicher für den user machen."
 *
 * <b>Was daran komisch war.</b> Die Zeile las sich
 * <code>Pflanze 1 · TOPF [1] · White Widow</code> — der Name und die Topfzahl
 * sagten dasselbe, zweimal nebeneinander. Und der Topf war ein Zahlenfeld:
 * wer 2 eintippte, während Topf 2 belegt war, bekam eine Fehlermeldung, statt
 * die belegten Töpfe vorher zu sehen. Die App weiss, welche frei sind.
 *
 * <b>Die Regel des Nutzers dazu</b> („es soll nichts doppelt sein und die
 * abbildung hat immer vorrang"): eine Angabe, eine Stelle. Der Topf ist die
 * Kennung der Zeile, und was belegt ist, steht in der Auswahl.
 */
test('der Topf steht einmal da und wird aus den freien gewaehlt', async ({ page, request }) => {
  darfUeberspringen(!(await request.get('/api/grows')).ok(), 'Kein Backend.')

  const grow = await (await request.get('/api/grows/1')).json()
  darfUeberspringen(grow.systemId == null, 'Der Grow hängt an keinem Hydro-System.')
  const system = await (await request.get(`/api/hydro-setups/${grow.systemId}`)).json()
  const pflanzen = await (await request.get('/api/plants?growId=1')).json() as
    Array<{ siteIndex: number | null }>
  darfUeberspringen(pflanzen.length === 0, 'Keine Pflanzen — dann gibt es keine Zeile.')

  await page.goto('/grows/1', { waitUntil: 'networkidle' })
  const karte = page.locator('[data-audit="grow-plants"]')
  await karte.scrollIntoViewIfNeeded()
  await page.waitForTimeout(600)

  /* <b>Kein Zahlenfeld mehr.</b> Ein Feld, in das man eine belegte Nummer
     tippen kann, verschiebt die Prüfung auf den Moment NACH der Eingabe. */
  expect(await karte.locator('.gp-topf input[type="number"]').count(),
    'Der Topf ist noch ein Zahlenfeld — wer eine belegte Nummer tippt, erfährt es '
    + 'erst danach.').toBe(0)

  const topfWahl = karte.locator('.gp-topf select')
  await expect(topfWahl.first(), 'Es gibt keine Topf-Auswahl.').toBeVisible()

  /* Die Auswahl kennt ALLE Töpfe des Systems — sonst kann man eine Pflanze
     nicht auf einen freien schieben, den es gibt. */
  const optionen = await topfWahl.first().locator('option').count()
  expect(optionen, `Die Auswahl hat ${optionen} Einträge, das System ${system.potCount} `
    + 'Töpfe (plus „kein Topf"). Ein Topf, der fehlt, ist nicht erreichbar.')
    .toBeGreaterThanOrEqual(system.potCount)

  /* Und die Zeile nennt die Nummer nur EINMAL. Vorher stand „Pflanze 1" neben
     „TOPF 1" — dieselbe Zahl zweimal, was nach zwei Angaben aussieht. */
  /* Der SICHTBARE Text der Zeile — inklusive der gewaehlten Option. Die erste
     Fassung nahm `innerText`, und das liest den Wert eines Auswahlfelds nicht
     mit: sie war gruen, waehrend im Bild „Topf 1 · TOPF · [1]" stand. */
  const ersteZeile = await karte.locator('.gp-liste li').first().evaluate((li) => {
    const stuecke: string[] = []
    for (const el of li.querySelectorAll('*')) {
      const eigen = [...el.childNodes].filter((k) => k.nodeType === 3)
        .map((k) => k.textContent ?? '').join('').trim()
      if (eigen) stuecke.push(eigen)
      if (el instanceof HTMLSelectElement) {
        stuecke.push(el.options[el.selectedIndex]?.textContent?.trim() ?? '')
      }
    }
    return stuecke.filter(Boolean).join(' · ')
  })
  const topfNummer = String(pflanzen[0].siteIndex ?? '')
  if (topfNummer) {
    const treffer = ersteZeile.split(new RegExp(`\b${topfNummer}\b`)).length - 1
    expect(treffer, `Die Nummer ${topfNummer} steht ${treffer}-mal in derselben Zeile: `
      + `„${ersteZeile.replace(/\n/g, ' · ')}"`).toBeLessThanOrEqual(1)
  }
})
