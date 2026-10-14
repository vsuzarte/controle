// Contas, categorias e contas do mês (regras).
import type { FormaPagamento, Frequencia, ModoRegra, RegraVencimento, TipoCategoria, TipoConta, TipoLancamento } from './enums'

export interface Conta {
  id: string
  nome: string
  tipo: TipoConta
  saldoInicial: number
  cor: string | null
  diaFechamento: number | null
  diaVencimento: number | null
  ativa: boolean
  saldo: number
  /** Só cartão. */
  limite: number | null
  limiteUsado: number | null
}

export interface Categoria {
  id: string
  nome: string
  tipo: TipoCategoria
  grupo: string | null
  cor: string | null
  icone: string | null
  ativa: boolean
}

export interface Regra {
  id: string
  modo: ModoRegra
  tipo: TipoLancamento
  descricao: string
  categoriaId: string
  categoria: string
  categoriaCor: string | null
  contaId: string
  conta: string
  formaPagamento: FormaPagamento | null
  valor: number
  valorVariavel: boolean
  parcelas: number | null
  frequencia: Frequencia
  intervalo: number
  dataInicio: string
  dataFim: string | null
  ativa: boolean
  observacao: string | null
  realizadas: number
  pendentes: number
  proximaData: string | null
  proximoValor: number | null
  atrasadas: number
  vencimento: RegraVencimento
  diaUtilNumero: number | null
  iniciaPeriodo: boolean
  /** Contas: dia de pagamento que paga esta conta na previsão (null = automático). */
  pagoComRegraId: string | null
  pagoCom: string | null
}
