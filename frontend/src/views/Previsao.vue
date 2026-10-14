<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { api, qs } from '../api'
import { avisar, confirmarLancamento, erro, estado, pagarFatura } from '../estado'
import { brl, dataBr, hojeIso, somarDias } from '../formato'
import type { ConfigPrevisao, ItemPrevisao, Lancamento, PeriodoPrevisao, PeriodoSalvo, Previsao } from '../tipos'
import QuadroPrevisao from '../components/QuadroPrevisao.vue'

/**
 * Previsão por períodos de pagamento. "Automático": o período de hoje e a janela configurada (padrão: um mês de
 * pagamentos à frente). Ou um período salvo, com datas escolhidas. A escolha fica lembrada neste aparelho.
 */
const p = ref<Previsao | null>(null)
const data = ref<string | null>(null)
const periodosSalvos = ref<PeriodoSalvo[]>([])
const lembrada = () => { try { return localStorage.getItem('fin.previsao.selecao') ?? 'auto' } catch { return 'auto' } }
const selecao = ref<string>(lembrada())
const salvo = computed(() => periodosSalvos.value.find((x) => x.id === selecao.value))
/** Quadro (arrastar contas entre pagamentos) ou lista. Lembrado neste aparelho. */
const modo = ref<'quadro' | 'lista'>((() => { try { return localStorage.getItem('fin.previsao.modo') === 'lista' ? 'lista' : 'quadro' } catch { return 'quadro' } })())
watch(modo, (v) => { try { localStorage.setItem('fin.previsao.modo', v) } catch { /* sem storage */ } })

async function carregar() {
  try {
    const params = salvo.value ? { de: salvo.value.de, ate: salvo.value.ate } : { data: data.value }
    p.value = await api.get<Previsao>('/previsao' + qs(params))
  } catch (e) {
    erro(e)
  }
}
async function carregarSalvos() {
  periodosSalvos.value = await api.get<PeriodoSalvo[]>('/previsao/periodos')
  if (selecao.value !== 'auto' && !salvo.value) selecao.value = 'auto'
}
onMounted(async () => {
  try { await carregarSalvos() } catch (e) { erro(e) }
  await carregar()
})
watch(() => estado.versao, carregar)
watch(data, carregar)
watch(selecao, (v) => {
  try { localStorage.setItem('fin.previsao.selecao', v) } catch { /* sem storage */ }
  data.value = null
  carregar()
})

const abertos = computed(() => p.value?.periodos.filter((x) => !x.quitado) ?? [])
const quitados = computed(() => p.value?.periodos.filter((x) => x.quitado) ?? [])
const anterior = () => { if (p.value) data.value = somarDias(p.value.inicio, -1) }
// As setas andam um período por vez; a janela mantém o tamanho configurado.
const proximo = () => { if (p.value) data.value = abertos.value[1]?.inicio ?? somarDias(p.value.fim, 1) }
const ehHoje = computed(() => !!p.value && p.value.inicio <= p.value.hoje && p.value.hoje <= p.value.fim)

// ----- Período salvo (criar / editar / excluir) -----
const dlgPeriodo = ref(false)
const editando = ref<PeriodoSalvo | null>(null)
const fp = reactive({ nome: '', de: hojeIso(), ate: hojeIso() })
function novoPeriodo() {
  editando.value = null
  Object.assign(fp, { nome: '', de: p.value?.inicio ?? hojeIso(), ate: p.value?.fim ?? hojeIso() })
  dlgPeriodo.value = true
}
function editarPeriodo(x: PeriodoSalvo) {
  editando.value = x
  Object.assign(fp, { nome: x.nome, de: x.de, ate: x.ate })
  dlgPeriodo.value = true
}
async function salvarPeriodo() {
  try {
    const r = editando.value
      ? await api.put<PeriodoSalvo>(`/previsao/periodos/${editando.value.id}`, fp)
      : await api.post<PeriodoSalvo>('/previsao/periodos', fp)
    dlgPeriodo.value = false
    await carregarSalvos()
    if (selecao.value === r.id) await carregar()
    else selecao.value = r.id
    avisar('Período salvo')
  } catch (e) {
    erro(e)
  }
}
async function excluirPeriodo() {
  if (!editando.value) return
  try {
    await api.del(`/previsao/periodos/${editando.value.id}`)
    dlgPeriodo.value = false
    if (selecao.value === editando.value.id) selecao.value = 'auto'
    await carregarSalvos()
    avisar('Período excluído')
  } catch (e) {
    erro(e)
  }
}

// ----- Configuração da janela automática -----
const dlgConfig = ref(false)
const cfg = ref<ConfigPrevisao | null>(null)
const fc = reactive({ ateAntesDoPagamento: 3, pularQuitado: true })
async function abrirConfig() {
  try {
    cfg.value = await api.get<ConfigPrevisao>('/previsao/config')
    Object.assign(fc, { ateAntesDoPagamento: cfg.value.ateAntesDoPagamento ?? cfg.value.padrao, pularQuitado: cfg.value.pularQuitado })
    dlgConfig.value = true
  } catch (e) {
    erro(e)
  }
}
async function salvarConfig() {
  if (!cfg.value) return
  try {
    const n = Number(fc.ateAntesDoPagamento)
    cfg.value = await api.put<ConfigPrevisao>('/previsao/config', {
      ateAntesDoPagamento: n === cfg.value.padrao ? null : n,
      pularQuitado: fc.pularQuitado,
    })
    dlgConfig.value = false
    estado.versao++
    avisar(cfg.value.ateHoje ? `Previsão até ${dataBr(cfg.value.ateHoje)}` : 'Configuração salva')
  } catch (e) {
    erro(e)
  }
}

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
    <!-- ===== Qual período ver ===== -->
    <div class="d-flex align-center flex-wrap ga-2 mb-4">
      <v-chip-group v-model="selecao" mandatory selected-class="text-primary" aria-label="Período da previsão">
        <v-chip value="auto" variant="tonal" filter prepend-icon="mdi-autorenew">Automático</v-chip>
        <v-chip v-for="x in periodosSalvos" :key="x.id" :value="x.id" variant="tonal" filter data-testid="periodo-salvo">
          {{ x.nome }}
          <v-icon size="16" class="ml-1" :aria-label="'Editar ' + x.nome" @click.stop="editarPeriodo(x)">mdi-pencil-outline</v-icon>
        </v-chip>
      </v-chip-group>
      <v-btn variant="text" size="small" prepend-icon="mdi-plus" @click="novoPeriodo">Período</v-btn>
      <v-spacer />
      <div v-if="!salvo" class="d-flex align-center">
        <v-btn icon="mdi-chevron-left" variant="text" size="small" aria-label="Período anterior" @click="anterior" />
        <v-chip v-if="p" variant="tonal" :color="ehHoje ? 'primary' : undefined" data-testid="ciclo">
          {{ dataBr(p.inicio) }} a {{ dataBr(p.fim) }}
        </v-chip>
        <v-btn icon="mdi-chevron-right" variant="text" size="small" aria-label="Próximo período" @click="proximo" />
        <v-btn v-if="data" variant="text" size="small" @click="data = null">Hoje</v-btn>
      </div>
      <v-chip v-else-if="p" variant="tonal" color="primary" data-testid="ciclo">{{ dataBr(p.inicio) }} a {{ dataBr(p.fim) }}</v-chip>
      <v-btn-toggle v-model="modo" mandatory density="compact" variant="outlined" divided rounded="lg" aria-label="Ver como">
        <v-btn value="quadro" size="small" prepend-icon="mdi-view-column-outline" title="Arraste uma conta ou fatura para pagar com o dinheiro de outro pagamento (só aquela; no celular, segure o card ou use o menu ...)">Quadro</v-btn>
        <v-btn value="lista" size="small" prepend-icon="mdi-format-list-bulleted">Lista</v-btn>
      </v-btn-toggle>
      <v-btn variant="tonal" size="small" prepend-icon="mdi-tune-variant" @click="abrirConfig">Configurar</v-btn>
    </div>

    <div v-if="p && !p.porSalario && !p.personalizado" class="text-caption text-medium-emphasis mb-3">
      Mostrando o mês inteiro. Marque seu salário como "dia de pagamento" em Contas do mês para dividir por pagamentos.
    </div>

    <div v-if="!p" class="d-flex justify-center pa-16"><v-progress-circular indeterminate color="primary" /></div>
    <template v-else>
      <!-- ===== Resumo do ciclo ===== -->
      <v-card class="destaque pa-5 mb-4" rounded="xl" data-testid="resumo-ciclo">
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
          Gastos do dia a dia já feitos nesses períodos (débito/Pix): {{ brl(p.diaADia) }} — já estão no saldo.
        </div>
      </v-card>

      <v-alert v-for="q in quitados" :key="q.inicio" type="success" variant="tonal" density="compact" class="mb-4" icon="mdi-check-all" data-testid="periodo-quitado">
        <b>{{ q.titulo }}</b> ({{ dataBr(q.inicio) }} a {{ dataBr(q.fim) }}): tudo pago e recebido. A previsão já mostra os próximos pagamentos.
      </v-alert>

      <!-- ===== Quadro ===== -->
      <template v-if="modo === 'quadro'">
        <QuadroPrevisao :periodos="abertos" @agir="agir" />
      </template>

      <!-- ===== Lista ===== -->
      <v-row v-else>
        <v-col v-for="per in abertos" :key="per.inicio" cols="12" :lg="abertos.length > 1 ? 6 : 12" :xl="abertos.length > 2 ? 4 : undefined">
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
                      <template v-if="i.pagoCom && i.pendente">vence {{ dataBr(i.data, true) }} · pago com {{ i.pagoCom }} ({{ dataBr(i.pagarEm, true) }})</template>
                      <template v-else-if="i.pagavelAte && i.pendente">vence {{ dataBr(i.data, true) }} · pode pagar até {{ dataBr(i.pagavelAte, true) }}</template>
                      <template v-else>{{ dataBr(i.data) }}</template>
                      <v-chip v-if="i.diasDepoisDoVencimento && i.pendente" size="x-small" color="warning" variant="tonal" label class="ml-1" data-testid="aviso-atraso">
                        {{ i.diasDepoisDoVencimento }} dia{{ i.diasDepoisDoVencimento > 1 ? 's' : '' }} depois do vencimento
                      </v-chip>
                      <template v-if="i.detalhe"> · {{ i.detalhe }}</template>
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

    <v-dialog v-model="dlgPeriodo" max-width="440">
      <v-card>
        <v-card-title class="pt-4">{{ editando ? 'Editar período' : 'Novo período' }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="fp.nome" label="Nome" placeholder="Ex.: Até o salário de dezembro" autofocus />
          <div class="d-flex ga-3">
            <v-text-field v-model="fp.de" type="date" label="De" />
            <v-text-field v-model="fp.ate" type="date" label="Até" />
          </div>
          <div class="text-caption text-medium-emphasis">
            Fica salvo e aparece como opção no PC e no celular. Os pagamentos dentro dele continuam dividindo os períodos.
          </div>
        </v-card-text>
        <v-card-actions>
          <v-btn v-if="editando" color="error" variant="text" @click="excluirPeriodo">Excluir</v-btn>
          <v-spacer />
          <v-btn variant="text" @click="dlgPeriodo = false">Voltar</v-btn>
          <v-btn color="primary" variant="flat" @click="salvarPeriodo">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="dlgConfig" max-width="480">
      <v-card v-if="cfg">
        <v-card-title class="pt-4">Configurar a previsão automática</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <div class="text-body-2">
            Os dias de pagamento vêm das entradas marcadas como "dia de pagamento" em Contas do mês
            (hoje: {{ cfg.pagamentosPorMes }} por mês). Trocou de emprego? Ajuste lá; aqui fica só até onde olhar.
          </div>
          <v-text-field v-model.number="fc.ateAntesDoPagamento" type="number" min="1" max="24" data-testid="ate-antes"
            label="Mostrar tudo que vence até antes do Nº próximo pagamento"
            :hint="`Padrão: ${cfg.padrao} (o período de hoje e mais um mês de pagamentos).`" persistent-hint />
          <v-switch v-model="fc.pularQuitado" color="primary" inset hide-details density="compact"
            label="Quando o período de hoje estiver todo pago, já mostrar o seguinte" />
          <div v-if="cfg.ateHoje" class="text-caption text-medium-emphasis">
            Com a configuração salva, hoje a previsão vai até {{ dataBr(cfg.ateHoje) }}. A tela inicial segue a mesma regra.
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dlgConfig = false">Voltar</v-btn>
          <v-btn color="primary" variant="flat" @click="salvarConfig">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
