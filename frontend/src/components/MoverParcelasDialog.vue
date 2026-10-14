<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { api } from '../api'
import { alterado, erro, estado } from '../estado'

/** Compra parcelada lançada no mês errado: move todas as parcelas juntas (no cartão, a fatura também). */
const sentido = ref<1 | -1>(1)
const meses = ref(1)
const salvando = ref(false)
const total = computed(() => sentido.value * Math.min(12, Math.max(1, meses.value || 1)))

watch(() => estado.moverParcelas.aberto, (aberto) => {
  if (!aberto) return
  sentido.value = 1
  meses.value = 1
})

async function mover() {
  salvando.value = true
  try {
    await api.post(`/regras/${estado.moverParcelas.regraId}/mover-parcelas`, { meses: total.value })
    estado.moverParcelas.aberto = false
    alterado(`Parcelas movidas ${Math.abs(total.value)} mês(es) para ${total.value > 0 ? 'frente' : 'trás'}`)
  } catch (e) {
    erro(e)
  } finally {
    salvando.value = false
  }
}
</script>

<template>
  <v-dialog v-model="estado.moverParcelas.aberto" max-width="420">
    <v-card>
      <v-card-title class="pt-5 font-weight-bold">Mover parcelas</v-card-title>
      <v-card-text class="d-flex flex-column ga-3">
        <div class="text-body-2">{{ estado.moverParcelas.descricao }}</div>
        <v-btn-toggle v-model="sentido" mandatory divided variant="outlined" color="primary" density="comfortable">
          <v-btn :value="-1" prepend-icon="mdi-arrow-left">Para trás</v-btn>
          <v-btn :value="1" append-icon="mdi-arrow-right">Para frente</v-btn>
        </v-btn-toggle>
        <v-text-field v-model.number="meses" type="number" min="1" max="12" label="Quantos meses" density="comfortable" hide-details />
        <v-alert type="info" variant="tonal" density="compact" class="text-body-2">
          Todas as parcelas andam {{ Math.abs(total) }} mês(es) para {{ total > 0 ? 'frente' : 'trás' }}. No cartão, cada parcela
          muda de fatura junto. Não dá para mover parcelas de uma fatura que já tem pagamento.
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="estado.moverParcelas.aberto = false">Voltar</v-btn>
        <v-btn color="primary" variant="flat" :loading="salvando" @click="mover">Mover parcelas</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
