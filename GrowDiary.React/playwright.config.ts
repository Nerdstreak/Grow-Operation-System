import { defineConfig, devices } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

const repoRoot = path.resolve(process.cwd(), '..')
const frontendUrl = process.env.GROW_OS_FRONTEND_URL ?? 'http://127.0.0.1:5174'
const backendUrl = process.env.GROW_OS_BACKEND_URL ?? 'http://127.0.0.1:5077'
const shouldStartServers = process.env.GROW_OS_AUDIT_START_SERVERS !== '0'
const defaultAuditDbPath = path.join(repoRoot, 'artifacts', 'test-data', 'grow-diary-audit.db')
const hasExplicitDbPath = Boolean(process.env.GROWDIARY_DB_PATH)
const auditDbPath = process.env.GROWDIARY_DB_PATH ?? defaultAuditDbPath

if (shouldStartServers) {
  fs.mkdirSync(path.dirname(auditDbPath), { recursive: true })
  if (!hasExplicitDbPath) {
    for (const file of [auditDbPath, `${auditDbPath}-wal`, `${auditDbPath}-shm`]) {
      fs.rmSync(file, { force: true })
    }
  }
  process.env.GROWDIARY_DB_PATH = auditDbPath
  process.env.GROW_OS_BACKEND_URL = backendUrl
  process.env.Hosting__DefaultUrls = backendUrl
}

export default defineConfig({
  testDir: './tests',
  timeout: 120_000,
  expect: {
    timeout: 10_000,
  },
  fullyParallel: false,
  workers: 1,
  reporter: [
    ['list'],
    ['html', { outputFolder: '../artifacts/playwright-report', open: 'never' }],
  ],
  outputDir: '../artifacts/playwright-results',
  use: {
    baseURL: frontendUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
  },
  webServer: shouldStartServers
    ? [
        {
          command: 'dotnet run --project ../GrowDiary.Web/GrowDiary.Web.csproj --no-launch-profile',
          url: `${backendUrl}/api/system/backend-health`,
          timeout: 180_000,
          reuseExistingServer: false,
          stdout: 'pipe',
          stderr: 'pipe',
        },
        {
          command: 'npm run dev -- --host 127.0.0.1 --port 5174',
          url: frontendUrl,
          timeout: 90_000,
          reuseExistingServer: false,
          stdout: 'pipe',
          stderr: 'pipe',
        },
      ]
    : undefined,
  projects: [
    {
      name: 'visual-audit',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
