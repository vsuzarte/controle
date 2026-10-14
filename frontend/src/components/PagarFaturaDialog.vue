<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { api } from '../api'
import { alterado, contasPagadoras, erro, estado } from '../estado'
import { brl, dataBr, hojeIso, nomeMes } from '../formato'
import CampoValor from './CampoValor.vue'
import { jurosPorPadrao, pagoAMais } from '../regras'

/** Pagamento de fatura usado pela tela de cartões, pelo início e pela previsão. */
const p = computed(() => estado.pagarFatura)
const f = reactive({ contaOrigemId: '', valor: null as number | null, data: hojeIso() })
const foiJuros = ref(true)
const salvando = ref(false)
const diferenca = computed(() => pagoAMais(f.valor, p.value.restante))

watch(() => estado.pagarFatura.aberto, (aberto) => {
  if (!aberto) return
  Object.assign(f, { contaOrigemId: contasPagadoras.value[0]?.id ?? '', valor: p.value.restante, data: hojeIso() })
  foiJuros.value = jurosPorPadrao(false, p.value.vencimento, hojeIso())
})

async function pagar() {
  salvando.value = true
  try {
    await api.post(`/cartoes/${p.value.cartaoId}/faturas/${p.value.mes.slice(0, 7)}/pagar`, {
      ...f,
      juros: diferenca.value > 0 && foiJuros.value ? diferenca.value : null,
    })
    estado.pagarFatura.aberto = false
    alterado('Pagamento da fatura registrado')
  } catch (e) {
    erro(e)
  } finally {
    salvando.value = false
  }
}
</script>

<template>
  <v-dialog v-model="estado.pagarFatura.aberto" max-width="440">
    <v-card>
      <v-card-title class="pt-4">Pagar fatura</v-card-title>
      <v-card-subtitle>
        {{ p.cartao }} · {{ nomeMes(p.mes.slice(0, 7)) }} · vence {{ dataBr(p.vencimento) }} · falta {{ brl(p.restante) }}
      </v-card-subtitle>
      <v-card-text class="d-flex flex-column ga-3 pt-4">
        <v-select v-model="f.contaOrigemId" :items="contasPagadoras.map((c) => ({ title: c.nome, value: c.id }))" label="Pagar com a conta" />
        <CampoValor v-model="f.valor" label="Valor pago" />
        <v-alert v-if="diferenca > 0" type="warning" variant="tonal" density="compact" icon="mdi-alert-circle-outline">
          <v-checkbox v-model="foiJuros" hide-details density="compact" data-testid="foi-juros"
            :label="`Os ${brl(diferenca)} a mais foram juros/multa por atraso`" />
        </v-alert>
        <v-text-field v-model="f.data" type="date" :max="hojeIso()" label="Data do pagamento" />
        <div class="text-caption text-medium-emphasis">
          É uma transferência da conta para o cartão: as compras já contaram nas categorias, o pagamento não vira gasto de novo.
          Juros e multa entram como gasto em "Tarifas e juros".
        </div>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="estado.pagarFatura.aberto = false">Voltar</v-btn>
        <v-btn color="primary" variant="flat" :loading="salvando" @click="pagar">Confirmar pagamento</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
