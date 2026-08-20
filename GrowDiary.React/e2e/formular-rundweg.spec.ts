import { expect, test, type Page } from '@playwright/test'
import { readFileSync, readdirSync } from 'node:fs'
import { darfUeberspringen } from './pflicht'

/**
 * Jedes Formular wird wirklich ausgefüllt, abgeschickt und nachgelesen.
 *
 * <b>Der Anlass.</b> In der gesamten E2E-Mappe gab es <b>zwei</b> `fill()` und
 * <b>keinen einzigen Absende-Klick</b>. Zwei Fehler dieser Klasse hat der
 * Tester gefunden, nicht die Sammlung:
 * <ul>
 *   <li>der Knopf „Messung speichern" tat nichts — er stand in einem
 *       `&lt;form onSubmit&gt;`, war aber `type="button"`, weil `V1Button` ohne
 *       Angabe genau das setzt;</li>
 *   <li>ein Zahlenfeld mit „6,2x" wurde <b>still als leer gespeichert</b> —
 *       mit Erfolgsmeldung.</li>
 * </ul>
 * Beide Male war die Seite da, das Feld war da, der Knopf war da. Nur gedrückt
 * hat nie jemand.
 *
 * <b>Was ein Rundweg hier heißt.</b> Ausfüllen, den ausgehenden Aufruf
 * abfangen und im Rumpf nachsehen, dass jeder getippte Wert wirklich drinsteht,
 * absenden, auf die Bestätigung warten, <b>neu laden</b> und prüfen, dass der
 * Wert noch da ist. Erst das letzte Stück schließt die Lücke: eine Oberfläche,
 * die den Wert nur anzeigt, ohne ihn gespeichert zu haben, besteht alles davor.
 *
 * <b>Diese Datei schreibt in die Datenbank.</b> Deshalb läuft sie seriell und
 * legt nur Datensätze an, die „Rundweg" im Namen tragen — im Demobestand
 * erkennbar, und sie stören keine Zählung, die auf Inhalt prüft.
 */

test.describe.configure({ mode: 'serial' })

const ORDNER = new URL('.', import.meta.url)
const QUELLE = new URL('../src/', import.meta.url)

/* ------------------------------------------------------------------ */
/* Die Grundmenge: jede Datei mit einem <form onSubmit>.               */
/* ------------------------------------------------------------------ */

/** Alle .tsx unterhalb von src, rekursiv. */
function alleBauteile(ordner = QUELLE, pfad = ''): string[] {
  const raus: string[] = []
  for (const eintrag of readdirSync(ordner, { withFileTypes: true })) {
    if (eintrag.name === 'node_modules') continue
    if (eintrag.isDirectory()) {
      raus.push(...alleBauteile(new URL(eintrag.name + '/', ordner), pfad + eintrag.name + '/'))
    } else if (eintrag.name.endsWith('.tsx')) {
      raus.push(pfad + eintrag.name)
    }
  }
  return raus
}

/**
 * Dateien, die ein Formular mit Absende-Behandlung tragen.
 *
 * Bewusst über `<form` plus `onSubmit`: das ist im Quelltext eindeutig zu
 * finden und lässt sich nicht vergessen. Knöpfe, die ohne `<form>` etwas
 * absenden (`GrowSetupPage`, `HydroEditorPage`), fallen NICHT darunter — die
 * stehen unten mit Grund, damit ihr Fehlen eine Entscheidung ist und kein
 * Versehen.
 */
function formularDateien(): string[] {
  return alleBauteile().filter((name) => {
    const inhalt = readFileSync(new URL(name, QUELLE), 'utf8')
    return /<form[^>]*\n?[^>]*onSubmit/.test(inhalt) || /<form[\s\S]{0,200}?onSubmit=/.test(inhalt)
  })
}

/**
 * Formulare ohne eigenen Rundweg — jedes mit Grund.
 *
 * Die Dateinamen sind aus dem Ordner abgeschrieben; ein Tippfehler würde die
 * Ausnahme wirkungslos machen, dagegen prüft der Test darunter.
 */
const OHNE_RUNDWEG: Record<string, string> = {
  'pages/GrowSetupPage.tsx':
    'Ein Assistent über mehrere Schritte mit Karten-Knöpfen statt Auswahlfeldern; ein Rundweg dafür braucht erst ein Zelt UND einen Hydro-Aufbau, die er selbst anlegen müsste. Eigenes Stück, noch nicht gebaut.',
  'features/hydro/HydroEditorPage.tsx':
    'Kein <form>, der Knopf bleibt grau, solange die Live-Prüfung Befunde hat. Gehört zum selben Stück wie der Grow-Assistent.',
  'pages/HarvestPage.tsx':
    'Braucht einen Grow, der geerntet werden darf — der Rundweg würde den Demobestand verändern, gegen den alle anderen Prüfungen laufen. Eigener Datensatz nötig.',
  'pages/HomeAssistantPage.tsx':
    'Schreibt die Verbindung zu Home Assistant. Ein Rundweg würde die Zuordnung der laufenden App verstellen und damit jede andere Prüfung, die Live-Werte erwartet. Braucht eine eigene Instanz.',
  'features/changeouts/ChangeoutsPanel.tsx':
    'Legt Wasserwechsel an und verschiebt damit die Fälligkeiten, gegen die der Wächter im selben Bestand prüft. Nächstes Stück, zusammen mit einem eigenen Grow für schreibende Prüfungen.',
  'features/plants/PlantActions.tsx':
    'Ändert Pflanzen eines Grows (Ausfall, Klon, Umtopfen); dieselbe Abhängigkeit wie oben — der Bestand ist geteilt.',
  'pages/HardwarePage.tsx':
    'Legt Geräte und Kalibrierungen an. Eine angelegte Kalibrierung erzeugt still eine Aufgabe (CreateCalibrationEvent bei Status Planned) und verändert damit die Aufgabenseite, gegen die andere Prüfungen laufen.',
}

/* ------------------------------------------------------------------ */
/* Hilfen                                                              */
/* ------------------------------------------------------------------ */

async function backendDa(seite: Page): Promise<boolean> {
  try {
    const antwort = await seite.request.get('/api/grows')
    return antwort.ok()
  } catch {
    return false
  }
}

/** Ein Wert, der in diesem Lauf einmalig ist — sonst prüft der zweite Lauf den ersten. */
function marke(): string {
  return `Rundweg ${new Date().toISOString().slice(11, 19)}`
}

/* ------------------------------------------------------------------ */
/* Die Zählung über die Grundmenge                                      */
/* ------------------------------------------------------------------ */

test.describe('Formular-Rundweg', () => {
  test('die Grundmenge wird überhaupt gelesen', () => {
    const dateien = formularDateien()
    expect(dateien.length,
      'Keine Datei mit <form onSubmit> gefunden — die Suche greift ins Leere und alles darunter '
      + 'wäre grundlos grün.').toBeGreaterThan(2)
  })

  test('jede ausgenommene Datei gibt es wirklich', () => {
    // Sonst schützt eine Ausnahme eine Datei, die es nicht gibt, während die
    // echte durchfällt — dreimal in diesem Projekt passiert.
    const vorhanden = alleBauteile()
    for (const name of Object.keys(OHNE_RUNDWEG)) {
      expect(vorhanden, `${name} steht in OHNE_RUNDWEG, aber nicht unter src/.`).toContain(name)
    }
  })

  test('jedes Formular hat einen Rundweg oder einen Grund', () => {
    // GEPRUEFT wird gegen die Testnamen in DIESER Datei: ein Rundweg zählt nur,
    // wenn er auch existiert. Die Datei liest sich dabei selbst — das ist hier
    // richtig, weil sie nach ihren EIGENEN Tests sucht und nicht nach dem
    // Gegenstand der Prüfung.
    const eigene = readFileSync(new URL('formular-rundweg.spec.ts', ORDNER), 'utf8')
    const ohne: string[] = []

    for (const datei of formularDateien()) {
      if (OHNE_RUNDWEG[datei]) continue
      const bauteil = datei.split('/').pop()!.replace('.tsx', '')
      if (!eigene.includes(`Rundweg: ${bauteil}`)) ohne.push(datei)
    }

    expect(ohne,
      'Diese Formulare werden nie ausgefüllt und nie abgeschickt:\n' + ohne.join('\n')
      + '\n\nEntweder einen Test `Rundweg: <Bauteilname>` schreiben — oder mit ausgeschriebenem '
      + 'Grund in OHNE_RUNDWEG eintragen. Ein Formular, das niemand bedient, ist ungeprüft: '
      + 'genau so kam ein toter Speichern-Knopf zum Tester.')
      .toEqual([])
  })

  /* ---------------------------------------------------------------- */
  /* Rundweg: ManualMeasurementPage                                    */
  /* ---------------------------------------------------------------- */

  test('Rundweg: ManualMeasurementPage — ausfüllen, absenden, nachlesen', async ({ page }) => {
    darfUeberspringen(!await backendDa(page),
      'Kein Backend — ein Formular ohne API kann nichts speichern. Gegen die laufende App: '
      + 'GROW_OS_URL=http://localhost:5076 npx playwright test formular-rundweg')

    await page.goto('/messung', { waitUntil: 'networkidle' })
    const formular = page.locator('[data-audit="measurement-form"]')
    await expect(formular).toBeVisible()

    // Ein pH-Wert, den es im Bestand sonst nicht gibt — damit die Gegenprobe
    // beim Nachlesen nicht versehentlich eine fremde Zeile findet.
    const ph = '5,71'
    const ec = '1,37'

    // Anker im Ausdruck, sonst trifft „pH" die Beschriftung „Phase":
    // getByLabel ohne `exact` vergleicht als Teilzeichenkette OHNE Ruecksicht
    // auf Gross- und Kleinschreibung, und „Phase" enthaelt „ph".
    await formular.getByLabel(/^pH\s*$/).fill(ph)
    // `exact`, sonst treffen auch „Input EC", „Drain EC" und „Addback EC".
    await formular.getByLabel('EC (mS/cm)', { exact: true }).fill(ec)
    await formular.getByLabel('Temperatur (°C)', { exact: true }).fill('24,3')
    await formular.getByLabel('Luftfeuchte (%)', { exact: true }).fill('57')

    // Den ausgehenden Aufruf abfangen: steht der getippte Wert wirklich im
    // Rumpf? Eine Oberflaeche, die ihn nur anzeigt, faellt hier durch.
    const anfrage = page.waitForRequest((r) =>
      r.method() === 'POST' && /\/api\/(grows\/\d+\/measurements|measurements)/.test(r.url()))

    await formular.getByRole('button', { name: 'Messung speichern' }).click()

    const gesendet = await anfrage
    const rumpf = JSON.parse(gesendet.postData() ?? '{}')
    expect(rumpf.reservoirPh, `pH ${ph} wurde getippt, gesendet wurde ${rumpf.reservoirPh}`).toBe(5.71)
    expect(rumpf.reservoirEc).toBe(1.37)
    expect(rumpf.airTemperatureC).toBe(24.3)

    // Und nachlesen — der Kern. Bis hierher könnte alles stimmen und trotzdem
    // nichts gespeichert sein.
    await page.goto('/messungen', { waitUntil: 'networkidle' })
    await page.waitForTimeout(1200)
    await expect(page.locator('main').last(),
      'Nach dem Speichern und Neuladen steht der Wert nicht im Protokoll — abgeschickt wurde er.')
      .toContainText(ph)
  })

  test('Rundweg: ManualMeasurementPage — ein unlesbarer Wert wird nicht still verschluckt', async ({ page }) => {
    darfUeberspringen(!await backendDa(page), 'Kein Backend — siehe oben.')

    // <b>Der ausgelieferte Fehler.</b> „6,2x" wurde als leer gespeichert, und
    // die App meldete Erfolg. 21 Zahlenfelder waren betroffen.
    await page.goto('/messung', { waitUntil: 'networkidle' })
    const formular = page.locator('[data-audit="measurement-form"]')
    await expect(formular).toBeVisible()

    await formular.getByLabel(/^pH\s*$/).fill('6,2x')

    let abgeschickt = false
    page.on('request', (r) => {
      if (r.method() === 'POST' && /measurements/.test(r.url())) abgeschickt = true
    })

    await formular.getByRole('button', { name: 'Messung speichern' }).click()
    await page.waitForTimeout(900)

    expect(abgeschickt,
      '„6,2x" ist keine Zahl. Trotzdem ging die Messung raus — dann wird der Wert als leer '
      + 'gespeichert und der Nutzer bekommt eine Erfolgsmeldung für etwas, das nicht da ist.')
      .toBe(false)

    await expect(formular.locator('..'),
      'Der unlesbare Wert wird zwar nicht gesendet, aber es steht auch nirgends, warum nichts passiert.')
      .toContainText(/nicht lesbar|keine Zahl|ungültig/i)
  })

  /* ---------------------------------------------------------------- */
  /* Rundweg: MeasurementEditPage                                      */
  /* ---------------------------------------------------------------- */

  test('Rundweg: MeasurementEditPage — ändern, speichern, nachlesen', async ({ page }) => {
    darfUeberspringen(!await backendDa(page), 'Kein Backend — siehe oben.')

    // <b>Warum gerade diese Seite.</b> Hier saß der stille Datenverlust: ein
    // Zahlenfeld mit unlesbarem Rest wurde als leer gespeichert, mit
    // Erfolgsmeldung. Das Bearbeiten ist ausserdem der einzige Weg, auf dem ein
    // vorhandener Wert VERSCHWINDEN kann — beim Anlegen gab es ihn vorher nicht.
    const antwort = await page.request.get('/api/grows/1/measurements')
    darfUeberspringen(!antwort.ok(), 'Grow 1 hat keine Messungen — der Demobestand sollte 90 anlegen.')
    const messungen = await antwort.json() as Array<{ id: number }>
    darfUeberspringen(messungen.length === 0, 'Keine Messung zum Bearbeiten im Bestand.')

    const id = messungen[0].id
    await page.goto(`/grows/measurements/${id}/edit`, { waitUntil: 'networkidle' })

    const neuerWert = '5,63'
    // Zwei Stolpersteine, beide aus dem gerenderten Baum geholt statt geraten:
    // (1) Auf DIESER Seite heisst das Feld „Reservoir-pH", nicht „pH".
    // (2) Die Einheit steht als <small> INNERHALB des <label> — der
    //     zugaengliche Name lautet dadurch „Reservoir-pH pH", und ein exakter
    //     Vergleich findet nichts. Deshalb ein verankerter Ausdruck.
    const phFeld = page.getByLabel(/^Reservoir-pH/)
    await expect(phFeld).toBeVisible()
    await phFeld.fill(neuerWert)

    const anfrage = page.waitForRequest((r) => r.method() === 'PUT' && /measurements/.test(r.url()))
    await page.getByRole('button', { name: 'Änderungen speichern' }).click()

    const rumpf = JSON.parse((await anfrage).postData() ?? '{}')
    expect(rumpf.reservoirPh, 'Der geänderte pH steht nicht im gesendeten Rumpf.').toBe(5.63)

    // Und wirklich nachlesen: die Seite frisch holen, nicht dem Zustand glauben.
    await page.goto(`/grows/measurements/${id}/edit`, { waitUntil: 'networkidle' })
    await page.waitForTimeout(900)
    await expect(page.getByLabel(/^Reservoir-pH/),
      'Nach dem Speichern und Neuladen steht der alte Wert wieder da — gespeichert wurde nichts.')
      .toHaveValue(/5[,.]63/)
  })

  /* ---------------------------------------------------------------- */
  /* Rundweg: JournalStreamSection                                     */
  /* ---------------------------------------------------------------- */

  test('Rundweg: JournalStreamSection — Eintrag anlegen und wiederfinden', async ({ page }) => {
    darfUeberspringen(!await backendDa(page), 'Kein Backend — siehe oben.')

    const titel = marke()
    await page.goto('/journal', { waitUntil: 'networkidle' })

    // Der Composer ist zugeklappt — ohne den Klick gibt es das Formular nicht.
    await page.locator('[data-audit="journal-add-entry"]').click()
    const formular = page.locator('[data-audit="journal-entry-form"]')
    await expect(formular).toBeVisible()

    await formular.getByLabel('Titel', { exact: true }).fill(titel)
    await formular.getByLabel('Text', { exact: true }).fill('Testdaten aus dem Formular-Rundweg.')

    const anfrage = page.waitForRequest((r) => r.method() === 'POST' && /\/journal/.test(r.url()))
    await formular.getByRole('button', { name: 'Eintrag speichern' }).click()

    const rumpf = JSON.parse((await anfrage).postData() ?? '{}')
    expect(rumpf.title, 'Der getippte Titel steht nicht im gesendeten Rumpf.').toBe(titel)

    await page.reload({ waitUntil: 'networkidle' })
    await page.waitForTimeout(1200)
    await expect(page.locator('main').last(),
      'Der Eintrag wurde abgeschickt, ist nach dem Neuladen aber nicht da.')
      .toContainText(titel)
  })

  /* ---------------------------------------------------------------- */
  /* Rundweg: TentsPage                                                */
  /* ---------------------------------------------------------------- */

  test('Rundweg: TentsPage — Zelt anlegen und wiederfinden', async ({ page }) => {
    darfUeberspringen(!await backendDa(page), 'Kein Backend — siehe oben.')

    const name = marke()
    await page.goto('/zelte/new', { waitUntil: 'networkidle' })
    const formular = page.locator('form.rc2-tent-form')
    await expect(formular).toBeVisible()

    await formular.getByLabel('Name', { exact: true }).first().fill(name)
    await formular.getByLabel('Breite cm', { exact: true }).fill('100')

    const anfrage = page.waitForRequest((r) => r.method() === 'POST' && /\/api\/settings\/tents/.test(r.url()))
    await formular.getByRole('button', { name: 'Speichern' }).click()

    const rumpf = JSON.parse((await anfrage).postData() ?? '{}')
    expect(rumpf.name).toBe(name)
    expect(rumpf.widthCm, 'Die getippte Breite steht nicht im gesendeten Rumpf.').toBe(100)

    await page.goto('/zelte', { waitUntil: 'networkidle' })
    await page.waitForTimeout(1000)
    await expect(page.locator('main').last(),
      'Das Zelt wurde abgeschickt, steht nach dem Neuladen aber nicht in der Liste.')
      .toContainText(name)
  })
})
