<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { api } from '../api'
import { alterado, contasAtivas, erro, estado } from '../estado'
import { brl, dataBr, hojeIso } from '../formato'
import { FORMAS, type FormaPagamento } from '../tipos'
import CampoValor from './CampoValor.vue'
import { jurosPorPadrao, pagoAMais } from '../regras'

const salvando = ref(false)
const f = reactive({ data: hojeIso(), valor: null as number | null, contaId: '', formaPagamento: null as FormaPagamento | null })
const l = computed(() => estado.confirmar.lancamento)
const receita = computed(() => l.value?.tipo === 'Receita')
const contas = computed(() =>
  contasAtivas.value
    .filter((c) => (receita.value ? c.tipo !== 'CartaoCredito' : c.tipo !== 'Beneficio' || c.id === l.value?.contaId))
    .map((c) => ({ title: c.nome, value: c.id })),
)
/** Pagou mais que o previsto (conta atrasada): a diferença costuma ser juros/multa. */
const diferenca = computed(() => (!receita.value && l.value ? pagoAMais(f.valor, l.value.valor) : 0))
const foiJuros = ref(true)

watch(() => estado.confirmar.aberto, (aberto) => {
  if (!aberto || !l.value) return
  const data = l.value.data <= hojeIso() ? l.value.data : hojeIso()
  Object.assign(f, { data, valor: estado.confirmar.valor ?? l.value.valor, contaId: l.value.contaId, formaPagamento: l.value.formaPagamento })
  // Pago depois do vencimento e com valor fixo: a diferença quase sempre é juros/multa.
  foiJuros.value = jurosPorPadrao(l.value.valorVariavel, l.value.data, hojeIso())
})

async function confirmar() {
  if (!l.value || !f.valor) return
  salvando.value = true
  try {
    await api.post(`/lancamentos/${l.value.id}/confirmar`, { ...f, juros: diferenca.value > 0 && foiJuros.value ? diferenca.value : null })
    estado.confirmar.aberto = false
    alterado(receita.value ? 'Recebimento confirmado' : 'Pagamento confirmado')
  } catch (e) {
    erro(e)
  } finally {
    salvando.value = false
  }
}
</script>

<template>
  <v-dialog v-model="estado.confirmar.aberto" max-width="440">
    <v-card v-if="l">
      <v-card-title class="pt-4">{{ receita ? 'Confirmar recebimento' : 'Confirmar pagamento' }}</v-card-title>
      <v-card-subtitle>{{ l.descricao }} · previsto {{ brl(l.valor) }} em {{ dataBr(l.data) }}</v-card-subtitle>
      <v-card-text class="d-flex flex-column ga-3 pt-4">
        <CampoValor v-model="f.valor" :label="receita ? 'Valor recebido' : 'Valor pago'" autofocus />
        <v-alert v-if="diferenca > 0" type="warning" variant="tonal" density="compact" icon="mdi-alert-circle-outline">
          <v-checkbox v-model="foiJuros" hide-details density="compact" data-testid="foi-juros"
            :label="`Os ${brl(diferenca)} a mais foram juros/multa por atraso`" />
        </v-alert>
        <v-text-field v-model="f.data" type="date" :max="hojeIso()" :label="receita ? 'Data do recebimento' : 'Data do pagamento'" />
        <v-select v-model="f.contaId" :items="contas" label="Conta" />
        <v-select v-model="f.formaPagamento" :items="FORMAS" label="Forma de pagamento" clearable />
        <div class="text-caption text-medium-emphasis">
          O previsto vira realizado: é o mesmo registro, então não há risco de contar duas vezes.
        </div>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="estado.confirmar.aberto = false">Voltar</v-btn>
        <v-btn color="success" variant="flat" :loading="salvando" @click="confirmar">Confirmar</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
