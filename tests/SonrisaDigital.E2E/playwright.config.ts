import { defineConfig, devices } from '@playwright/test';

// La URL y las credenciales llegan por variables de entorno para que el mismo
// proyecto sirva en local y en el pipeline, sin guardar secretos en el código.
const baseURL = process.env.SONRISADIGITAL_E2E_BASE_URL ?? 'http://localhost:5175';

export default defineConfig({
  testDir: './tests',
  outputDir: './test-results',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 120_000,
  expect: {
    timeout: 20_000
  },
  reporter: process.env.CI
    ? [['github'], ['html', { open: 'never' }]]
    : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    locale: 'es-DO',
    timezoneId: 'America/Santo_Domingo',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ]
});
