export type TipoConta = 'Corrente' | 'Poupanca' | 'Dinheiro' | 'CarteiraDigital' | 'CartaoCredito' | 'Beneficio'
export type TipoCategoria = 'Receita' | 'Despesa'
export type TipoLancamento = 'Receita' | 'Despesa' | 'Transferencia'
export type StatusLancamento = 'Previsto' | 'Realizado' | 'Cancelado'
export type FormaPagamento =
  | 'Pix' | 'Boleto' | 'DebitoAutomatico' | 'CartaoCredito' | 'CartaoDebito' | 'Dinheiro' | 'Transferencia' | 'Outro'
export type ModoRegra = 'Recorrente' | 'Parcelado'
export type Frequencia = 'Semanal' | 'Mensal' | 'Anual'
export type RegraVencimento = 'DiaFixo' | 'DiaUtil' | 'UltimoDia' | 'UltimoDiaUtil'

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
  regraId: string | null
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
  { value: 'Beneficio', title: 'Vale-refeição / alimentação', icone: 'mdi-food' },
]

export const VENCIMENTOS: { value: RegraVencimento; title: string }[] = [
  { value: 'DiaFixo', title: 'Todo mês no mesmo dia' },
  { value: 'DiaUtil', title: 'N-ésimo dia útil' },
  { value: 'UltimoDiaUtil', title: 'Último dia útil' },
  { value: 'UltimoDia', title: 'Último dia do mês' },
]
