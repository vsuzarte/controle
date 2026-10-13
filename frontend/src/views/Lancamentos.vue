<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { api, qs } from '../api'
import { erro, estado } from '../estado'
import { brl, dataBr } from '../formato'
import type { Lancamento } from '../tipos'
import LancamentoLinha from '../components/LancamentoLinha.vue'
import SeletorMes from '../components/SeletorMes.vue'

const lista = ref<Lancamento[]>([])
const carregando = ref(false)
const filtros = reactive({ busca: '', contaId: null as string | null, categoriaId: null as string | null, tipo: null as string | null, status: null as string | null })
const verCancelados = ref(false)
const verFiltros = ref(false)

async function carregar() {
  carregando.value = true
  try {
    lista.value = await api.get<Lancamento[]>('/lancamentos' + qs({ mes: estado.mes, ...filtros }))
  } catch (e) {
    erro(e)
  } finally {
    carregando.value = false
  }
}
onMounted(carregar)
let espera: ReturnType<typeof setTimeout>
watch(() => [estado.mes, estado.versao, filtros.contaId, filtros.categoriaId, filtros.tipo, filtros.status], carregar)
watch(() => filtros.busca, () => { clearTimeout(espera); espera = setTimeout(carregar, 300) })

const visiveis = computed(() => lista.value.filter((l) => verCancelados.value || l.status !== 'Cancelado'))
const porDia = computed(() => {
  const grupos = new Map<string, Lancamento[]>()
  for (const l of visiveis.value) grupos.set(l.data, [...(grupos.get(l.data) ?? []), l])
  return [...grupos.entries()]
})
const totais = computed(() => {
  const ok = lista.value.filter((l) => l.status === 'Realizado')
  return {
    entradas: ok.filter((l) => l.tipo === 'Receita').reduce((s, l) => s + l.valor, 0),
    saidas: ok.filter((l) => l.tipo === 'Despesa').reduce((s, l) => s + l.valor, 0),
    previsto: lista.value.filter((l) => l.status === 'Previsto' && l.tipo === 'Despesa').reduce((s, l) => s + l.valor, 0),
  }
})
const qtdCancelados = computed(() => lista.value.filter((l) => l.status === 'Cancelado').length)
const filtrosAtivos = computed(() => [filtros.contaId, filtros.categoriaId, filtros.tipo, filtros.status].filter(Boolean).length)

const opcoesContas = computed(() => estado.contas.map((c) => ({ title: c.nome, value: c.id })))
const opcoesCategorias = computed(() => estado.categorias.map((c) => ({ title: `${c.nome}${c.ativa ? '' : ' (inativa)'}`, value: c.id })))
</script>

<template>
  <div class="pagina">
    <div class="d-flex align-center flex-wrap ga-2 mb-3">
      <SeletorMes v-model="estado.mes" />
      <v-spacer />
      <v-text-field v-model="filtros.busca" prepend-inner-icon="mdi-magnify" placeholder="Buscar" density="compact" clearable style="max-width: 240px; min-width: 160px" />
      <v-btn variant="tonal" prepend-icon="mdi-filter-variant" @click="verFiltros = !verFiltros">
        Filtros<v-badge v-if="filtrosAtivos" :content="filtrosAtivos" inline color="primary" />
      </v-btn>
    </div>

    <v-expand-transition>
      <v-card v-if="verFiltros" class="pa-3 mb-3">
        <v-row dense>
          <v-col cols="12" sm="6" md="3"><v-select v-model="filtros.tipo" :items="['Receita', 'Despesa', 'Transferencia']" label="Tipo" clearable density="compact" /></v-col>
          <v-col cols="12" sm="6" md="3"><v-select v-model="filtros.status" :items="['Previsto', 'Realizado', 'Cancelado']" label="Status" clearable density="compact" /></v-col>
          <v-col cols="12" sm="6" md="3"><v-autocomplete v-model="filtros.categoriaId" :items="opcoesCategorias" label="Categoria" clearable density="compact" /></v-col>
          <v-col cols="12" sm="6" md="3"><v-select v-model="filtros.contaId" :items="opcoesContas" label="Conta" clearable density="compact" /></v-col>
        </v-row>
      </v-card>
    </v-expand-transition>

    <v-row dense class="mb-2">
      <v-col cols="4"><v-card class="pa-3"><div class="rotulo">Entradas</div><div class="valor text-success">{{ brl(totais.entradas) }}</div></v-card></v-col>
      <v-col cols="4"><v-card class="pa-3"><div class="rotulo">Gastos</div><div class="valor text-error">{{ brl(totais.saidas) }}</div></v-card></v-col>
      <v-col cols="4"><v-card class="pa-3"><div class="rotulo">A pagar</div><div class="valor text-warning">{{ brl(totais.previsto) }}</div></v-card></v-col>
    </v-row>

    <v-card>
      <v-progress-linear v-if="carregando" indeterminate color="primary" />
      <div v-if="!porDia.length && !carregando" class="pa-8 text-center text-medium-emphasis">
        <v-icon size="48" class="mb-2">mdi-tray</v-icon>
        <div>Nenhum lançamento neste mês.</div>
        <div class="text-caption">Use o botão + (ou a tecla N) para lançar.</div>
      </div>
      <template v-for="[dia, itens] in porDia" :key="dia">
        <div class="lista-dia">{{ dataBr(dia, true) }}</div>
        <v-list class="py-0">
          <LancamentoLinha v-for="l in itens" :key="l.id" :l="l" />
        </v-list>
      </template>
    </v-card>

    <div v-if="qtdCancelados" class="text-center mt-3">
      <v-btn variant="text" size="small" @click="verCancelados = !verCancelados">
        {{ verCancelados ? 'Ocultar' : 'Mostrar' }} {{ qtdCancelados }} cancelado(s)
      </v-btn>
    </div>
  </div>
</template>
