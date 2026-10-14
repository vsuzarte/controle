// Tela inicial.
import type { Fatura } from './cartoes'
import type { TipoConta, TipoLancamento } from './enums'
import type { Planejamento } from './planejamento'
import type { Previsao } from './previsao'

export interface Alerta {
  tipo: 'Vencido' | 'Hoje' | 'Proximo'
  dias: number
  descricao: string
  valor: number
  data: string
  tipoLancamento: TipoLancamento | null
  lancamentoId: string | null
  cartaoId: string | null
  faturaMes: string | null
  regraId: string | null
  /** Vencimento original quando cai em fim de semana/feriado; "data" já é o próximo dia útil. */
  venceEm: string | null
}

export interface Resumo {
  hoje: string
  saldoDisponivel: number
  contas: { id: string; nome: string; tipo: TipoConta; cor: string | null; saldo: number }[]
  entradasMes: number
  saidasMes: number
  alertas: Alerta[]
  faturasAbertas: Fatura[]
  receitasAReceber: number
  despesasAPagar: number
  faturasAPagar: number
  variavelRestante: number
  saldoProjetado: number
  planejamento: Planejamento
  gastosPorCategoria: { categoriaId: string; categoria: string; cor: string | null; gasto: number; variavel: number; orcado: number | null }[]
  previsao: Previsao
  beneficios: Beneficio[]
  /** Recargas do vale no mês (fora de entradasMes: só servem para compras). */
  entradasVale: number
}

export interface Beneficio {
  id: string
  nome: string
  cor: string | null
  saldo: number
  proximaRecarga: string | null
  valorRecarga: number | null
  gastoPorDia: number
  duraAte: string | null
}
