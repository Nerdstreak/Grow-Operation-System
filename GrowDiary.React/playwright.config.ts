import { defineConfig, devices } from '@playwright/test'

const frontendUrl = process.env.GROW_OS_FRONTEND_URL ?? 'http://127.0.0.1:5173'
const backendUrl = process.env.GROW_OS_BACKEND_URL ?? 'http://127.0.0.1:5076'
const shouldStartServers = process.env.GROW_OS_AUDIT_START_SERVERS !== '0'

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
          command: `dotnet run --project ../GrowDiary.Web/GrowDiary.Web.csproj --no-launch-profile --urls ${backendUrl}`,
          url: `${backendUrl}/api/system/backend-health`,
          timeout: 180_000,
          reuseExistingServer: true,
          stdout: 'pipe',
          stderr: 'pipe',
        },
        {
          command: 'npm run dev -- --host 127.0.0.1 --port 5173',
          url: frontendUrl,
          timeout: 90_000,
          reuseExistingServer: true,
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
