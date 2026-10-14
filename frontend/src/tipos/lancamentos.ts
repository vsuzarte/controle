// Lançamentos e histórico de alterações.
import type { FormaPagamento, StatusLancamento, TipoLancamento } from './enums'

export interface Lancamento {
  id: string
  tipo: TipoLancamento
  status: StatusLancamento
  descricao: string
  valor: number
  data: string
  contaId: string
  conta: string
  contaDestinoId: string | null
  contaDestino: string | null
  categoriaId: string | null
  categoria: string | null
  categoriaCor: string | null
  categoriaIcone: string | null
  formaPagamento: FormaPagamento | null
  observacao: string | null
  programado: boolean
  regraId: string | null
  sequencia: number | null
  totalParcelas: number | null
  faturaMes: string | null
  estornoDeId: string | null
  motivoCancelamento: string | null
  criadoEm: string
  atualizadoEm: string
  juros: number | null
  valorVariavel: boolean
  /** Veio de uma nota fiscal: itens em /compras/{compraId}. */
  compraId: string | null
  /** Só na busca: itens da nota que batem com o texto (ex.: a Coca dentro da compra do mercado). */
  itensEncontrados?: { descricao: string; quantidade: number; unidade: string | null; valorPago: number }[] | null
}

export interface AuditLog {
  id: number
  entidade: string
  entidadeId: string
  operacao: string
  antes: string | null
  depois: string | null
  motivo: string | null
  dataHora: string
}
