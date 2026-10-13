// Tira capturas de tela de todas as páginas (computador e celular) para revisão visual.
// Uso (com o ambiente de testes no ar): node capturas.mjs  → arquivos em ./capturas
import { chromium, devices } from '@playwright/test'

const base = process.env.BASE_URL ?? 'http://localhost:8090'
const senha = process.env.SENHA_E2E ?? 'e2e'
const rotas = ['/', '/lancamentos', '/contas-do-mes', '/planejamento', '/cartoes', '/cadastros']
const telas = {
  computador: { viewport: { width: 1366, height: 860 } },
  celular: devices['Pixel 7'],
}
const temas = (process.env.TEMAS ?? 'light').split(',')

const navegador = await chromium.launch()
for (const tema of temas) {
  for (const [nome, cfg] of Object.entries(telas)) {
    const ctx = await navegador.newContext({ ...cfg, baseURL: base, locale: 'pt-BR', colorScheme: tema })
    const page = await ctx.newPage()
    await page.goto('/')
    if (await page.getByLabel('Senha').isVisible().catch(() => false)) {
      await page.getByLabel('Senha').fill(senha)
      await page.getByRole('button', { name: 'Entrar' }).click()
      await page.getByTestId('saldo-disponivel').waitFor()
    }
    for (const rota of rotas) {
      await page.goto(rota)
      await page.waitForLoadState('networkidle')
      await page.waitForTimeout(700)
      const arquivo = `capturas/${nome}-${tema}${rota === '/' ? '-inicio' : rota.replace(/\//g, '-')}.png`
      await page.screenshot({ path: arquivo, fullPage: true })
      console.log(arquivo)
    }
    if (process.env.FORM) {
      await page.goto('/')
      await page.getByRole('button', { name: /Lançar|Novo lançamento/ }).first().click()
      await page.waitForTimeout(600)
      await page.screenshot({ path: `capturas/${nome}-${tema}-formulario.png` })
    }
    await ctx.close()
  }
}
await navegador.close()
