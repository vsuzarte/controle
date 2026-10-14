// Previsão por períodos de pagamento.

export type TipoItemPrevisao = 'Entrada' | 'Conta' | 'Fatura'

export interface ItemPrevisao {
  tipo: TipoItemPrevisao
  descricao: string
  detalhe: string | null
  cor: string | null
  icone: string | null
  data: string
  valor: number
  pago: number
  pendente: boolean
  atrasado: boolean
  lancamentoId: string | null
  regraId: string | null
  cartaoId: string | null
  faturaMes: string | null
  juros: number | null
  /** Vence em fim de semana/feriado: dá para pagar até este dia útil (é ele que define o período). */
  pagavelAte: string | null
  /** Conta marcada "pagar com": o pagamento e o dia em que entra o dinheiro. */
  pagoCom: string | null
  pagarEm: string | null
  /** Pagando com esse dinheiro, fica X dias depois do prazo sem juros. */
  diasDepoisDoVencimento: number | null
  /** Movida à mão no quadro (só esta ocorrência). */
  movido: boolean
}

export interface PeriodoPrevisao {
  inicio: string
  fim: string
  titulo: string
  atual: boolean
  encerrado: boolean
  saldoInicio: number
  saldoFim: number
  entradas: number
  aReceber: number
  contas: number
  contasAPagar: number
  faturas: number
  faturasAPagar: number
  diaADia: number
  diasRestantes: number
  porDia: number | null
  itens: ItemPrevisao[]
  /** Período de hoje já todo pago/recebido: aparece resumido e a janela avança. */
  quitado: boolean
}

export interface Previsao {
  hoje: string
  inicio: string
  fim: string
  porSalario: boolean
  periodos: PeriodoPrevisao[]
  saldoInicio: number
  saldoFim: number
  entradas: number
  contas: number
  faturas: number
  diaADia: number
  personalizado: boolean
}

export interface PeriodoSalvo {
  id: string
  nome: string
  de: string
  ate: string
}

export interface ConfigPrevisao {
  /** Mostrar até antes do N-ésimo próximo pagamento (null = padrão). */
  ateAntesDoPagamento: number | null
  pularQuitado: boolean
  padrao: number
  pagamentosPorMes: number
  ateHoje: string | null
}
