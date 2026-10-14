<script setup lang="ts">
import { ref, watch } from 'vue'
import { api } from '../api'
import { erro, estado } from '../estado'
import { brl, dataHoraBr } from '../formato'
import type { Compra } from '../tipos'

/** Itens de uma compra lançada pela nota (abre pelo lançamento ou pela tela "O que comprei"). */
const compra = ref<Compra | null>(null)

watch(() => estado.compra.aberto, async (aberto) => {
  if (!aberto) return
  compra.value = null
  try {
    compra.value = await api.get<Compra>(`/compras/${estado.compra.id}`)
  } catch (e) {
    erro(e)
  }
})
</script>

<template>
  <v-dialog v-model="estado.compra.aberto" max-width="640" scrollable>
    <v-card>
      <div class="d-flex align-center ga-2 px-4 pt-3 pb-2">
        <v-icon color="primary">mdi-receipt-text-outline</v-icon>
        <div style="min-width: 0">
          <div class="font-weight-bold text-truncate">{{ compra?.loja ?? 'Compra' }}</div>
          <div v-if="compra" class="text-caption text-medium-emphasis">{{ dataHoraBr(compra.emissao) }} · {{ compra.conta }}<template v-if="compra.formaPagamentoNota"> · {{ compra.formaPagamentoNota }}</template></div>
        </div>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" aria-label="Fechar" @click="estado.compra.aberto = false" />
      </div>
      <v-divider />
      <v-card-text class="pa-0">
        <div v-if="!compra" class="d-flex justify-center pa-8"><v-progress-circular indeterminate color="primary" /></div>
        <div v-for="i in compra?.itens ?? []" :key="i.sequencia" class="item d-flex align-center ga-3 px-4 py-2" data-testid="item-compra">
          <div class="flex-grow-1" style="min-width: 0">
            <div class="font-weight-medium text-truncate">{{ i.descricao }}</div>
            <div class="text-caption text-medium-emphasis">
              {{ i.quantidade.toLocaleString('pt-BR', { maximumFractionDigits: 3 }) }} {{ i.unidade?.toLowerCase() }} × {{ brl(i.valorUnitario) }} · {{ i.categoria }}
            </div>
          </div>
          <div class="valor">{{ brl(i.valorPago) }}</div>
        </div>
      </v-card-text>
      <v-divider />
      <div v-if="compra" class="d-flex align-center px-4 py-3">
        <span class="text-body-2">{{ compra.itens.length }} itens<template v-if="compra.desconto"> · desconto {{ brl(compra.desconto) }}</template></span>
        <v-spacer />
        <span class="medio">{{ brl(compra.total) }}</span>
      </div>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.item { border-bottom: 1px solid rgba(var(--v-border-color), 0.12); }
.item:nth-child(even) { background: rgba(var(--v-theme-on-surface), 0.03); }
</style>
