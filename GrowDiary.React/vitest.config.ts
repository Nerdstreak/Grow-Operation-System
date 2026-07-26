import { defineConfig } from 'vitest/config'

/**
 * Unit tests live next to the code in src/. The e2e/ folder belongs to Playwright —
 * without this, vitest picks up those specs and they fail with "Playwright Test did
 * not expect test() to be called here".
 */
export default defineConfig({
  test: {
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    environment: 'node',
  },
})
