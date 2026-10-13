import { test, expect, irPara, textoValor } from './apoio'

test.describe('planejamento', () => {
  test('sugere orçamento pelo mês anterior, ignorando contas fixas', async ({ page, api }) => {
    await api.lancar({ descricao: 'Cinema', valor: 300, categoria: 'Lazer', data: '2026-09-12' })
    await api.contaDoMes({ descricao: 'Aluguel', valor: 1200, categoria: 'Moradia', inicio: '2026-09-10' })
    await page.goto('/planejamento')

    const lazer = page.locator('.v-list-item').filter({ hasText: 'Lazer' })
    await expect(lazer).toContainText('sugerido')
    await expect(lazer).toContainText('300,00')
    // Aluguel é compromisso fixo: aparece nos compromissos, não como sugestão de gasto variável.
    await expect(page.locator('.v-list-item').filter({ hasText: 'Moradia' }).filter({ hasText: 'sugerido' })).toHaveCount(0)
    await expect(page.getByTestId('lancamento').filter({ hasText: 'Aluguel' })).toHaveCount(1)
  })

  test('remover da previsão não volta sozinho e pode ser restaurado', async ({ page, api }) => {
    await api.lancar({ descricao: 'Cinema', valor: 300, categoria: 'Lazer', data: '2026-09-12' })
    await page.goto('/planejamento')
    const lazer = page.locator('.v-list-item').filter({ hasText: 'Lazer' })
    await lazer.getByRole('button', { name: 'Remover da previsão' }).click()
    await expect(page.getByText('removida da previsão')).toBeVisible()

    await page.reload()
    await expect(lazer.getByRole('button', { name: 'Restaurar' })).toBeVisible()
    await page.getByRole('button', { name: 'Sugerir orçamento' }).click()
    await page.getByText('Com base no mês anterior').click()
    await expect(lazer.getByRole('button', { name: 'Restaurar' })).toBeVisible()

    await lazer.getByRole('button', { name: 'Restaurar' }).click()
    await expect(lazer).toContainText('300,00')
  })

  test('editar o orçamento de uma categoria', async ({ page, api }) => {
    await api.lancar({ descricao: 'Feira', valor: 800, categoria: 'Mercado', data: '2026-09-05' })
    await page.goto('/planejamento')
    const mercado = page.locator('.v-list-item').filter({ hasText: 'Mercado' })
    await mercado.getByRole('button', { name: 'Editar valor' }).click()
    const campo = page.locator('.v-dialog').getByLabel('Quanto pretende gastar no mês')
    await campo.click()
    for (let i = 0; i < 6; i++) await campo.press('Backspace')
    await campo.pressSequentially('65000')
    await page.locator('.v-dialog').getByRole('button', { name: 'Salvar' }).click()
    await expect(mercado).toContainText('ajustado')
    await expect(mercado).toContainText('650,00')

    await irPara(page, 'Início')
    await expect(page.getByTestId('sobra-prevista')).toHaveText(textoValor(-650))
  })
})
