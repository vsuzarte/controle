// Extrato de uma conta no mês.
import type { TipoConta } from './enums'
import type { Lancamento } from './lancamentos'

export interface ExtratoLinha {
  lancamento: Lancamento
  /** Quanto mexe na conta: + entra, − sai. */
  efeito: number
  /** Saldo da conta depois deste movimento (null = previsto ou cancelado, não mexe no saldo). */
  saldo: number | null
}

export interface Extrato {
  contaId: string
  conta: string
  tipo: TipoConta
  mes: string
  saldoInicio: number
  entrou: number
  saiu: number
  saldoFim: number
  saldoPrevisto: number
  linhas: ExtratoLinha[]
}
