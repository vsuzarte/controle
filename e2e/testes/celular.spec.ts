import { test, expect, digitarValor, escolher, salvar, textoValor } from './apoio'

test.describe('celular', () => {
  test('barra inferior leva às telas principais', async ({ page, api: _ }) => {
    await page.goto('/')
    await page.getByRole('link', { name: 'Contas' }).click()
    await expect(page.getByText('Contas do mês').first()).toBeVisible()
    await page.getByRole('link', { name: 'Lançamentos' }).click()
    await expect(page).toHaveURL(/lancamentos/)
    await page.getByRole('button', { name: 'Mais' }).click()
    await page.getByRole('link', { name: 'Cartões' }).click()
    await expect(page).toHaveURL(/cartoes/)
  })

  test('lançar pelo botão + em tela cheia', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 100)
    await page.goto('/')
    await page.getByRole('button', { name: 'Novo lançamento' }).click()
    await expect(page.locator('.v-dialog--fullscreen')).toBeVisible()
    await digitarValor(page, '2550')
    await escolher(page, 'Mercado')
    await salvar(page)
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(74.5))
  })

  test('não há rolagem horizontal', async ({ page, api: _ }) => {
    for (const rota of ['/', '/lancamentos', '/contas-do-mes', '/planejamento', '/cartoes', '/cadastros']) {
      await page.goto(rota)
      await page.waitForLoadState('networkidle')
      const largura = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
      expect(largura, `rolagem horizontal em ${rota}`).toBeLessThanOrEqual(1)
    }
  })
})
