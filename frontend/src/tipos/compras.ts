// Compras pela nota fiscal (NFC-e).

export interface ItemLido {
  sequencia: number
  descricao: string
  codigo: string | null
  quantidade: number
  unidade: string | null
  valorUnitario: number
  valorTotal: number
  /** Com a parte do desconto da nota: a soma bate com o que foi pago. */
  valorPago: number
  categoriaId: string
}

export interface LeituraNota {
  url: string
  chave: string
  loja: string
  lojaNaNota: string
  cnpj: string | null
  data: string
  emissao: string
  valorTotal: number
  desconto: number
  valorAPagar: number
  formaPagamento: string | null
  contaSugeridaId: string | null
  categoriaPadraoId: string | null
  itens: ItemLido[]
  /** Já importada (a mesma nota não entra duas vezes). */
  compraExistenteId: string | null
}

export interface ItemCompra {
  sequencia: number
  descricao: string
  codigo: string | null
  quantidade: number
  unidade: string | null
  valorUnitario: number
  valorPago: number
  categoriaId: string
  categoria: string
  categoriaCor: string | null
}

export interface Compra {
  id: string
  loja: string
  emissao: string
  total: number
  desconto: number
  formaPagamentoNota: string | null
  contaId: string
  conta: string
  itens: ItemCompra[]
}
