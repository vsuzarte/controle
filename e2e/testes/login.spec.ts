import { test, expect } from './apoio'

test.describe('login', () => {
  test.use({ storageState: { cookies: [], origins: [] } })

  test('senha errada não entra', async ({ page }) => {
    await page.goto('/')
    await page.getByLabel('Senha').fill('errada')
    await page.getByRole('button', { name: 'Entrar' }).click()
    await expect(page.getByText('Senha incorreta.')).toBeVisible()
    await expect(page.getByTestId('saldo-disponivel')).toHaveCount(0)
  })

  test('API recusa quem não está logado', async ({ playwright, baseURL }) => {
    const anonimo = await playwright.request.newContext({ baseURL })
    expect((await anonimo.get('/api/resumo')).status()).toBe(401)
    await anonimo.dispose()
  })
})
