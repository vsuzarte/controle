<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api'
import { confirmarLancamento, erro, estado, novoLancamento } from '../estado'
import { brl, dataBr, nomeMes } from '../formato'
import type { Alerta, Lancamento, Resumo } from '../tipos'

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

const maxGasto = computed(() => Math.max(1, ...(r.value?.gastosPorCategoria.map((g) => Math.max(g.gasto, g.orcado ?? 0)) ?? [])))

const corAlerta = (a: Alerta) => (a.tipo === 'Vencido' ? 'error' : a.tipo === 'Hoje' ? 'warning' : 'info')
const textoAlerta = (a: Alerta) =>
  a.tipo === 'Vencido' ? `venceu há ${a.dias} dia${a.dias > 1 ? 's' : ''}` : a.tipo === 'Hoje' ? 'vence hoje' : `em ${a.dias} dia${a.dias > 1 ? 's' : ''} · ${dataBr(a.data)}`

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
</script>

<template>
  <div class="pagina">
    <div v-if="!r" class="d-flex justify-center pa-12"><v-progress-circular indeterminate color="primary" /></div>
    <template v-else>
      <div class="d-flex align-center mb-4 flex-wrap ga-2">
        <div>
          <div class="text-h6">{{ nomeMes(r.hoje.slice(0, 7)) }}</div>
          <div class="text-caption text-medium-emphasis">Hoje, {{ dataBr(r.hoje, true) }}</div>
        </div>
        <v-spacer />
        <v-btn variant="tonal" color="success" prepend-icon="mdi-arrow-up" @click="novoLancamento('Receita')">Receita</v-btn>
        <v-btn variant="tonal" color="error" prepend-icon="mdi-arrow-down" @click="novoLancamento('Despesa')">Despesa</v-btn>
      </div>

      <v-row dense>
        <v-col cols="12" md="4">
          <v-card class="pa-4 h-100" @click="verContas = !verContas">
            <div class="rotulo">Saldo disponível hoje</div>
            <div class="grande" :class="r.saldoDisponivel < 0 ? 'text-error' : ''">{{ brl(r.saldoDisponivel) }}</div>
            <div class="text-caption text-medium-emphasis">Soma das contas (sem cartões) · toque para detalhar</div>
            <v-expand-transition>
              <div v-if="verContas" class="mt-3">
                <div v-for="c in r.contas" :key="c.id" class="d-flex justify-space-between text-body-2 py-1">
                  <span><v-icon size="small" :color="c.cor ?? undefined" class="mr-1">{{ c.tipo === 'CartaoCredito' ? 'mdi-credit-card' : 'mdi-bank' }}</v-icon>{{ c.nome }}</span>
                  <span class="valor" :class="c.saldo < 0 ? 'text-error' : ''">{{ brl(c.saldo) }}</span>
                </div>
              </div>
            </v-expand-transition>
          </v-card>
        </v-col>

        <v-col cols="12" md="4">
          <v-card class="pa-4 h-100">
            <div class="rotulo">Saldo projetado no fim do mês</div>
            <div class="grande" :class="r.saldoProjetado < 0 ? 'text-error' : 'text-primary'">{{ brl(r.saldoProjetado) }}</div>
            <div class="text-caption mt-2">
              <div class="d-flex justify-space-between"><span>Saldo hoje</span><span class="valor">{{ brl(r.saldoDisponivel) }}</span></div>
              <div class="d-flex justify-space-between text-success"><span>+ A receber</span><span class="valor">{{ brl(r.receitasAReceber) }}</span></div>
              <div class="d-flex justify-space-between"><span>− Contas a pagar</span><span class="valor">{{ brl(r.despesasAPagar) }}</span></div>
              <div class="d-flex justify-space-between"><span>− Faturas a vencer</span><span class="valor">{{ brl(r.faturasAPagar) }}</span></div>
              <div class="d-flex justify-space-between"><span>− Orçamento restante</span><span class="valor">{{ brl(r.variavelRestante) }}</span></div>
            </div>
          </v-card>
        </v-col>

        <v-col cols="12" md="4">
          <v-card class="pa-4 h-100">
            <div class="rotulo">Movimento do mês (realizado)</div>
            <div class="d-flex justify-space-between mt-2">
              <span>Entradas</span><span class="valor text-success">{{ brl(r.entradasMes) }}</span>
            </div>
            <div class="d-flex justify-space-between">
              <span>Gastos</span><span class="valor text-error">{{ brl(r.saidasMes) }}</span>
            </div>
            <v-divider class="my-2" />
            <div class="d-flex justify-space-between">
              <span>Resultado</span>
              <span class="valor" :class="r.entradasMes - r.saidasMes < 0 ? 'text-error' : 'text-success'">{{ brl(r.entradasMes - r.saidasMes) }}</span>
            </div>
            <div class="text-caption text-medium-emphasis mt-1">Gastos no cartão contam na data da compra.</div>
          </v-card>
        </v-col>
      </v-row>

      <v-row dense class="mt-1">
        <v-col cols="12" md="6">
          <v-card class="h-100">
            <v-card-title class="d-flex align-center text-subtitle-1 font-weight-bold">
              <v-icon class="mr-2">mdi-bell-outline</v-icon>Vencimentos
              <v-chip v-if="r.alertas.some((a) => a.tipo === 'Vencido')" color="error" size="small" class="ml-2">
                {{ r.alertas.filter((a) => a.tipo === 'Vencido').length }} atrasado(s)
              </v-chip>
            </v-card-title>
            <div v-if="!r.alertas.length" class="px-4 pb-4 text-medium-emphasis">
              <v-icon color="success" class="mr-1">mdi-check-circle</v-icon>Nada vencendo nos próximos 7 dias.
            </div>
            <v-list v-else density="comfortable" class="py-0">
              <v-list-item v-for="(a, i) in r.alertas" :key="i">
                <template #prepend>
                  <v-icon :color="corAlerta(a)">{{ a.cartaoId ? 'mdi-credit-card-outline' : a.tipoLancamento === 'Receita' ? 'mdi-cash-plus' : 'mdi-file-document-outline' }}</v-icon>
                </template>
                <v-list-item-title>{{ a.descricao }}</v-list-item-title>
                <v-list-item-subtitle :class="'text-' + corAlerta(a)">{{ textoAlerta(a) }}</v-list-item-subtitle>
                <template #append>
                  <span class="valor mr-2">{{ brl(a.valor) }}</span>
                  <v-btn size="small" variant="tonal" :color="a.tipoLancamento === 'Receita' ? 'success' : 'primary'" @click="agirAlerta(a)">
                    {{ a.cartaoId ? 'Ver fatura' : a.tipoLancamento === 'Receita' ? 'Recebi' : 'Paguei' }}
                  </v-btn>
                </template>
              </v-list-item>
            </v-list>
          </v-card>
        </v-col>

        <v-col cols="12" md="6">
          <v-card class="h-100">
            <v-card-title class="d-flex align-center text-subtitle-1 font-weight-bold">
              <v-icon class="mr-2">mdi-calendar-check-outline</v-icon>Previsão do mês
              <v-spacer />
              <v-btn size="small" variant="text" to="/planejamento">Abrir</v-btn>
            </v-card-title>
            <v-card-text>
              <div class="d-flex justify-space-between"><span>Receitas previstas</span><span class="valor text-success">{{ brl(r.planejamento.totalReceitas) }}</span></div>
              <div class="d-flex justify-space-between"><span>Contas fixas e parcelas</span><span class="valor">− {{ brl(r.planejamento.totalCompromissos) }}</span></div>
              <div class="d-flex justify-space-between"><span>Gastos variáveis (orçamento)</span><span class="valor">− {{ brl(r.planejamento.totalVariaveis) }}</span></div>
              <v-divider class="my-2" />
              <div class="d-flex justify-space-between font-weight-bold">
                <span>Sobra prevista no mês</span>
                <span class="valor" :class="r.planejamento.resultadoPrevisto < 0 ? 'text-error' : 'text-success'">{{ brl(r.planejamento.resultadoPrevisto) }}</span>
              </div>
              <div class="text-caption text-medium-emphasis mt-1">Só receitas menos despesas do mês; não inclui o saldo que você já tem.</div>
            </v-card-text>
          </v-card>
        </v-col>

        <v-col v-if="r.faturasAbertas.length" cols="12" md="6">
          <v-card class="h-100">
            <v-card-title class="text-subtitle-1 font-weight-bold"><v-icon class="mr-2">mdi-credit-card-outline</v-icon>Faturas</v-card-title>
            <v-list density="comfortable" class="py-0">
              <v-list-item
                v-for="f in r.faturasAbertas"
                :key="f.cartaoId + f.mes"
                :to="{ path: '/cartoes', query: { cartao: f.cartaoId, mes: f.mes.slice(0, 7) } }"
                :title="`${f.cartao} · ${nomeMes(f.mes.slice(0, 7), true)}`"
                :subtitle="`${f.status} · vence ${dataBr(f.vencimento)}`"
              >
                <template #append><span class="valor">{{ brl(f.restante || f.total) }}</span></template>
              </v-list-item>
            </v-list>
          </v-card>
        </v-col>

        <v-col cols="12" :md="r.faturasAbertas.length ? 6 : 12">
          <v-card class="h-100">
            <v-card-title class="text-subtitle-1 font-weight-bold"><v-icon class="mr-2">mdi-chart-bar</v-icon>Gastos por categoria</v-card-title>
            <v-card-text>
              <div v-if="!r.gastosPorCategoria.length" class="text-medium-emphasis">Nenhum gasto realizado neste mês ainda.</div>
              <div v-for="g in r.gastosPorCategoria" :key="g.categoriaId" class="mb-3">
                <div class="d-flex justify-space-between text-body-2">
                  <span>{{ g.categoria }}</span>
                  <span>
                    <span class="valor">{{ brl(g.gasto) }}</span>
                    <span v-if="g.orcado" class="text-medium-emphasis"> / {{ brl(g.orcado) }}</span>
                  </span>
                </div>
                <v-progress-linear
                  :model-value="(g.gasto / (g.orcado && g.orcado > 0 ? g.orcado : maxGasto)) * 100"
                  :color="g.orcado && g.gasto > g.orcado ? 'error' : g.cor ?? 'primary'"
                  height="8"
                  rounded
                  bg-opacity="0.15"
                />
              </div>
              <div class="text-caption text-medium-emphasis">Barra cheia = orçamento da categoria (quando houver).</div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </template>
  </div>
</template>
