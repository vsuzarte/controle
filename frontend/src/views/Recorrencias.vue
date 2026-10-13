<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useDisplay } from 'vuetify'
import { api } from '../api'
import { alterado, contasAtivas, erro, estado, verHistorico } from '../estado'
import { brl, dataBr, hojeIso } from '../formato'
import { FORMAS, type FormaPagamento, type Frequencia, type Regra, type TipoLancamento } from '../tipos'
import CampoValor from '../components/CampoValor.vue'

const { mobile } = useDisplay()
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
  const ativas = regras.value.filter((r) => r.ativa)
  return [
    { titulo: 'Receitas programadas', icone: 'mdi-cash-plus', itens: ativas.filter((r) => r.tipo === 'Receita') },
    { titulo: 'Contas fixas', icone: 'mdi-repeat', itens: ativas.filter((r) => r.tipo === 'Despesa' && r.modo === 'Recorrente') },
    { titulo: 'Parcelamentos', icone: 'mdi-credit-card-clock-outline', itens: ativas.filter((r) => r.modo === 'Parcelado') },
  ]
})
const inativas = computed(() => regras.value.filter((r) => !r.ativa))
const totalMensal = computed(() => {
  const mensal = (r: Regra) => (r.frequencia === 'Mensal' ? r.valor / r.intervalo : r.frequencia === 'Anual' ? r.valor / 12 / r.intervalo : (r.valor * 52) / 12 / r.intervalo)
  const ativas = regras.value.filter((r) => r.ativa && r.modo === 'Recorrente')
  return {
    receitas: ativas.filter((r) => r.tipo === 'Receita').reduce((s, r) => s + mensal(r), 0),
    despesas: ativas.filter((r) => r.tipo === 'Despesa').reduce((s, r) => s + mensal(r), 0),
  }
})

const freqTexto = (r: Regra) =>
  r.modo === 'Parcelado'
    ? `${r.realizadas}/${r.parcelas} pagas · total ${brl(r.valor)}`
    : `${r.intervalo > 1 ? `a cada ${r.intervalo} ` : ''}${{ Mensal: r.intervalo > 1 ? 'meses' : 'mensal', Semanal: r.intervalo > 1 ? 'semanas' : 'semanal', Anual: r.intervalo > 1 ? 'anos' : 'anual' }[r.frequencia]}${r.valorVariavel ? ' · valor variável' : ''}`

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
})

const categorias = computed(() =>
  estado.categorias.filter((c) => (c.ativa || c.id === f.categoriaId) && c.tipo === (f.tipo === 'Receita' ? 'Receita' : 'Despesa')).map((c) => ({ title: c.nome, value: c.id })),
)
const contas = computed(() => contasAtivas.value.filter((c) => f.tipo === 'Despesa' || c.tipo !== 'CartaoCredito').map((c) => ({ title: c.nome, value: c.id })))
const regraEditada = computed(() => regras.value.find((r) => r.id === editandoId.value))

function nova(tipo: TipoLancamento) {
  editandoId.value = null
  Object.assign(f, {
    tipo, descricao: '', valor: null, valorVariavel: false, categoriaId: null,
    contaId: contasAtivas.value.find((c) => c.tipo !== 'CartaoCredito')?.id ?? null,
    formaPagamento: tipo === 'Receita' ? 'Transferencia' : 'Boleto', frequencia: 'Mensal', intervalo: 1,
    dataInicio: hojeIso(), dataFim: null, observacao: '', aplicarAosPrevistos: true, motivo: '',
  })
  dialogo.value = true
}

function editar(r: Regra) {
  editandoId.value = r.id
  Object.assign(f, {
    tipo: r.tipo, descricao: r.descricao, valor: r.valor, valorVariavel: r.valorVariavel, categoriaId: r.categoriaId,
    contaId: r.contaId, formaPagamento: r.formaPagamento, frequencia: r.frequencia, intervalo: r.intervalo,
    dataInicio: r.dataInicio, dataFim: r.dataFim, observacao: r.observacao ?? '', aplicarAosPrevistos: true, motivo: '',
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
      })
      alterado('Recorrência atualizada')
    } else {
      await api.post('/regras', {
        modo: 'Recorrente', tipo: f.tipo, descricao: f.descricao, categoriaId: f.categoriaId, contaId: f.contaId,
        formaPagamento: f.formaPagamento, valor: f.valor, valorVariavel: f.valorVariavel, parcelas: null,
        frequencia: f.frequencia, intervalo: f.intervalo, dataInicio: f.dataInicio, dataFim: f.dataFim || null, observacao: f.observacao || null,
      })
      alterado('Recorrência criada: os próximos vencimentos já aparecem no planejamento')
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
    alterado('Desativada. O histórico continua guardado.')
  } catch (e) {
    erro(e)
  }
}

async function reativar(r: Regra) {
  try {
    await api.post(`/regras/${r.id}/reativar`)
    alterado('Recorrência reativada')
  } catch (e) {
    erro(e)
  }
}
</script>

<template>
  <div class="pagina">
    <div class="d-flex align-center flex-wrap ga-2 mb-3">
      <div>
        <div class="text-h6">Recorrências</div>
        <div class="text-caption text-medium-emphasis">
          Salário, contas fixas e assinaturas. Cada uma gera os vencimentos previstos dos próximos meses.
        </div>
      </div>
      <v-spacer />
      <v-btn variant="tonal" color="success" prepend-icon="mdi-plus" @click="nova('Receita')">Receita</v-btn>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="nova('Despesa')">Conta fixa</v-btn>
    </div>

    <v-row dense class="mb-2">
      <v-col cols="6"><v-card class="pa-3"><div class="rotulo">Receitas fixas / mês</div><div class="valor text-success">{{ brl(totalMensal.receitas) }}</div></v-card></v-col>
      <v-col cols="6"><v-card class="pa-3"><div class="rotulo">Contas fixas / mês</div><div class="valor text-error">{{ brl(totalMensal.despesas) }}</div></v-card></v-col>
    </v-row>

    <template v-for="g in grupos" :key="g.titulo">
      <v-card v-if="g.itens.length" class="mb-3">
        <v-card-title class="text-subtitle-1 font-weight-bold"><v-icon class="mr-2">{{ g.icone }}</v-icon>{{ g.titulo }}</v-card-title>
        <v-list class="py-0">
          <v-list-item v-for="r in g.itens" :key="r.id" class="py-2" @click="editar(r)">
            <template #prepend>
              <v-avatar :color="r.categoriaCor ?? 'grey'" variant="tonal" size="38"><v-icon size="20">{{ g.icone }}</v-icon></v-avatar>
            </template>
            <v-list-item-title class="d-flex align-center ga-2">
              {{ r.descricao }}
              <v-chip v-if="r.atrasadas" size="x-small" color="error" variant="flat">{{ r.atrasadas }} atrasada(s)</v-chip>
            </v-list-item-title>
            <v-list-item-subtitle>{{ r.categoria }} · {{ r.conta }} · {{ freqTexto(r) }}</v-list-item-subtitle>
            <v-list-item-subtitle v-if="r.proximaData">Próximo: {{ dataBr(r.proximaData) }} ({{ brl(r.proximoValor) }})</v-list-item-subtitle>
            <template #append>
              <span class="valor mr-1">{{ brl(r.modo === 'Parcelado' ? r.proximoValor ?? 0 : r.valor) }}</span>
              <v-menu>
                <template #activator="{ props }"><v-btn v-bind="props" icon="mdi-dots-vertical" variant="text" size="small" @click.stop /></template>
                <v-list density="compact">
                  <v-list-item prepend-icon="mdi-pencil" title="Editar" @click="editar(r)" />
                  <v-list-item prepend-icon="mdi-history" title="Histórico" @click="verHistorico(r.id, r.descricao)" />
                  <v-list-item
                    prepend-icon="mdi-stop-circle-outline"
                    :title="r.modo === 'Parcelado' ? 'Cancelar parcelas futuras' : 'Desativar'"
                    base-color="error"
                    @click="abrirDesativar(r)"
                  />
                </v-list>
              </v-menu>
            </template>
          </v-list-item>
        </v-list>
      </v-card>
    </template>

    <v-card v-if="!regras.length" class="pa-8 text-center text-medium-emphasis">
      <v-icon size="48" class="mb-2">mdi-calendar-sync</v-icon>
      <div>Comece cadastrando seu salário e as contas que se repetem (aluguel, energia, internet...).</div>
      <div class="text-caption mt-1">Compras parceladas são criadas pelo botão "Lançar", informando o número de parcelas.</div>
    </v-card>

    <div v-if="inativas.length" class="text-center">
      <v-btn variant="text" size="small" @click="verInativas = !verInativas">{{ verInativas ? 'Ocultar' : 'Ver' }} {{ inativas.length }} desativada(s)</v-btn>
    </div>
    <v-card v-if="verInativas && inativas.length" class="mt-2">
      <v-list>
        <v-list-item v-for="r in inativas" :key="r.id" :title="r.descricao" :subtitle="`${r.categoria} · ${freqTexto(r)}`" class="text-medium-emphasis">
          <template #append>
            <v-btn v-if="r.modo === 'Recorrente'" size="small" variant="tonal" @click="reativar(r)">Reativar</v-btn>
            <v-btn icon="mdi-history" size="small" variant="text" @click="verHistorico(r.id, r.descricao)" />
          </template>
        </v-list-item>
      </v-list>
    </v-card>

    <v-dialog v-model="dialogo" :fullscreen="mobile" max-width="560" scrollable>
      <v-card>
        <v-toolbar :color="f.tipo === 'Receita' ? 'success' : 'primary'" density="comfortable">
          <v-btn icon="mdi-close" @click="dialogo = false" />
          <v-toolbar-title>
            {{ editandoId ? 'Editar' : 'Nova' }} {{ regraEditada?.modo === 'Parcelado' ? 'compra parcelada' : f.tipo === 'Receita' ? 'receita programada' : 'conta fixa' }}
          </v-toolbar-title>
        </v-toolbar>
        <v-card-text class="d-flex flex-column ga-3 pt-4">
          <v-text-field v-model="f.descricao" label="Descrição" :placeholder="f.tipo === 'Receita' ? 'Ex.: Salário' : 'Ex.: Aluguel, Energia, Netflix'" />
          <template v-if="regraEditada?.modo !== 'Parcelado'">
            <CampoValor v-model="f.valor" :label="f.valorVariavel ? 'Valor estimado' : 'Valor'" />
            <v-checkbox v-model="f.valorVariavel" hide-details density="compact" label="Valor muda todo mês (ex.: energia, água): informo o real ao pagar" />
          </template>
          <v-autocomplete v-model="f.categoriaId" :items="categorias" label="Categoria" />
          <v-select v-model="f.contaId" :items="contas" :label="f.tipo === 'Receita' ? 'Cai na conta' : 'Paga com'" />
          <v-select v-if="regraEditada?.modo !== 'Parcelado'" v-model="f.formaPagamento" :items="FORMAS" label="Forma de pagamento" clearable
            hint="Débito automático e cartão com valor fixo são confirmados sozinhos na data." persistent-hint />
          <template v-if="!editandoId">
            <div class="d-flex ga-3">
              <v-select v-model="f.frequencia" :items="[{ title: 'Mensal', value: 'Mensal' }, { title: 'Semanal', value: 'Semanal' }, { title: 'Anual', value: 'Anual' }]" label="Repete" />
              <v-text-field v-model.number="f.intervalo" type="number" min="1" label="A cada" style="max-width: 110px" />
            </div>
            <v-text-field v-model="f.dataInicio" type="date" label="Primeiro vencimento" hint="O dia desta data vira o dia de vencimento." persistent-hint />
          </template>
          <v-text-field v-if="regraEditada?.modo !== 'Parcelado'" v-model="f.dataFim" type="date" label="Termina em (opcional)" clearable />
          <v-textarea v-model="f.observacao" label="Observação" />
          <template v-if="editandoId">
            <v-checkbox v-model="f.aplicarAosPrevistos" hide-details density="compact" label="Aplicar aos vencimentos futuros ainda não pagos" />
            <v-text-field v-model="f.motivo" label="Motivo da alteração (opcional)" />
          </template>
        </v-card-text>
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn color="primary" variant="flat" :loading="salvando" @click="salvar">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog :model-value="!!desativando" max-width="440" @update:model-value="desativando = null">
      <v-card v-if="desativando">
        <v-card-title class="pt-4">{{ desativando.modo === 'Parcelado' ? 'Cancelar parcelas futuras' : 'Desativar recorrência' }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-2">
          <div class="text-body-2">
            Novos vencimentos de <b>{{ desativando.descricao }}</b> deixam de ser gerados. Pagamentos e histórico continuam guardados.
          </div>
          <v-checkbox v-model="desat.cancelarPrevistos" hide-details density="compact"
            :label="`Cancelar também os ${desativando.pendentes} vencimento(s) previsto(s) a partir de hoje`" />
          <div v-if="desativando.atrasadas" class="text-caption text-warning">
            {{ desativando.atrasadas }} atrasado(s) não serão cancelados: pague ou cancele cada um.
          </div>
          <v-text-field v-model="desat.motivo" label="Motivo (opcional)" />
        </v-card-text>
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="desativando = null">Voltar</v-btn>
          <v-btn color="error" variant="flat" @click="desativar">Confirmar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
