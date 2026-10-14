import type { Alerta } from './tipos'

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
