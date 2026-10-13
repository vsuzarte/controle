import { test, expect, abrirNovoLancamento } from './apoio'

test.describe('máscara de valor', () => {
  test.beforeEach(async ({ page, api: _ }) => {
    await page.goto('/')
    await abrirNovoLancamento(page)
  })

  test('números entram pela direita, como no app do banco', async ({ page }) => {
    const campo = page.getByRole('textbox', { name: 'Valor', exact: true })
    await campo.click()
    await campo.pressSequentially('1')
    await expect(campo).toHaveValue('0,01')
    await campo.pressSequentially('9347')
    await expect(campo).toHaveValue('193,47')
    await campo.pressSequentially('00')
    await expect(campo).toHaveValue('19.347,00')
  })

  test('apagar remove o último dígito', async ({ page }) => {
    const campo = page.getByRole('textbox', { name: 'Valor', exact: true })
    await campo.click()
    await campo.pressSequentially('19347')
    await campo.press('Backspace')
    await expect(campo).toHaveValue('19,34')
  })

  test('letras e símbolos são ignorados', async ({ page }) => {
    const campo = page.getByRole('textbox', { name: 'Valor', exact: true })
    await campo.click()
    await campo.pressSequentially('5a0,b0')
    await expect(campo).toHaveValue('5,00')
  })

  test('colar valor formatado funciona', async ({ page }) => {
    const campo = page.getByRole('textbox', { name: 'Valor', exact: true })
    await campo.click()
    await campo.evaluate((el) => {
      const dt = new DataTransfer()
      dt.setData('text', '1.234,56')
      el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }))
    })
    await expect(campo).toHaveValue('1.234,56')
  })

  test('sem valor não salva e avisa', async ({ page }) => {
    await page.getByRole('button', { name: 'Salvar', exact: true }).click()
    await expect(page.getByText('Digite o valor')).toBeVisible()
  })
})
