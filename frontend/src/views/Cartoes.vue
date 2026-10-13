<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../api'
import { alterado, cartoes, contasNaoCartao, erro, estado } from '../estado'
import { brl, dataBr, hojeIso, nomeMes } from '../formato'
import type { Fatura, StatusFatura } from '../tipos'
import CampoValor from '../components/CampoValor.vue'
import LancamentoLinha from '../components/LancamentoLinha.vue'

const route = useRoute()
const router = useRouter()
const cartaoId = ref<string | null>((route.query.cartao as string) ?? cartoes.value[0]?.id ?? null)
const faturas = ref<Fatura[]>([])
const mes = ref<string | null>((route.query.mes as string) ?? null)
const fatura = ref<Fatura | null>(null)
const pagando = ref(false)
const pg = reactive({ contaOrigemId: '', valor: null as number | null, data: hojeIso() })
const salvando = ref(false)

async function carregarFaturas() {
  if (!cartaoId.value) return
  try {
    faturas.value = await api.get<Fatura[]>(`/cartoes/${cartaoId.value}/faturas`)
    if (!mes.value || !faturas.value.some((f) => f.mes.startsWith(mes.value!))) {
      const atual = faturas.value.find((f) => f.status === 'Aberta') ?? faturas.value[0]
      mes.value = atual?.mes.slice(0, 7) ?? null
    }
    await carregarFatura()
  } catch (e) {
    erro(e)
  }
}

async function carregarFatura() {
  if (!cartaoId.value || !mes.value) { fatura.value = null; return }
  fatura.value = await api.get<Fatura>(`/cartoes/${cartaoId.value}/faturas/${mes.value}`)
  router.replace({ query: { cartao: cartaoId.value, mes: mes.value } })
}

onMounted(carregarFaturas)
watch(cartaoId, () => { mes.value = null; carregarFaturas() })
watch(mes, (m, antigo) => { if (antigo && m) carregarFatura().catch(erro) })
watch(() => estado.versao, carregarFaturas)

const cores: Record<StatusFatura, string> = { Aberta: 'info', Fechada: 'warning', Paga: 'success', Vencida: 'error', Futura: 'grey' }
const ordenadas = computed(() => [...faturas.value].reverse())

function abrirPagamento() {
  if (!fatura.value) return
  Object.assign(pg, { contaOrigemId: contasNaoCartao.value[0]?.id ?? '', valor: fatura.value.restante, data: hojeIso() })
  pagando.value = true
}

async function pagar() {
  if (!fatura.value) return
  salvando.value = true
  try {
    await api.post(`/cartoes/${cartaoId.value}/faturas/${mes.value}/pagar`, pg)
    pagando.value = false
    alterado('Pagamento da fatura registrado')
  } catch (e) {
    erro(e)
  } finally {
    salvando.value = false
  }
}
</script>

<template>
  <div class="pagina">
    <v-card v-if="!cartoes.length" class="pa-8 text-center">
      <v-icon size="48" class="mb-2">mdi-credit-card-plus-outline</v-icon>
      <div class="mb-3">Nenhum cartão cadastrado.</div>
      <v-btn color="primary" to="/cadastros">Cadastrar cartão</v-btn>
    </v-card>

    <template v-else>
      <v-tabs v-model="cartaoId" color="primary" class="mb-3" show-arrows>
        <v-tab v-for="c in cartoes" :key="c.id" :value="c.id" prepend-icon="mdi-credit-card">{{ c.nome }}</v-tab>
      </v-tabs>

      <v-slide-group v-model="mes" show-arrows class="mb-3" mandatory>
        <v-slide-group-item v-for="f in ordenadas" :key="f.mes" v-slot="{ isSelected, toggle }" :value="f.mes.slice(0, 7)">
          <v-card :variant="isSelected ? 'tonal' : 'outlined'" :color="isSelected ? 'primary' : undefined" class="ma-1 pa-2 text-center" width="110" @click="toggle">
            <div class="text-caption text-capitalize">{{ nomeMes(f.mes.slice(0, 7), true) }}</div>
            <div class="valor text-body-2">{{ brl(f.total) }}</div>
            <v-chip :color="cores[f.status]" size="x-small" variant="flat" class="mt-1">{{ f.status }}</v-chip>
          </v-card>
        </v-slide-group-item>
      </v-slide-group>

      <v-card v-if="fatura" class="mb-3 pa-4">
        <div class="d-flex flex-wrap align-center ga-4">
          <div>
            <div class="rotulo">Fatura de {{ nomeMes(fatura.mes.slice(0, 7)) }}</div>
            <div class="grande">{{ brl(fatura.total) }}</div>
            <v-chip :color="cores[fatura.status]" size="small" variant="flat">{{ fatura.status }}</v-chip>
          </div>
          <v-divider vertical class="d-none d-sm-flex" />
          <div class="text-body-2">
            <div>Fecha em <b>{{ dataBr(fatura.fechamento) }}</b></div>
            <div>Vence em <b>{{ dataBr(fatura.vencimento) }}</b></div>
          </div>
          <div class="text-body-2">
            <div>Pago: <span class="valor text-success">{{ brl(fatura.pago) }}</span></div>
            <div>Falta: <span class="valor" :class="fatura.restante > 0 ? 'text-error' : ''">{{ brl(fatura.restante) }}</span></div>
          </div>
          <v-spacer />
          <v-btn v-if="fatura.restante > 0" color="primary" prepend-icon="mdi-cash-check" @click="abrirPagamento">Pagar fatura</v-btn>
        </div>
      </v-card>

      <v-card v-if="fatura">
        <v-card-title class="text-subtitle-1 font-weight-bold">Compras nesta fatura</v-card-title>
        <v-list class="py-0">
          <div v-if="!fatura.lancamentos.length" class="pa-4 text-medium-emphasis">Nenhuma compra nesta fatura.</div>
          <LancamentoLinha v-for="l in fatura.lancamentos" :key="l.id" :l="l" mostrar-data />
        </v-list>
        <template v-if="fatura.pagamentos.length">
          <v-divider />
          <v-card-title class="text-subtitle-2">Pagamentos</v-card-title>
          <v-list class="py-0">
            <LancamentoLinha v-for="l in fatura.pagamentos" :key="l.id" :l="l" mostrar-data />
          </v-list>
        </template>
      </v-card>
    </template>

    <v-dialog v-model="pagando" max-width="420">
      <v-card>
        <v-card-title class="pt-4">Pagar fatura</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <div class="text-body-2">
            Registra uma transferência da conta para o cartão. As compras já foram contadas nas categorias; o pagamento não vira gasto de novo.
          </div>
          <v-select v-model="pg.contaOrigemId" :items="contasNaoCartao.map((c) => ({ title: c.nome, value: c.id }))" label="Pagar com a conta" />
          <CampoValor v-model="pg.valor" label="Valor pago" />
          <v-text-field v-model="pg.data" type="date" :max="hojeIso()" label="Data do pagamento" />
        </v-card-text>
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="pagando = false">Voltar</v-btn>
          <v-btn color="primary" variant="flat" :loading="salvando" @click="pagar">Confirmar pagamento</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
