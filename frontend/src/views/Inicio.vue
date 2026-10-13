<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api'
import { confirmarLancamento, erro, estado, novoLancamento } from '../estado'
import { brl, dataBr, nomeMes } from '../formato'
import type { Alerta, Lancamento, Resumo } from '../tipos'
import Rosca from '../components/Rosca.vue'

const router = useRouter()
const r = ref<Resumo | null>(null)
const verContas = ref(false)

async function carregar() {
  try {
    r.value = await api.get<Resumo>('/resumo')
  } catch (e) {
    erro(e)
  }
}
onMounted(carregar)
watch(() => estado.versao, carregar)

const CORES_RESERVA = ['#6366F1', '#F59E0B', '#EC4899', '#14B8A6', '#8B5CF6', '#F97316', '#0EA5E9', '#84CC16']
const gastos = computed(() =>
  (r.value?.gastosPorCategoria ?? []).map((g, i) => ({ ...g, corFinal: g.cor ?? CORES_RESERVA[i % CORES_RESERVA.length] })),
)
const fatias = computed(() => gastos.value.map((g) => ({ nome: g.categoria, valor: g.gasto, cor: g.corFinal })))

const acoes = [
  { titulo: 'Despesa', icone: 'mdi-arrow-down', cor: 'error', fazer: () => novoLancamento('Despesa') },
  { titulo: 'Receita', icone: 'mdi-arrow-up', cor: 'success', fazer: () => novoLancamento('Receita') },
  { titulo: 'Transferir', icone: 'mdi-swap-horizontal', cor: 'secondary', fazer: () => novoLancamento('Transferencia') },
  { titulo: 'Contas', icone: 'mdi-calendar-blank', cor: 'info', fazer: () => router.push('/contas-do-mes') },
]

const corAlerta = (a: Alerta) => (a.tipo === 'Vencido' ? 'error' : a.tipo === 'Hoje' ? 'warning' : 'info')
const textoAlerta = (a: Alerta) =>
  a.tipo === 'Vencido'
    ? `venceu há ${a.dias} dia${a.dias > 1 ? 's' : ''}`
    : a.tipo === 'Hoje'
      ? 'vence hoje'
      : `em ${a.dias} dia${a.dias > 1 ? 's' : ''}`
const iconeAlerta = (a: Alerta) => (a.cartaoId ? 'mdi-credit-card-outline' : a.tipoLancamento === 'Receita' ? 'mdi-cash-plus' : 'mdi-receipt-text-outline')

async function agirAlerta(a: Alerta) {
  if (a.cartaoId) {
    router.push({ path: '/cartoes', query: { cartao: a.cartaoId, mes: a.faturaMes?.slice(0, 7) } })
    return
  }
  try {
    confirmarLancamento(await api.get<Lancamento>(`/lancamentos/${a.lancamentoId}`))
  } catch (e) {
    erro(e)
  }
}

/** Para onde vão as receitas do mês: fixas, dia a dia e o que sobra. */
const previsao = computed(() => {
  const p = r.value?.planejamento
  if (!p) return []
  const base = Math.max(p.totalReceitas, p.totalCompromissos + p.totalVariaveis, 1)
  return [
    { nome: 'Contas fixas e parcelas', valor: p.totalCompromissos, cor: 'secondary' },
    { nome: 'Gastos do dia a dia', valor: p.totalVariaveis, cor: 'warning' },
    { nome: 'Sobra', valor: Math.max(0, p.resultadoPrevisto), cor: 'success' },
  ].map((x) => ({ ...x, pct: (x.valor / base) * 100 }))
})
</script>

<template>
  <div class="pagina">
    <div v-if="!r" class="d-flex justify-center pa-16"><v-progress-circular indeterminate color="primary" /></div>
    <template v-else>
      <div class="cabecalho">
        <div>
          <div class="titulo-pagina">{{ nomeMes(r.hoje.slice(0, 7)).split(' de ')[0] }}</div>
          <div class="text-body-2 text-medium-emphasis">Hoje é {{ dataBr(r.hoje, true) }}</div>
        </div>
      </div>

      <!-- ===== Saldo ===== -->
      <v-card class="destaque pa-6 mb-4" rounded="xl" data-testid="card-saldo">
        <div class="d-flex align-start">
          <div>
            <div class="rotulo" style="opacity: 0.85">Saldo disponível</div>
            <div class="grande mt-1" data-testid="saldo-disponivel">{{ brl(r.saldoDisponivel) }}</div>
          </div>
          <v-spacer />
          <v-btn variant="tonal" color="white" size="small" :prepend-icon="verContas ? 'mdi-chevron-up' : 'mdi-wallet-outline'" @click="verContas = !verContas">
            Contas
          </v-btn>
        </div>

        <v-expand-transition>
          <div v-if="verContas" class="mt-3">
            <div v-for="c in r.contas" :key="c.id" class="d-flex justify-space-between text-body-2 py-1" style="opacity: 0.92">
              <span>{{ c.nome }}</span><span class="valor">{{ brl(c.saldo) }}</span>
            </div>
          </div>
        </v-expand-transition>

        <v-divider class="my-4" color="white" style="opacity: 0.25" />
        <v-row dense>
          <v-col cols="12" sm="4">
            <div class="rotulo" style="opacity: 0.8">Previsto no fim do mês</div>
            <div class="medio" data-testid="saldo-projetado">{{ brl(r.saldoProjetado) }}</div>
          </v-col>
          <v-col cols="6" sm="4">
            <div class="rotulo" style="opacity: 0.8">Entrou no mês</div>
            <div class="medio">{{ brl(r.entradasMes) }}</div>
          </v-col>
          <v-col cols="6" sm="4">
            <div class="rotulo" style="opacity: 0.8">Gastei no mês</div>
            <div class="medio" data-testid="gastos-mes">{{ brl(r.saidasMes) }}</div>
          </v-col>
        </v-row>
      </v-card>

      <!-- ===== Ações rápidas ===== -->
      <v-row dense class="mb-3">
        <v-col v-for="a in acoes" :key="a.titulo" cols="3">
          <v-card class="text-center py-3 px-1" @click="a.fazer">
            <v-avatar :color="a.cor" variant="tonal" rounded="lg"><v-icon>{{ a.icone }}</v-icon></v-avatar>
            <div class="text-caption font-weight-bold mt-2 text-truncate">{{ a.titulo }}</div>
          </v-card>
        </v-col>
      </v-row>

      <v-row>
        <!-- ===== Vencimentos ===== -->
        <v-col cols="12" md="6">
          <v-card class="h-100" data-testid="card-vencimentos">
            <v-card-item>
              <v-card-title class="font-weight-bold">Vencimentos</v-card-title>
              <template #append>
                <v-chip v-if="r.alertas.some((a) => a.tipo === 'Vencido')" color="error" variant="tonal" size="small" label>
                  {{ r.alertas.filter((a) => a.tipo === 'Vencido').length }} atrasado(s)
                </v-chip>
              </template>
            </v-card-item>
            <v-list v-if="r.alertas.length" class="pt-0">
              <v-list-item v-for="(a, i) in r.alertas" :key="i" data-testid="alerta" :aria-label="a.descricao">
                <template #prepend>
                  <v-avatar :color="corAlerta(a)" variant="tonal" rounded="lg"><v-icon>{{ iconeAlerta(a) }}</v-icon></v-avatar>
                </template>
                <v-list-item-title class="font-weight-medium">{{ a.descricao }}</v-list-item-title>
                <v-list-item-subtitle :class="'text-' + corAlerta(a)" style="opacity: 1">{{ textoAlerta(a) }}</v-list-item-subtitle>
                <template #append>
                  <div class="d-flex align-center ga-3">
                    <span class="valor">{{ brl(a.valor) }}</span>
                    <v-btn size="small" variant="flat" :color="a.tipoLancamento === 'Receita' ? 'success' : 'primary'" @click="agirAlerta(a)">
                      {{ a.cartaoId ? 'Ver fatura' : a.tipoLancamento === 'Receita' ? 'Recebi' : 'Paguei' }}
                    </v-btn>
                  </div>
                </template>
              </v-list-item>
            </v-list>
            <v-card-text v-else class="d-flex align-center ga-3">
              <v-avatar color="success" variant="tonal" rounded="lg"><v-icon>mdi-check-all</v-icon></v-avatar>
              <span class="text-medium-emphasis">Tudo em dia. Nada vence nos próximos 7 dias.</span>
            </v-card-text>
          </v-card>
        </v-col>

        <!-- ===== Previsão ===== -->
        <v-col cols="12" md="6">
          <v-card class="h-100" data-testid="card-previsao">
            <v-card-item>
              <v-card-title class="font-weight-bold">Previsão do mês</v-card-title>
              <template #append>
                <v-btn size="small" variant="text" color="primary" to="/planejamento" append-icon="mdi-chevron-right">Planejar</v-btn>
              </template>
            </v-card-item>
            <v-card-text>
              <div class="d-flex justify-space-between align-end mb-4">
                <div>
                  <div class="rotulo">Sobra prevista</div>
                  <div class="medio" :class="r.planejamento.resultadoPrevisto < 0 ? 'text-error' : 'text-success'" data-testid="sobra-prevista">
                    {{ brl(r.planejamento.resultadoPrevisto) }}
                  </div>
                </div>
                <div class="text-right">
                  <div class="rotulo">Receitas</div>
                  <div class="valor">{{ brl(r.planejamento.totalReceitas) }}</div>
                </div>
              </div>
              <div v-for="p in previsao" :key="p.nome" class="mb-3">
                <div class="d-flex justify-space-between text-body-2 mb-1">
                  <span>{{ p.nome }}</span><span class="valor">{{ brl(p.valor) }}</span>
                </div>
                <v-progress-linear :model-value="p.pct" :color="p.cor" height="8" rounded bg-opacity="0.12" />
              </div>
            </v-card-text>
          </v-card>
        </v-col>

        <!-- ===== Para onde foi ===== -->
        <v-col cols="12" :md="r.faturasAbertas.length ? 7 : 12">
          <v-card class="h-100">
            <v-card-item><v-card-title class="font-weight-bold">Para onde foi o dinheiro</v-card-title></v-card-item>
            <v-card-text>
              <div v-if="!gastos.length" class="d-flex align-center ga-3">
                <v-avatar color="info" variant="tonal" rounded="lg"><v-icon>mdi-chart-donut</v-icon></v-avatar>
                <span class="text-medium-emphasis">Seus gastos do mês aparecem aqui.</span>
              </div>
              <div v-else class="d-flex align-center ga-6 flex-wrap justify-center">
                <Rosca :itens="fatias" titulo="Gastos por categoria" />
                <div class="flex-grow-1" style="min-width: 200px">
                  <div v-for="g in gastos.slice(0, 6)" :key="g.categoriaId" class="mb-3">
                    <div class="d-flex justify-space-between text-body-2 mb-1">
                      <span class="d-flex align-center ga-2">
                        <v-icon size="10" :color="g.corFinal">mdi-circle</v-icon>{{ g.categoria }}
                      </span>
                      <span class="valor">{{ brl(g.gasto) }}</span>
                    </div>
                    <template v-if="g.orcado">
                      <v-progress-linear
                        :model-value="(g.variavel / g.orcado) * 100"
                        :color="g.variavel > g.orcado ? 'error' : 'primary'"
                        height="6"
                        rounded
                        bg-opacity="0.12"
                      />
                      <div class="text-caption text-medium-emphasis mt-1">
                        Dia a dia: {{ brl(g.variavel) }} de {{ brl(g.orcado) }}
                        <span v-if="g.variavel > g.orcado" class="text-error">· passou {{ brl(g.variavel - g.orcado) }}</span>
                      </div>
                    </template>
                  </div>
                </div>
              </div>
            </v-card-text>
          </v-card>
        </v-col>

        <!-- ===== Cartões ===== -->
        <v-col v-if="r.faturasAbertas.length" cols="12" md="5">
          <v-card class="h-100">
            <v-card-item><v-card-title class="font-weight-bold">Cartões</v-card-title></v-card-item>
            <v-card-text class="d-flex flex-column ga-3">
              <v-card
                v-for="f in r.faturasAbertas"
                :key="f.cartaoId + f.mes"
                class="destaque destaque--indigo pa-5"
                rounded="xl"
                :to="{ path: '/cartoes', query: { cartao: f.cartaoId, mes: f.mes.slice(0, 7) } }"
              >
                <div class="d-flex justify-space-between align-center">
                  <span class="font-weight-bold">{{ f.cartao }}</span>
                  <v-chip size="x-small" variant="flat" color="white" class="text-indigo-darken-3" label>{{ f.status }}</v-chip>
                </div>
                <div class="medio mt-4">{{ brl(f.restante || f.total) }}</div>
                <div class="text-caption" style="opacity: 0.85">Fatura de {{ nomeMes(f.mes.slice(0, 7), true) }} · vence {{ dataBr(f.vencimento) }}</div>
              </v-card>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </template>
  </div>
</template>
