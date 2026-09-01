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

    /**
     * Abdeckung — und zwar ueber ALLES, nicht nur ueber das Geprüfte.
     *
     * <b>Der Anlass (01.09.2026).</b> Die Oberfläche war nie gemessen worden;
     * `@vitest/coverage-v8` fehlte ganz. Beim ersten Lauf kamen 84 % heraus —
     * eine Zahl, die nur die Dateien zählt, die ein Test überhaupt importiert.
     * Genau die Falle, gegen die dieses Projekt sonst Mengenwächter baut: was
     * nicht in der Grundmenge ist, kann nicht durchfallen.
     *
     * `all: true` nimmt jede Quelldatei auf. Reine Darstellung (.tsx) bleibt
     * draussen — dafür gibt es die 29 Playwright-Läufe, und eine Komponente,
     * die nur JSX zurückgibt, sagt als Prozentzahl nichts.
     */
    coverage: {
      provider: 'v8',
      all: true,
      include: ['src/**/*.ts'],
      exclude: [
        'src/**/*.test.ts',
        'src/**/*.d.ts',
        // Reine Typen und Aufzählungen — dort gibt es nichts auszuführen.
        'src/types/**',
        'src/main.tsx',
      ],
      reporter: ['text-summary', 'json-summary'],
      reportsDirectory: './coverage',
    },
  },
})
