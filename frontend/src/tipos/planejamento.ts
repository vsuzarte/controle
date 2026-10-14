// Planejamento (orçamento) do mês.
import type { Lancamento } from './lancamentos'

export interface CategoriaPlanejada {
  categoriaId: string
  categoria: string
  cor: string | null
  icone: string | null
  grupo: string | null
  orcado: number | null
  sugerido: number | null
  gasto: number
  editado: boolean
  dispensado: boolean
  considerado: number
}

export interface Planejamento {
  mes: string
  receitas: Lancamento[]
  compromissos: Lancamento[]
  variaveis: CategoriaPlanejada[]
  totalReceitas: number
  receitasRecebidas: number
  totalCompromissos: number
  compromissosPagos: number
  totalVariaveis: number
  gastoVariavel: number
  resultadoPrevisto: number
}
