import { test, expect, abrirNovoLancamento, digitarValor, escolher, salvar, irPara, textoValor } from './apoio'

test.describe('cartão de crédito', () => {
  test('cadastrar cartão pela tela de ajustes', async ({ page, api: _ }) => {
    await page.goto('/cadastros')
    await page.getByRole('button', { name: 'Nova conta / cartão' }).click()
    const form = page.locator('.v-dialog')
    await form.getByLabel('Nome').fill('Nubank')
    await form.locator('.v-select').filter({ hasText: 'Tipo' }).click()
    await page.getByRole('option', { name: 'Cartão de crédito' }).click()
    await form.getByLabel('Dia do fechamento').fill('3')
    await form.getByLabel('Dia do vencimento').fill('10')
    await form.getByRole('button', { name: 'Salvar' }).click()
    await expect(page.getByText('Conta salva')).toBeVisible()
    await expect(page.getByText('fecha dia 3, vence dia 10')).toBeVisible()
  })

  test('compra parcelada cai nas faturas certas e pagar a fatura não conta gasto duas vezes', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 2000)
    await api.criarCartao('Nubank', 3, 10)
    await page.goto('/')

    // Compra em 3x no cartão: hoje (15/10) é depois do fechamento (dia 3) → 1ª parcela na fatura de novembro.
    await abrirNovoLancamento(page)
    await digitarValor(page, '60000')
    await escolher(page, 'Compras diversas')
    await escolher(page, 'Nubank')
    await page.getByRole('button', { name: 'Mais parcelas' }).click()
    await page.getByRole('button', { name: 'Mais parcelas' }).click()
    await expect(page.getByTestId('parcelas')).toHaveText('3x')
    await salvar(page, 'Compra lançada em 3x')

    // Compra à vista no cartão.
    await abrirNovoLancamento(page)
    await digitarValor(page, '5000')
    await escolher(page, 'Restaurantes / Delivery')
    await escolher(page, 'Nubank')
    await salvar(page)

    await expect(page.getByTestId('gastos-mes')).toHaveText(textoValor(250))
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(2000))

    await irPara(page, 'Cartões')
    await expect(page.getByTestId('fatura-total')).toHaveText(textoValor(250))
    await page.getByText('dez/26').click()
    await expect(page.getByTestId('fatura-total')).toHaveText(textoValor(200))
    await page.getByText('nov/26').click()

    await page.getByRole('button', { name: 'Pagar fatura' }).click()
    await page.locator('.v-dialog').getByRole('button', { name: 'Confirmar pagamento' }).click()
    await expect(page.getByText('Pagamento da fatura registrado')).toBeVisible()
    await expect(page.getByTestId('fatura-restante')).toHaveText(textoValor(0))

    await irPara(page, 'Início')
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(1750))
    // O pagamento é transferência: o gasto do mês continua 250, não 500.
    await expect(page.getByTestId('gastos-mes')).toHaveText(textoValor(250))
  })
})
