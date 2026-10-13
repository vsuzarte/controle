import { test as setup, expect } from '@playwright/test'

/** Entra uma vez e guarda o cookie; os demais testes já começam logados. */
setup('entrar no sistema', async ({ page, request }) => {
  await request.post('/api/e2e/reset')
  await page.goto('/')
  await page.getByLabel('Senha').fill(process.env.SENHA_E2E ?? 'e2e')
  await page.getByRole('button', { name: 'Entrar' }).click()
  await expect(page.getByTestId('saldo-disponivel')).toBeVisible()
  await page.context().storageState({ path: '.auth/sessao.json' })
})
