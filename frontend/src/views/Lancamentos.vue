<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useDisplay } from 'vuetify'
import { api, qs } from '../api'
import { erro, estado, lerNotaFiscal } from '../estado'
import { brl, dataBr, hojeIso, somarMeses } from '../formato'
import { ordenarLancamentos, somarEntradas, type OrdemLancamentos } from '../regras'
import type { Lancamento } from '../tipos'
import LancamentoLinha from '../components/LancamentoLinha.vue'
import SeletorMes from '../components/SeletorMes.vue'

const { lgAndUp } = useDisplay()
const lista = ref<Lancamento[]>([])
const carregando = ref(false)
const filtros = reactive({ busca: '', contaId: null as string | null, categoriaId: null as string | null, status: null as string | null })
const verCancelados = ref(false)
const verFiltros = ref(false)
/** Com busca: procurar só no mês ou nos meses anteriores também (ex.: quanto gastei com coca no ano). */
const periodoBusca = ref<'mes' | '12' | 'tudo'>('12')
const desdeBusca = computed(() => periodoBusca.value === 'mes' ? null : periodoBusca.value === 'tudo' ? '2000-01-01' : somarMeses(estado.mes, -11) + '-01')

// ---------- preferências lembradas neste aparelho ----------
const lembrar = <T extends string>(chave: string, padrao: T, validos: T[]) => {
  const v = ref<T>((() => { try { const x = localStorage.getItem(chave) as T; return validos.includes(x) ? x : padrao } catch { return padrao } })())
  watch(v, (x) => { try { localStorage.setItem(chave, x) } catch { /* sem storage */ } })
  return v
}
const ordem = lembrar<OrdemLancamentos>('fin.lancamentos.ordem', 'recentes', ['recentes', 'antigos', 'valor'])
/** Telas menores: uma seção por vez. */
const secao = lembrar<'saidas' | 'entradas' | 'transferencias'>('fin.lancamentos.secao', 'saidas', ['saidas', 'entradas', 'transferencias'])
const ORDENS: { value: OrdemLancamentos; title: string; icone: string }[] = [
  { value: 'recentes', title: 'Mais recentes primeiro', icone: 'mdi-sort-calendar-descending' },
  { value: 'antigos', title: 'Mais antigos primeiro', icone: 'mdi-sort-calendar-ascending' },
  { value: 'valor', title: 'Maior valor primeiro', icone: 'mdi-sort-numeric-descending' },
]

async function carregar() {
  carregando.value = true
  try {
    lista.value = await api.get<Lancamento[]>('/lancamentos' + qs({ mes: estado.mes, ...filtros, desde: filtros.busca ? desdeBusca.value : null }))
  } catch (e) {
    erro(e)
  } finally {
    carregando.value = false
  }
}
onMounted(carregar)
let espera: ReturnType<typeof setTimeout>
watch(() => [estado.mes, estado.versao, filtros.contaId, filtros.categoriaId, filtros.status, periodoBusca.value], carregar)
watch(() => filtros.busca, () => { clearTimeout(espera); espera = setTimeout(carregar, 300) })

/** Vales (VR/VA): a recarga aparece nas entradas, mas somada à parte. */
const vales = computed(() => new Set(estado.contas.filter((c) => c.tipo === 'Beneficio').map((c) => c.id)))
const visiveis = computed(() => lista.value.filter((l) => verCancelados.value || l.status !== 'Cancelado'))
const montar = (tipo: Lancamento['tipo']) => {
  const itens = visiveis.value.filter((l) => l.tipo === tipo)
  return {
    grupos: ordenarLancamentos(itens, ordem.value, hojeIso()),
    quantidade: itens.length,
    ...(() => {
      const validos = itens.filter((l) => l.status !== 'Cancelado')
      if (tipo !== 'Receita') return { total: validos.reduce((s, l) => s + l.valor, 0), vale: 0 }
      const e = somarEntradas(validos, vales.value)
      return { total: e.dinheiro, vale: e.vale }
    })(),
  }
}
const secoes = computed(() => [
  { chave: 'saidas' as const, titulo: 'Saídas', icone: 'mdi-arrow-down', cor: 'error', ...montar('Despesa') },
  { chave: 'entradas' as const, titulo: 'Entradas', icone: 'mdi-arrow-up', cor: 'success', ...montar('Receita') },
  { chave: 'transferencias' as const, titulo: 'Transferências', icone: 'mdi-swap-horizontal', cor: 'secondary', ...montar('Transferencia') },
])
/** Computador: saídas à esquerda, entradas (e transferências) à direita. Celular: uma por vez. */
const colunas = computed(() => lgAndUp.value
  ? [secoes.value.filter((s) => s.chave === 'saidas'), secoes.value.filter((s) => s.chave !== 'saidas' && (s.quantidade || s.chave === 'entradas'))]
  : [secoes.value.filter((s) => s.chave === secao.value)])

const totais = computed(() => {
  const ok = lista.value.filter((l) => l.status === 'Realizado')
  return {
    entradas: somarEntradas(ok.filter((l) => l.tipo === 'Receita'), vales.value).dinheiro,
    vale: somarEntradas(ok.filter((l) => l.tipo === 'Receita'), vales.value).vale,
    saidas: ok.filter((l) => l.tipo === 'Despesa').reduce((s, l) => s + l.valor, 0),
    previsto: lista.value.filter((l) => l.status === 'Previsto' && l.tipo === 'Despesa').reduce((s, l) => s + l.valor, 0),
  }
})
/** Com busca: quanto foi gasto com o que foi buscado (na compra do mercado, só o item encontrado, não a nota inteira). */
const gastoNaBusca = computed(() => filtros.busca
  ? lista.value.filter((l) => l.tipo === 'Despesa' && l.status !== 'Cancelado')
    .reduce((s, l) => s + (l.itensEncontrados?.length ? l.itensEncontrados.reduce((t, i) => t + i.valorPago, 0) : l.valor), 0)
  : 0)
const qtdCancelados = computed(() => lista.value.filter((l) => l.status === 'Cancelado').length)
const filtrosAtivos = computed(() => [filtros.contaId, filtros.categoriaId, filtros.status].filter(Boolean).length)

const opcoesContas = computed(() => estado.contas.map((c) => ({ title: c.nome, value: c.id })))
const opcoesCategorias = computed(() => estado.categorias.map((c) => ({ title: `${c.nome}${c.ativa ? '' : ' (inativa)'}`, value: c.id })))
</script>

<template>
  <div class="pagina">
    <!-- ===== Mês, totais e ferramentas numa linha só ===== -->
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <SeletorMes v-model="estado.mes" />
      <v-chip v-for="t in [
        { rotulo: 'Entrou', valor: totais.entradas, cor: 'success' },
        { rotulo: 'Gastei', valor: totais.saidas, cor: 'error' },
        { rotulo: 'A pagar', valor: totais.previsto, cor: 'warning' },
        ...(totais.vale ? [{ rotulo: 'Vale', valor: totais.vale, cor: 'amber-darken-2' }] : []),
      ]" :key="t.rotulo" :color="t.cor" variant="tonal" label>
        <span class="text-caption mr-2">{{ t.rotulo }}</span><span class="valor">{{ brl(t.valor) }}</span>
      </v-chip>
      <v-spacer />
      <template v-if="filtros.busca">
        <v-btn-toggle v-model="periodoBusca" mandatory density="compact" variant="outlined" divided rounded="lg" aria-label="Buscar em">
          <v-btn value="mes" size="small">Mês</v-btn>
          <v-btn value="12" size="small">12 meses</v-btn>
          <v-btn value="tudo" size="small">Tudo</v-btn>
        </v-btn-toggle>
        <v-chip color="primary" variant="tonal" label data-testid="gasto-busca"><span class="text-caption mr-2">Gasto na busca</span><span class="valor">{{ brl(gastoNaBusca) }}</span></v-chip>
      </template>
      <v-text-field v-model="filtros.busca" prepend-inner-icon="mdi-magnify" placeholder="Buscar (também itens das notas)" density="compact" clearable hide-details
        aria-label="Buscar" style="min-width: 180px; max-width: 280px" />
      <v-btn variant="tonal" icon="mdi-qrcode-scan" rounded="lg" size="40" color="primary" title="Lançar pela nota fiscal (QR code)" aria-label="Lançar pela nota fiscal" @click="lerNotaFiscal()" />
      <v-menu>
        <template #activator="{ props }">
          <v-btn v-bind="props" variant="tonal" :icon="ORDENS.find((o) => o.value === ordem)!.icone" rounded="lg" size="40"
            :title="'Ordem: ' + ORDENS.find((o) => o.value === ordem)!.title" aria-label="Ordenar" data-testid="ordenar" />
        </template>
        <v-list density="compact" rounded="lg">
          <v-list-item v-for="o in ORDENS" :key="o.value" :prepend-icon="o.icone" :title="o.title" :active="ordem === o.value" color="primary" @click="ordem = o.value" />
        </v-list>
      </v-menu>
      <v-badge :model-value="!!filtrosAtivos" :content="filtrosAtivos" color="primary">
        <v-btn variant="tonal" :color="filtrosAtivos ? 'primary' : undefined" icon="mdi-tune-variant" rounded="lg" size="40"
          title="Filtros (situação, categoria, conta)" aria-label="Filtros" @click="verFiltros = !verFiltros" />
      </v-badge>
    </div>

    <v-expand-transition>
      <v-card v-if="verFiltros" class="pa-3 mb-3">
        <v-row dense>
          <v-col cols="12" sm="4"><v-select v-model="filtros.status" :items="['Previsto', 'Realizado', 'Cancelado']" label="Situação" clearable density="compact" hide-details /></v-col>
          <v-col cols="12" sm="4"><v-autocomplete v-model="filtros.categoriaId" :items="opcoesCategorias" label="Categoria" clearable density="compact" hide-details /></v-col>
          <v-col cols="12" sm="4"><v-select v-model="filtros.contaId" :items="opcoesContas" label="Conta" clearable density="compact" hide-details /></v-col>
        </v-row>
      </v-card>
    </v-expand-transition>

    <!-- Celular/tablet: escolhe a seção -->
    <v-btn-toggle v-if="!lgAndUp" v-model="secao" mandatory divided variant="outlined" color="primary" density="comfortable" class="mb-3 w-100">
      <v-btn v-for="s in secoes" :key="s.chave" :value="s.chave" class="flex-grow-1" :prepend-icon="s.icone">
        {{ s.titulo }} ({{ s.quantidade }})
      </v-btn>
    </v-btn-toggle>

    <v-progress-linear v-if="carregando" indeterminate color="primary" class="mb-2" />
    <v-row>
      <v-col v-for="(col, i) in colunas" :key="i" cols="12" :lg="i === 0 ? 7 : 5">
        <v-card v-for="s in col" :key="s.chave" class="mb-4" :data-testid="'secao-' + s.chave">
          <div class="d-flex align-center ga-3 px-4 py-3 cabeca-secao">
            <v-avatar :color="s.cor" variant="tonal" rounded="lg" size="34"><v-icon size="18">{{ s.icone }}</v-icon></v-avatar>
            <div class="font-weight-bold">{{ s.titulo }}</div>
            <span class="text-caption text-medium-emphasis">{{ s.quantidade }}</span>
            <v-spacer />
            <div class="text-right">
              <div class="valor" :class="'text-' + s.cor">{{ brl(s.total) }}</div>
              <div v-if="s.vale" class="text-caption text-medium-emphasis" data-testid="entradas-vale">+ {{ brl(s.vale) }} no vale (só compras)</div>
            </div>
          </div>
          <div v-if="!s.grupos.length" class="text-body-2 text-medium-emphasis pa-6 text-center">Nada neste mês.</div>
          <template v-for="g in s.grupos" :key="g.chave">
            <div v-if="g.inicioProximos" class="proximos px-4 py-1 text-caption font-weight-bold">Próximos (ainda vão acontecer)</div>
            <div v-if="g.dia" class="dia px-4 py-2 font-weight-bold text-body-2 text-capitalize">{{ dataBr(g.dia, true) }}</div>
            <v-list class="linhas py-0" density="comfortable">
              <LancamentoLinha v-for="l in g.itens" :key="l.id" :l="l" :mostrar-data="!g.dia" />
            </v-list>
          </template>
        </v-card>
      </v-col>
    </v-row>

    <div v-if="qtdCancelados" class="text-center">
      <v-btn variant="text" size="small" @click="verCancelados = !verCancelados">
        {{ verCancelados ? 'Ocultar' : 'Mostrar' }} {{ qtdCancelados }} cancelado(s)
      </v-btn>
    </div>
  </div>
</template>

<style scoped>
/* Separação nítida: faixa do dia, linhas alternadas e um traço entre elas, para o olho não se perder do nome até o valor. */
.cabeca-secao { border-bottom: 1px solid rgba(var(--v-border-color), 0.16); }
.dia { background: rgba(var(--v-theme-on-surface), 0.06); border-top: 1px solid rgba(var(--v-border-color), 0.12); }
.proximos { background: rgba(var(--v-theme-warning), 0.14); color: rgb(var(--v-theme-warning)); text-transform: uppercase; letter-spacing: 0.05em !important; }
.linhas :deep(.v-list-item) { border-radius: 0 !important; border-bottom: 1px solid rgba(var(--v-border-color), 0.12); }
.linhas :deep(.v-list-item:nth-child(even)) { background: rgba(var(--v-theme-on-surface), 0.025); }
.linhas :deep(.v-list-item:last-child) { border-bottom: 0; }
.linhas :deep(.v-list-item:hover) { background: rgba(var(--v-theme-primary), 0.08); }
</style>
