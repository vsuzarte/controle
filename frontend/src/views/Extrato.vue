<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../api'
import { contasAtivas, erro, estado } from '../estado'
import { brl, dataBr, hojeIso, nomeMes } from '../formato'
import { ordenarLancamentos } from '../regras'
import { TIPOS_CONTA, type Extrato, type ExtratoLinha } from '../tipos'
import LancamentoLinha from '../components/LancamentoLinha.vue'
import SeletorMes from '../components/SeletorMes.vue'

/** Extrato da conta, como o do banco: tudo o que passou por ela no mês, com o saldo depois de cada movimento. */
const route = useRoute()
const router = useRouter()
const contaId = ref<string | null>((route.query.conta as string) ?? null)
const extrato = ref<Extrato | null>(null)
const carregando = ref(false)

watch(contasAtivas, (lista) => {
  if (!contaId.value || !lista.some((c) => c.id === contaId.value)) contaId.value = lista[0]?.id ?? null
}, { immediate: true })

async function carregar() {
  if (!contaId.value) return
  carregando.value = true
  try {
    extrato.value = await api.get<Extrato>(`/contas/${contaId.value}/extrato?mes=${estado.mes}`)
    router.replace({ query: { conta: contaId.value } })
  } catch (e) {
    erro(e)
  } finally {
    carregando.value = false
  }
}
watch(() => [contaId.value, estado.mes, estado.versao], carregar, { immediate: true })

/** Mais recente em cima; o que ainda vai acontecer fica embaixo, em "Próximos". */
const grupos = computed(() => ordenarLancamentos(
  (extrato.value?.linhas ?? []).map((x) => ({ data: x.lancamento.data, valor: x.efeito, linha: x as ExtratoLinha })),
  'recentes', hojeIso()))
const vale = computed(() => extrato.value?.tipo === 'Beneficio')
const irParaFatura = (mes: string) => router.push({ path: '/cartoes', query: { cartao: contaId.value!, mes: mes.slice(0, 7) } })
const icone = (tipo: string) => TIPOS_CONTA.find((t) => t.value === tipo)?.icone ?? 'mdi-bank'
</script>

<template>
  <div class="pagina">
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <SeletorMes v-model="estado.mes" />
      <v-chip-group v-model="contaId" mandatory selected-class="text-primary" aria-label="Conta" class="flex-grow-1" style="min-width: 0">
        <v-chip v-for="c in contasAtivas" :key="c.id" :value="c.id" variant="tonal" filter :prepend-icon="icone(c.tipo)" data-testid="conta-extrato">
          {{ c.nome }}
        </v-chip>
      </v-chip-group>
    </div>

    <v-progress-linear v-if="carregando" indeterminate color="primary" class="mb-2" />
    <template v-if="extrato">
      <!-- Cartão: o que importa é o limite e a fatura (não "saldo" nem "entrou") -->
      <v-card v-if="extrato.cartao" class="pa-4 mb-3" data-testid="resumo-cartao">
        <v-row dense align="center">
          <v-col cols="6" sm="3">
            <div class="rotulo">Disponível</div>
            <div class="medio" :class="(extrato.cartao.disponivel ?? 0) < 0 ? 'text-error' : 'text-success'">
              {{ extrato.cartao.disponivel !== null ? brl(extrato.cartao.disponivel) : '—' }}
            </div>
            <div class="text-caption text-medium-emphasis">
              <template v-if="extrato.cartao.limite">usado {{ brl(extrato.cartao.limiteUsado) }} de {{ brl(extrato.cartao.limite) }}</template>
              <template v-else>sem limite cadastrado</template>
            </div>
            <v-progress-linear v-if="extrato.cartao.limite" :model-value="Math.min(100, extrato.cartao.limiteUsado / extrato.cartao.limite * 100)"
              :color="extrato.cartao.limiteUsado / extrato.cartao.limite > 0.85 ? 'error' : 'primary'" rounded height="6" class="mt-1" />
          </v-col>
          <v-col cols="6" sm="3">
            <div class="rotulo">Compras no mês</div>
            <div class="medio">{{ brl(extrato.saiu) }}</div>
            <div class="text-caption text-medium-emphasis">pagamentos de fatura: {{ brl(extrato.entrou) }}</div>
          </v-col>
          <v-col v-if="extrato.cartao.faturaAPagar" cols="6" sm="3">
            <div class="rotulo">Fatura a pagar · {{ nomeMes(extrato.cartao.faturaAPagar.mes, true) }}</div>
            <div class="medio text-error">{{ brl(extrato.cartao.faturaAPagar.restante) }}</div>
            <div class="text-caption text-medium-emphasis">vence {{ dataBr(extrato.cartao.faturaAPagar.vencimento).slice(0, 5) }}</div>
          </v-col>
          <v-col v-if="extrato.cartao.faturaAberta" cols="6" sm="3">
            <div class="rotulo">Fatura aberta · {{ nomeMes(extrato.cartao.faturaAberta.mes, true) }}</div>
            <div class="medio">{{ brl(extrato.cartao.faturaAberta.total) }}</div>
            <div class="text-caption text-medium-emphasis">fecha {{ dataBr(extrato.cartao.faturaAberta.fechamento).slice(0, 5) }} · vence {{ dataBr(extrato.cartao.faturaAberta.vencimento).slice(0, 5) }}</div>
          </v-col>
          <v-col cols="12" class="d-flex justify-end pt-2">
            <v-btn color="primary" variant="tonal" prepend-icon="mdi-receipt-text-outline" data-testid="ver-fatura"
              @click="irParaFatura((extrato.cartao.faturaAPagar ?? extrato.cartao.faturaAberta)?.mes ?? extrato.mes)">Ver fatura</v-btn>
          </v-col>
        </v-row>
      </v-card>

      <!-- Banco, carteira e vale: saldo -->
      <v-card v-else class="pa-4 mb-3" data-testid="resumo-extrato">
        <v-row dense>
          <v-col cols="6" sm="3"><div class="rotulo">Saldo no início</div><div class="medio">{{ brl(extrato.saldoInicio) }}</div></v-col>
          <v-col cols="6" sm="3"><div class="rotulo">{{ vale ? 'Recargas' : 'Entrou' }}</div><div class="medio text-success">+ {{ brl(extrato.entrou) }}</div></v-col>
          <v-col cols="6" sm="3"><div class="rotulo">{{ vale ? 'Gastos' : 'Saiu' }}</div><div class="medio text-error">− {{ brl(extrato.saiu) }}</div></v-col>
          <v-col cols="6" sm="3">
            <div class="rotulo">{{ vale ? 'Saldo do vale' : 'Saldo agora' }}</div>
            <div class="medio" :class="extrato.saldoFim < 0 ? 'text-error' : ''" data-testid="saldo-fim">{{ brl(extrato.saldoFim) }}</div>
            <div v-if="extrato.saldoPrevisto !== extrato.saldoFim" class="text-caption text-medium-emphasis">
              com os previstos: {{ brl(extrato.saldoPrevisto) }}
            </div>
          </v-col>
        </v-row>
      </v-card>

      <v-card>
        <div v-if="!grupos.length" class="pa-8 text-center text-medium-emphasis">Nada passou por esta conta neste mês.</div>
        <template v-for="g in grupos" :key="g.chave">
          <div v-if="g.inicioProximos" class="proximos px-4 py-1 text-caption font-weight-bold">Previstos (ainda não mexem no saldo)</div>
          <div class="dia px-4 py-2 font-weight-bold text-body-2 text-capitalize">{{ dataBr(g.dia, true) }}</div>
          <v-list class="linhas py-0" density="comfortable">
            <LancamentoLinha v-for="x in g.itens" :key="x.linha.lancamento.id" :l="x.linha.lancamento" :efeito="x.linha.efeito" :saldo="x.linha.saldo" />
          </v-list>
        </template>
      </v-card>
    </template>
  </div>
</template>

<style scoped>
.dia { background: rgba(var(--v-theme-on-surface), 0.06); border-top: 1px solid rgba(var(--v-border-color), 0.12); }
.proximos { background: rgba(var(--v-theme-warning), 0.14); color: rgb(var(--v-theme-warning)); text-transform: uppercase; }
.linhas :deep(.v-list-item) { border-radius: 0 !important; border-bottom: 1px solid rgba(var(--v-border-color), 0.12); }
.linhas :deep(.v-list-item:nth-child(even)) { background: rgba(var(--v-theme-on-surface), 0.025); }
.linhas :deep(.v-list-item:last-child) { border-bottom: 0; }
.linhas :deep(.v-list-item:hover) { background: rgba(var(--v-theme-primary), 0.08); }
</style>
