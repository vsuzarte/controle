import { test, expect, abrirNovoLancamento, digitarValor, escolher, salvar, textoValor } from './apoio'

// Hoje no ambiente E2E: quinta, 15/10/2026. O 5º dia útil de outubro é 07/10 e o de novembro, 09/11 (dia 2 é Finados).

test.describe('previsão entre salários', () => {
  test('divide o ciclo nos dois pagamentos com todas as contas e paga a fatura por ali', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 1000)
    await api.contaDoMes({ descricao: 'Salário', tipo: 'Receita', valor: 4000, categoria: 'Salário', inicio: '2026-11-01', diaUtil: 5 })
    await api.contaDoMes({ descricao: 'Adiantamento', tipo: 'Receita', valor: 2000, categoria: 'Salário', inicio: '2026-10-20' })
    await api.contaDoMes({ descricao: 'Aluguel', valor: 900, categoria: 'Moradia', inicio: '2026-10-22' })
    await api.criarCartao('Nubank', 3, 10)
    await api.lancar({ descricao: 'Feira', valor: 100, conta: 'Nubank', categoria: 'Mercado', data: '2026-10-01' }) // fatura venceu 10/10

    await page.goto('/previsao')
    await expect(page.getByTestId('ciclo')).toHaveText('07/10/2026 a 08/11/2026')
    const periodos = page.getByTestId('periodo')
    await expect(periodos).toHaveCount(2)

    const primeiro = periodos.nth(0)
    await expect(primeiro).toContainText('Salário')
    await expect(primeiro).toContainText('agora')
    await expect(primeiro.getByTestId('item-previsao').filter({ hasText: 'Fatura Nubank' })).toContainText('atrasada')
    await expect(primeiro.getByTestId('sobra-periodo')).toHaveText(textoValor(900))

    const segundo = periodos.nth(1)
    await expect(segundo).toContainText('Adiantamento')
    await expect(segundo.getByTestId('item-previsao').filter({ hasText: 'Adiantamento' })).toContainText('2.000,00')
    await expect(segundo.getByTestId('item-previsao').filter({ hasText: 'Aluguel' })).toContainText('900,00')
    await expect(segundo.getByTestId('sobra-periodo')).toHaveText(textoValor(2000))

    // Pagar a fatura atrasada direto da previsão.
    await primeiro.getByTestId('item-previsao').filter({ hasText: 'Fatura Nubank' }).getByRole('button', { name: 'Pagar' }).click()
    await page.locator('.v-dialog').getByRole('button', { name: 'Confirmar pagamento' }).click()
    await expect(page.getByText('Pagamento da fatura registrado')).toBeVisible()
    await expect(primeiro.getByTestId('item-previsao').filter({ hasText: 'Fatura Nubank' })).toContainText('pago')

    // O próximo ciclo começa no salário de 09/11.
    await page.getByRole('button', { name: 'Próximo ciclo' }).click()
    await expect(page.getByTestId('ciclo')).toHaveText(/^09\/11\/2026 a/)

    await page.goto('/')
    await expect(page.getByTestId('card-saldo')).toContainText('Livre até 19/10/2026')
    await expect(page.getByTestId('saldo-projetado')).toHaveText(textoValor(900))
    await expect(page.getByTestId('periodo-resumo')).toHaveCount(2)
  })

  test('a pagar vai até o próximo salário, inclui fatura do cartão e agrupa atrasadas', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 1000)
    await api.contaDoMes({ descricao: 'Salário', tipo: 'Receita', valor: 4000, categoria: 'Salário', inicio: '2026-10-05' })
    await api.contaDoMes({ descricao: 'Internet', valor: 100, categoria: 'Internet / Telefone', inicio: '2026-08-28' })
    await api.criarCartao('Nubank', 10, 20)
    await api.lancar({ descricao: 'Tênis', valor: 300, conta: 'Nubank', categoria: 'Roupas', data: '2026-10-01' }) // vence 20/10

    await page.goto('/')
    const vencimentos = page.getByTestId('card-vencimentos')
    await expect(vencimentos).toContainText('até 04/11/2026')
    await expect(vencimentos.getByTestId('alerta-grupo').filter({ hasText: 'Internet' })).toContainText('2 meses sem pagar')

    const fatura = vencimentos.getByTestId('alerta').filter({ hasText: 'Fatura Nubank' })
    await expect(fatura).toContainText('300,00')
    await fatura.getByRole('button', { name: 'Pagar' }).click()
    await page.locator('.v-dialog').getByRole('button', { name: 'Confirmar pagamento' }).click()
    await expect(page.getByText('Pagamento da fatura registrado')).toBeVisible()
    await expect(vencimentos.getByTestId('alerta').filter({ hasText: 'Fatura Nubank' })).toHaveCount(0)
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(700))

    // A internet de 28/10 fica em "Depois", recolhida.
    await vencimentos.getByRole('button', { name: /ver 1/ }).click()
    await expect(vencimentos.getByTestId('alerta').filter({ hasText: '28/10/2026' })).toBeVisible()
    // Salário fica à parte, em "A receber".
    await expect(vencimentos.getByTestId('alerta').filter({ hasText: 'Salário' }).getByRole('button', { name: 'Recebi' })).toBeVisible()
  })
})

test.describe('juros, vale e cartões', () => {
  test('pagar conta atrasada com juros mostra o dinheiro perdido nos relatórios', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 1000)
    await api.contaDoMes({ descricao: 'Condomínio', valor: 500, categoria: 'Moradia', inicio: '2026-10-05' })
    await page.goto('/')

    await page.getByTestId('alerta').filter({ hasText: 'Condomínio' }).getByRole('button', { name: 'Paguei' }).click()
    const dialogo = page.locator('.v-dialog').filter({ hasText: 'Confirmar pagamento' })
    const valor = dialogo.getByLabel('Valor pago')
    await valor.click()
    for (let i = 0; i < 6; i++) await valor.press('Backspace')
    await valor.pressSequentially('52500')
    await expect(dialogo.getByTestId('foi-juros').locator('input')).toBeChecked()
    await dialogo.getByRole('button', { name: 'Confirmar' }).click()
    await expect(page.getByText('Pagamento confirmado')).toBeVisible()
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(475))

    await page.goto('/relatorios')
    await expect(page.getByTestId('juros-mes')).toHaveText(textoValor(25))
    await expect(page.getByTestId('tabela-comparativo').locator('tr').filter({ hasText: 'Moradia' })).toContainText('525,00')
  })

  test('vale-refeição: compra maior que o saldo completa com outra conta', async ({ page, api }) => {
    await api.definirSaldo('Conta corrente', 1000)
    await api.criarVale('VR', 50)
    await page.goto('/')
    await expect(page.getByTestId('vale').filter({ hasText: 'VR' }).getByTestId('vale-saldo')).toHaveText(textoValor(50))
    // O vale não entra no dinheiro que paga contas.
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(1000))

    await abrirNovoLancamento(page)
    await digitarValor(page, '8000')
    await escolher(page, 'Restaurantes / Delivery')
    await escolher(page, 'VR', 'Conta')
    const aviso = page.getByTestId('aviso-vale')
    await expect(aviso).toContainText('30,00')
    await aviso.locator('.v-chip', { hasText: 'Conta corrente' }).click()
    await salvar(page)

    await expect(page.getByTestId('vale-saldo')).toHaveText(textoValor(0))
    await expect(page.getByTestId('saldo-disponivel')).toHaveText(textoValor(970))
    await expect(page.getByTestId('gastos-mes')).toHaveText(textoValor(80))
  })

  test('cartões: visão de todos com totais e ajuste de limite com histórico', async ({ page, api }) => {
    await api.criarCartao('Nubank', 3, 10, 2000)
    await api.criarCartao('Itaú', 20, 28, 1000)
    await api.lancar({ descricao: 'Feira', valor: 100, conta: 'Nubank', categoria: 'Mercado', data: '2026-10-01' })
    await api.lancar({ descricao: 'Jantar', valor: 250, conta: 'Itaú', categoria: 'Restaurantes / Delivery', data: '2026-10-12' })

    await page.goto('/cartoes')
    await expect(page.getByTestId('total-a-pagar')).toHaveText(textoValor(100))
    await expect(page.getByTestId('total-em-aberto')).toHaveText(textoValor(250))
    await expect(page.getByTestId('cartao-resumo')).toHaveCount(2)

    await page.getByRole('tab', { name: 'Itaú' }).click()
    await expect(page.getByTestId('limite-disponivel')).toHaveText(textoValor(750))
    await page.getByRole('button', { name: 'Ajustar limite' }).click()
    const dialogo = page.locator('.v-dialog')
    const campo = dialogo.getByLabel('Novo limite')
    await campo.click()
    for (let i = 0; i < 8; i++) await campo.press('Backspace')
    await campo.pressSequentially('150000')
    await dialogo.getByLabel('Motivo (opcional)').fill('Banco aumentou')
    await dialogo.getByRole('button', { name: 'Salvar limite' }).click()
    await expect(page.getByText('Limite atualizado')).toBeVisible()
    await expect(page.getByTestId('limite-disponivel')).toHaveText(textoValor(1250))

    await page.getByRole('button', { name: 'Histórico do cartão' }).click()
    await expect(page.locator('.v-dialog')).toContainText('Ajuste de limite')
    await expect(page.locator('.v-dialog')).toContainText('Banco aumentou')
  })

  test('compra no cartão acima do limite disponível avisa', async ({ page, api }) => {
    await api.criarCartao('Nubank', 3, 10, 500)
    await page.goto('/')
    await abrirNovoLancamento(page)
    await digitarValor(page, '60000')
    await escolher(page, 'Compras diversas')
    await escolher(page, 'Nubank', 'Conta')
    await expect(page.getByTestId('aviso-limite')).toContainText('500,00')
  })
})

test.describe('contas do mês e visual', () => {
  test('cadastrar salário no quinto dia útil', async ({ page, api: _ }) => {
    await page.goto('/contas-do-mes')
    await page.getByRole('button', { name: 'Entrada', exact: true }).click()
    const form = page.locator('.v-dialog')
    await form.getByLabel('Nome').fill('Salário')
    await form.getByRole('textbox', { name: 'Valor' }).click()
    await form.getByRole('textbox', { name: 'Valor' }).pressSequentially('400000')
    await form.getByLabel('Categoria').click()
    await page.getByRole('option', { name: 'Salário', exact: true }).click()
    await form.locator('.v-select').filter({ hasText: 'Quando cai' }).click()
    await page.getByRole('option', { name: 'N-ésimo dia útil' }).click()
    await expect(form.getByLabel('Qual dia útil')).toHaveValue('5')
    await expect(form.getByTestId('inicia-periodo').locator('input')).toBeChecked()
    await form.getByRole('button', { name: 'Salvar' }).click()
    await expect(page.getByText('Entrada cadastrada')).toBeVisible()

    const salario = page.getByTestId('conta-do-mes').filter({ hasText: 'Salário' })
    await expect(salario).toContainText('todo 5º dia útil')
    await expect(salario).toContainText('dia de pagamento')
    await expect(salario).toContainText('Próxima: 09/11/2026')
  })

  test('menus abrem com fundo sólido (não transparente)', async ({ page, api: _ }) => {
    await page.goto('/planejamento')
    await page.getByRole('button', { name: 'Sugerir orçamento' }).click()
    const lista = page.locator('.v-overlay--active .v-list').first()
    await expect(lista).toBeVisible()
    const fundo = await lista.evaluate((el) => getComputedStyle(el).backgroundColor)
    expect(fundo).not.toBe('rgba(0, 0, 0, 0)')
    expect(fundo).not.toBe('transparent')
  })
})
