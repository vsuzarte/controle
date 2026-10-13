export type TipoConta = 'Corrente' | 'Poupanca' | 'Dinheiro' | 'CarteiraDigital' | 'CartaoCredito'
export type TipoCategoria = 'Receita' | 'Despesa'
export type TipoLancamento = 'Receita' | 'Despesa' | 'Transferencia'
export type StatusLancamento = 'Previsto' | 'Realizado' | 'Cancelado'
export type FormaPagamento =
  | 'Pix' | 'Boleto' | 'DebitoAutomatico' | 'CartaoCredito' | 'CartaoDebito' | 'Dinheiro' | 'Transferencia' | 'Outro'
export type ModoRegra = 'Recorrente' | 'Parcelado'
export type Frequencia = 'Semanal' | 'Mensal' | 'Anual'

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
}

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
  gastosPorCategoria: { categoriaId: string; categoria: string; cor: string | null; gasto: number; orcado: number | null }[]
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

export const FORMAS: { value: FormaPagamento; title: string }[] = [
  { value: 'Pix', title: 'Pix' },
  { value: 'CartaoDebito', title: 'Cartão de débito' },
  { value: 'Boleto', title: 'Boleto' },
  { value: 'DebitoAutomatico', title: 'Débito automático' },
  { value: 'Dinheiro', title: 'Dinheiro' },
  { value: 'Transferencia', title: 'Transferência' },
  { value: 'CartaoCredito', title: 'Cartão de crédito' },
  { value: 'Outro', title: 'Outro' },
]

export const TIPOS_CONTA: { value: TipoConta; title: string; icone: string }[] = [
  { value: 'Corrente', title: 'Conta corrente', icone: 'mdi-bank' },
  { value: 'Poupanca', title: 'Poupança', icone: 'mdi-piggy-bank' },
  { value: 'CarteiraDigital', title: 'Carteira digital', icone: 'mdi-cellphone' },
  { value: 'Dinheiro', title: 'Dinheiro', icone: 'mdi-cash' },
  { value: 'CartaoCredito', title: 'Cartão de crédito', icone: 'mdi-credit-card' },
]
