// Relatórios.
import type { Lancamento } from './lancamentos'

export interface SerieMensal {
  id: string | null
  nome: string
  cor: string | null
  icone: string | null
  valores: number[]
}

export interface Comparativo {
  meses: string[]
  total: SerieMensal
  categorias: SerieMensal[]
  contasFixas: SerieMensal[]
  cartoes: SerieMensal[]
  juros: SerieMensal
  jurosDetalhe: Lancamento[]
}
