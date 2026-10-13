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
    <div class="cabecalho">
      <div class="titulo-pagina">Lançamentos</div>
      <v-spacer />
      <SeletorMes v-model="estado.mes" />
    </div>

    <v-row dense class="mb-3">
      <v-col v-for="t in [
        { rotulo: 'Entradas', valor: totais.entradas, icone: 'mdi-arrow-up', cor: 'success' },
        { rotulo: 'Gastos', valor: totais.saidas, icone: 'mdi-arrow-down', cor: 'error' },
        { rotulo: 'A pagar', valor: totais.previsto, icone: 'mdi-clock-outline', cor: 'warning' },
      ]" :key="t.rotulo" cols="4">
        <v-card class="pa-3 d-flex flex-column flex-sm-row align-sm-center ga-2 ga-sm-3">
          <v-avatar :color="t.cor" variant="tonal" rounded="lg" size="36"><v-icon size="18">{{ t.icone }}</v-icon></v-avatar>
          <div style="min-width: 0">
            <div class="rotulo">{{ t.rotulo }}</div>
            <div class="valor text-truncate">{{ brl(t.valor) }}</div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <div class="d-flex ga-2 mb-3 align-center">
      <v-text-field v-model="filtros.busca" prepend-inner-icon="mdi-magnify" placeholder="Buscar lançamento" density="compact" clearable aria-label="Buscar" />
      <v-btn variant="tonal" :color="filtrosAtivos ? 'primary' : undefined" height="40" prepend-icon="mdi-tune-variant" @click="verFiltros = !verFiltros">
        Filtros<span v-if="filtrosAtivos" class="ml-1">({{ filtrosAtivos }})</span>
      </v-btn>
    </div>

    <v-expand-transition>
      <v-card v-if="verFiltros" class="pa-3 mb-3">
        <v-row dense>
          <v-col cols="12" sm="6" md="3">
            <v-select v-model="filtros.tipo" :items="[{ title: 'Receita', value: 'Receita' }, { title: 'Despesa', value: 'Despesa' }, { title: 'Transferência', value: 'Transferencia' }]" label="Tipo" clearable density="compact" />
          </v-col>
          <v-col cols="12" sm="6" md="3"><v-select v-model="filtros.status" :items="['Previsto', 'Realizado', 'Cancelado']" label="Situação" clearable density="compact" /></v-col>
          <v-col cols="12" sm="6" md="3"><v-autocomplete v-model="filtros.categoriaId" :items="opcoesCategorias" label="Categoria" clearable density="compact" /></v-col>
          <v-col cols="12" sm="6" md="3"><v-select v-model="filtros.contaId" :items="opcoesContas" label="Conta" clearable density="compact" /></v-col>
        </v-row>
      </v-card>
    </v-expand-transition>

    <v-card>
      <v-progress-linear v-if="carregando" indeterminate color="primary" />
      <v-empty-state
        v-if="!porDia.length && !carregando"
        icon="mdi-receipt-text-outline"
        title="Nada por aqui"
        text="Toque no + para lançar seu primeiro gasto do mês."
      />
      <v-list v-else class="pa-2">
        <template v-for="[dia, itens] in porDia" :key="dia">
          <v-list-subheader class="text-capitalize font-weight-bold">{{ dataBr(dia, true) }}</v-list-subheader>
          <LancamentoLinha v-for="l in itens" :key="l.id" :l="l" />
        </template>
      </v-list>
    </v-card>

    <div v-if="qtdCancelados" class="text-center mt-3">
      <v-btn variant="text" size="small" @click="verCancelados = !verCancelados">
        {{ verCancelados ? 'Ocultar' : 'Mostrar' }} {{ qtdCancelados }} cancelado(s)
      </v-btn>
    </div>
  </div>
</template>
