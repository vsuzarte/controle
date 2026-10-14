import type { Alerta, ItemPrevisao, PeriodoPrevisao, RegraVencimento } from './tipos'

/**
 * Lógica das telas, separada dos componentes para ter testes unitários (regras.test.ts).
 * Regras de dinheiro de verdade ficam no backend; aqui só o que a interface decide.
 */

const centavos = (v: number) => Math.round(v * 100) / 100

/** Quanto foi pago a mais que o previsto (0 se pagou igual ou menos). */
export function pagoAMais(pago: number | null | undefined, previsto: number) {
  return pago ? Math.max(0, centavos(pago - previsto)) : 0
}

/**
 * Se a caixa "foi juros/multa" já vem marcada: só quando o valor é fixo e foi pago depois do vencimento.
 * Em contas de valor variável (luz, água) pagar mais costuma ser a própria conta.
 */
export function jurosPorPadrao(valorVariavel: boolean, vencimento: string, hoje: string) {
  return !valorVariavel && vencimento < hoje
}

/** Quanto falta no vale para a compra (0 = cabe no saldo; saldo negativo conta como zero). */
export function faltaNoVale(valor: number | null | undefined, saldo: number) {
  return valor ? Math.max(0, centavos(valor - Math.max(0, saldo))) : 0
}

/** Limite disponível quando a compra passa dele; null quando cabe ou não há limite cadastrado. */
export function passaDoLimite(valor: number | null | undefined, limite: number | null | undefined, usado: number | null | undefined) {
  if (!valor || !limite) return null
  const disponivel = centavos(limite - (usado ?? 0))
  return valor > disponivel ? disponivel : null
}

export interface GrupoAtrasado {
  chave: string
  itens: Alerta[]
  total: number
}

/** Vencimentos da tela inicial: atrasados agrupados por conta, próximos 7 dias, depois e o que há a receber. */
export function separarVencimentos(alertas: Alerta[]) {
  const aPagar = alertas.filter((a) => a.tipoLancamento !== 'Receita')
  const grupos = new Map<string, Alerta[]>()
  for (const a of aPagar.filter((x) => x.tipo === 'Vencido')) {
    // Mesma conta do mês (ou mesma fatura) atrasada em vários meses fica numa linha só.
    const chave = a.regraId ?? a.cartaoId ?? a.lancamentoId ?? a.descricao
    grupos.set(chave, [...(grupos.get(chave) ?? []), a])
  }
  const atrasadas: GrupoAtrasado[] = [...grupos.entries()].map(([chave, itens]) => ({
    chave,
    itens,
    total: centavos(itens.reduce((s, a) => s + a.valor, 0)),
  }))
  return {
    aPagar,
    aReceber: alertas.filter((a) => a.tipoLancamento === 'Receita'),
    atrasadas,
    semana: aPagar.filter((a) => a.tipo !== 'Vencido' && a.dias <= 7),
    depois: aPagar.filter((a) => a.tipo !== 'Vencido' && a.dias > 7),
    total: centavos(aPagar.reduce((s, a) => s + a.valor, 0)),
  }
}

/** Situação de um card do quadro da previsão: atrasada, paga depois do prazo (risco de multa) ou em dia. */
export function situacaoCard(i: Pick<ItemPrevisao, 'atrasado' | 'diasDepoisDoVencimento'>) {
  if (i.atrasado) return { cor: 'error', texto: 'atrasada' }
  if (i.diasDepoisDoVencimento) return { cor: 'warning', texto: `+${i.diasDepoisDoVencimento} dia${i.diasDepoisDoVencimento > 1 ? 's' : ''} do vencimento` }
  return { cor: 'success', texto: 'no prazo' }
}

/**
 * Data para gravar ao soltar um card numa coluna (período). Se a coluna é o lugar natural da conta
 * (o prazo dela cai ali), devolve null = volta ao automático. No período de hoje, planeja para hoje.
 */
export function dataAoMover(
  i: Pick<ItemPrevisao, 'data' | 'pagavelAte' | 'pagoCom'>,
  coluna: Pick<PeriodoPrevisao, 'inicio' | 'fim' | 'atual'>,
  hoje: string,
): string | null {
  const prazo = i.pagavelAte ?? i.data
  const natural = (prazo >= coluna.inicio && prazo <= coluna.fim) || (coluna.atual && prazo < coluna.inicio)
  if (natural && !i.pagoCom) return null
  return coluna.atual && coluna.inicio < hoje ? hoje : coluna.inicio
}

// ---------------- Parcelas ----------------

export type ModoParcela = 'total' | 'parcela'

/** Mesma divisão do backend: parcelas iguais e os centavos que sobram na primeira (a soma fecha com o total). */
export function dividirParcelas(total: number, n: number): number[] {
  const centavosTotal = Math.round(total * 100)
  const base = Math.floor(centavosTotal / n)
  return Array.from({ length: n }, (_, i) => (i === 0 ? base + (centavosTotal - base * n) : base) / 100)
}

/**
 * Valor digitado → total da compra e parcelas. No modo "total" o valor é dividido;
 * no modo "parcela" ele é o valor de cada parcela e o total é a multiplicação.
 */
export function calcularParcelas(valor: number | null | undefined, n: number, modo: ModoParcela) {
  if (!valor || n < 1) return null
  const total = modo === 'parcela' ? centavos(valor * n) : valor
  return { total, parcelas: dividirParcelas(total, n) }
}

/** Mês (AAAA-MM) da fatura em que cai uma compra no cartão — mesma regra do backend (compra no dia do fechamento ou depois vai para a seguinte). */
export function faturaDaCompra(data: string, fechamento: number, vencimento: number): string {
  const [a, m, d] = data.split('-').map(Number)
  const diasNoMes = new Date(a, m, 0).getDate()
  let mes = new Date(a, m - 1, 1)
  if (d >= Math.min(fechamento, diasNoMes)) mes = new Date(a, m, 1)
  if (vencimento <= fechamento) mes = new Date(mes.getFullYear(), mes.getMonth() + 1, 1)
  return `${mes.getFullYear()}-${String(mes.getMonth() + 1).padStart(2, '0')}`
}

/** Quando cai uma entrada/conta mensal, em texto: "todo dia 20", "todo 5º dia útil", "todo último dia útil". */
export function quandoCai(vencimento: RegraVencimento, diaUtil: number | null | undefined, dia: number | null | undefined) {
  switch (vencimento) {
    case 'DiaUtil': return `todo ${diaUtil ?? 1}º dia útil`
    case 'UltimoDiaUtil': return 'todo último dia útil'
    case 'UltimoDia': return 'todo último dia do mês'
    default: return dia ? `todo dia ${dia}` : 'todo mês'
  }
}
