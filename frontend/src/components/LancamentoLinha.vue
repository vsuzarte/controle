<script setup lang="ts">
import { computed, ref } from 'vue'
import { useDisplay } from 'vuetify'
import { api } from '../api'
import { alterado, confirmarLancamento, editarLancamento, erro, verHistorico } from '../estado'
import { brl, dataBr, hojeIso, nomeMes } from '../formato'
import type { Lancamento } from '../tipos'
import CampoValor from './CampoValor.vue'

const props = defineProps<{ l: Lancamento; mostrarData?: boolean }>()
const { xs } = useDisplay()

const dialogo = ref<'cancelar' | 'estornar' | 'reabrir' | null>(null)
const motivo = ref('')
const valorEstorno = ref<number | null>(null)
const dataEstorno = ref(hojeIso())
const salvando = ref(false)

const cancelado = computed(() => props.l.status === 'Cancelado')
const previsto = computed(() => props.l.status === 'Previsto')
// Compras no cartão são cobradas na fatura: não há "pagar" individual.
const noCartao = computed(() => !!props.l.faturaMes && props.l.tipo !== 'Transferencia')
const atrasado = computed(() => previsto.value && !noCartao.value && props.l.data < hojeIso())
const podeConfirmar = computed(() => previsto.value && props.l.tipo !== 'Transferencia' && !noCartao.value)
const icone = computed(() =>
  props.l.tipo === 'Transferencia' ? 'mdi-swap-horizontal' : props.l.categoriaIcone ?? (props.l.tipo === 'Receita' ? 'mdi-arrow-up' : 'mdi-arrow-down'),
)
const sinal = computed(() => (props.l.tipo === 'Receita' ? 1 : props.l.tipo === 'Despesa' ? -1 : 0))
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
      <v-icon v-if="l.regraId" size="14" class="text-medium-emphasis">{{ l.totalParcelas ? 'mdi-credit-card-clock-outline' : 'mdi-repeat' }}</v-icon>
    </v-list-item-title>
    <v-list-item-subtitle>{{ subtitulo }}</v-list-item-subtitle>
    <v-list-item-subtitle v-if="cancelado && l.motivoCancelamento" class="font-italic">{{ l.motivoCancelamento }}</v-list-item-subtitle>

    <template #append>
      <div class="d-flex align-center ga-2">
        <div class="d-flex flex-column align-end">
          <span class="valor" :class="corValor">{{ sinal < 0 ? '−' : sinal > 0 ? '+' : '' }}{{ brl(Math.abs(l.valor)) }}</span>
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
