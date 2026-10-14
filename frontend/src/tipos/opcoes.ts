// Opções dos campos de seleção.
import type { FormaPagamento, RegraVencimento, TipoConta } from './enums'

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
