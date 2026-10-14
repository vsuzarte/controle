import { computed, reactive } from 'vue'
import { api } from './api'
import { hojeIso, mesIso } from './formato'
import type { Categoria, Conta, Lancamento } from './tipos'

/**
 * Estado compartilhado da interface. Regras financeiras ficam no backend;
 * aqui só há cache de cadastros, mês selecionado e os diálogos globais.
 */
export const estado = reactive({
  contas: [] as Conta[],
  categorias: [] as Categoria[],
  mes: mesIso(hojeIso()),
  /** Incrementado a cada alteração; as telas observam para recarregar. */
  versao: 0,
  aviso: { mostrar: false, texto: '', cor: 'success' },
  formulario: { aberto: false, editar: null as Lancamento | null, tipo: 'Despesa' as Lancamento['tipo'] },
  confirmar: { aberto: false, lancamento: null as Lancamento | null, valor: null as number | null },
  historico: { aberto: false, entidadeId: '', titulo: '' },
  pagarFatura: { aberto: false, cartaoId: '', cartao: '', mes: '', vencimento: '', restante: 0 },
})

export const contasAtivas = computed(() => estado.contas.filter((c) => c.ativa))
export const contasNaoCartao = computed(() => contasAtivas.value.filter((c) => c.tipo !== 'CartaoCredito'))
/** Contas que pagam boletos e faturas: sem cartão e sem vale. */
export const contasPagadoras = computed(() => contasNaoCartao.value.filter((c) => c.tipo !== 'Beneficio'))
export const cartoes = computed(() => contasAtivas.value.filter((c) => c.tipo === 'CartaoCredito'))

export async function carregarCadastros() {
  const [contas, categorias] = await Promise.all([api.get<Conta[]>('/contas'), api.get<Categoria[]>('/categorias')])
  // Bancos primeiro, depois dinheiro, cartões e vale (o servidor ordena pelo nome do tipo).
  const ordem: Record<string, number> = { Corrente: 0, CarteiraDigital: 1, Poupanca: 2, Dinheiro: 3, CartaoCredito: 4, Beneficio: 5 }
  estado.contas = [...contas].sort((a, b) => (ordem[a.tipo] ?? 9) - (ordem[b.tipo] ?? 9) || a.nome.localeCompare(b.nome))
  estado.categorias = categorias
}

export function avisar(texto: string, cor = 'success') {
  estado.aviso = { mostrar: true, texto, cor }
}

export function erro(e: unknown) {
  avisar(e instanceof Error ? e.message : String(e), 'error')
}

/** Marca que algo mudou: telas recarregam e cadastros (saldos) são atualizados. */
export function alterado(msg?: string) {
  estado.versao++
  if (msg) avisar(msg)
  carregarCadastros().catch(() => {})
}

export function novoLancamento(tipo: Lancamento['tipo'] = 'Despesa') {
  estado.formulario = { aberto: true, editar: null, tipo }
}

export function editarLancamento(l: Lancamento) {
  estado.formulario = { aberto: true, editar: l, tipo: l.tipo }
}

export function confirmarLancamento(l: Lancamento, valor: number | null = null) {
  estado.confirmar = { aberto: true, lancamento: l, valor }
}

export function pagarFatura(f: { cartaoId: string; cartao: string; mes: string; vencimento: string; restante: number }) {
  estado.pagarFatura = { aberto: true, cartaoId: f.cartaoId, cartao: f.cartao, mes: f.mes, vencimento: f.vencimento, restante: f.restante }
}

export function verHistorico(entidadeId: string, titulo: string) {
  estado.historico = { aberto: true, entidadeId, titulo }
}
