<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { Line } from 'vue-chartjs'
import { CategoryScale, Chart as ChartJS, Legend, LinearScale, LineElement, PointElement, Tooltip, type ChartOptions } from 'chart.js'
import { useTheme } from 'vuetify'
import { api, qs } from '../api'
import { erro, estado } from '../estado'
import { brl, dataBr, hojeIso, mesIso, nomeMes } from '../formato'
import type { Comparativo, SerieMensal } from '../tipos'
import SeletorMes from '../components/SeletorMes.vue'

ChartJS.register(LineElement, PointElement, CategoryScale, LinearScale, Tooltip, Legend)

const tema = useTheme()
const mes = ref(mesIso(hojeIso()))
const meses = ref(6)
const aba = ref<'categorias' | 'contasFixas' | 'cartoes'>('categorias')
const c = ref<Comparativo | null>(null)

async function carregar() {
  try {
    c.value = await api.get<Comparativo>('/relatorios/comparativo' + qs({ mes: mes.value, meses: String(meses.value) }))
  } catch (e) {
    erro(e)
  }
}
onMounted(carregar)
watch([mes, meses], carregar)
watch(() => estado.versao, carregar)

const emAndamento = computed(() => mes.value === mesIso(hojeIso()))
const ultimo = (s: SerieMensal) => s.valores[s.valores.length - 1]
const penultimo = (s: SerieMensal) => s.valores[s.valores.length - 2] ?? 0
const media = (s: SerieMensal) => {
  const anteriores = s.valores.slice(0, -1)
  return anteriores.length ? anteriores.reduce((a, b) => a + b, 0) / anteriores.length : 0
}
const variacao = (s: SerieMensal) => ultimo(s) - penultimo(s)
const pct = (s: SerieMensal) => (penultimo(s) ? (variacao(s) / penultimo(s)) * 100 : null)

const series = computed(() => (c.value ? c.value[aba.value] : []))

/** O que mais subiu do mês anterior para este, juntando categorias, contas fixas e cartões. */
const subiram = computed(() => {
  if (!c.value) return []
  const todas = [
    ...c.value.categorias.map((s) => ({ ...s, grupo: 'Categoria' })),
    ...c.value.contasFixas.map((s) => ({ ...s, grupo: 'Conta fixa' })),
    ...c.value.cartoes.map((s) => ({ ...s, grupo: 'Cartão' })),
  ]
  return todas.filter((s) => variacao(s) > 0.009).sort((a, b) => variacao(b) - variacao(a)).slice(0, 5)
})

const jurosPeriodo = computed(() => c.value?.juros.valores.reduce((a, b) => a + b, 0) ?? 0)

const CORES = ['#6366F1', '#F59E0B', '#EC4899', '#14B8A6', '#8B5CF6', '#F97316', '#0EA5E9']
const grafico = computed(() => {
  if (!c.value) return { labels: [], datasets: [] }
  const cores = tema.current.value.colors
  const top = [...series.value].sort((a, b) => b.valores.reduce((x, y) => x + y, 0) - a.valores.reduce((x, y) => x + y, 0)).slice(0, 5)
  return {
    labels: c.value.meses.map((m) => nomeMes(m.slice(0, 7), true)),
    datasets: [
      ...(aba.value === 'categorias'
        ? [{ label: 'Total', data: c.value.total.valores, borderColor: cores.primary, backgroundColor: cores.primary, borderWidth: 3, tension: 0.3 }]
        : []),
      ...top.map((s, i) => ({ label: s.nome, data: s.valores, borderColor: s.cor ?? CORES[i % CORES.length], backgroundColor: s.cor ?? CORES[i % CORES.length], borderWidth: 2, tension: 0.3 })),
    ],
  }
})
const opcoes = computed<ChartOptions<'line'>>(() => {
  const texto = tema.current.value.dark ? '#A3B1C2' : '#64748B'
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom', labels: { color: texto, usePointStyle: true, boxWidth: 8 } },
      tooltip: { callbacks: { label: (x) => ` ${x.dataset.label}: ${brl(x.parsed.y)}` } },
    },
    scales: {
      x: { grid: { display: false }, ticks: { color: texto } },
      y: { grid: { color: tema.current.value.dark ? 'rgba(255,255,255,0.06)' : 'rgba(15,23,42,0.06)' }, ticks: { color: texto, callback: (v) => brl(Number(v)).replace(',00', '') }, border: { display: false } },
    },
  }
})
</script>

<template>
  <div class="pagina">
    <div class="cabecalho">
      <div>
        <div class="titulo-pagina">Relatórios</div>
        <div class="text-body-2 text-medium-emphasis">Compare os meses e veja onde os gastos estão aumentando.</div>
      </div>
      <v-spacer />
      <v-btn-toggle v-model="meses" mandatory density="comfortable" variant="outlined" divided rounded="lg">
        <v-btn :value="3">3 meses</v-btn>
        <v-btn :value="6">6 meses</v-btn>
        <v-btn :value="12">12 meses</v-btn>
      </v-btn-toggle>
      <SeletorMes v-model="mes" />
    </div>

    <div v-if="!c" class="d-flex justify-center pa-16"><v-progress-circular indeterminate color="primary" /></div>
    <template v-else>
      <v-alert v-if="emAndamento" type="info" variant="tonal" density="compact" class="mb-4 text-body-2">
        {{ nomeMes(mes) }} ainda está em andamento: compare com cuidado (o mês anterior já está completo).
      </v-alert>

      <v-row class="mb-1">
        <v-col cols="12" md="7">
          <v-card class="h-100" data-testid="subiram">
            <v-card-item><v-card-title class="font-weight-bold">O que mais subiu</v-card-title>
              <v-card-subtitle>{{ nomeMes(c.meses[c.meses.length - 2].slice(0, 7), true) }} → {{ nomeMes(c.meses[c.meses.length - 1].slice(0, 7), true) }}</v-card-subtitle>
            </v-card-item>
            <v-list v-if="subiram.length" class="pt-0">
              <v-list-item v-for="s in subiram" :key="s.grupo + s.id" :aria-label="s.nome">
                <template #prepend><v-avatar :color="s.cor ?? 'error'" variant="tonal" rounded="lg" size="36"><v-icon size="20">{{ s.icone ?? 'mdi-trending-up' }}</v-icon></v-avatar></template>
                <v-list-item-title>{{ s.nome }}</v-list-item-title>
                <v-list-item-subtitle>{{ s.grupo }} · {{ brl(penultimo(s)) }} → {{ brl(ultimo(s)) }}</v-list-item-subtitle>
                <template #append>
                  <span class="valor text-error">+{{ brl(variacao(s)) }}<template v-if="pct(s) !== null"> ({{ Math.round(pct(s)!) }}%)</template></span>
                </template>
              </v-list-item>
            </v-list>
            <v-card-text v-else class="text-medium-emphasis">Nada subiu em relação ao mês anterior.</v-card-text>
          </v-card>
        </v-col>
        <v-col cols="12" md="5">
          <v-card class="h-100" data-testid="juros">
            <v-card-item>
              <template #prepend><v-avatar color="warning" variant="tonal" rounded="lg"><v-icon>mdi-alert-circle-outline</v-icon></v-avatar></template>
              <v-card-title class="font-weight-bold">Dinheiro perdido com juros</v-card-title>
              <v-card-subtitle>Juros, multas e tarifas</v-card-subtitle>
            </v-card-item>
            <v-card-text>
              <div class="d-flex ga-6">
                <div><div class="rotulo">Neste mês</div><div class="medio text-warning" data-testid="juros-mes">{{ brl(ultimo(c.juros)) }}</div></div>
                <div><div class="rotulo">Em {{ c.meses.length }} meses</div><div class="medio">{{ brl(jurosPeriodo) }}</div></div>
              </div>
              <v-list v-if="c.jurosDetalhe.length" density="compact" class="mt-2">
                <v-list-item v-for="l in c.jurosDetalhe.slice(0, 5)" :key="l.id" :title="l.descricao" :subtitle="dataBr(l.data)">
                  <template #append><span class="valor">{{ brl(l.juros ?? l.valor) }}</span></template>
                </v-list-item>
              </v-list>
              <div v-else class="text-caption text-medium-emphasis mt-2">
                Quando pagar algo com atraso e o valor vier maior, marque "foi juros/multa" ao confirmar: aparece aqui.
              </div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <v-card>
        <v-tabs v-model="aba" color="primary">
          <v-tab value="categorias">Categorias</v-tab>
          <v-tab value="contasFixas">Contas fixas</v-tab>
          <v-tab value="cartoes">Cartões</v-tab>
        </v-tabs>
        <v-card-text>
          <div v-if="grafico.datasets.length" style="height: 260px" class="mb-4"><Line :data="grafico" :options="opcoes" /></div>
          <v-table density="comfortable" data-testid="tabela-comparativo">
            <thead>
              <tr>
                <th>{{ aba === 'categorias' ? 'Categoria' : aba === 'contasFixas' ? 'Conta' : 'Cartão (fatura por vencimento)' }}</th>
                <th class="text-right">{{ nomeMes(c.meses[c.meses.length - 2].slice(0, 7), true) }}</th>
                <th class="text-right">{{ nomeMes(c.meses[c.meses.length - 1].slice(0, 7), true) }}</th>
                <th class="text-right">Diferença</th>
                <th class="text-right d-none d-sm-table-cell">Média antes</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="s in series" :key="s.id ?? s.nome" :aria-label="s.nome">
                <td><v-icon size="10" :color="s.cor ?? 'grey'" class="mr-2">mdi-circle</v-icon>{{ s.nome }}</td>
                <td class="text-right valor">{{ brl(penultimo(s)) }}</td>
                <td class="text-right valor">{{ brl(ultimo(s)) }}</td>
                <td class="text-right valor" :class="variacao(s) > 0 ? 'text-error' : variacao(s) < 0 ? 'text-success' : ''">
                  {{ variacao(s) > 0 ? '+' : '' }}{{ brl(variacao(s)) }}
                </td>
                <td class="text-right valor d-none d-sm-table-cell">{{ brl(media(s)) }}</td>
              </tr>
              <tr v-if="aba === 'categorias'" class="font-weight-bold">
                <td>Total</td>
                <td class="text-right valor">{{ brl(penultimo(c.total)) }}</td>
                <td class="text-right valor" data-testid="total-mes">{{ brl(ultimo(c.total)) }}</td>
                <td class="text-right valor" :class="variacao(c.total) > 0 ? 'text-error' : 'text-success'">{{ variacao(c.total) > 0 ? '+' : '' }}{{ brl(variacao(c.total)) }}</td>
                <td class="text-right valor d-none d-sm-table-cell">{{ brl(media(c.total)) }}</td>
              </tr>
              <tr v-if="!series.length"><td colspan="5" class="text-medium-emphasis">Nada neste período.</td></tr>
            </tbody>
          </v-table>
        </v-card-text>
      </v-card>
    </template>
  </div>
</template>
