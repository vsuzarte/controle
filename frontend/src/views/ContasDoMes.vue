<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useDisplay } from 'vuetify'
import { api } from '../api'
import { alterado, contasAtivas, contasPagadoras, erro, estado, moverParcelas, verHistorico } from '../estado'
import { brl, dataBr, hojeIso } from '../formato'
import { FORMAS, VENCIMENTOS, type FormaPagamento, type Frequencia, type Regra, type RegraVencimento, type TipoLancamento } from '../tipos'
import CampoValor from '../components/CampoValor.vue'

const { smAndDown } = useDisplay()
const regras = ref<Regra[]>([])
const verInativas = ref(false)

async function carregar() {
  try {
    regras.value = await api.get<Regra[]>('/regras')
  } catch (e) {
    erro(e)
  }
}
onMounted(carregar)
watch(() => estado.versao, carregar)

const grupos = computed(() => {
  const ativas = regras.value.filter((r) => r.ativa).sort((a, b) => diaDe(a) - diaDe(b) || a.descricao.localeCompare(b.descricao))
  return [
    { titulo: 'Contas', icone: 'mdi-receipt-text-outline', cor: 'error', itens: ativas.filter((r) => r.tipo === 'Despesa' && r.modo === 'Recorrente') },
    { titulo: 'Entradas', icone: 'mdi-cash-plus', cor: 'success', itens: ativas.filter((r) => r.tipo === 'Receita') },
    { titulo: 'Parcelamentos', icone: 'mdi-credit-card-clock-outline', cor: 'secondary', itens: ativas.filter((r) => r.modo === 'Parcelado') },
  ]
})
const inativas = computed(() => regras.value.filter((r) => !r.ativa))
const totalMensal = computed(() => {
  const mensal = (r: Regra) =>
    r.frequencia === 'Mensal' ? r.valor / r.intervalo : r.frequencia === 'Anual' ? r.valor / 12 / r.intervalo : (r.valor * 52) / 12 / r.intervalo
  const ativas = regras.value.filter((r) => r.ativa && r.modo === 'Recorrente')
  return {
    entradas: ativas.filter((r) => r.tipo === 'Receita').reduce((s, r) => s + mensal(r), 0),
    contas: ativas.filter((r) => r.tipo === 'Despesa').reduce((s, r) => s + mensal(r), 0),
  }
})

const diaDe = (r: Regra) => Number((r.proximaData ?? r.dataInicio).slice(8, 10))
const detalhe = (r: Regra) => {
  if (r.modo === 'Parcelado') return `${r.realizadas} de ${r.parcelas} pagas · total ${brl(r.valor)}`
  const freq = r.frequencia === 'Mensal' ? (r.intervalo > 1 ? `a cada ${r.intervalo} meses` : 'todo mês')
    : r.frequencia === 'Anual' ? (r.intervalo > 1 ? `a cada ${r.intervalo} anos` : 'todo ano')
      : r.intervalo > 1 ? `a cada ${r.intervalo} semanas` : 'toda semana'
  return `${r.categoria} · ${r.conta} · ${quando(r) ?? freq}${r.valorVariavel ? ' · valor varia' : ''}${r.iniciaPeriodo ? ' · dia de pagamento' : ''}${r.pagoCom ? ` · pago com ${r.pagoCom}` : ''}`
}
/** "5º dia útil", "último dia útil"... (só para regras mensais que não são dia fixo). */
const quando = (r: Regra) =>
  r.frequencia !== 'Mensal' || r.vencimento === 'DiaFixo' ? null
    : r.vencimento === 'DiaUtil' ? `todo ${r.diaUtilNumero}º dia útil`
      : r.vencimento === 'UltimoDiaUtil' ? 'todo último dia útil' : 'todo último dia do mês'

// ---------- formulário ----------
const dialogo = ref(false)
const editandoId = ref<string | null>(null)
const salvando = ref(false)
const f = reactive({
  tipo: 'Despesa' as TipoLancamento,
  descricao: '',
  valor: null as number | null,
  valorVariavel: false,
  categoriaId: null as string | null,
  contaId: null as string | null,
  formaPagamento: 'Boleto' as FormaPagamento | null,
  frequencia: 'Mensal' as Frequencia,
  intervalo: 1,
  dataInicio: hojeIso(),
  dataFim: null as string | null,
  observacao: '',
  aplicarAosPrevistos: true,
  motivo: '',
  vencimento: 'DiaFixo' as RegraVencimento,
  diaUtilNumero: 5 as number | null,
  dia: null as number | null,
  iniciaPeriodo: false,
  pagoComRegraId: null as string | null,
})

const categorias = computed(() =>
  estado.categorias
    .filter((c) => (c.ativa || c.id === f.categoriaId) && c.tipo === (f.tipo === 'Receita' ? 'Receita' : 'Despesa'))
    .map((c) => ({ title: c.nome, value: c.id, icone: c.icone, cor: c.cor })),
)
const contas = computed(() =>
  contasAtivas.value
    .filter((c) => (f.tipo === 'Despesa' ? c.tipo !== 'Beneficio' : c.tipo !== 'CartaoCredito'))
    .map((c) => ({ title: c.nome, value: c.id })),
)
const mensal = computed(() => f.frequencia === 'Mensal')
/** Dias de pagamento cadastrados (entradas marcadas), para escolher com qual dinheiro a conta é paga. */
const pagamentos = computed(() => [
  { title: 'Automático (pelo vencimento)', value: null as string | null },
  ...regras.value.filter((r) => r.ativa && r.tipo === 'Receita' && r.iniciaPeriodo).map((r) => ({ title: r.descricao, value: r.id as string | null })),
])
const salarioCategoria = computed(() => estado.categorias.find((c) => c.id === f.categoriaId)?.nome.toLowerCase().startsWith('sal') ?? false)
// Entrada na categoria Salário já nasce marcada como dia de pagamento.
watch(() => f.categoriaId, () => { if (!editandoId.value && f.tipo === 'Receita') f.iniciaPeriodo = salarioCategoria.value })
const regraEditada = computed(() => regras.value.find((r) => r.id === editandoId.value))
const parcelado = computed(() => regraEditada.value?.modo === 'Parcelado')

function nova(tipo: TipoLancamento) {
  editandoId.value = null
  Object.assign(f, {
    tipo, descricao: '', valor: null, valorVariavel: false, categoriaId: null,
    contaId: contasPagadoras.value[0]?.id ?? null,
    formaPagamento: tipo === 'Receita' ? 'Transferencia' : 'Boleto', frequencia: 'Mensal', intervalo: 1,
    dataInicio: hojeIso(), dataFim: null, observacao: '', aplicarAosPrevistos: true, motivo: '',
    vencimento: 'DiaFixo', diaUtilNumero: 5, dia: null, iniciaPeriodo: false, pagoComRegraId: null,
  })
  dialogo.value = true
}

function editar(r: Regra) {
  editandoId.value = r.id
  Object.assign(f, {
    tipo: r.tipo, descricao: r.descricao, valor: r.valor, valorVariavel: r.valorVariavel, categoriaId: r.categoriaId,
    contaId: r.contaId, formaPagamento: r.formaPagamento, frequencia: r.frequencia, intervalo: r.intervalo,
    dataInicio: r.dataInicio, dataFim: r.dataFim, observacao: r.observacao ?? '', aplicarAosPrevistos: true, motivo: '',
    vencimento: r.vencimento, diaUtilNumero: r.diaUtilNumero ?? 5, dia: Number(r.dataInicio.slice(8, 10)), iniciaPeriodo: r.iniciaPeriodo, pagoComRegraId: r.pagoComRegraId,
  })
  dialogo.value = true
}

async function salvar() {
  salvando.value = true
  try {
    if (editandoId.value) {
      await api.put(`/regras/${editandoId.value}`, {
        descricao: f.descricao, categoriaId: f.categoriaId, contaId: f.contaId, formaPagamento: f.formaPagamento,
        valor: f.valor, valorVariavel: f.valorVariavel, dataFim: f.dataFim || null, observacao: f.observacao || null,
        aplicarAosPrevistos: f.aplicarAosPrevistos, motivo: f.motivo || null,
        vencimento: parcelado.value || !mensal.value ? null : f.vencimento,
        diaUtilNumero: f.vencimento === 'DiaUtil' ? f.diaUtilNumero : null,
        dia: f.vencimento === 'DiaFixo' ? f.dia : null,
        iniciaPeriodo: f.tipo === 'Receita' ? f.iniciaPeriodo : null,
        pagoComRegraId: f.tipo === 'Despesa' ? f.pagoComRegraId : null,
      })
      alterado('Conta atualizada')
    } else {
      await api.post('/regras', {
        modo: 'Recorrente', tipo: f.tipo, descricao: f.descricao, categoriaId: f.categoriaId, contaId: f.contaId,
        formaPagamento: f.formaPagamento, valor: f.valor, valorVariavel: f.valorVariavel, parcelas: null,
        frequencia: f.frequencia, intervalo: f.intervalo, dataInicio: f.dataInicio, dataFim: f.dataFim || null, observacao: f.observacao || null,
        vencimento: mensal.value ? f.vencimento : null,
        diaUtilNumero: f.vencimento === 'DiaUtil' ? f.diaUtilNumero : null,
        iniciaPeriodo: f.tipo === 'Receita' ? f.iniciaPeriodo : null,
        pagoComRegraId: f.tipo === 'Despesa' ? f.pagoComRegraId : null,
      })
      alterado(f.tipo === 'Receita' ? 'Entrada cadastrada' : 'Conta cadastrada: os próximos vencimentos já aparecem')
    }
    dialogo.value = false
  } catch (e) {
    erro(e)
  } finally {
    salvando.value = false
  }
}

// ---------- desativar ----------
const desativando = ref<Regra | null>(null)
const desat = reactive({ cancelarPrevistos: true, motivo: '' })

function abrirDesativar(r: Regra) {
  desativando.value = r
  Object.assign(desat, { cancelarPrevistos: true, motivo: '' })
}

async function desativar() {
  if (!desativando.value) return
  try {
    await api.post(`/regras/${desativando.value.id}/desativar`, desat)
    desativando.value = null
    alterado('Encerrada. O histórico continua guardado.')
  } catch (e) {
    erro(e)
  }
}

async function reativar(r: Regra) {
  try {
    await api.post(`/regras/${r.id}/reativar`)
    alterado('Reativada')
  } catch (e) {
    erro(e)
  }
}
</script>

<template>
  <div class="pagina">
    <!-- Totais na mesma linha dos botões: sobra tela para os cards -->
    <div class="d-flex flex-wrap align-center ga-3 mb-4">
      <v-card class="px-4 py-2 d-flex align-center ga-3" variant="tonal" color="error">
        <v-icon>mdi-receipt-text-outline</v-icon>
        <div><div class="rotulo">Contas por mês</div><div class="font-weight-bold" data-testid="total-contas">{{ brl(totalMensal.contas) }}</div></div>
      </v-card>
      <v-card class="px-4 py-2 d-flex align-center ga-3" variant="tonal" color="success">
        <v-icon>mdi-cash-plus</v-icon>
        <div><div class="rotulo">Entradas por mês</div><div class="font-weight-bold">{{ brl(totalMensal.entradas) }}</div></div>
      </v-card>
      <v-spacer />
      <v-btn variant="tonal" color="success" prepend-icon="mdi-plus" @click="nova('Receita')">Entrada</v-btn>
      <v-btn color="primary" variant="flat" prepend-icon="mdi-plus" @click="nova('Despesa')">Conta</v-btn>
    </div>

    <template v-for="g in grupos" :key="g.titulo">
      <div v-if="g.itens.length" class="mb-5">
        <div class="rotulo mb-2">{{ g.titulo }} · {{ g.itens.length }}</div>
        <v-row dense>
          <v-col v-for="r in g.itens" :key="r.id" cols="12" sm="6" lg="4" xl="3">
            <v-card class="h-100 d-flex flex-column" data-testid="conta-do-mes" :aria-label="r.descricao" @click="editar(r)">
              <div class="d-flex align-start ga-3 pa-4 pb-2">
                <v-avatar :color="g.cor" variant="tonal" rounded="lg" size="44" class="flex-column">
                  <span class="text-caption" style="line-height: 1; opacity: 0.8">dia</span>
                  <span class="font-weight-bold" style="line-height: 1.1; font-size: 1rem">{{ diaDe(r) }}</span>
                </v-avatar>
                <div class="flex-grow-1" style="min-width: 0">
                  <div class="font-weight-bold text-truncate">{{ r.descricao }}</div>
                  <div class="text-caption text-medium-emphasis detalhe">{{ detalhe(r) }}</div>
                </div>
                <v-menu>
                  <template #activator="{ props }">
                    <v-btn v-bind="props" icon="mdi-dots-vertical" variant="text" size="small" class="mt-n1 mr-n2" :aria-label="'Ações de ' + r.descricao" @click.stop />
                  </template>
                  <v-list density="compact" rounded="lg">
                    <v-list-item prepend-icon="mdi-pencil-outline" title="Editar" @click="editar(r)" />
                    <v-list-item v-if="r.modo === 'Parcelado'" prepend-icon="mdi-calendar-arrow-right" title="Mover parcelas" @click="moverParcelas(r.id, r.descricao)" />
                    <v-list-item prepend-icon="mdi-history" title="Histórico" @click="verHistorico(r.id, r.descricao)" />
                    <v-list-item
                      prepend-icon="mdi-stop-circle-outline"
                      :title="r.modo === 'Parcelado' ? 'Cancelar parcelas futuras' : 'Encerrar'"
                      base-color="error"
                      @click="abrirDesativar(r)"
                    />
                  </v-list>
                </v-menu>
              </div>
              <div class="d-flex align-end px-4 pb-4 mt-auto">
                <div class="text-caption text-medium-emphasis">
                  <template v-if="r.proximaData">Próxima: {{ dataBr(r.proximaData) }}</template>
                  <v-chip v-if="r.atrasadas" size="x-small" color="error" variant="tonal" label class="ml-1">{{ r.atrasadas }} atrasada(s)</v-chip>
                </div>
                <v-spacer />
                <span class="medio" :class="g.titulo === 'Entradas' ? 'text-success' : ''">{{ brl(r.modo === 'Parcelado' ? r.proximoValor ?? 0 : r.valor) }}</span>
              </div>
            </v-card>
          </v-col>
        </v-row>
      </div>
    </template>

    <v-card v-if="!regras.length">
      <v-empty-state
        icon="mdi-calendar-blank-outline"
        title="Cadastre o que se repete"
        text="Salário, aluguel, luz, internet, assinaturas… Os vencimentos aparecem sozinhos todo mês e você só confirma quando pagar."
      >
        <template #actions>
          <v-btn color="primary" variant="flat" @click="nova('Despesa')">Cadastrar conta</v-btn>
        </template>
      </v-empty-state>
    </v-card>

    <div v-if="inativas.length" class="text-center">
      <v-btn variant="text" size="small" @click="verInativas = !verInativas">{{ verInativas ? 'Ocultar' : 'Ver' }} {{ inativas.length }} encerrada(s)</v-btn>
    </div>
    <v-card v-if="verInativas && inativas.length" class="mt-2">
      <v-list>
        <v-list-item v-for="r in inativas" :key="r.id" :title="r.descricao" :subtitle="detalhe(r)" class="text-medium-emphasis">
          <template #append>
            <v-btn v-if="r.modo === 'Recorrente'" size="small" variant="tonal" @click="reativar(r)">Reativar</v-btn>
            <v-btn icon="mdi-history" size="small" variant="text" @click="verHistorico(r.id, r.descricao)" />
          </template>
        </v-list-item>
      </v-list>
    </v-card>

    <!-- ===== Formulário ===== -->
    <v-dialog v-model="dialogo" :fullscreen="smAndDown" max-width="540" scrollable>
      <v-card :rounded="smAndDown ? 0 : 'xl'">
        <v-card-item>
          <template #prepend><v-btn icon="mdi-close" variant="text" size="small" aria-label="Fechar" @click="dialogo = false" /></template>
          <v-card-title class="font-weight-bold">
            {{ editandoId ? 'Editar' : 'Nova' }} {{ parcelado ? 'compra parcelada' : f.tipo === 'Receita' ? 'entrada' : 'conta' }}
          </v-card-title>
        </v-card-item>
        <v-card-text class="d-flex flex-column ga-4">
          <v-text-field v-model="f.descricao" label="Nome" :placeholder="f.tipo === 'Receita' ? 'Ex.: Salário' : 'Ex.: Aluguel, Luz, Netflix'" persistent-placeholder />
          <template v-if="!parcelado">
            <CampoValor v-model="f.valor" :label="f.valorVariavel ? 'Valor médio' : 'Valor'" />
            <v-switch v-model="f.valorVariavel" color="primary" inset hide-details density="compact" label="O valor muda todo mês (luz, água…)" />
          </template>
          <v-autocomplete v-model="f.categoriaId" :items="categorias" label="Categoria">
            <template #item="{ props, item }">
              <v-list-item v-bind="props">
                <template #prepend><v-icon :color="item.raw.cor ?? undefined">{{ item.raw.icone ?? 'mdi-tag' }}</v-icon></template>
              </v-list-item>
            </template>
          </v-autocomplete>
          <v-select v-model="f.contaId" :items="contas" :label="f.tipo === 'Receita' ? 'Cai na conta' : 'Paga com'" />
          <template v-if="!editandoId">
            <div class="d-flex ga-3">
              <v-select
                v-model="f.frequencia"
                :items="[{ title: 'Todo mês', value: 'Mensal' }, { title: 'Toda semana', value: 'Semanal' }, { title: 'Todo ano', value: 'Anual' }]"
                label="Repete"
              />
              <v-text-field v-model.number="f.intervalo" type="number" min="1" label="A cada" style="max-width: 110px" />
            </div>
          </template>
          <template v-if="!parcelado">
            <v-select v-if="mensal" v-model="f.vencimento" :items="VENCIMENTOS" :label="f.tipo === 'Receita' ? 'Quando cai' : 'Quando vence'" data-testid="vencimento" />
            <v-text-field v-if="mensal && f.vencimento === 'DiaUtil'" v-model.number="f.diaUtilNumero" type="number" min="1" max="23" label="Qual dia útil"
              hint="Ex.: 5 para o 5º dia útil. Conta de segunda a sexta, sem feriados nacionais." persistent-hint />
            <v-text-field v-if="!editandoId" v-model="f.dataInicio" type="date"
              :label="!mensal || f.vencimento === 'DiaFixo' ? 'Próximo vencimento' : 'A partir de'"
              :hint="!mensal || f.vencimento === 'DiaFixo' ? 'O dia desta data vira o dia de vencimento.' : 'A primeira data calculada a partir deste dia.'" persistent-hint />
            <v-text-field v-else-if="mensal && f.vencimento === 'DiaFixo'" v-model.number="f.dia" type="number" min="1" max="31" label="Dia do vencimento" />
            <v-alert v-if="editandoId && mensal && regraEditada && (f.vencimento !== regraEditada.vencimento || (f.vencimento === 'DiaUtil' && f.diaUtilNumero !== regraEditada.diaUtilNumero) || (f.vencimento === 'DiaFixo' && f.dia !== Number(regraEditada.dataInicio.slice(8, 10))))"
              type="info" variant="tonal" density="compact" class="text-body-2">
              As datas dos vencimentos ainda não pagos, a partir deste mês, serão recalculadas. O que já foi pago não muda.
            </v-alert>
            <v-select v-if="f.tipo === 'Despesa' && pagamentos.length > 1" v-model="f.pagoComRegraId" :items="pagamentos" label="Pagar com"
              data-testid="pagar-com" hint="Com qual pagamento você costuma pagar esta conta. Só muda a previsão." persistent-hint />
            <v-switch v-if="f.tipo === 'Receita'" v-model="f.iniciaPeriodo" color="primary" inset hide-details density="compact"
              label="É dia de pagamento (salário, adiantamento): divide a previsão em períodos" data-testid="inicia-periodo" />
          </template>
          <v-select
            v-if="!parcelado"
            v-model="f.formaPagamento"
            :items="FORMAS"
            label="Forma de pagamento"
            clearable
            hint="Débito automático e cartão com valor fixo são confirmados sozinhos na data."
            persistent-hint
          />
          <v-text-field v-if="!parcelado" v-model="f.dataFim" type="date" label="Termina em (opcional)" clearable />
          <v-textarea v-model="f.observacao" label="Observação" />
          <template v-if="editandoId">
            <v-checkbox v-model="f.aplicarAosPrevistos" hide-details density="compact" label="Aplicar aos próximos vencimentos ainda não pagos" />
            <v-text-field v-model="f.motivo" label="Motivo da alteração (opcional)" />
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions>
          <v-btn color="primary" variant="flat" size="large" block :loading="salvando" @click="salvar">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ===== Encerrar ===== -->
    <v-dialog :model-value="!!desativando" max-width="440" @update:model-value="desativando = null">
      <v-card v-if="desativando" rounded="xl">
        <v-card-title class="pt-5 font-weight-bold">{{ desativando.modo === 'Parcelado' ? 'Cancelar parcelas futuras' : 'Encerrar conta' }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-2">
          <div class="text-body-2">
            <b>{{ desativando.descricao }}</b> para de gerar vencimentos novos. O que já foi pago continua guardado.
          </div>
          <v-checkbox
            v-model="desat.cancelarPrevistos"
            hide-details
            density="compact"
            :label="`Cancelar também os ${desativando.pendentes} vencimento(s) a partir de hoje`"
          />
          <v-alert v-if="desativando.atrasadas" type="warning" variant="tonal" density="compact" rounded="lg" class="text-body-2">
            {{ desativando.atrasadas }} atrasado(s) não serão cancelados: pague ou cancele cada um.
          </v-alert>
          <v-text-field v-model="desat.motivo" label="Motivo (opcional)" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="desativando = null">Voltar</v-btn>
          <v-btn color="error" variant="flat" @click="desativar">Confirmar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.detalhe { display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
</style>
