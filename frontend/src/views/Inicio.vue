<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { api } from '../api'
import { confirmarLancamento, erro, estado, pagarFatura } from '../estado'
import { brl, dataBr, nomeMes, somarDias } from '../formato'
import type { Alerta, Lancamento, Resumo } from '../tipos'
import { separarVencimentos } from '../regras'
import Rosca from '../components/Rosca.vue'

const r = ref<Resumo | null>(null)
const verContas = ref(false)
const verResto = ref(false)

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


// ----- Previsão: período de hoje e "livre até o próximo pagamento" -----
const previsao = computed(() => r.value?.previsao)
// Período quitado não conta: "Livre até" e o resumo olham o primeiro período com algo em aberto.
const periodosAbertos = computed(() => previsao.value?.periodos.filter((p) => !p.quitado) ?? [])
const periodoAtual = computed(() => periodosAbertos.value.find((p) => p.atual) ?? periodosAbertos.value[0])
const proximoPagamento = computed(() => (periodoAtual.value ? somarDias(periodoAtual.value.fim, 1) : null))

// ----- Vencimentos até o próximo salário -----
const corAlerta = (a: Alerta) => (a.tipo === 'Vencido' ? 'error' : a.tipo === 'Hoje' ? 'warning' : 'info')
const textoAlerta = (a: Alerta) =>
  (a.venceEm ? `vence ${dataBr(a.venceEm, true)}, paga até ${dataBr(a.data, true)} · ` : '') +
  (a.tipo === 'Vencido'
    ? `venceu há ${a.dias} dia${a.dias > 1 ? 's' : ''}`
    : a.tipo === 'Hoje' ? (a.venceEm ? 'último dia hoje' : 'vence hoje') : `${a.venceEm ? '' : dataBr(a.data) + ' · '}em ${a.dias} dia${a.dias > 1 ? 's' : ''}`)
const iconeAlerta = (a: Alerta) => (a.cartaoId ? 'mdi-credit-card-outline' : a.tipoLancamento === 'Receita' ? 'mdi-cash-plus' : 'mdi-receipt-text-outline')

const vencimentos = computed(() => separarVencimentos(r.value?.alertas ?? []))
const aPagar = computed(() => vencimentos.value.aPagar)
const aReceber = computed(() => vencimentos.value.aReceber)
const atrasadas = computed(() => vencimentos.value.atrasadas)
const semana = computed(() => vencimentos.value.semana)
const resto = computed(() => vencimentos.value.depois)
const totalAPagar = computed(() => vencimentos.value.total)

async function agirAlerta(a: Alerta) {
  if (a.cartaoId && a.faturaMes) {
    const cartao = estado.contas.find((c) => c.id === a.cartaoId)?.nome ?? ''
    pagarFatura({ cartaoId: a.cartaoId, cartao, mes: a.faturaMes, vencimento: a.data, restante: a.valor })
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
    <div v-if="!r" class="d-flex justify-center pa-16"><v-progress-circular indeterminate color="primary" /></div>
    <template v-else>

      <!-- ===== Saldo ===== -->
      <v-card class="destaque pa-6 mb-4" rounded="xl" data-testid="card-saldo">
        <div class="d-flex align-start">
          <div>
            <div class="rotulo" style="opacity: 0.85">Saldo disponível</div>
            <div class="grande mt-1" data-testid="saldo-disponivel">{{ brl(r.saldoDisponivel) }}</div>
          </div>
          <v-spacer />
          <div class="text-right" data-testid="hoje">
            <div class="font-weight-bold text-capitalize">{{ dataBr(r.hoje, true) }}</div>
            <div class="text-caption" style="opacity: 0.8">{{ nomeMes(r.hoje.slice(0, 7)) }}</div>
          </div>
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
            <div class="rotulo" style="opacity: 0.8">
              {{ previsao?.porSalario && proximoPagamento ? `Livre até ${dataBr(somarDias(proximoPagamento, -1))}` : 'Previsto no fim do mês' }}
            </div>
            <div class="medio" data-testid="saldo-projetado">{{ brl(periodoAtual?.saldoFim ?? r.saldoProjetado) }}</div>
            <div v-if="periodoAtual?.porDia" class="text-caption" style="opacity: 0.8">≈ {{ brl(periodoAtual.porDia) }} por dia</div>
          </v-col>
          <v-col cols="6" sm="3">
            <div class="rotulo" style="opacity: 0.8">Entrou no mês</div>
            <div class="medio">{{ brl(r.entradasMes) }}</div>
            <div v-if="r.entradasVale" class="text-caption" style="opacity: 0.85" data-testid="entrou-vale">+ {{ brl(r.entradasVale) }} no vale (só compras)</div>
          </v-col>
          <v-col cols="6" sm="3">
            <div class="rotulo" style="opacity: 0.8">Gastei no mês</div>
            <div class="medio" data-testid="gastos-mes">{{ brl(r.saidasMes) }}</div>
          </v-col>
          <v-col cols="12" sm="2" class="d-flex align-end justify-sm-end">
            <v-btn variant="tonal" color="white" size="small" :prepend-icon="verContas ? 'mdi-chevron-up' : 'mdi-wallet-outline'" @click="verContas = !verContas">
              Contas
            </v-btn>
          </v-col>
        </v-row>
      </v-card>

      <!-- ===== Vales ===== -->
      <v-row v-if="r.beneficios.length" dense class="mb-3">
        <!-- Todos os vales numa linha só (até 4; acima disso, quebra em linhas de 4). -->
        <v-col v-for="b in r.beneficios" :key="b.id" cols="12" sm="6" :md="12 / Math.min(4, r.beneficios.length)">
          <v-card class="pa-3 d-flex align-center ga-3 h-100" data-testid="vale" :aria-label="b.nome">
            <v-avatar :color="b.cor ?? 'warning'" variant="tonal" rounded="lg"><v-icon>mdi-food</v-icon></v-avatar>
            <div class="flex-grow-1">
              <div class="rotulo">{{ b.nome }}</div>
              <div class="medio" :class="b.saldo < 0 ? 'text-error' : ''" data-testid="vale-saldo">{{ brl(b.saldo) }}</div>
              <div class="text-caption text-medium-emphasis">
                <template v-if="b.proximaRecarga">Recarga {{ dataBr(b.proximaRecarga).slice(0, 5) }}<template v-if="b.valorRecarga"> · {{ brl(b.valorRecarga) }}</template></template>
                <template v-if="b.duraAte"> · dura até {{ dataBr(b.duraAte).slice(0, 5) }}</template>
              </div>
            </div>
          </v-card>
        </v-col>
      </v-row>

      <v-row>
        <!-- ===== A pagar até o próximo salário ===== -->
        <v-col cols="12" md="6">
          <v-card class="h-100" data-testid="card-vencimentos">
            <v-card-item>
              <v-card-title class="font-weight-bold">A pagar</v-card-title>
              <v-card-subtitle v-if="previsao">até {{ dataBr(previsao.fim) }} · {{ brl(totalAPagar) }}</v-card-subtitle>
              <template #append>
                <v-chip v-if="atrasadas.length" color="error" variant="tonal" size="small" label>
                  {{ atrasadas.reduce((s, g) => s + g.itens.length, 0) }} atrasado(s)
                </v-chip>
              </template>
            </v-card-item>

            <v-list v-if="aPagar.length" class="pt-0">
              <template v-if="atrasadas.length">
                <v-list-subheader class="text-error font-weight-bold">Atrasadas</v-list-subheader>
                <template v-for="g in atrasadas" :key="g.chave">
                  <v-list-group v-if="g.itens.length > 1" :value="g.chave">
                    <template #activator="{ props }">
                      <v-list-item v-bind="props" data-testid="alerta-grupo" :aria-label="g.itens[0].descricao">
                        <template #prepend><v-avatar color="error" variant="tonal" rounded="lg"><v-icon>{{ iconeAlerta(g.itens[0]) }}</v-icon></v-avatar></template>
                        <v-list-item-title class="font-weight-medium">{{ g.itens[0].descricao }}</v-list-item-title>
                        <v-list-item-subtitle class="text-error" style="opacity: 1">{{ g.itens.length }} meses sem pagar · {{ brl(g.total) }}</v-list-item-subtitle>
                      </v-list-item>
                    </template>
                    <v-list-item v-for="a in g.itens" :key="a.lancamentoId ?? a.data" data-testid="alerta" :aria-label="a.descricao">
                      <v-list-item-subtitle class="text-error" style="opacity: 1">{{ dataBr(a.data) }} · {{ textoAlerta(a) }}</v-list-item-subtitle>
                      <template #append>
                        <div class="d-flex align-center ga-3">
                          <span class="valor">{{ brl(a.valor) }}</span>
                          <v-btn size="small" variant="flat" color="primary" @click="agirAlerta(a)">{{ a.cartaoId ? 'Pagar' : 'Paguei' }}</v-btn>
                        </div>
                      </template>
                    </v-list-item>
                  </v-list-group>
                  <v-list-item v-else data-testid="alerta" :aria-label="g.itens[0].descricao">
                    <template #prepend><v-avatar color="error" variant="tonal" rounded="lg"><v-icon>{{ iconeAlerta(g.itens[0]) }}</v-icon></v-avatar></template>
                    <v-list-item-title class="font-weight-medium">{{ g.itens[0].descricao }}</v-list-item-title>
                    <v-list-item-subtitle class="text-error" style="opacity: 1">{{ textoAlerta(g.itens[0]) }}</v-list-item-subtitle>
                    <template #append>
                      <div class="d-flex align-center ga-3">
                        <span class="valor">{{ brl(g.itens[0].valor) }}</span>
                        <v-btn size="small" variant="flat" color="primary" @click="agirAlerta(g.itens[0])">{{ g.itens[0].cartaoId ? 'Pagar' : 'Paguei' }}</v-btn>
                      </div>
                    </template>
                  </v-list-item>
                </template>
              </template>

              <template v-for="bloco in [{ titulo: 'Próximos 7 dias', itens: semana, mostrar: true }, { titulo: 'Depois', itens: resto, mostrar: verResto }]" :key="bloco.titulo">
                <template v-if="bloco.itens.length">
                  <v-list-subheader class="font-weight-bold">
                    {{ bloco.titulo }}
                    <v-btn v-if="bloco.titulo === 'Depois'" size="x-small" variant="text" class="ml-1" @click="verResto = !verResto">
                      {{ verResto ? 'ocultar' : `ver ${bloco.itens.length} · ${brl(bloco.itens.reduce((s, a) => s + a.valor, 0))}` }}
                    </v-btn>
                  </v-list-subheader>
                  <template v-if="bloco.mostrar">
                    <v-list-item v-for="(a, i) in bloco.itens" :key="i" data-testid="alerta" :aria-label="a.descricao">
                      <template #prepend><v-avatar :color="corAlerta(a)" variant="tonal" rounded="lg"><v-icon>{{ iconeAlerta(a) }}</v-icon></v-avatar></template>
                      <v-list-item-title class="font-weight-medium">{{ a.descricao }}</v-list-item-title>
                      <v-list-item-subtitle :class="'text-' + corAlerta(a)" style="opacity: 1">{{ textoAlerta(a) }}</v-list-item-subtitle>
                      <template #append>
                        <div class="d-flex align-center ga-3">
                          <span class="valor">{{ brl(a.valor) }}</span>
                          <v-btn size="small" variant="flat" color="primary" @click="agirAlerta(a)">{{ a.cartaoId ? 'Pagar' : 'Paguei' }}</v-btn>
                        </div>
                      </template>
                    </v-list-item>
                  </template>
                </template>
              </template>
            </v-list>
            <v-card-text v-else class="d-flex align-center ga-3">
              <v-avatar color="success" variant="tonal" rounded="lg"><v-icon>mdi-check-all</v-icon></v-avatar>
              <span class="text-medium-emphasis">Tudo em dia até o próximo pagamento.</span>
            </v-card-text>

            <template v-if="aReceber.length">
              <v-divider />
              <v-list density="compact">
                <v-list-subheader class="font-weight-bold">A receber</v-list-subheader>
                <v-list-item v-for="(a, i) in aReceber" :key="i" data-testid="alerta" :aria-label="a.descricao">
                  <template #prepend><v-icon color="success">mdi-cash-plus</v-icon></template>
                  <v-list-item-title>{{ a.descricao }}</v-list-item-title>
                  <v-list-item-subtitle>{{ textoAlerta(a) }}</v-list-item-subtitle>
                  <template #append>
                    <div class="d-flex align-center ga-3">
                      <span class="valor text-success">{{ brl(a.valor) }}</span>
                      <v-btn size="small" variant="tonal" color="success" @click="agirAlerta(a)">Recebi</v-btn>
                    </div>
                  </template>
                </v-list-item>
              </v-list>
            </template>
          </v-card>
        </v-col>

        <!-- ===== Previsão por período ===== -->
        <v-col cols="12" md="6">
          <v-card v-if="previsao" class="h-100" data-testid="card-previsao">
            <v-card-item>
              <v-card-title class="font-weight-bold">Previsão</v-card-title>
              <v-card-subtitle>{{ dataBr(previsao.inicio) }} a {{ dataBr(previsao.fim) }}{{ previsao.porSalario ? ' · por pagamento' : '' }}</v-card-subtitle>
              <template #append>
                <v-btn size="small" variant="text" color="primary" to="/previsao" append-icon="mdi-chevron-right">Ver tudo</v-btn>
              </template>
            </v-card-item>
            <v-card-text class="d-flex flex-column ga-3">
              <v-sheet v-for="p in periodosAbertos" :key="p.inicio" rounded="lg" :color="p.atual ? 'surface-variant' : undefined"
                :border="!p.atual" class="pa-3" data-testid="periodo-resumo">
                <div class="d-flex align-center">
                  <div>
                    <div class="font-weight-bold">{{ p.titulo }}</div>
                    <div class="text-caption text-medium-emphasis">{{ dataBr(p.inicio) }} a {{ dataBr(p.fim) }}<template v-if="p.atual"> · agora</template></div>
                  </div>
                  <v-spacer />
                  <div class="text-right">
                    <div class="rotulo">Sobra</div>
                    <div class="valor" :class="p.saldoFim < 0 ? 'text-error' : 'text-success'">{{ brl(p.saldoFim) }}</div>
                  </div>
                </div>
                <div class="d-flex flex-wrap ga-x-4 text-caption mt-2">
                  <span>Começa com <b class="valor">{{ brl(p.saldoInicio) }}</b></span>
                  <span class="text-success">+ entradas {{ brl(p.entradas) }}</span>
                  <span class="text-error">− contas {{ brl(p.contas) }}</span>
                  <span v-if="p.faturas" class="text-error">− faturas {{ brl(p.faturas) }}</span>
                </div>
              </v-sheet>
              <div class="text-caption text-medium-emphasis">
                Sobra = o que fica livre para o dia a dia, se tudo for pago. Cartão entra pela fatura, no dia em que vence.
              </div>
            </v-card-text>
          </v-card>
        </v-col>

        <!-- ===== Para onde foi ===== -->
        <v-col cols="12" :md="r.faturasAbertas.length ? 7 : 12">
          <v-card class="h-100">
            <v-card-item>
              <v-card-title class="font-weight-bold">Para onde foi o dinheiro</v-card-title>
              <template #append><v-btn size="small" variant="text" color="primary" to="/relatorios" append-icon="mdi-chevron-right">Comparar meses</v-btn></template>
            </v-card-item>
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
            <v-card-item>
              <v-card-title class="font-weight-bold">Cartões</v-card-title>
              <template #append><v-btn size="small" variant="text" color="primary" to="/cartoes" append-icon="mdi-chevron-right">Todos</v-btn></template>
            </v-card-item>
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
                <template v-for="c in [estado.contas.find((x) => x.id === f.cartaoId)]" :key="c?.id">
                  <div v-if="c?.limite && f.status === 'Aberta'" class="text-caption mt-1" style="opacity: 0.85">
                    Limite disponível {{ brl(c.limite - (c.limiteUsado ?? 0)) }}
                  </div>
                </template>
              </v-card>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </template>
  </div>
</template>
