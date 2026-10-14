<script setup lang="ts">
import { ref, watch } from 'vue'
import draggable from 'vuedraggable'
import { api } from '../api'
import { alterado, erro } from '../estado'
import { brl, dataBr, hojeIso } from '../formato'
import { dataAoMover, situacaoCard } from '../regras'
import type { ItemPrevisao, PeriodoPrevisao } from '../tipos'

/**
 * Previsão como quadro: cada coluna é um período de pagamento e cada card uma conta ou fatura pendente.
 * Arrastar um card = pagar com o dinheiro daquele período (só esta conta/fatura; a conta do mês não muda).
 */
const props = defineProps<{ periodos: PeriodoPrevisao[] }>()
const emit = defineEmits<{ agir: [ItemPrevisao] }>()

interface Coluna { periodo: PeriodoPrevisao; cards: ItemPrevisao[] }
const colunas = ref<Coluna[]>([])
const verPagas = ref<Record<string, boolean>>({})

watch(() => props.periodos, (ps) => {
  colunas.value = ps.map((p) => ({ periodo: p, cards: p.itens.filter((i) => i.tipo !== 'Entrada' && i.pendente) }))
}, { immediate: true })

const entradas = (p: PeriodoPrevisao) => p.itens.filter((i) => i.tipo === 'Entrada')
const pagas = (p: PeriodoPrevisao) => p.itens.filter((i) => i.tipo !== 'Entrada' && !i.pendente)
const chave = (i: ItemPrevisao) => i.lancamentoId ?? `${i.cartaoId}-${i.faturaMes}`

async function mover(i: ItemPrevisao, coluna: PeriodoPrevisao) {
  try {
    await api.put('/previsao/mover', {
      lancamentoId: i.lancamentoId,
      cartaoId: i.lancamentoId ? null : i.cartaoId,
      faturaMes: i.lancamentoId ? null : i.faturaMes,
      pagarEm: dataAoMover(i, coluna, hojeIso()),
    })
    alterado()
  } catch (e) {
    erro(e)
    alterado() // volta o quadro ao que está salvo
  }
}

async function voltarAoAutomatico(i: ItemPrevisao) {
  try {
    await api.put('/previsao/mover', {
      lancamentoId: i.lancamentoId, cartaoId: i.lancamentoId ? null : i.cartaoId, faturaMes: i.lancamentoId ? null : i.faturaMes, pagarEm: null,
    })
    alterado('Voltou para o vencimento')
  } catch (e) {
    erro(e)
  }
}

function aoMudar(evento: { added?: { element: ItemPrevisao } }, coluna: PeriodoPrevisao) {
  if (evento.added) mover(evento.added.element, coluna)
}
</script>

<template>
  <div class="quadro" data-testid="quadro">
    <v-card v-for="(c, idx) in colunas" :key="c.periodo.inicio" class="coluna" :class="{ 'coluna--atual': c.periodo.atual }"
      data-testid="coluna" :aria-label="c.periodo.titulo">
      <!-- Cabeçalho: de onde vem o dinheiro -->
      <div class="pa-4 pb-2">
        <div class="d-flex align-center ga-2">
          <v-avatar :color="c.periodo.atual ? 'primary' : 'surface-variant'" rounded="lg" size="32"><v-icon size="18">mdi-cash-fast</v-icon></v-avatar>
          <div class="flex-grow-1" style="min-width: 0">
            <div class="font-weight-bold text-truncate">{{ c.periodo.titulo }}</div>
            <div class="text-caption text-medium-emphasis">{{ dataBr(c.periodo.inicio) }} a {{ dataBr(c.periodo.fim) }}</div>
          </div>
          <v-chip v-if="c.periodo.atual" size="x-small" color="primary" variant="flat">agora</v-chip>
        </div>
        <div class="d-flex justify-space-between text-body-2 mt-3">
          <span class="text-medium-emphasis">Começa com</span><span class="valor">{{ brl(c.periodo.saldoInicio) }}</span>
        </div>
        <div v-for="e in entradas(c.periodo)" :key="chave(e)" class="d-flex justify-space-between text-body-2">
          <span class="text-truncate" :class="{ riscado: !e.pendente }">+ {{ e.descricao }} <span class="text-caption text-medium-emphasis">{{ dataBr(e.data).slice(0, 5) }}</span></span>
          <span class="valor text-success">{{ brl(e.valor) }}</span>
        </div>
      </div>

      <v-divider />

      <!-- Cards: o que esse dinheiro paga -->
      <draggable
        :list="c.cards"
        group="contas"
        :item-key="chave"
        class="cards pa-3"
        ghost-class="card--fantasma"
        :delay="180"
        :delay-on-touch-only="true"
        :animation="150"
        @change="aoMudar($event, c.periodo)"
      >
        <template #item="{ element: i }">
          <v-card class="card mb-2" :class="'card--' + situacaoCard(i).cor" variant="flat" data-testid="card-conta" :aria-label="i.descricao">
            <div class="d-flex align-start ga-2 pa-3">
              <v-icon size="18" :color="i.cor ?? undefined" class="mt-1">{{ i.icone ?? (i.tipo === 'Fatura' ? 'mdi-credit-card-outline' : 'mdi-receipt-text-outline') }}</v-icon>
              <div class="flex-grow-1" style="min-width: 0">
                <div class="font-weight-medium text-truncate">{{ i.descricao }}</div>
                <div class="text-caption text-medium-emphasis">
                  vence {{ dataBr(i.data, true) }}<template v-if="i.tipo === 'Fatura' && i.pago > 0"> · falta {{ brl(i.valor - i.pago) }}</template>
                </div>
                <div class="d-flex flex-wrap ga-1 mt-1">
                  <v-chip size="x-small" :color="situacaoCard(i).cor" variant="tonal" label data-testid="situacao">{{ situacaoCard(i).texto }}</v-chip>
                  <v-chip v-if="i.movido" size="x-small" variant="tonal" label prepend-icon="mdi-cursor-move">movida</v-chip>
                  <v-chip v-else-if="i.pagoCom" size="x-small" variant="tonal" label>pago com {{ i.pagoCom }}</v-chip>
                </div>
              </div>
              <div class="text-right">
                <div class="valor">{{ brl(i.valor - i.pago) }}</div>
                <v-menu>
                  <template #activator="{ props: p }">
                    <v-btn v-bind="p" icon="mdi-dots-horizontal" variant="text" size="x-small" :aria-label="'Ações de ' + i.descricao" />
                  </template>
                  <v-list density="compact" rounded="lg">
                    <v-list-item :prepend-icon="i.tipo === 'Fatura' ? 'mdi-cash-check' : 'mdi-check'" :title="i.tipo === 'Fatura' ? 'Pagar fatura' : 'Paguei'" @click="emit('agir', i)" />
                    <v-list-subheader>Pagar com o dinheiro de</v-list-subheader>
                    <v-list-item v-for="(o, j) in colunas" :key="o.periodo.inicio" :disabled="j === idx"
                      :title="o.periodo.titulo" :subtitle="`${dataBr(o.periodo.inicio)} a ${dataBr(o.periodo.fim)}`" @click="mover(i, o.periodo)" />
                    <v-list-item v-if="i.movido" prepend-icon="mdi-undo" title="Voltar para o vencimento" @click="voltarAoAutomatico(i)" />
                  </v-list>
                </v-menu>
              </div>
            </div>
          </v-card>
        </template>
        <template #footer>
          <div v-if="!c.cards.length" class="vazio text-caption text-medium-emphasis text-center pa-4">Nada a pagar aqui. Arraste uma conta para cá.</div>
        </template>
      </draggable>

      <div v-if="pagas(c.periodo).length" class="px-4 pb-2">
        <v-btn variant="text" size="small" class="px-0" :append-icon="verPagas[c.periodo.inicio] ? 'mdi-chevron-up' : 'mdi-chevron-down'"
          @click="verPagas[c.periodo.inicio] = !verPagas[c.periodo.inicio]">
          {{ pagas(c.periodo).length }} já paga(s)
        </v-btn>
        <template v-if="verPagas[c.periodo.inicio]">
          <div v-for="i in pagas(c.periodo)" :key="chave(i)" class="d-flex justify-space-between text-body-2 riscado">
            <span class="text-truncate">{{ i.descricao }}</span><span class="valor">{{ brl(i.valor) }}</span>
          </div>
        </template>
      </div>

      <!-- Rodapé: o que sobra -->
      <v-divider class="mt-auto" />
      <div class="pa-4 d-flex align-center">
        <div>
          <div class="rotulo">Sobra</div>
          <div v-if="c.periodo.porDia" class="text-caption text-medium-emphasis">≈ {{ brl(c.periodo.porDia) }}/dia</div>
        </div>
        <v-spacer />
        <div class="medio" :class="c.periodo.saldoFim < 0 ? 'text-error' : 'text-success'" data-testid="sobra-coluna">{{ brl(c.periodo.saldoFim) }}</div>
      </div>
    </v-card>
  </div>
</template>

<style scoped>
.quadro { display: flex; gap: 16px; overflow-x: auto; padding-bottom: 8px; scroll-snap-type: x mandatory; }
.coluna { flex: 1 1 320px; min-width: 300px; display: flex; flex-direction: column; scroll-snap-align: start; }
.coluna--atual { outline: 2px solid rgb(var(--v-theme-primary)); outline-offset: -2px; }
.cards { min-height: 96px; flex-grow: 1; }
.card { border: 1px solid var(--borda); border-left-width: 4px; cursor: grab; user-select: none; }
.card:active { cursor: grabbing; }
.card--success { border-left-color: rgb(var(--v-theme-success)); }
.card--warning { border-left-color: rgb(var(--v-theme-warning)); }
.card--error { border-left-color: rgb(var(--v-theme-error)); }
.card--fantasma { opacity: 0.35; }
.vazio { border: 1px dashed var(--borda); border-radius: 12px; }
@media (max-width: 600px) { .coluna { flex-basis: 85%; } }
</style>
