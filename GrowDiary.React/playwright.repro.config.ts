import { defineConfig, devices } from '@playwright/test'

// TEMPORAER — Nachstellung des gemeldeten 404-Fehlers gegen die LAUFENDE App
// auf http://localhost:5076. Kein eigener webServer.
export default defineConfig({
  testDir: './e2e-repro-tmp',
  testMatch: 'repro-mobile.spec.ts',
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  use: { baseURL: 'http://localhost:5076' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
