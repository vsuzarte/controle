import { test, expect, abrirNovoLancamento, digitarValor, escolher, salvar, irPara, textoValor } from './apoio'

test.describe('lançamentos', () => {
  test('lançar despesa à vista atualiza saldo e lista', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 1000)
    await page.goto('/')
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(1000))

    await abrirNovoLancamento(page)
    await digitarValor(page, '8990')
    await escolher(page, 'Mercado')
    await salvar(page)

    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(910.1))
    await expect(page.getByTestId('gastos-mes')).toHaveText(textoValor(89.9))

    await irPara(page, 'Lançamentos')
    const linha = page.getByTestId('lancamento').filter({ hasText: 'Mercado' })
    await expect(linha).toHaveCount(1)
    await expect(linha).toContainText('89,90')
  })

  test('tocar de novo numa opção escolhida não desmarca', async ({ page, api: _ }) => {
    await page.goto('/')
    await abrirNovoLancamento(page)
    await digitarValor(page, '1000')
    await escolher(page, 'Mercado')
    await escolher(page, 'Mercado')
    await escolher(page, 'Conta corrente')
    await salvar(page)
  })

  test('receita vai para o saldo', async ({ page, api }) => {
    await page.goto('/')
    await page.getByText('Receita', { exact: true }).first().click()
    await digitarValor(page, '450000')
    await escolher(page, 'Salário')
    await salvar(page)
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(4500))
    void api
  })

  test('transferência entre contas não é gasto nem receita', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 500)
    await page.goto('/')
    await abrirNovoLancamento(page)
    await page.getByRole('button', { name: 'Transferir' }).click()
    await digitarValor(page, '20000')
    await escolher(page, 'Dinheiro', 'Conta de destino')
    await salvar(page)
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(500))
    await expect(page.getByTestId('gastos-mes')).toHaveText(textoValor(0))
  })

  test('corrigir guarda o valor antigo no histórico', async ({ page, api }) => {
    await api.lancar({ descricao: 'Padaria', valor: 150, categoria: 'Mercado' })
    await page.goto('/lancamentos')
    await page.getByTestId('lancamento').filter({ hasText: 'Padaria' }).click()
    await expect(page.getByText('Corrigir lançamento')).toBeVisible()

    const campo = page.getByRole('textbox', { name: 'Valor', exact: true })
    await campo.click()
    for (let i = 0; i < 6; i++) await campo.press('Backspace')
    await campo.pressSequentially('10500')
    await page.getByLabel('Motivo da correção (opcional)').fill('Digitei errado')
    await salvar(page, 'Lançamento corrigido')

    const linha = page.getByTestId('lancamento').filter({ hasText: 'Padaria' })
    await expect(linha).toContainText('105,00')
    await linha.getByRole('button', { name: 'Ações de Padaria' }).click()
    await page.getByRole('listitem').filter({ hasText: 'Histórico' }).click()
    const historico = page.locator('.v-dialog').filter({ hasText: 'Correção' })
    await expect(historico).toContainText('Digitei errado')
    await expect(historico).toContainText('150,00')
    await expect(historico).toContainText('105,00')
  })

  test('cancelar exige motivo, some dos totais e continua no histórico', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 1000)
    await api.lancar({ descricao: 'Duplicado', valor: 200, categoria: 'Lazer' })
    await page.goto('/lancamentos')
    const linha = page.getByTestId('lancamento').filter({ hasText: 'Duplicado' })
    await linha.getByRole('button', { name: 'Ações de Duplicado' }).click()
    await page.getByRole('listitem').filter({ hasText: 'Cancelar lançamento' }).click()

    const confirmar = page.locator('.v-dialog').getByRole('button', { name: 'Confirmar' })
    await expect(confirmar).toBeDisabled()
    await page.getByLabel('Motivo (obrigatório)').fill('Lancei duas vezes')
    await confirmar.click()
    await expect(page.getByText('Lançamento cancelado')).toBeVisible()

    await expect(linha).toHaveCount(0)
    await page.getByRole('button', { name: /Mostrar 1 cancelado/ }).click()
    await expect(linha).toContainText('cancelado')
    await expect(linha).toContainText('Lancei duas vezes')

    await irPara(page, 'Início')
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(1000))
  })

  test('estorno parcial reduz o gasto', async ({ page, api }) => {
    await api.lancar({ descricao: 'Tênis', valor: 300, categoria: 'Roupas' })
    await page.goto('/lancamentos')
    await page.getByTestId('lancamento').filter({ hasText: 'Tênis' }).getByRole('button', { name: 'Ações de Tênis' }).click()
    await page.getByRole('listitem').filter({ hasText: 'Estornar' }).click()
    const valor = page.locator('.v-dialog').getByLabel('Valor devolvido')
    await valor.click()
    for (let i = 0; i < 6; i++) await valor.press('Backspace')
    await valor.pressSequentially('12000')
    await page.locator('.v-dialog').getByRole('button', { name: 'Confirmar' }).click()
    await expect(page.getByText('Estorno registrado')).toBeVisible()
    await expect(page.getByTestId('lancamento').filter({ hasText: 'Estorno: Tênis' })).toContainText('120,00')

    await irPara(page, 'Início')
    await expect(page.getByTestId('gastos-mes')).toHaveText(textoValor(180))
  })

  test('despesa futura fica prevista e não mexe no saldo', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 1000)
    await page.goto('/')
    await abrirNovoLancamento(page)
    await digitarValor(page, '40000')
    await escolher(page, 'Saúde')
    await page.getByText('Outra data').click()
    await page.getByRole('textbox', { name: 'Data' }).fill('2026-10-20')
    await expect(page.getByLabel('Já paguei')).toBeDisabled()
    await salvar(page)

    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(1000))
    await expect(page.getByTestId('saldo-projetado')).toHaveText(textoValor(600))
    await expect(page.getByTestId('alerta').filter({ hasText: 'Saúde' })).toContainText('em 5 dias')
  })
})
