// Valores fixos que vêm da API (enums do backend, como texto).

export type TipoConta = 'Corrente' | 'Poupanca' | 'Dinheiro' | 'CarteiraDigital' | 'CartaoCredito' | 'Beneficio'
export type TipoCategoria = 'Receita' | 'Despesa'
export type TipoLancamento = 'Receita' | 'Despesa' | 'Transferencia'
export type StatusLancamento = 'Previsto' | 'Realizado' | 'Cancelado'
export type FormaPagamento =
  | 'Pix' | 'Boleto' | 'DebitoAutomatico' | 'CartaoCredito' | 'CartaoDebito' | 'Dinheiro' | 'Transferencia' | 'Outro'
export type ModoRegra = 'Recorrente' | 'Parcelado'
export type Frequencia = 'Semanal' | 'Mensal' | 'Anual'
export type RegraVencimento = 'DiaFixo' | 'DiaUtil' | 'UltimoDia' | 'UltimoDiaUtil'
