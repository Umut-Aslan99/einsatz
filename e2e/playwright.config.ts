import { defineConfig, devices } from '@playwright/test';

// Runs inside the web container's network namespace: localhost is a secure context,
// which service workers and crypto.randomUUID require.
const baseURL = process.env.BASE_URL ?? 'http://localhost:3000';

export default defineConfig({
  testDir: '.',
  timeout: 60_000,
  retries: process.env.CI ? 1 : 0,
  workers: 1, // tests share one demo database and run as a story
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL,
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'tablet', use: { ...devices['Galaxy Tab S4'], browserName: 'chromium' } }],
});
