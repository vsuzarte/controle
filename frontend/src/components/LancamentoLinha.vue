<script setup lang="ts">
import { computed, ref } from 'vue'
import { useDisplay } from 'vuetify'
import { api } from '../api'
import { alterado, confirmarLancamento, editarLancamento, erro, moverParcelas, verCompra, verHistorico } from '../estado'
import { brl, dataBr, hojeIso, nomeMes } from '../formato'
import type { Compra, ItemCompra, Lancamento } from '../tipos'
import CampoValor from './CampoValor.vue'

/** No extrato: efeito na conta (+/−, vale também para transferência) e o saldo depois do movimento. */
const props = defineProps<{ l: Lancamento; mostrarData?: boolean; efeito?: number; saldo?: number | null }>()
const { xs } = useDisplay()

const dialogo = ref<'cancelar' | 'estornar' | 'reabrir' | null>(null)
const motivo = ref('')
const valorEstorno = ref<number | null>(null)
const dataEstorno = ref(hojeIso())
const salvando = ref(false)

// Itens da nota fiscal abertos direto na linha (setinha). Só os desta categoria: a nota vira um lançamento por categoria.
const verItens = ref(false)
const itensNota = ref<ItemCompra[] | null>(null)
async function alternarItens() {
  verItens.value = !verItens.value
  if (!verItens.value || itensNota.value || !props.l.compraId) return
  try {
    const compra = await api.get<Compra>(`/compras/${props.l.compraId}`)
    itensNota.value = compra.itens.filter((i) => i.categoriaId === props.l.categoriaId)
  } catch (e) {
    erro(e)
    verItens.value = false
  }
}

const cancelado = computed(() => props.l.status === 'Cancelado')
const previsto = computed(() => props.l.status === 'Previsto')
// Compras no cartão são cobradas na fatura: não há "pagar" individual.
const noCartao = computed(() => !!props.l.faturaMes && props.l.tipo !== 'Transferencia')
const atrasado = computed(() => previsto.value && !noCartao.value && props.l.data < hojeIso())
const podeConfirmar = computed(() => previsto.value && props.l.tipo !== 'Transferencia' && !noCartao.value)
const icone = computed(() =>
  props.l.tipo === 'Transferencia' ? 'mdi-swap-horizontal' : props.l.categoriaIcone ?? (props.l.tipo === 'Receita' ? 'mdi-arrow-up' : 'mdi-arrow-down'),
)
const sinal = computed(() => props.efeito !== undefined ? Math.sign(props.efeito)
  : props.l.tipo === 'Receita' ? 1 : props.l.tipo === 'Despesa' ? -1 : 0)
const corValor = computed(() => (sinal.value === 0 ? 'text-secondary' : sinal.value * props.l.valor > 0 ? 'text-success' : ''))

const subtitulo = computed(() => {
  const l = props.l
  const partes: string[] = []
  if (props.mostrarData) partes.push(dataBr(l.data))
  partes.push(l.tipo === 'Transferencia' ? `${l.conta} → ${l.contaDestino}` : `${l.categoria ?? ''} · ${l.conta}`)
  if (l.faturaMes && l.tipo !== 'Transferencia') partes.push(`fatura ${nomeMes(l.faturaMes.slice(0, 7), true)}`)
  return partes.join(' · ')
})

function abrir(tipo: 'cancelar' | 'estornar' | 'reabrir') {
  motivo.value = ''
  valorEstorno.value = props.l.valor
  dataEstorno.value = hojeIso()
  dialogo.value = tipo
}

async function executar() {
  salvando.value = true
  try {
    if (dialogo.value === 'cancelar') {
      await api.post(`/lancamentos/${props.l.id}/cancelar`, { motivo: motivo.value })
      alterado('Lançamento cancelado (continua no histórico)')
    } else if (dialogo.value === 'estornar') {
      await api.post(`/lancamentos/${props.l.id}/estornar`, { valor: valorEstorno.value, data: dataEstorno.value, motivo: motivo.value })
      alterado('Estorno registrado')
    } else {
      await api.post(`/lancamentos/${props.l.id}/reabrir`, { motivo: motivo.value })
      alterado('Voltou para previsto')
    }
    dialogo.value = null
  } catch (e) {
    erro(e)
  } finally {
    salvando.value = false
  }
}
</script>

<template>
  <v-list-item
    :class="{ riscado: cancelado }"
    rounded="lg"
    class="py-2"
    data-testid="lancamento"
    :aria-label="l.descricao"
    @click="!cancelado && editarLancamento(l)"
  >
    <template #prepend>
      <v-avatar :color="l.categoriaCor ?? (l.tipo === 'Transferencia' ? 'secondary' : 'grey')" variant="tonal" rounded="lg" size="42">
        <v-icon size="20">{{ icone }}</v-icon>
      </v-avatar>
    </template>

    <v-list-item-title class="font-weight-semibold d-flex align-center ga-1">
      <span class="text-truncate">{{ l.descricao }}</span>
      <v-icon v-if="l.compraId" size="14" class="text-medium-emphasis" title="Lançado pela nota fiscal">mdi-receipt-text-outline</v-icon>
      <v-icon v-if="l.regraId" size="14" class="text-medium-emphasis">{{ l.totalParcelas ? 'mdi-credit-card-clock-outline' : 'mdi-repeat' }}</v-icon>
    </v-list-item-title>
    <v-list-item-subtitle>{{ subtitulo }}</v-list-item-subtitle>
    <div v-for="i in l.itensEncontrados ?? []" :key="i.descricao" class="text-caption text-primary d-flex align-center ga-1" data-testid="item-encontrado">
      <v-icon size="13">mdi-receipt-text-outline</v-icon>
      {{ i.descricao }} · {{ i.quantidade.toLocaleString('pt-BR', { maximumFractionDigits: 3 }) }} {{ i.unidade?.toLowerCase() }} · {{ brl(i.valorPago) }}
    </div>
    <div v-if="verItens" class="itens-nota mt-2" data-testid="itens-da-linha" @click.stop>
      <v-progress-linear v-if="!itensNota" indeterminate color="primary" height="2" />
      <div v-for="i in itensNota ?? []" :key="i.sequencia" class="item-nota d-flex align-center ga-2 py-1 text-body-2">
        <span class="flex-grow-1 text-truncate">{{ i.descricao }}</span>
        <span class="text-caption text-medium-emphasis text-no-wrap">{{ i.quantidade.toLocaleString('pt-BR', { maximumFractionDigits: 3 }) }} {{ i.unidade?.toLowerCase() }} × {{ brl(i.valorUnitario) }}</span>
        <span class="valor text-no-wrap" style="min-width: 80px; text-align: right">{{ brl(i.valorPago) }}</span>
      </div>
    </div>
    <v-list-item-subtitle v-if="cancelado && l.motivoCancelamento" class="font-italic">{{ l.motivoCancelamento }}</v-list-item-subtitle>

    <template #append>
      <div class="d-flex align-center ga-2">
        <v-btn v-if="l.compraId" :icon="verItens ? 'mdi-chevron-up' : 'mdi-chevron-down'" variant="text" size="small" density="comfortable"
          :title="verItens ? 'Esconder itens da nota' : 'Ver itens da nota'" :aria-label="(verItens ? 'Esconder' : 'Ver') + ' itens da nota de ' + l.descricao"
          data-testid="abrir-itens" @click.stop="alternarItens" />
        <div class="d-flex flex-column align-end">
          <span class="valor" :class="corValor">{{ sinal < 0 ? '−' : sinal > 0 ? '+' : '' }}{{ brl(Math.abs(l.valor)) }}</span>
          <span v-if="saldo !== undefined && saldo !== null" class="text-caption text-medium-emphasis text-no-wrap" data-testid="saldo-linha">saldo {{ brl(saldo) }}</span>
          <v-chip v-if="atrasado" size="x-small" color="error" variant="tonal" label>atrasado</v-chip>
          <v-chip v-else-if="previsto" size="x-small" color="warning" variant="tonal" label>{{ noCartao ? 'a cobrar' : 'previsto' }}</v-chip>
          <v-chip v-else-if="cancelado" size="x-small" variant="tonal" label>cancelado</v-chip>
        </div>
        <v-btn
          v-if="podeConfirmar && !xs"
          size="small"
          variant="tonal"
          :color="l.tipo === 'Receita' ? 'success' : 'primary'"
          @click.stop="confirmarLancamento(l)"
        >
          {{ l.tipo === 'Receita' ? 'Recebi' : 'Paguei' }}
        </v-btn>
        <v-menu>
          <template #activator="{ props: p }">
            <v-btn v-bind="p" icon="mdi-dots-vertical" variant="text" size="small" density="comfortable" :aria-label="'Ações de ' + l.descricao" @click.stop />
          </template>
          <v-list density="compact" rounded="lg">
            <v-list-item
              v-if="podeConfirmar"
              prepend-icon="mdi-check-circle-outline"
              :title="l.tipo === 'Receita' ? 'Recebi' : 'Paguei'"
              base-color="primary"
              @click="confirmarLancamento(l)"
            />
            <v-list-item v-if="!cancelado" prepend-icon="mdi-pencil-outline" title="Corrigir" @click="editarLancamento(l)" />
            <v-list-item v-if="l.status === 'Realizado' && l.programado" prepend-icon="mdi-undo" title="Voltar para previsto" @click="abrir('reabrir')" />
            <v-list-item
              v-if="l.status === 'Realizado' && !l.estornoDeId && l.tipo !== 'Transferencia'"
              prepend-icon="mdi-cash-refund"
              title="Estornar / devolução"
              @click="abrir('estornar')"
            />
            <v-list-item v-if="!cancelado" prepend-icon="mdi-cancel" title="Cancelar lançamento" base-color="error" @click="abrir('cancelar')" />
            <v-list-item v-if="!cancelado && l.totalParcelas && l.regraId" prepend-icon="mdi-calendar-arrow-right" title="Mover parcelas" @click="moverParcelas(l.regraId, l.descricao)" />
            <v-list-item v-if="l.compraId" prepend-icon="mdi-receipt-text-outline" title="Ver itens da nota" @click="verCompra(l.compraId)" />
            <v-list-item prepend-icon="mdi-history" title="Histórico" @click="verHistorico(l.id, l.descricao)" />
          </v-list>
        </v-menu>
      </div>
    </template>
  </v-list-item>

  <v-dialog :model-value="!!dialogo" max-width="420" @update:model-value="dialogo = null">
    <v-card rounded="xl">
      <v-card-title class="pt-5 font-weight-bold">
        {{ dialogo === 'cancelar' ? 'Cancelar lançamento' : dialogo === 'estornar' ? 'Registrar estorno' : 'Voltar para previsto' }}
      </v-card-title>
      <v-card-text class="d-flex flex-column ga-3">
        <div class="text-body-2">
          <template v-if="dialogo === 'cancelar'">
            Use quando o lançamento <b>não deveria existir</b> (erro, duplicado). Ele deixa de contar nos totais, mas fica no histórico.
          </template>
          <template v-else-if="dialogo === 'estornar'">
            Use quando houve <b>devolução real</b> do dinheiro. Cria um lançamento negativo ligado a "{{ l.descricao }}".
          </template>
          <template v-else>O pagamento confirmado volta a ser um compromisso previsto.</template>
        </div>
        <template v-if="dialogo === 'estornar'">
          <CampoValor v-model="valorEstorno" label="Valor devolvido" />
          <v-text-field v-model="dataEstorno" type="date" :max="hojeIso()" label="Data" />
        </template>
        <v-text-field v-model="motivo" :label="dialogo === 'cancelar' ? 'Motivo (obrigatório)' : 'Motivo (opcional)'" autofocus />
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" @click="dialogo = null">Voltar</v-btn>
        <v-btn
          :color="dialogo === 'cancelar' ? 'error' : 'primary'"
          variant="flat"
          :loading="salvando"
          :disabled="dialogo === 'cancelar' && !motivo.trim()"
          @click="executar"
        >
          Confirmar
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.itens-nota { border-left: 3px solid rgba(var(--v-theme-primary), 0.5); padding-left: 10px; max-width: 640px; cursor: default; }
.item-nota + .item-nota { border-top: 1px dashed rgba(var(--v-border-color), 0.2); }
</style>
