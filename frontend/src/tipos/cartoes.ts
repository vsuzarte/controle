// Faturas e visão dos cartões.
import type { Lancamento } from './lancamentos'

export type StatusFatura = 'Aberta' | 'Fechada' | 'Paga' | 'Vencida' | 'Futura'

export interface Fatura {
  cartaoId: string
  cartao: string
  mes: string
  fechamento: string
  vencimento: string
  total: number
  pago: number
  restante: number
  status: StatusFatura
  lancamentos: Lancamento[]
  pagamentos: Lancamento[]
}

export interface CartaoResumo {
  id: string
  nome: string
  cor: string | null
  diaFechamento: number
  diaVencimento: number
  limite: number | null
  limiteUsado: number
  limiteDisponivel: number | null
  aPagar: Fatura[]
  aberta: Fatura | null
}

export interface TodosCartoes {
  cartoes: CartaoResumo[]
  totalAPagar: number
  totalEmAberto: number
  limiteTotal: number
  limiteUsadoTotal: number
}
