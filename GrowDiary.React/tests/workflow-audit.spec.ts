import { expect, test, type APIRequestContext, type Locator } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

const repoRoot = path.resolve(process.cwd(), '..')
const outputDir = path.join(repoRoot, 'artifacts', 'workflow-audit-current')
const backendUrl = (process.env.GROW_OS_BACKEND_URL ?? 'http://127.0.0.1:5076').replace(/\/$/, '')
const workflowTentName = 'E2E Workflow Audit Zelt'
const workflowHydroName = 'E2E Workflow Audit RDWC'
const workflowGrowName = 'E2E Workflow Audit Grow'
const workflowManageHydroName = 'E2E Workflow Manage RDWC'
const workflowManageGrowName = 'E2E Workflow Manage Grow'
const workflowHardwareName = 'E2E Workflow Audit pH Sensor'
const workflowDeleteHardwareName = 'E2E Workflow Delete Sensor'
const workflowDeleteTentName = 'E2E Workflow Delete Zelt'

type LayoutFinding = {
  tag: string
  selector: string
  text: string
  horizontalClip: boolean
  outOfViewport: boolean
  coveredByBottomNav: boolean
  tooCloseToBottomNav: boolean
  width: number
  left: number
  right: number
  top: number
  bottom: number
  bottomNavTop?: number | null
  bottomNavGap?: number | null
  scrollWidth: number
  clientWidth: number
}

type WorkflowStepReport = {
  name: string
  path: string
  url: string
  heading: string | null
  activeWizardStep: number | null
  bodyOverflow: boolean
  documentOverflow: boolean
  hardOffenders: LayoutFinding[]
  clipWarnings: LayoutFinding[]
}

type WorkflowTent = {
  id: number
  name: string
  status?: string | null
}

type WorkflowHydroSetup = {
  id: number
  tentId: number | null
  name: string
  status?: string | null
}

const workflowReports: WorkflowStepReport[] = []

test.beforeAll(() => {
  fs.rmSync(outputDir, { recursive: true, force: true })
  fs.mkdirSync(outputDir, { recursive: true })
})

test.afterAll(() => {
  fs.mkdirSync(outputDir, { recursive: true })
  fs.writeFileSync(path.join(outputDir, 'workflow-audit-report.json'), JSON.stringify({ generatedAtUtc: new Date().toISOString(), results: workflowReports }, null, 2), 'utf8')

  const lines = [
    '# Grow OS Workflow Audit',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
    '| Step | Path | Heading | Wizard | Body Overflow | Document Overflow | Hard Offenders | Clip Warnings |',
    '|---|---|---|---:|---|---|---:|---:|',
    ...workflowReports.map((row) => `| ${row.name} | ${row.path} | ${row.heading ?? ''} | ${row.activeWizardStep ?? ''} | ${row.bodyOverflow ? 'YES' : 'no'} | ${row.documentOverflow ? 'YES' : 'no'} | ${row.hardOffenders.length} | ${row.clipWarnings.length} |`),
  ]
  fs.writeFileSync(path.join(outputDir, 'workflow-audit-report.md'), lines.join('\n'), 'utf8')
})

test.describe.configure({ mode: 'serial' })

test.describe('workflow audit mobile', () => {
  test.beforeEach(async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })
  })

  test('walk focused create workflows without saving', async ({ page }) => {
    await ensureHydroSetupForWorkflowAudit(page.request)
    await auditRoute(page, '/zelte/new', 'mobile-zelt-new')
    await fillIfVisible(page, 'input[placeholder="Hauptzelt"]', `E2E Zelt ${Date.now()}`)
    await screenshotAndLayout(page, 'mobile-zelt-new-filled')

    await auditRoute(page, '/hydro/new', 'mobile-hydro-step-1')
    await fillIfVisible(page, 'input[placeholder="RDWC 4-Site"]', `E2E RDWC ${Date.now()}`)
    await selectFirstNonEmptyOption(page, 'select')
    await expectWizardStep(page, 1, 'hydro initial')
    await clickNextAndExpectStep(page, 2, 'hydro step 2')
    await screenshotAndLayout(page, 'mobile-hydro-step-2')
    await clickNextAndExpectStep(page, 3, 'hydro step 3')
    await assertHydroLayoutControls(page)
    await screenshotAndLayout(page, 'mobile-hydro-step-3')
    await clickNextAndExpectStep(page, 4, 'hydro step 4')
    await screenshotAndLayout(page, 'mobile-hydro-step-4')
    await clickNextAndExpectStep(page, 5, 'hydro step 5')
    await expect(page.locator('[data-audit="hydro-preview"]').filter({ hasText: /2×3 · Tank rechts/i })).toBeVisible()
    await expect(page.getByText(/Tankposition/i)).toBeVisible()
    await expect(page.locator('.v1-info').filter({ hasText: /Tankposition/i }).filter({ hasText: /rechts/i })).toBeVisible()
    await screenshotAndLayout(page, 'mobile-hydro-step-5')

    await ensureHydroSetupForWorkflowAudit(page.request)
    await auditRoute(page, '/grows/new', 'mobile-grow-step-1')
    await fillIfVisible(page, 'input[placeholder="Purple Lemonade RDWC"]', `E2E Grow ${Date.now()}`)
    await expectWizardStep(page, 1, 'grow initial')

    await clickNextAndExpectStep(page, 2, 'grow step 2')
    await selectGrowTent(page)
    await screenshotAndLayout(page, 'mobile-grow-step-2')

    await clickNextAndExpectStep(page, 3, 'grow step 3')
    await selectGrowHydro(page)
    await screenshotAndLayout(page, 'mobile-grow-step-3')

    await clickNextAndExpectStep(page, 4, 'grow step 4')
    await screenshotAndLayout(page, 'mobile-grow-step-4')

    await clickNextAndExpectStep(page, 5, 'grow step 5')
    await selectProgramIfPresent(page)
    await screenshotAndLayout(page, 'mobile-grow-step-5')

    await clickNextAndExpectStep(page, 6, 'grow step 6')
    await screenshotAndLayout(page, 'mobile-grow-step-6')
  })
})

test.describe('workflow audit desktop', () => {
  test.beforeEach(async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 1000 })
  })

  test('walk admin and mapping workflows', async ({ page }) => {
    const workflowGrowId = await ensureHydroSetupForWorkflowAudit(page.request)
    const manageGrowId = await ensureManageGrowForWorkflowAudit(page.request)
    await ensureHardwareForWorkflowAudit(page.request)
    await auditRoute(page, '/aufgaben', 'desktop-aufgaben')
    await assertActionPage(page)
    await auditRoute(page, '/home-assistant', 'desktop-ha-setup')
    await auditRoute(page, '/wissen', 'desktop-knowledge')
    await assertKnowledgePage(page)
    await auditRoute(page, '/release', 'desktop-release')
    await assertFileInput(page)
    await auditRoute(page, '/settings', 'desktop-settings')
    await assertSettingsPage(page)
    await auditRoute(page, '/connect', 'desktop-connect')
    await assertConnectPage(page)
    await assertHardwareEditFlow(page)
    await assertHardwareDeleteFlow(page)
    await assertGrowsOverview(page, workflowGrowId)
    await assertOpenDoesNotNotFound(page, '/hydro', 'hydro-open')
    await assertHydroBlockedDeleteShowsGrowLinks(page)
    await assertOpenDoesNotNotFound(page, '/zelte', 'tent-open')
    await assertEmptyTentDeleteFlow(page)
    await assertTentBlockedDeleteShowsDependencyLinks(page)
    await assertGrowManagementFlow(page, manageGrowId)
    await auditRoute(page, `/grows/${workflowGrowId}/addback`, 'desktop-addback-flow')
    await expect(page.locator('[data-audit="addback-stepper"]')).toBeVisible()
  })
})

async function ensureHydroSetupForWorkflowAudit(request: APIRequestContext) {
  const [tents, hydroSetups] = await Promise.all([
    apiJson<WorkflowTent[]>(request, 'GET', '/api/settings/tents?includeArchived=true'),
    apiJson<WorkflowHydroSetup[]>(request, 'GET', '/api/hydro-setups?includeArchived=true'),
  ])

  const activeTents = tents.filter((tent) => tent.status !== 'Archived')
  const activeTentIds = new Set(activeTents.map((tent) => tent.id))
  let tent = activeTents.find((item) => item.name === workflowTentName)
  if (!tent) {
    tent = await apiJson<WorkflowTent>(request, 'POST', '/api/settings/tents', {
      name: workflowTentName,
      kind: 'Grow Tent',
      tentType: 'Production',
      status: 'Active',
      notes: 'Automatisch angelegte Testdaten fuer Playwright Workflow Audit',
      displayOrder: 9001,
      accentColor: '#22c55e',
      widthCm: 120,
      depthCm: 120,
      tentHeightCm: 200,
      lightType: 'LED',
      lightWatt: 400,
      lightController: null,
      lightControllerEntityId: null,
      exhaustFanCount: 1,
      exhaustM3h: 400,
      circulationFanCount: 2,
      hvacController: null,
      hvacControllerEntityId: null,
      co2Available: false,
      cameraEntityId: null,
      sensors: [],
    })
  }

  const existingHydros = hydroSetups.filter((setup) => setup.status !== 'Archived' && setup.name === workflowHydroName && (setup.tentId == null || activeTentIds.has(setup.tentId)))
  if (existingHydros.length > 0) {
    const growIds = await Promise.all(existingHydros.map((setup) => ensureGrowForWorkflowAudit(request, setup.tentId ?? tent.id, setup.id)))
    return growIds[0]
  }

  const hydro = await apiJson<WorkflowHydroSetup>(request, 'POST', '/api/hydro-setups', {
    tentId: tent.id,
    name: workflowHydroName,
    hydroStyle: 'RDWC',
    potCount: 4,
    potSizeLiters: 19,
    reservoirLiters: 60,
    layoutType: 'Grid2x2',
    reservoirPosition: 'Left',
    hasCirculationPump: true,
    circulationPumpNotes: null,
    hasAirPump: true,
    airPumpNotes: null,
    airStoneCount: 4,
    hasChiller: false,
    hasUvSterilizer: false,
    notes: 'Automatisch angelegte Testdaten fuer Playwright Workflow Audit',
    displayOrder: 9001,
  })

  return await ensureGrowForWorkflowAudit(request, tent.id, hydro.id)
}

async function ensureGrowForWorkflowAudit(request: APIRequestContext, tentId: number, hydroSetupId: number) {
  const grows = await apiJson<Array<{ id: number; name: string; status?: string | null; systemId?: number | null; setupId?: number | null }>>(request, 'GET', '/api/grows?archived=false')
  const existing = grows.find((grow) => grow.name === workflowGrowName && (grow.systemId === hydroSetupId || grow.setupId === hydroSetupId) && grow.status !== 'Archived')
  if (existing) {
    await ensureDeviationMeasurementsForWorkflowAudit(request, existing.id)
    return existing.id
  }

  const created = await apiJson<{ id: number }>(request, 'POST', '/api/grows', {
    name: workflowGrowName,
    tentId,
    systemId: hydroSetupId,
    setupId: null,
    hydroStyle: 'RDWC',
    startDate: '2026-01-01',
    status: 'Running',
    environment: 'Indoor',
    seedType: 'Feminized',
    startMaterial: 'Seed',
    waterSource: 'RO',
  })
  await ensureDeviationMeasurementsForWorkflowAudit(request, created.id)
  return created.id
}

async function ensureDeviationMeasurementsForWorkflowAudit(request: APIRequestContext, growId: number) {
  const measurements = await apiJson<Array<{ id: number; takenAt: string; reservoirEc: number | null; orpMv: number | null; reservoirWaterTempC: number | null }>>(request, 'GET', `/api/grows/${growId}/measurements`)
  const hasAuditDeviation = measurements.some((measurement) =>
    measurement.takenAt.startsWith('2026-05-20') &&
    measurement.reservoirEc === 3.2 &&
    measurement.orpMv === 700 &&
    measurement.reservoirWaterTempC === 25)
  if (hasAuditDeviation) return

  await apiJson(request, 'POST', `/api/grows/${growId}/measurements`, {
    takenAtLocal: '2026-05-20T12:00',
    stage: 'Veg',
    source: 'Manual',
    notes: 'Workflow-Audit Deviation-Seed',
    airTemperatureC: 25,
    humidityPercent: 60,
    heightCm: null,
    waterAmountMl: null,
    runoffAmountMl: null,
    irrigationPh: null,
    irrigationEc: null,
    drainPh: null,
    drainEc: null,
    reservoirPh: 6.0,
    reservoirEc: 3.2,
    reservoirWaterTempC: 25,
    reservoirLevelCm: null,
    reservoirLevelLiters: null,
    dissolvedOxygenMgL: 8,
    orpMv: 700,
    topOffLiters: null,
    addbackEc: null,
    solutionChange: false,
    ppfdMol: null,
    co2Ppm: null,
  })
}

async function ensureManageGrowForWorkflowAudit(request: APIRequestContext) {
  await ensureHydroSetupForWorkflowAudit(request)
  const [tents, hydroSetups] = await Promise.all([
    apiJson<WorkflowTent[]>(request, 'GET', '/api/settings/tents?includeArchived=true'),
    apiJson<WorkflowHydroSetup[]>(request, 'GET', '/api/hydro-setups?includeArchived=true'),
  ])
  const tent = tents.find((item) => item.name === workflowTentName && item.status !== 'Archived') ?? tents.find((item) => item.status !== 'Archived')
  if (!tent) throw new Error('Workflow-Audit braucht ein aktives Zelt fuer Grow-Verwaltung.')

  const activeTentIds = new Set(tents.filter((item) => item.status !== 'Archived').map((item) => item.id))
  let hydro = hydroSetups.find((setup) => setup.name === workflowManageHydroName && setup.status !== 'Archived' && (setup.tentId == null || activeTentIds.has(setup.tentId)))
  if (!hydro) {
    hydro = await apiJson<WorkflowHydroSetup>(request, 'POST', '/api/hydro-setups', {
      tentId: tent.id,
      name: workflowManageHydroName,
      hydroStyle: 'RDWC',
      potCount: 2,
      potSizeLiters: 19,
      reservoirLiters: 45,
      layoutType: 'Row',
      reservoirPosition: 'Left',
      hasCirculationPump: true,
      circulationPumpNotes: null,
      hasAirPump: true,
      airPumpNotes: null,
      airStoneCount: 2,
      hasChiller: false,
      hasUvSterilizer: false,
      notes: 'Automatisch angelegte Testdaten fuer Grow-Verwaltung',
      displayOrder: 9002,
    })
  }

  const grows = await apiJson<Array<{ id: number; name: string; status?: string | null; systemId?: number | null; setupId?: number | null }>>(request, 'GET', '/api/grows?archived=false')
  const existing = grows.find((grow) => grow.name === workflowManageGrowName && (grow.systemId === hydro.id || grow.setupId === hydro.id))
  if (existing) return existing.id
  const growTentId = hydro.tentId ?? tent.id

  const created = await apiJson<{ id: number }>(request, 'POST', '/api/grows', {
    name: workflowManageGrowName,
    tentId: growTentId,
    systemId: hydro.id,
    setupId: null,
    hydroStyle: 'RDWC',
    startDate: '2026-01-02',
    status: 'Running',
    environment: 'Indoor',
    seedType: 'Feminized',
    startMaterial: 'Seed',
    waterSource: 'RO',
  })
  return created.id
}

async function ensureHardwareForWorkflowAudit(request: APIRequestContext) {
  await ensureHydroSetupForWorkflowAudit(request)
  const items = await apiJson<Array<{ id: number; name: string }>>(request, 'GET', '/api/hardware-items')
  const existing = items.find((item) => item.name === workflowHardwareName)
  if (existing) return existing.id

  const [tents, hydroSetups] = await Promise.all([
    apiJson<WorkflowTent[]>(request, 'GET', '/api/settings/tents?includeArchived=true'),
    apiJson<WorkflowHydroSetup[]>(request, 'GET', '/api/hydro-setups?includeArchived=true'),
  ])
  const activeTentIds = new Set(tents.filter((item) => item.status !== 'Archived').map((item) => item.id))
  const hydro = hydroSetups.find((setup) => setup.name === workflowHydroName && setup.status !== 'Archived' && (setup.tentId == null || activeTentIds.has(setup.tentId))) ?? null
  const tent = hydro?.tentId != null
    ? tents.find((item) => item.id === hydro.tentId && item.status !== 'Archived')
    : tents.find((item) => item.name === workflowTentName && item.status !== 'Archived') ?? tents.find((item) => item.status !== 'Archived')
  if (!tent) throw new Error('Workflow-Audit braucht ein aktives Zelt fuer Sensor-Testdaten.')

  const created = await apiJson<{ id: number }>(request, 'POST', '/api/hardware-items', {
    name: workflowHardwareName,
    category: 'pH Sensor',
    status: 'Active',
    criticality: 'High',
    tentId: tent.id,
    setupId: null,
    hydroSetupId: hydro?.id ?? null,
    growId: null,
    wearTemplateId: null,
    tentSensorId: null,
    haEntityId: 'sensor.e2e_workflow_ph',
    manufacturer: 'E2E',
    model: 'Probe A',
    serialNumber: 'E2E-PH-001',
    installedAtUtc: '2026-01-01T00:00:00.000Z',
    retiredAtUtc: null,
    expectedLifespanDays: null,
    inspectionIntervalDays: null,
    notes: 'Automatisch angelegte Testdaten fuer Sensor-Bearbeiten',
  })
  return created.id
}

async function ensureDeletableHardwareForWorkflowAudit(request: APIRequestContext) {
  const items = await apiJson<Array<{ id: number; name: string }>>(request, 'GET', '/api/hardware-items')
  const existing = items.find((item) => item.name === workflowDeleteHardwareName)
  if (existing) return existing.id

  const created = await apiJson<{ id: number }>(request, 'POST', '/api/hardware-items', {
    name: workflowDeleteHardwareName,
    category: 'pH Sensor',
    status: 'Active',
    criticality: 'Medium',
    tentId: null,
    setupId: null,
    hydroSetupId: null,
    growId: null,
    wearTemplateId: null,
    tentSensorId: null,
    haEntityId: 'sensor.e2e_delete_sensor',
    manufacturer: 'E2E',
    model: 'Delete Probe',
    serialNumber: 'E2E-DELETE-SENSOR',
    installedAtUtc: '2026-01-01T00:00:00.000Z',
    retiredAtUtc: null,
    expectedLifespanDays: null,
    inspectionIntervalDays: null,
    notes: 'Automatisch angelegte Testdaten für Sensor-Löschen',
  })
  return created.id
}

async function ensureDeletableTentForWorkflowAudit(request: APIRequestContext) {
  const tents = await apiJson<WorkflowTent[]>(request, 'GET', '/api/settings/tents?includeArchived=true')
  const existing = tents.find((item) => item.name === workflowDeleteTentName && item.status !== 'Archived')
  if (existing) return existing.id

  const created = await apiJson<WorkflowTent>(request, 'POST', '/api/settings/tents', {
    name: workflowDeleteTentName,
    kind: 'Grow Tent',
    tentType: 'Production',
    status: 'Active',
    notes: 'Automatisch angelegte Testdaten für Zelt-Löschen',
    displayOrder: 9002,
    accentColor: '#22c55e',
    widthCm: 80,
    depthCm: 80,
    tentHeightCm: 160,
    lightType: 'LED',
    lightWatt: 120,
    lightController: null,
    lightControllerEntityId: null,
    exhaustFanCount: 0,
    exhaustM3h: null,
    circulationFanCount: 0,
    hvacController: null,
    hvacControllerEntityId: null,
    co2Available: false,
    cameraEntityId: null,
    sensors: [],
  })
  return created.id
}

async function apiJson<T>(request: APIRequestContext, method: 'GET' | 'POST' | 'DELETE', pathName: string, data?: unknown): Promise<T> {
  const response = await request.fetch(`${backendUrl}${pathName}`, {
    method,
    data,
    headers: data == null ? undefined : { 'Content-Type': 'application/json' },
  })

  if (!response.ok()) {
    const body = await response.text().catch(() => '')
    throw new Error(`Workflow-Audit Testdaten API fehlgeschlagen: ${method} ${pathName} -> ${response.status()} ${body}`)
  }

  if (response.status() === 204) return undefined as T
  return await response.json() as T
}

async function auditRoute(page: import('@playwright/test').Page, url: string, name: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  await screenshotAndLayout(page, name)
}

async function assertSettingsPage(page: import('@playwright/test').Page) {
  await expect(page.getByRole('button', { name: /Vollbackup herunterladen/i })).toBeVisible()
  await assertFileInput(page)
}

async function assertFileInput(page: import('@playwright/test').Page) {
  await expect(page.locator('.rc-file-input').first()).toBeVisible()
}

async function assertConnectPage(page: import('@playwright/test').Page) {
  await expect(page.getByRole('button', { name: /^(Addback|Messung|HA)$/i })).toHaveCount(0)
  const frontendOrigin = await page.evaluate(() => window.location.origin)
  const network = await apiJson<{ recommendedBaseUrl: string }>(page.request, 'GET', `/api/system/network?frontendOrigin=${encodeURIComponent(frontendOrigin)}`)
  if (network.recommendedBaseUrl && !network.recommendedBaseUrl.includes('127.0.0.1')) {
    await expect(page.getByText(`${network.recommendedBaseUrl.replace(/\/$/, '')}/`, { exact: true })).toBeVisible()
  }
}

async function assertHardwareEditFlow(page: import('@playwright/test').Page) {
  await page.goto('/hardware', { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  await page.getByRole('button', { name: /Inventar/i }).click()
  const card = page.locator('.v1-card').filter({ hasText: workflowHardwareName }).first()
  await expect(card).toBeVisible()
  await card.getByRole('button', { name: /Bearbeiten/i }).click()
  const form = page.locator('[data-audit="hardware-edit-form"]')
  await expect(form).toBeVisible()
  await form.getByLabel(/Modell/i).fill('Probe B')
  await form.getByRole('button', { name: /^Speichern$/i }).click()
  await expect(page.getByText(/Sensor gespeichert/i)).toBeVisible()
  await page.reload({ waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  await page.getByRole('button', { name: /Inventar/i }).click()
  await expect(page.locator('.v1-card').filter({ hasText: workflowHardwareName }).filter({ hasText: 'Probe B' }).first()).toBeVisible()
  await screenshotAndLayout(page, 'hardware-edit-flow')
}

async function assertGrowsOverview(page: import('@playwright/test').Page, growId: number) {
  await page.goto('/grows', { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  await expect(page.getByRole('heading', { name: /^Grows$/i })).toBeVisible()
  await expect(page.getByRole('link', { name: /Neuen Grow anlegen/i })).toBeVisible()
  await expect(page.locator('.v1-desktop-nav, .v1-mobile-more-panel').getByText(/^Grow starten$/)).toHaveCount(0)
  const card = page.locator('.grow-overview-card').filter({ hasText: workflowGrowName }).first()
  await expect(card).toBeVisible()
  await expect(card.getByText(workflowHydroName).first()).toBeVisible()
  await expect(card.getByRole('link', { name: /^Öffnen$/i })).toHaveAttribute('href', `/grows/${growId}`)
  await expect(card.getByRole('link', { name: /^Bearbeiten$/i })).toHaveAttribute('href', `/grows/${growId}/setup`)
  await expect(card.getByRole('button', { name: /^Beenden$/i })).toBeVisible()
  await expect(card.getByRole('button', { name: /^Löschen$/i })).toBeVisible()
  await card.getByRole('link', { name: /^Öffnen$/i }).click()
  await waitForAppIdle(page)
  await expect(page).toHaveURL(new RegExp(`/grows/${growId}$`))
  await expect(page.getByRole('heading', { name: /Nicht gefunden/i })).toHaveCount(0)
  await expect(page.locator('.grow-hero-sub').filter({ hasText: workflowHydroName }).first()).toBeVisible()
  await screenshotAndLayout(page, 'grows-overview')
}

async function assertHydroLayoutControls(page: import('@playwright/test').Page) {
  const preview = page.locator('[data-audit="hydro-preview"]').first()
  await expect(preview).toBeVisible()
  await page.locator('[data-audit="hydro-pot-count"]').fill('6')
  await page.locator('[data-audit="hydro-layout-select"]').selectOption('Grid2x3')
  await page.locator('[data-audit="hydro-reservoir-select"]').selectOption('Right')
  await expect(preview).toContainText('2×3 · Tank rechts')
  await assertRdwcTankSide(preview, 'right')
  await page.locator('[data-audit="hydro-reservoir-select"]').selectOption('Left')
  await expect(preview).toContainText('2×3 · Tank links')
  await assertRdwcTankSide(preview, 'left')
  await page.locator('[data-audit="hydro-reservoir-select"]').selectOption('Right')
  await expect(preview).toContainText('2×3 · Tank rechts')
  await assertRdwcTankSide(preview, 'right')
}

async function assertRdwcTankSide(preview: Locator, side: 'left' | 'right') {
  const placement = await preview.evaluate((element) => {
    const tank = element.querySelector('.rdwc-preview__tank') as HTMLElement | null
    const sites = element.querySelector('.rdwc-preview__sites') as HTMLElement | null
    if (!tank || !sites) return null
    const tankRect = tank.getBoundingClientRect()
    const sitesRect = sites.getBoundingClientRect()
    return {
      tankLeft: tankRect.left,
      tankRight: tankRect.right,
      sitesLeft: sitesRect.left,
      sitesRight: sitesRect.right,
    }
  })
  expect(placement, `RDWC preview needs tank and site grid for ${side} placement`).not.toBeNull()
  if (side === 'right') {
    expect(placement!.tankLeft, 'Tankposition rechts must render tank right of the site grid').toBeGreaterThanOrEqual(placement!.sitesRight - 1)
  } else {
    expect(placement!.tankRight, 'Tankposition links must render tank left of the site grid').toBeLessThanOrEqual(placement!.sitesLeft + 1)
  }
}

async function assertHardwareDeleteFlow(page: import('@playwright/test').Page) {
  await ensureDeletableHardwareForWorkflowAudit(page.request)
  await page.goto('/hardware', { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  await page.getByRole('button', { name: /Inventar/i }).click()
  const card = page.locator('.v1-card').filter({ hasText: workflowDeleteHardwareName }).first()
  await expect(card).toBeVisible()
  page.once('dialog', (dialog) => void dialog.accept())
  await card.getByRole('button', { name: /^Löschen$/i }).click()
  await expect(page.getByText(/Sensor gelöscht/i)).toBeVisible()
  await page.reload({ waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  await page.getByRole('button', { name: /Inventar/i }).click()
  await expect(page.locator('.v1-card').filter({ hasText: workflowDeleteHardwareName })).toHaveCount(0)
  await screenshotAndLayout(page, 'hardware-delete-flow')
}

async function assertEmptyTentDeleteFlow(page: import('@playwright/test').Page) {
  await ensureDeletableTentForWorkflowAudit(page.request)
  await page.goto('/zelte', { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  const card = page.locator('.v1-tent-card').filter({ hasText: workflowDeleteTentName }).first()
  await expect(card).toBeVisible()
  page.once('dialog', (dialog) => void dialog.accept())
  await card.getByRole('button', { name: /^Löschen$/i }).click()
  await expect(card).toHaveCount(0)
  await page.reload({ waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  await expect(page.locator('.v1-tent-card').filter({ hasText: workflowDeleteTentName })).toHaveCount(0)
  await screenshotAndLayout(page, 'tent-delete-empty-flow')
}

async function assertGrowManagementFlow(page: import('@playwright/test').Page, growId: number) {
  await page.goto(`/grows/${growId}`, { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  const actions = page.locator('[data-audit="grow-management-actions"]')
  await expect(actions).toBeVisible()
  await expect(actions.getByRole('link', { name: /Bearbeiten/i })).toBeVisible()
  await actions.getByRole('link', { name: /Bearbeiten/i }).click()
  await waitForAppIdle(page)
  await expect(page).toHaveURL(new RegExp(`/grows/${growId}/setup$`))
  await page.goto(`/grows/${growId}`, { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  page.once('dialog', (dialog) => void dialog.accept())
  await page.locator('[data-audit="grow-management-actions"]').getByRole('button', { name: /Beenden/i }).click()
  await expect(page.getByText(/Grow beendet und archiviert/i)).toBeVisible()
  await page.reload({ waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  await expect(page.locator('.badge').filter({ hasText: /Completed/i }).first()).toBeVisible()
  await screenshotAndLayout(page, 'grow-management-archived')
}

async function assertActionPage(page: import('@playwright/test').Page) {
  await expect(page.locator('.rc-action-guide-card')).toHaveCount(4)
  await expect(page.getByRole('heading', { name: /Addback berechnen/i })).toBeVisible()
  await expect(page.getByRole('heading', { name: /Werte dokumentieren/i })).toBeVisible()
  await expect(page.getByRole('heading', { name: /Sensoren prüfen/i })).toBeVisible()
  await expect(page.getByRole('heading', { name: /HA-Mapping prüfen/i })).toBeVisible()
  const actionRows = page.locator('[data-audit="open-action-row"]')
  await expect(actionRows.first()).toBeVisible()
  await expect(actionRows.filter({ hasText: /Ec|EC/i }).first()).toBeVisible()
  await expect(actionRows.filter({ hasText: /Orp|ORP/i }).first()).toBeVisible()
  await expect(actionRows.filter({ hasText: /WaterTemp|Wassertemperatur/i }).first()).toBeVisible()
  await expect(actionRows.filter({ hasText: /Critical/i }).first()).toBeVisible()
  await page.reload({ waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  const rowsAfterReload = page.locator('[data-audit="open-action-row"]')
  await expect(rowsAfterReload.filter({ hasText: /Ec|EC/i }).first()).toBeVisible()
  await expect(rowsAfterReload.filter({ hasText: /Orp|ORP/i }).first()).toBeVisible()
  await expect(rowsAfterReload.filter({ hasText: /WaterTemp|Wassertemperatur/i }).first()).toBeVisible()
}

async function assertKnowledgePage(page: import('@playwright/test').Page) {
  await expect(page.locator('[data-audit="knowledge-search"]')).toBeVisible()
  await expect(page.locator('[data-audit="knowledge-topic-nav"]')).toBeVisible()
  await expect(page.locator('[data-audit="knowledge-article"]')).toBeVisible()
}

async function assertOpenDoesNotNotFound(page: import('@playwright/test').Page, url: string, name: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  const openLink = page.getByRole('link', { name: /^Öffnen$/i }).first()
  await expect(openLink, `${name}: Öffnen-Link fehlt`).toBeVisible()
  await openLink.click()
  await waitForAppIdle(page)
  await expect(page.getByRole('heading', { name: /Nicht gefunden/i })).toHaveCount(0)
  await screenshotAndLayout(page, name)
}

async function assertHydroBlockedDeleteShowsGrowLinks(page: import('@playwright/test').Page) {
  await page.goto('/hydro', { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  await page.getByRole('button', { name: new RegExp(workflowHydroName, 'i') }).first().click()
  await page.getByRole('button', { name: /L.schen/i }).click()
  const panel = page.locator('[data-audit="hydro-delete-blocked"]:visible').filter({ hasText: workflowGrowName }).first()
  const growRow = panel.locator('.dependency-row').filter({ hasText: workflowGrowName }).first()
  await expect(panel).toBeVisible()
  await expect(growRow).toBeVisible()
  await expect(growRow.getByRole('link', { name: /Verwalten|Öffnen/i })).toBeVisible()
  await expect(growRow.getByRole('link', { name: /Bearbeiten/i })).toBeVisible()
  await expect(growRow.getByRole('button', { name: /Beenden/i })).toBeVisible()
  await resetScrollForLayoutCheck(page)
  await screenshotAndLayout(page, 'hydro-delete-blocked')
  await growRow.getByRole('link', { name: /Verwalten|Öffnen/i }).first().click()
  await waitForAppIdle(page)
  await expect(page.locator('[data-audit="grow-management-actions"]')).toBeVisible()
  await expect(page.getByRole('heading', { name: /Nicht gefunden/i })).toHaveCount(0)
}

async function assertTentBlockedDeleteShowsDependencyLinks(page: import('@playwright/test').Page) {
  await page.goto('/zelte', { waitUntil: 'domcontentloaded' })
  await waitForAppIdle(page)
  const card = page.locator('.v1-tent-card').filter({ hasText: workflowTentName }).first()
  await expect(card).toBeVisible()
  page.once('dialog', (dialog) => void dialog.accept())
  await card.getByRole('button', { name: /^Löschen$/i }).click()
  const panel = card.locator('[data-audit="tent-delete-blocked"]:visible').filter({ hasText: workflowGrowName }).first()
  const growRow = panel.locator('.dependency-row').filter({ hasText: workflowGrowName }).first()
  await expect(panel).toBeVisible()
  await expect(growRow).toBeVisible()
  await expect(panel.getByText(workflowHydroName).first()).toBeVisible()
  await expect(growRow.getByRole('link', { name: /Verwalten|Öffnen/i }).first()).toBeVisible()
  await expect(growRow.getByRole('link', { name: /Bearbeiten/i }).first()).toBeVisible()
  await expect(growRow.getByRole('button', { name: /Beenden/i }).first()).toBeVisible()
  await resetScrollForLayoutCheck(page)
  await screenshotAndLayout(page, 'tent-delete-blocked')
  await growRow.getByRole('link', { name: /Verwalten|Öffnen/i }).first().click()
  await waitForAppIdle(page)
  await expect(page.locator('[data-audit="grow-management-actions"]')).toBeVisible()
  await expect(page.getByRole('heading', { name: /Nicht gefunden/i })).toHaveCount(0)
}

async function resetScrollForLayoutCheck(page: import('@playwright/test').Page) {
  await page.evaluate(() => {
    window.scrollTo(0, 0)
    document.scrollingElement?.scrollTo(0, 0)
    document.querySelectorAll<HTMLElement>('*').forEach((element) => {
      element.scrollTo(0, 0)
    })
  })
}

async function waitForAppIdle(page: import('@playwright/test').Page) {
  await page.waitForTimeout(850)
  try {
    await page.waitForLoadState('networkidle', { timeout: 2500 })
  } catch {
    // Live-/HA-Requests dürfen laufen.
  }
}

async function fillIfVisible(page: import('@playwright/test').Page, selector: string, value: string) {
  const locator = page.locator(selector).first()
  if ((await locator.count()) === 0) return

  try {
    if (await locator.isVisible({ timeout: 1000 })) {
      await locator.fill(value, { timeout: 1200 })
    }
  } catch {
    // Optionaler Workflow-Schritt. Der Audit soll die Seite prüfen, nicht Testdaten erzwingen.
  }
}

async function selectFirstNonEmptyOption(page: import('@playwright/test').Page, selector: string) {
  const locator = page.locator(selector).first()
  if ((await locator.count()) === 0) return

  const values = await locator.locator('option').evaluateAll((options) => options.map((option) => (option as HTMLOptionElement).value).filter(Boolean))
  if (values.length === 0) {
    throw new Error(`Keine auswählbare Option für ${selector}. Für diesen Workflow fehlen Testdaten.`)
  }

  await locator.selectOption(values[0], { timeout: 1200 })
}

async function clickNextAndExpectStep(page: import('@playwright/test').Page, expectedStep: number, label: string) {
  const next = page.getByRole('button', { name: /^Weiter$/i }).first()
  if ((await next.count()) === 0) {
    throw new Error(`${label}: Weiter-Button nicht gefunden.`)
  }

  if (!(await next.isVisible({ timeout: 1500 })) || !(await next.isEnabled({ timeout: 1500 }))) {
    throw new Error(`${label}: Weiter-Button ist nicht sichtbar oder nicht aktiv.`)
  }

  await next.click({ timeout: 2000 })
  await waitForAppIdle(page)
  await expectWizardStep(page, expectedStep, label)
}

async function expectWizardStep(page: import('@playwright/test').Page, expectedStep: number, label: string) {
  const activeStep = await getActiveWizardStep(page)
  if (activeStep !== expectedStep) {
    const alertText = await page.locator('.v1-alert, [role="alert"]').first().textContent({ timeout: 500 }).catch(() => null)
    throw new Error(`${label}: Wizard ist nicht auf Schritt ${expectedStep}, sondern auf Schritt ${activeStep ?? 'unbekannt'}.${alertText ? ` Hinweis: ${alertText.trim()}` : ''}`)
  }
}

async function getActiveWizardStep(page: import('@playwright/test').Page) {
  return await page.evaluate(() => {
    const steps = Array.from(document.querySelectorAll('.v1-wizard-step'))
    const activeIndex = steps.findIndex((element) => element.classList.contains('active'))
    return activeIndex >= 0 ? activeIndex + 1 : null
  })
}

async function selectGrowTent(page: import('@playwright/test').Page) {
  const cards = page.locator('.grow-select-card')
  const count = await cards.count()
  if (count === 0) {
    throw new Error('Grow Schritt Zelt: keine Zelt-Karte gefunden. Für den Workflow braucht die DB mindestens ein Zelt.')
  }

  const target = cards.filter({ hasText: workflowTentName }).first()
  if ((await target.count()) > 0) {
    await target.scrollIntoViewIfNeeded()
    await target.click({ timeout: 1500 })
    await waitForAppIdle(page)
    return
  }

  const visibleCards = await cards.evaluateAll((elements) => elements.map((element) => (element.textContent ?? '').trim().replace(/\s+/g, ' ')).filter(Boolean))
  throw new Error(`Grow Schritt Zelt: Karte "${workflowTentName}" nicht gefunden. Sichtbare Karten: ${visibleCards.join(' | ') || 'keine'}`)
}

async function selectGrowHydro(page: import('@playwright/test').Page) {
  const cards = page.locator('.grow-select-card')
  const count = await cards.count()
  if (count === 0) {
    throw new Error('Grow Schritt Hydro: kein Hydro-Setup gefunden. Wähle im Testdatenstand ein Zelt mit aktivem Hydro-Setup oder lege vorher ein Setup an.')
  }

  const target = cards.filter({ hasText: workflowHydroName }).first()
  if ((await target.count()) > 0) {
    await target.scrollIntoViewIfNeeded()
    await target.click({ timeout: 1500 })
    await waitForAppIdle(page)
    return
  }

  const visibleCards = await cards.evaluateAll((elements) => elements.map((element) => (element.textContent ?? '').trim().replace(/\s+/g, ' ')).filter(Boolean))
  throw new Error(`Grow Schritt Hydro: Karte "${workflowHydroName}" nicht gefunden. Sichtbare Karten: ${visibleCards.join(' | ') || 'keine'}`)
}

async function selectProgramIfPresent(page: import('@playwright/test').Page) {
  const cards = page.locator('.program-card')
  const count = await cards.count()
  if (count > 0) {
    await cards.first().click({ timeout: 1500 })
    await waitForAppIdle(page)
    return
  }

  const customInput = page.locator('input[placeholder*="eigene"], input[placeholder*="Mischung"]').first()
  if ((await customInput.count()) > 0 && await customInput.isVisible({ timeout: 800 })) {
    await customInput.fill('E2E Programm', { timeout: 1200 })
  }
}

async function screenshotAndLayout(page: import('@playwright/test').Page, name: string) {
  const result = await page.evaluate(() => {
    function selectorFor(element: Element) {
      if (element.id) return `#${element.id}`
      const className = Array.from(element.classList).slice(0, 3).join('.')
      return `${element.tagName.toLowerCase()}${className ? `.${className}` : ''}`
    }

    function isBottomNavCriticalElement(element: HTMLElement) {
      const style = window.getComputedStyle(element)
      return (
        style.position === 'fixed' ||
        style.position === 'sticky' ||
        Boolean(element.closest('.sticky-actions, .ops1b-sticky-actions')) ||
        Boolean(element.matches('button, a, input, select, textarea, .v1-card, .v1-section, .v1-wizard-step, .v1-form-actions, .v1-action-row, .grow-select-card'))
      )
    }

    const bottomNav = document.querySelector('.v1-bottom-nav') as HTMLElement | null
    const bottomNavRect = bottomNav?.getBoundingClientRect() ?? null
    const bottomNavStyle = bottomNav ? window.getComputedStyle(bottomNav) : null

    const candidates = Array.from(document.querySelectorAll('button, a, input, select, textarea, .v1-tab, .v1-wizard-step, .v1-card, .v1-section'))
      .map((element) => {
        const html = element as HTMLElement
        const rect = html.getBoundingClientRect()
        const style = window.getComputedStyle(html)
        const sampleX = Math.min(Math.max(rect.left + Math.min(rect.width / 2, 24), 0), window.innerWidth - 1)
        const sampleY = Math.min(Math.max(rect.bottom - 2, 0), window.innerHeight - 1)
        const elementAtPoint = document.elementFromPoint(sampleX, sampleY)
        const pointVisible = Boolean(elementAtPoint && (html === elementAtPoint || html.contains(elementAtPoint)))
        const visible = style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 1 && rect.height > 1 && rect.bottom > 0 && rect.top < window.innerHeight && pointVisible
        const horizontalClip = html.scrollWidth > html.clientWidth + 8
        const outOfViewport = rect.left < -4 || rect.right > window.innerWidth + 4
        const insideBottomNav = Boolean(html.closest('.v1-bottom-nav'))
        const insideMobileMore = Boolean(html.closest('.v1-mobile-more-panel'))
        const intentionallyHidden = html.hasAttribute('hidden') || html.getAttribute('aria-hidden') === 'true'
        const criticalForBottomNav = isBottomNavCriticalElement(html)
        const bottomNavGap = bottomNavRect ? bottomNavRect.top - rect.bottom : null
        const coveredByBottomNav = Boolean(
          bottomNavRect &&
          criticalForBottomNav &&
          !insideBottomNav &&
          !insideMobileMore &&
          !intentionallyHidden &&
          rect.bottom > bottomNavRect.top + 4 &&
          rect.top < bottomNavRect.bottom - 4,
        )
        const tooCloseToBottomNav = Boolean(
          bottomNavRect &&
          criticalForBottomNav &&
          !insideBottomNav &&
          !insideMobileMore &&
          !intentionallyHidden &&
          rect.bottom > bottomNavRect.top - 12 &&
          rect.top < bottomNavRect.top,
        )

        return {
          tag: html.tagName,
          selector: selectorFor(html),
          text: (html.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 100),
          visible,
          horizontalClip,
          outOfViewport,
          coveredByBottomNav,
          tooCloseToBottomNav,
          criticalForBottomNav,
          width: Math.round(rect.width),
          left: Math.round(rect.left),
          right: Math.round(rect.right),
          top: Math.round(rect.top),
          bottom: Math.round(rect.bottom),
          bottomNavTop: bottomNavRect ? Math.round(bottomNavRect.top) : null,
          bottomNavGap: bottomNavGap == null ? null : Math.round(bottomNavGap),
          scrollWidth: html.scrollWidth,
          clientWidth: html.clientWidth,
          role: html.getAttribute('role'),
          className: html.className,
        }
      })
      .filter((item) => item.visible)

    const hardOffenders = candidates
      .filter((item) => item.outOfViewport || item.coveredByBottomNav || item.tooCloseToBottomNav)
      .filter((item) => !String(item.className).includes('v1-bottom-nav'))
      .filter((item) => !String(item.className).includes('v1-mobile-more-panel'))
      .filter((item) => item.tag !== 'SECTION')

    const clipWarnings = candidates
      .filter((item) => item.horizontalClip)
      .filter((item) => !String(item.className).includes('v1-tabs'))
      .filter((item) => item.tag !== 'INPUT' && item.tag !== 'TEXTAREA')

    const heading =
      document.querySelector('h1')?.textContent?.trim() ??
      document.querySelector('[data-audit-title]')?.textContent?.trim() ??
      null

    const steps = Array.from(document.querySelectorAll('.v1-wizard-step'))
    const activeIndex = steps.findIndex((element) => element.classList.contains('active'))

    return {
      path: window.location.pathname,
      url: window.location.href,
      heading,
      activeWizardStep: activeIndex >= 0 ? activeIndex + 1 : null,
      bodyScrollWidth: document.body.scrollWidth,
      documentScrollWidth: document.documentElement.scrollWidth,
      innerWidth: window.innerWidth,
      bodyOverflow: document.body.scrollWidth > window.innerWidth + 4,
      documentOverflow: document.documentElement.scrollWidth > window.innerWidth + 4,
      bottomNavDock: bottomNavRect && bottomNavStyle ? {
        visible: bottomNavStyle.display !== 'none' && bottomNavRect.width > 1 && bottomNavRect.height > 1,
        bottomGap: Math.abs(window.innerHeight - bottomNavRect.bottom),
        backgroundColor: bottomNavStyle.backgroundColor,
        paddingBottom: Number.parseFloat(bottomNavStyle.paddingBottom || '0'),
      } : null,
      hardOffenders,
      clipWarnings,
    }
  })

  workflowReports.push({
    name,
    path: result.path,
    url: result.url,
    heading: result.heading,
    activeWizardStep: result.activeWizardStep,
    bodyOverflow: result.bodyOverflow,
    documentOverflow: result.documentOverflow,
    hardOffenders: result.hardOffenders,
    clipWarnings: result.clipWarnings,
  })

  fs.writeFileSync(path.join(outputDir, `${name}.json`), JSON.stringify(result, null, 2), 'utf8')
  await page.screenshot({ path: path.join(outputDir, `${name}.png`), fullPage: true })

  const hardOffenders = result.hardOffenders as LayoutFinding[]
  if (hardOffenders.length > 0) {
    const details = hardOffenders.slice(0, 5).map((item) => {
      const problem = item.coveredByBottomNav ? 'coveredByBottomNav' : item.tooCloseToBottomNav ? 'tooCloseToBottomNav' : 'outsideViewport'
      return `${item.selector} "${item.text}" ${problem} left=${item.left} right=${item.right} top=${item.top} bottom=${item.bottom} navTop=${item.bottomNavTop ?? 'n/a'} gap=${item.bottomNavGap ?? 'n/a'}`
    }).join(' | ')
    throw new Error(`${name}: visible elements blocked or outside viewport: ${details}`)
  }

  if (result.innerWidth <= 860) {
    if (!result.bottomNavDock?.visible) {
      throw new Error(`${name}: mobile bottom nav is not visible`)
    }
    if (result.bottomNavDock.bottomGap > 1) {
      throw new Error(`${name}: mobile bottom nav floats above viewport bottom gap=${result.bottomNavDock.bottomGap}`)
    }
    if (/rgba\(0,\s*0,\s*0,\s*0\)|transparent/i.test(result.bottomNavDock.backgroundColor)) {
      throw new Error(`${name}: mobile bottom nav background is transparent`)
    }
    if (result.bottomNavDock.paddingBottom < 8) {
      throw new Error(`${name}: mobile bottom nav does not reserve safe-area padding`)
    }
  }

  if (result.bodyOverflow) {
    throw new Error(`${name}: horizontal overflow on ${result.path} body=${result.bodyScrollWidth} document=${result.documentScrollWidth} viewport=${result.innerWidth}`)
  }
}
