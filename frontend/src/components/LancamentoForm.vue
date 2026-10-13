<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useDisplay } from 'vuetify'
import { api } from '../api'
import { alterado, confirmarLancamento, contasAtivas, erro, estado } from '../estado'
import { brl, dataBr, hojeIso } from '../formato'
import { FORMAS, type FormaPagamento, type Lancamento, type StatusLancamento, type TipoLancamento } from '../tipos'
import CampoValor from './CampoValor.vue'

const { mobile } = useDisplay()
const salvando = ref(false)
const maisDetalhes = ref(false)
const pendentes = ref<Lancamento[]>([])
const formRef = ref()

const f = reactive({
  tipo: 'Despesa' as TipoLancamento,
  valor: null as number | null,
  descricao: '',
  categoriaId: null as string | null,
  contaId: null as string | null,
  contaDestinoId: null as string | null,
  data: hojeIso(),
  pago: true,
  parcelas: 1,
  formaPagamento: 'Pix' as FormaPagamento | null,
  observacao: '',
  faturaMes: null as string | null,
  motivo: '',
})

const editando = computed(() => estado.formulario.editar)
const conta = computed(() => estado.contas.find((c) => c.id === f.contaId))
const ehCartao = computed(() => conta.value?.tipo === 'CartaoCredito')
const futura = computed(() => f.data > hojeIso())
const ontem = computed(() => {
  const d = new Date()
  d.setDate(d.getDate() - 1)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
})

const categorias = computed(() =>
  estado.categorias
    .filter((c) => (c.ativa || c.id === f.categoriaId) && c.tipo === (f.tipo === 'Receita' ? 'Receita' : 'Despesa'))
    .map((c) => ({ ...c, title: c.nome, value: c.id })),
)
const contas = computed(() =>
  contasAtivas.value
    .filter((c) => f.tipo === 'Despesa' || c.tipo !== 'CartaoCredito')
    .map((c) => ({ title: c.nome, value: c.id, tipo: c.tipo })),
)
const destinos = computed(() =>
  contasAtivas.value.filter((c) => c.id !== f.contaId).map((c) => ({ title: c.nome, value: c.id })),
)

// Lembra a última conta/categoria usada por tipo: lançar vira "valor + Enter" na maioria das vezes.
const memoria = (chave: string) => { try { return localStorage.getItem('fin.' + chave) } catch { return null } }
const lembrar = (chave: string, v: string | null) => { try { if (v) localStorage.setItem('fin.' + chave, v) } catch { /* sem storage */ } }

function preencher() {
  const l = estado.formulario.editar
  pendentes.value = []
  maisDetalhes.value = false
  if (l) {
    Object.assign(f, {
      tipo: l.tipo, valor: Math.abs(l.valor), descricao: l.descricao, categoriaId: l.categoriaId,
      contaId: l.contaId, contaDestinoId: l.contaDestinoId, data: l.data, pago: l.status === 'Realizado',
      parcelas: 1, formaPagamento: l.formaPagamento, observacao: l.observacao ?? '', faturaMes: l.faturaMes, motivo: '',
    })
    maisDetalhes.value = !!l.observacao
    return
  }
  const tipo = estado.formulario.tipo
  const contaMem = memoria('conta.' + tipo)
  const catMem = memoria('categoria.' + tipo)
  Object.assign(f, {
    tipo, valor: null, descricao: '', parcelas: 1, observacao: '', faturaMes: null, motivo: '', pago: true,
    data: hojeIso(), contaDestinoId: null,
    contaId: contasAtivas.value.some((c) => c.id === contaMem) ? contaMem : contasAtivas.value.find((c) => c.tipo !== 'CartaoCredito')?.id ?? null,
    categoriaId: estado.categorias.some((c) => c.id === catMem && c.ativa) ? catMem : null,
    formaPagamento: (memoria('forma.' + tipo) as FormaPagamento) ?? 'Pix',
  })
}

watch(() => estado.formulario.aberto, (aberto) => { if (aberto) preencher() })

watch(() => f.tipo, (tipo, antigo) => {
  if (editando.value || tipo === antigo) return
  const cat = memoria('categoria.' + tipo)
  f.categoriaId = estado.categorias.some((c) => c.id === cat) ? cat : null
  if (tipo !== 'Despesa' && ehCartao.value) f.contaId = contasAtivas.value.find((c) => c.tipo !== 'CartaoCredito')?.id ?? null
})

watch(() => f.data, (d, antigo) => {
  // Mudou a data de uma compra no cartão: a fatura é recalculada pelo servidor.
  if (antigo && d !== antigo && d !== editando.value?.data) f.faturaMes = null
  if (!editando.value) f.pago = d <= hojeIso()
})

// Evita duplicidade: se há um previsto dessa categoria por perto, oferece confirmá-lo.
watch(() => [f.categoriaId, estado.formulario.aberto] as const, async ([cat, aberto]) => {
  pendentes.value = []
  if (!cat || !aberto || editando.value || f.tipo === 'Transferencia') return
  try {
    pendentes.value = await api.get<Lancamento[]>(`/lancamentos/pendentes?categoriaId=${cat}`)
  } catch { /* dica opcional */ }
})

function usarPendente(l: Lancamento) {
  estado.formulario.aberto = false
  confirmarLancamento(l, f.valor)
}

function descricaoPadrao() {
  if (f.descricao.trim()) return f.descricao.trim()
  if (f.tipo === 'Transferencia') return 'Transferência'
  return estado.categorias.find((c) => c.id === f.categoriaId)?.nome ?? ''
}

async function salvar(outro = false) {
  const { valid } = await formRef.value.validate()
  if (!valid) return
  salvando.value = true
  try {
    const status: StatusLancamento | null = ehCartao.value ? null : f.pago ? 'Realizado' : 'Previsto'
    const corpo = {
      descricao: descricaoPadrao(),
      valor: f.valor,
      data: f.data,
      contaId: f.contaId,
      contaDestinoId: f.tipo === 'Transferencia' ? f.contaDestinoId : null,
      categoriaId: f.tipo === 'Transferencia' ? null : f.categoriaId,
      formaPagamento: ehCartao.value ? 'CartaoCredito' : f.formaPagamento,
      observacao: f.observacao || null,
      faturaMes: f.faturaMes,
    }
    if (editando.value) {
      await api.put(`/lancamentos/${editando.value.id}`, { ...corpo, motivo: f.motivo || null })
      alterado('Lançamento corrigido')
    } else {
      await api.post('/lancamentos', { ...corpo, tipo: f.tipo, status, parcelas: f.tipo === 'Despesa' && f.parcelas > 1 ? f.parcelas : null })
      lembrar('conta.' + f.tipo, f.contaId)
      lembrar('categoria.' + f.tipo, f.categoriaId)
      lembrar('forma.' + f.tipo, f.formaPagamento)
      alterado(f.parcelas > 1 ? `Compra lançada em ${f.parcelas}x` : 'Lançamento salvo')
    }
    if (outro) {
      Object.assign(f, { valor: null, descricao: '', parcelas: 1, observacao: '' })
      pendentes.value = []
      formRef.value.resetValidation()
    } else {
      estado.formulario.aberto = false
    }
  } catch (e) {
    erro(e)
  } finally {
    salvando.value = false
  }
}

const cores: Record<TipoLancamento, string> = { Despesa: 'error', Receita: 'success', Transferencia: 'info' }
</script>

<template>
  <v-dialog v-model="estado.formulario.aberto" :fullscreen="mobile" max-width="560" scrollable>
    <v-card>
      <v-toolbar :color="cores[f.tipo]" density="comfortable">
        <v-btn icon="mdi-close" aria-label="Fechar" @click="estado.formulario.aberto = false" />
        <v-toolbar-title>{{ editando ? 'Corrigir lançamento' : 'Novo lançamento' }}</v-toolbar-title>
      </v-toolbar>

      <v-card-text class="pt-4">
        <v-form ref="formRef" @submit.prevent="salvar()">
          <v-btn-toggle v-if="!editando" v-model="f.tipo" mandatory divided variant="outlined" class="mb-4 w-100" :color="cores[f.tipo]">
            <v-btn value="Despesa" class="flex-grow-1" prepend-icon="mdi-arrow-down">Despesa</v-btn>
            <v-btn value="Receita" class="flex-grow-1" prepend-icon="mdi-arrow-up">Receita</v-btn>
            <v-btn value="Transferencia" class="flex-grow-1" prepend-icon="mdi-swap-horizontal">Transf.</v-btn>
          </v-btn-toggle>

          <CampoValor v-model="f.valor" :autofocus="!mobile" class="mb-3" />

          <v-autocomplete
            v-if="f.tipo !== 'Transferencia'"
            v-model="f.categoriaId"
            :items="categorias"
            label="Categoria"
            class="mb-3"
            :rules="[(v: string) => !!v || 'Escolha a categoria']"
            auto-select-first
          >
            <template #item="{ props, item }">
              <v-list-item v-bind="props" :prepend-icon="item.raw.icone ?? 'mdi-tag'" :subtitle="item.raw.grupo ?? undefined">
                <template #prepend><v-icon :color="item.raw.cor ?? undefined">{{ item.raw.icone ?? 'mdi-tag' }}</v-icon></template>
              </v-list-item>
            </template>
          </v-autocomplete>

          <v-alert v-for="p in pendentes" :key="p.id" type="info" variant="tonal" density="compact" class="mb-3" icon="mdi-lightbulb-on-outline">
            <div class="text-body-2">
              <b>{{ p.descricao }}</b> está previsto para {{ dataBr(p.data) }} ({{ brl(p.valor) }}).
              Se for esse pagamento, confirme o previsto para não duplicar.
            </div>
            <v-btn size="small" variant="flat" color="info" class="mt-2" @click="usarPendente(p)">Confirmar este</v-btn>
          </v-alert>

          <v-text-field v-model="f.descricao" label="Descrição" :placeholder="descricaoPadrao() || 'Ex.: Mercado do mês'" class="mb-3" persistent-placeholder />

          <div class="d-flex ga-3 mb-3 flex-wrap">
            <v-select
              v-model="f.contaId"
              :items="contas"
              :label="f.tipo === 'Transferencia' ? 'De' : 'Conta / cartão'"
              style="min-width: 200px"
              class="flex-grow-1"
              :rules="[(v: string) => !!v || 'Escolha a conta']"
            >
              <template #item="{ props, item }">
                <v-list-item v-bind="props" :prepend-icon="item.raw.tipo === 'CartaoCredito' ? 'mdi-credit-card' : 'mdi-bank'" />
              </template>
            </v-select>
            <v-select
              v-if="f.tipo === 'Transferencia'"
              v-model="f.contaDestinoId"
              :items="destinos"
              label="Para"
              style="min-width: 200px"
              class="flex-grow-1"
              :rules="[(v: string) => !!v || 'Escolha o destino']"
            />
          </div>

          <div class="d-flex ga-3 mb-1 flex-wrap align-center">
            <v-text-field v-model="f.data" type="date" label="Data" style="min-width: 170px; max-width: 220px" />
            <v-chip-group v-model="f.data" mandatory selected-class="text-primary">
              <v-chip :value="hojeIso()" size="small" variant="outlined">Hoje</v-chip>
              <v-chip :value="ontem" size="small" variant="outlined">Ontem</v-chip>
            </v-chip-group>
          </div>

          <div v-if="!ehCartao && f.tipo !== 'Transferencia' && !editando" class="mb-2">
            <v-switch
              v-model="f.pago"
              :disabled="futura"
              color="success"
              hide-details
              density="comfortable"
              :label="f.tipo === 'Receita' ? 'Já recebi' : 'Já paguei'"
            />
            <div v-if="!f.pago" class="text-caption text-medium-emphasis mt-n2">
              Fica como <b>previsto</b>: aparece nos lembretes e só afeta o saldo quando você confirmar.
            </div>
          </div>

          <div v-if="ehCartao" class="text-caption text-medium-emphasis mb-3">
            <v-icon size="small">mdi-credit-card-outline</v-icon>
            Entra na fatura do cartão com a categoria escolhida. O pagamento da fatura depois não conta como gasto de novo.
          </div>

          <v-text-field
            v-if="f.tipo === 'Despesa' && !editando"
            v-model.number="f.parcelas"
            type="number"
            min="1"
            max="120"
            label="Parcelas"
            class="mb-3"
            style="max-width: 160px"
            :hint="f.parcelas > 1 && f.valor ? `${f.parcelas}x de ${brl(f.valor / f.parcelas)} (valor total)` : 'Valor informado é o total'"
            persistent-hint
          />

          <v-btn v-if="!maisDetalhes" variant="text" size="small" prepend-icon="mdi-chevron-down" class="px-1" @click="maisDetalhes = true">
            Mais detalhes
          </v-btn>
          <template v-if="maisDetalhes">
            <v-select v-if="!ehCartao && f.tipo !== 'Transferencia'" v-model="f.formaPagamento" :items="FORMAS" label="Forma de pagamento" class="mb-3" clearable />
            <v-textarea v-model="f.observacao" label="Observação" class="mb-3" />
          </template>

          <v-text-field v-if="editando" v-model="f.motivo" label="Motivo da correção (opcional)" class="mt-3" prepend-inner-icon="mdi-history" />
          <button type="submit" hidden />
        </v-form>
      </v-card-text>

      <v-divider />
      <v-card-actions class="pa-3">
        <v-btn v-if="!editando" variant="text" :disabled="salvando" @click="salvar(true)">Salvar e lançar outro</v-btn>
        <v-spacer />
        <v-btn color="primary" variant="flat" :loading="salvando" @click="salvar()">Salvar</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
