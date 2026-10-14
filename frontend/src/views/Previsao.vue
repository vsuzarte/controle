<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { api, qs } from '../api'
import { confirmarLancamento, erro, estado, pagarFatura } from '../estado'
import { brl, dataBr, somarDias } from '../formato'
import type { ItemPrevisao, Lancamento, PeriodoPrevisao, Previsao } from '../tipos'

/** Ciclo de salário a salário: o período de hoje, o seguinte e cada conta/entrada de cada um. */
const p = ref<Previsao | null>(null)
const data = ref<string | null>(null)
const carregando = ref(false)

async function carregar() {
  carregando.value = true
  try {
    p.value = await api.get<Previsao>('/previsao' + qs({ data: data.value }))
  } catch (e) {
    erro(e)
  } finally {
    carregando.value = false
  }
}
onMounted(carregar)
watch(() => estado.versao, carregar)
watch(data, carregar)

const anterior = () => { if (p.value) data.value = somarDias(p.value.inicio, -1) }
const proximo = () => { if (p.value) data.value = somarDias(p.value.fim, 1) }
const ehHoje = computed(() => !!p.value && p.value.inicio <= p.value.hoje && p.value.hoje <= p.value.fim)

const GRUPOS = [
  { tipo: 'Entrada', titulo: 'Entradas', icone: 'mdi-cash-plus', cor: 'success' },
  { tipo: 'Conta', titulo: 'Contas', icone: 'mdi-receipt-text-outline', cor: 'error' },
  { tipo: 'Fatura', titulo: 'Faturas do cartão', icone: 'mdi-credit-card-outline', cor: 'secondary' },
] as const
const itensDe = (per: PeriodoPrevisao, tipo: string) => per.itens.filter((i) => i.tipo === tipo)
const totalDe = (per: PeriodoPrevisao, tipo: string) => (tipo === 'Entrada' ? per.entradas : tipo === 'Conta' ? per.contas : per.faturas)

function status(i: ItemPrevisao) {
  if (i.atrasado) return { texto: 'atrasada', cor: 'error' }
  if (!i.pendente) return { texto: i.tipo === 'Entrada' ? 'recebido' : 'pago', cor: 'success' }
  if (i.tipo === 'Fatura' && i.pago > 0) return { texto: `falta ${brl(i.valor - i.pago)}`, cor: 'warning' }
  return { texto: 'pendente', cor: 'grey' }
}

async function agir(i: ItemPrevisao) {
  if (i.tipo === 'Fatura' && i.cartaoId && i.faturaMes) {
    pagarFatura({ cartaoId: i.cartaoId, cartao: i.descricao.replace(/^Fatura /, ''), mes: i.faturaMes, vencimento: i.data, restante: i.valor - i.pago })
    return
  }
  try {
    confirmarLancamento(await api.get<Lancamento>(`/lancamentos/${i.lancamentoId}`))
  } catch (e) {
    erro(e)
  }
}
</script>

<template>
  <div class="pagina">
    <div class="cabecalho">
      <div>
        <div class="titulo-pagina">Previsão</div>
        <div class="text-body-2 text-medium-emphasis">
          {{ p?.porSalario ? 'De um salário ao próximo, dividido em cada pagamento.' : 'Mês inteiro (cadastre seu salário em Contas do mês para dividir por pagamentos).' }}
        </div>
      </div>
      <v-spacer />
      <div class="d-flex align-center ga-1">
        <v-btn icon="mdi-chevron-left" variant="text" aria-label="Ciclo anterior" @click="anterior" />
        <v-chip v-if="p" variant="tonal" :color="ehHoje ? 'primary' : undefined" data-testid="ciclo">
          {{ dataBr(p.inicio) }} a {{ dataBr(p.fim) }}
        </v-chip>
        <v-btn icon="mdi-chevron-right" variant="text" aria-label="Próximo ciclo" @click="proximo" />
        <v-btn v-if="!ehHoje" variant="text" size="small" @click="data = null">Hoje</v-btn>
      </div>
    </div>

    <div v-if="!p" class="d-flex justify-center pa-16"><v-progress-circular indeterminate color="primary" /></div>
    <template v-else>
      <!-- ===== Resumo do ciclo ===== -->
      <v-card class="destaque pa-6 mb-4" rounded="xl" data-testid="resumo-ciclo">
        <v-row dense>
          <v-col cols="6" sm="3">
            <div class="rotulo" style="opacity: 0.8">Começa com</div>
            <div class="medio">{{ brl(p.saldoInicio) }}</div>
          </v-col>
          <v-col cols="6" sm="3">
            <div class="rotulo" style="opacity: 0.8">Entradas</div>
            <div class="medio">+ {{ brl(p.entradas) }}</div>
          </v-col>
          <v-col cols="6" sm="3">
            <div class="rotulo" style="opacity: 0.8">Contas e faturas</div>
            <div class="medio">− {{ brl(p.contas + p.faturas) }}</div>
          </v-col>
          <v-col cols="6" sm="3">
            <div class="rotulo" style="opacity: 0.8">Sobra no fim</div>
            <div class="medio" data-testid="sobra-ciclo">{{ brl(p.saldoFim) }}</div>
          </v-col>
        </v-row>
        <div v-if="p.diaADia" class="text-caption mt-2" style="opacity: 0.85">
          Gastos do dia a dia já feitos no ciclo (débito/Pix): {{ brl(p.diaADia) }} — já estão no saldo.
        </div>
      </v-card>

      <!-- ===== Períodos ===== -->
      <v-row>
        <v-col v-for="per in p.periodos" :key="per.inicio" cols="12" :lg="p.periodos.length > 1 ? 6 : 12">
          <v-card class="h-100" :class="{ 'opacity-70': per.encerrado }" data-testid="periodo" :aria-label="per.titulo">
            <v-card-item>
              <template #prepend>
                <v-avatar :color="per.atual ? 'primary' : 'surface-variant'" rounded="lg"><v-icon>mdi-cash-fast</v-icon></v-avatar>
              </template>
              <v-card-title class="font-weight-bold">{{ per.titulo }}</v-card-title>
              <v-card-subtitle>
                {{ dataBr(per.inicio) }} a {{ dataBr(per.fim) }}
                <v-chip v-if="per.atual" size="x-small" color="primary" variant="flat" class="ml-1">agora</v-chip>
                <v-chip v-else-if="per.encerrado" size="x-small" class="ml-1">encerrado</v-chip>
              </v-card-subtitle>
            </v-card-item>

            <v-card-text class="pb-0">
              <v-sheet rounded="lg" color="surface-variant" class="pa-3 d-flex flex-wrap ga-4 align-center">
                <div><div class="rotulo">Começa com</div><div class="valor">{{ brl(per.saldoInicio) }}</div></div>
                <div><div class="rotulo">A receber</div><div class="valor text-success">{{ brl(per.aReceber) }}</div></div>
                <div><div class="rotulo">A pagar</div><div class="valor text-error">{{ brl(per.contasAPagar + per.faturasAPagar) }}</div></div>
                <v-spacer />
                <div class="text-right">
                  <div class="rotulo">Sobra</div>
                  <div class="medio" :class="per.saldoFim < 0 ? 'text-error' : 'text-success'" data-testid="sobra-periodo">{{ brl(per.saldoFim) }}</div>
                  <div v-if="per.porDia" class="text-caption text-medium-emphasis">≈ {{ brl(per.porDia) }}/dia em {{ per.diasRestantes }} dias</div>
                </div>
              </v-sheet>
            </v-card-text>

            <v-list density="comfortable">
              <template v-for="g in GRUPOS" :key="g.tipo">
                <template v-if="itensDe(per, g.tipo).length">
                  <v-list-subheader class="font-weight-bold d-flex">
                    <span>{{ g.titulo }}</span>
                    <span class="ml-2 valor" :class="'text-' + g.cor">{{ brl(totalDe(per, g.tipo)) }}</span>
                  </v-list-subheader>
                  <v-list-item v-for="i in itensDe(per, g.tipo)" :key="(i.lancamentoId ?? i.cartaoId ?? '') + i.data" data-testid="item-previsao" :aria-label="i.descricao">
                    <template #prepend>
                      <v-avatar :color="i.cor ?? g.cor" variant="tonal" rounded="lg" size="36"><v-icon size="20">{{ i.icone ?? g.icone }}</v-icon></v-avatar>
                    </template>
                    <v-list-item-title :class="{ 'riscado': !i.pendente }">{{ i.descricao }}</v-list-item-title>
                    <v-list-item-subtitle>
                      {{ dataBr(i.data) }}<template v-if="i.detalhe"> · {{ i.detalhe }}</template>
                      <template v-if="i.juros"> · <span class="text-warning">juros {{ brl(i.juros) }}</span></template>
                    </v-list-item-subtitle>
                    <template #append>
                      <div class="d-flex align-center ga-2">
                        <v-chip size="x-small" :color="status(i).cor" variant="tonal" label>{{ status(i).texto }}</v-chip>
                        <span class="valor" :class="g.tipo === 'Entrada' ? 'text-success' : ''">{{ brl(i.valor) }}</span>
                        <v-btn v-if="i.pendente" size="small" variant="tonal" :color="g.tipo === 'Entrada' ? 'success' : 'primary'" @click="agir(i)">
                          {{ g.tipo === 'Entrada' ? 'Recebi' : g.tipo === 'Fatura' ? 'Pagar' : 'Paguei' }}
                        </v-btn>
                      </div>
                    </template>
                  </v-list-item>
                </template>
              </template>
              <div v-if="!per.itens.length" class="pa-4 text-medium-emphasis">Nada programado neste período.</div>
              <v-list-item v-if="per.diaADia" class="text-medium-emphasis">
                <template #prepend><v-avatar variant="tonal" rounded="lg" size="36"><v-icon size="20">mdi-cart-outline</v-icon></v-avatar></template>
                <v-list-item-title>Gastos do dia a dia (débito/Pix)</v-list-item-title>
                <v-list-item-subtitle>Já descontados do saldo</v-list-item-subtitle>
                <template #append><span class="valor">{{ brl(per.diaADia) }}</span></template>
              </v-list-item>
            </v-list>
          </v-card>
        </v-col>
      </v-row>

      <div class="text-caption text-medium-emphasis mt-4">
        Só conta o dinheiro que paga contas (sem cartões e vales). Compras no cartão entram pela fatura, no dia do vencimento.
        "Sobra" é o que fica livre para o dia a dia se tudo for pago e recebido como previsto.
      </div>
    </template>
  </div>
</template>
