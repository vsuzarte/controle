import { test as base, expect, type APIRequestContext, type Page } from '@playwright/test'

/** Data fixa do ambiente E2E (DataFixa no docker-compose.e2e.yml). Quinta-feira. */
export const HOJE = '2026-10-15'

/** Formata como o sistema (Intl pt-BR), para comparar valores na tela. */
export const brl = (v: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v)

/** Atalhos de API para montar cenários rápido; a tela é testada nos passos que importam. */
export class Api {
  constructor(private r: APIRequestContext) {}

  async resetar() {
    const resp = await this.r.post('/api/e2e/reset')
    expect(resp.ok(), 'reset do banco de testes').toBeTruthy()
  }

  private async json<T>(resp: Awaited<ReturnType<APIRequestContext['get']>>): Promise<T> {
    expect(resp.ok(), `${resp.url()} → ${resp.status()} ${await resp.text()}`).toBeTruthy()
    return resp.json() as Promise<T>
  }

  async conta(nome: string): Promise<string> {
    const contas = await this.json<{ id: string; nome: string }[]>(await this.r.get('/api/contas'))
    const c = contas.find((x) => x.nome === nome)
    expect(c, `conta "${nome}"`).toBeTruthy()
    return c!.id
  }

  async categoria(nome: string): Promise<string> {
    const cats = await this.json<{ id: string; nome: string }[]>(await this.r.get('/api/categorias'))
    const c = cats.find((x) => x.nome === nome)
    expect(c, `categoria "${nome}"`).toBeTruthy()
    return c!.id
  }

  async criarCartao(nome = 'Nubank', fechamento = 3, vencimento = 10): Promise<string> {
    const c = await this.json<{ id: string }>(
      await this.r.post('/api/contas', { data: { nome, tipo: 'CartaoCredito', saldoInicial: 0, diaFechamento: fechamento, diaVencimento: vencimento, ativa: true } }),
    )
    return c.id
  }

  async definirSaldo(nome: string, saldo: number) {
    const id = await this.conta(nome)
    await this.json(await this.r.put(`/api/contas/${id}`, { data: { nome, tipo: 'Corrente', saldoInicial: saldo, ativa: true } }))
  }

  async lancar(d: {
    tipo?: 'Despesa' | 'Receita' | 'Transferencia'
    descricao: string
    valor: number
    data?: string
    conta?: string
    categoria?: string
    status?: 'Previsto' | 'Realizado'
    parcelas?: number
  }) {
    return this.json<string[]>(
      await this.r.post('/api/lancamentos', {
        data: {
          tipo: d.tipo ?? 'Despesa',
          descricao: d.descricao,
          valor: d.valor,
          data: d.data ?? HOJE,
          contaId: await this.conta(d.conta ?? 'Conta corrente'),
          categoriaId: d.categoria ? await this.categoria(d.categoria) : null,
          status: d.status ?? null,
          parcelas: d.parcelas ?? null,
          formaPagamento: 'Pix',
        },
      }),
    )
  }

  async contaDoMes(d: { descricao: string; valor: number; categoria: string; inicio: string; tipo?: 'Despesa' | 'Receita'; conta?: string; variavel?: boolean }) {
    return this.json<string>(
      await this.r.post('/api/regras', {
        data: {
          modo: 'Recorrente',
          tipo: d.tipo ?? 'Despesa',
          descricao: d.descricao,
          categoriaId: await this.categoria(d.categoria),
          contaId: await this.conta(d.conta ?? 'Conta corrente'),
          formaPagamento: 'Boleto',
          valor: d.valor,
          valorVariavel: d.variavel ?? false,
          frequencia: 'Mensal',
          intervalo: 1,
          dataInicio: d.inicio,
        },
      }),
    )
  }
}

/** `test` com banco zerado antes de cada teste e a API de apoio disponível. */
export const test = base.extend<{ api: Api }>({
  api: async ({ request }, usar) => {
    const api = new Api(request)
    await api.resetar()
    await usar(api)
  },
})
export { expect }

// ---------------- Ações de tela ----------------

export async function abrirNovoLancamento(page: Page) {
  await page.getByRole('button', { name: /Lançar|Novo lançamento/ }).first().click()
  await expect(page.getByText('Novo lançamento')).toBeVisible()
}

/** Digita só os números, como no app do banco: "8990" → 89,90. */
export async function digitarValor(page: Page, digitos: string) {
  const campo = page.getByRole('textbox', { name: 'Valor', exact: true })
  await campo.click()
  await campo.pressSequentially(digitos)
}

/** Escolhe um chip do formulário (categoria, conta…). Abre "Mais N" se a opção estiver recolhida. */
/** Escolhe um chip do formulário (categoria, conta…). Abre "Mais N" se a opção estiver recolhida. */
export async function escolher(page: Page, texto: string, grupo?: string) {
  const escopo = grupo ? page.locator(`.v-dialog [aria-label="${grupo}"]`) : page.locator('.v-dialog')
  const literal = texto.replace(/[.*+?^${}()|[\]\\/]/g, '\\$&')
  const chip = escopo.locator('.v-chip', { hasText: new RegExp(`^\\s*${literal}\\s*$`) }).first()
  if (!(await chip.isVisible())) {
    const mais = escopo.locator('.v-chip', { hasText: /^\s*Mais \d+\s*$/ })
    if (await mais.count()) await mais.first().click()
  }
  await chip.click()
}

export async function salvar(page: Page, mensagem = 'Lançamento salvo') {
  await page.getByRole('button', { name: /^Salvar( correção)?$/ }).click()
  await expect(page.getByText(mensagem).first()).toBeVisible()
}

export async function irPara(page: Page, nome: string) {
  await page.getByRole('link', { name: nome, exact: true }).first().click()
}

/** Compara texto ignorando o espaço especial que o Intl coloca depois de "R$". */
export const textoValor = (v: number) => new RegExp(brl(v).replace(/\s/g, '\\s').replace(/\$/g, '\\$').replace(/\./g, '\\.'))
