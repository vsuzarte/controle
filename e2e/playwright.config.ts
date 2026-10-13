import { defineConfig, devices } from '@playwright/test'

/**
 * Testes ponta a ponta contra uma cópia isolada do sistema (docker-compose.e2e.yml):
 * banco próprio, apagado antes de cada teste, e "hoje" fixo em 15/10/2026.
 */
export default defineConfig({
  testDir: './testes',
  // Um banco só, zerado antes de cada teste: os testes rodam em sequência.
  workers: 1,
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  timeout: 30_000,
  expect: { timeout: 7_000 },
  reporter: [['list'], ['html', { outputFolder: 'relatorio', open: 'never' }]],
  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:8090',
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    { name: 'login', testMatch: /login\.setup\.ts/ },
    {
      name: 'computador',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1366, height: 860 }, storageState: '.auth/sessao.json' },
      dependencies: ['login'],
      testIgnore: /celular\.spec\.ts/,
    },
    {
      name: 'celular',
      use: { ...devices['Pixel 7'], storageState: '.auth/sessao.json' },
      dependencies: ['login'],
      testMatch: /celular\.spec\.ts/,
    },
  ],
})
