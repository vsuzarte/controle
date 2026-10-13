import { test, expect, abrirNovoLancamento, digitarValor, escolher, irPara, textoValor } from './apoio'

test.describe('contas do mês', () => {
  test('cadastrar conta fixa gera vencimento e alerta', async ({ page, api: _ }) => {
    await page.goto('/')
    await irPara(page, 'Contas do mês')
    await page.getByRole('button', { name: 'Conta', exact: true }).click()

    const form = page.locator('.v-dialog')
    await form.getByLabel('Nome').fill('Aluguel')
    await form.getByRole('textbox', { name: 'Valor' }).click()
    await form.getByRole('textbox', { name: 'Valor' }).pressSequentially('120000')
    await form.getByLabel('Categoria').click()
    await page.getByRole('option', { name: 'Moradia' }).click()
    await form.getByLabel('Próximo vencimento').fill('2026-10-20')
    await form.getByRole('button', { name: 'Salvar' }).click()
    await expect(page.getByText('Conta cadastrada')).toBeVisible()

    const conta = page.getByTestId('conta-do-mes').filter({ hasText: 'Aluguel' })
    await expect(conta).toContainText('dia')
    await expect(conta).toContainText('20')
    await expect(page.getByTestId('total-contas')).toHaveText(textoValor(1200))

    await irPara(page, 'Início')
    await expect(page.getByTestId('alerta').filter({ hasText: 'Aluguel' })).toContainText('em 5 dias')
  })

  test('pagar pelo alerta usa o valor real e baixa o saldo', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 1000)
    await api.contaDoMes({ descricao: 'Luz', valor: 180, categoria: 'Energia', inicio: '2026-10-10', variavel: true })
    await page.goto('/')

    const alerta = page.getByTestId('alerta').filter({ hasText: 'Luz' })
    await expect(alerta).toContainText('venceu há 5 dias')
    await alerta.getByRole('button', { name: 'Paguei' }).click()

    const dialogo = page.locator('.v-dialog').filter({ hasText: 'Confirmar pagamento' })
    const valor = dialogo.getByLabel('Valor pago')
    await valor.click()
    for (let i = 0; i < 6; i++) await valor.press('Backspace')
    await valor.pressSequentially('19347')
    await dialogo.getByRole('button', { name: 'Confirmar' }).click()
    await expect(page.getByText('Pagamento confirmado')).toBeVisible()

    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(806.53))
    await expect(page.getByTestId('alerta').filter({ hasText: 'Luz' })).toHaveCount(0)
  })

  test('lançar algo que já está previsto sugere confirmar em vez de duplicar', async ({ page, api }) => {
    await api.contaDoMes({ descricao: 'Internet', valor: 99.9, categoria: 'Internet / Telefone', inicio: '2026-10-14' })
    await page.goto('/')
    await abrirNovoLancamento(page)
    await digitarValor(page, '9990')
    await escolher(page, 'Internet / Telefone')

    await expect(page.getByText('É esse pagamento?')).toBeVisible()
    await page.getByRole('button', { name: 'Sim, confirmar' }).click()
    const dialogo = page.locator('.v-dialog').filter({ hasText: 'Confirmar pagamento' })
    await expect(dialogo.getByLabel('Valor pago')).toHaveValue('99,90')
    await dialogo.getByRole('button', { name: 'Confirmar' }).click()
    await expect(page.getByText('Pagamento confirmado')).toBeVisible()

    await irPara(page, 'Lançamentos')
    await expect(page.getByTestId('lancamento').filter({ hasText: 'Internet' })).toHaveCount(1)
  })

  test('encerrar conta cancela os próximos vencimentos', async ({ page, api }) => {
    await api.contaDoMes({ descricao: 'Academia', valor: 120, categoria: 'Saúde', inicio: '2026-10-25' })
    await page.goto('/contas-do-mes')
    await page.getByRole('button', { name: 'Ações de Academia' }).click()
    await page.getByRole('listitem').filter({ hasText: 'Encerrar' }).click()
    await page.locator('.v-dialog').getByRole('button', { name: 'Confirmar' }).click()
    await expect(page.getByText('Encerrada. O histórico')).toBeVisible()

    await expect(page.getByTestId('conta-do-mes').filter({ hasText: 'Academia' })).toHaveCount(0)
    await page.getByRole('button', { name: /Ver 1 encerrada/ }).click()
    await expect(page.getByText('Academia')).toBeVisible()

    await irPara(page, 'Lançamentos')
    await page.getByRole('button', { name: /Mostrar 1 cancelado/ }).click()
    await expect(page.getByTestId('lancamento').filter({ hasText: 'Academia' })).toContainText('cancelado')
  })

  test('salário programado aparece como a receber e não duplica ao confirmar', async ({ page, api }) => {
    await api.contaDoMes({ descricao: 'Salário', tipo: 'Receita', valor: 4500, categoria: 'Salário', inicio: '2026-10-05' })
    await page.goto('/')
    await expect(page.getByTestId('saldo-projetado')).toHaveText(textoValor(4500))
    await page.getByTestId('alerta').filter({ hasText: 'Salário' }).getByRole('button', { name: 'Recebi' }).click()
    await page.locator('.v-dialog').getByRole('button', { name: 'Confirmar' }).click()
    await expect(page.getByText('Recebimento confirmado')).toBeVisible()
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(4500))
    await expect(page.getByTestId('saldo-projetado')).toHaveText(textoValor(4500))
  })
})
