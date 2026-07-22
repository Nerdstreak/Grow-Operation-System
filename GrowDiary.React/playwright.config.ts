import { defineConfig, devices } from '@playwright/test'

// Smoke-focused Playwright config. Runs the built app via `vite preview` on a fixed
// port so the tests exercise the real production bundle. The backend/API is not
// running, so pages render their loading/error/empty states — which is exactly what
// we want to smoke-test: every route must render without an uncaught render crash.
const PORT = 4173
const BASE_URL = `http://localhost:${PORT}`

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['list']] : 'list',
  use: {
    baseURL: BASE_URL,
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    // Build the real production bundle (into GrowDiary.Web/wwwroot), then serve it
    // through a static server that injects <base href="/"> exactly like the backend.
    command: `npm run build && node e2e/preview-server.mjs ${PORT}`,
    url: BASE_URL,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
  },
})
