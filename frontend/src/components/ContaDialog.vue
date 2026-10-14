<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { api } from '../api'
import { PALETA } from '../cores'
import { alterado, erro } from '../estado'
import { brl, dataBr } from '../formato'
import { quandoCai } from '../regras'
import { TIPOS_CONTA, VENCIMENTOS, type Conta, type RegraVencimento, type TipoConta } from '../tipos'
import CampoValor from './CampoValor.vue'

/**
 * Nova conta / editar conta. Usado em Ajustes e em Cartões ("+ Cartão").
 * Vale: também cadastra a recarga automática. "Excluir" desativa (o histórico fica).
 */
const aberto = defineModel<boolean>({ required: true })
const props = withDefaults(defineProps<{ conta?: Conta | null; tipo?: TipoConta }>(), { conta: null, tipo: 'Corrente' })

const c = reactive({
  nome: '', tipo: 'Corrente' as TipoConta, saldoInicial: 0 as number | null, cor: PALETA[0] as string | null,
  diaFechamento: null as number | null, diaVencimento: null as number | null, limite: null as number | null, ativa: true,
})
const recarga = reactive({ valor: null as number | null, vencimento: 'DiaFixo' as RegraVencimento, diaUtilNumero: 1 as number | null, dia: null as number | null })
const excluindo = ref(false)
const motivo = ref('')
const salvando = ref(false)

const vale = computed(() => c.tipo === 'Beneficio')
const recargaMudou = computed(() => {
  const r = props.conta?.recarga
  if (!r) return !!recarga.valor
  return recarga.valor !== r.valor || recarga.vencimento !== r.vencimento
    || (recarga.vencimento === 'DiaUtil' && recarga.diaUtilNumero !== r.diaUtilNumero)
    || (recarga.vencimento === 'DiaFixo' && recarga.dia !== r.dia)
})

watch(aberto, (sim) => {
  if (!sim) return
  const x = props.conta
  excluindo.value = false
  motivo.value = ''
  Object.assign(c, x
    ? { nome: x.nome, tipo: x.tipo, saldoInicial: x.saldoInicial, cor: x.cor, diaFechamento: x.diaFechamento, diaVencimento: x.diaVencimento, limite: x.limite, ativa: x.ativa }
    : { nome: '', tipo: props.tipo, saldoInicial: 0, cor: PALETA[0], diaFechamento: null, diaVencimento: null, limite: null, ativa: true })
  const r = x?.recarga
  Object.assign(recarga, r
    ? { valor: r.valor, vencimento: r.vencimento, diaUtilNumero: r.diaUtilNumero ?? 1, dia: r.dia }
    : { valor: null, vencimento: 'DiaFixo', diaUtilNumero: 1, dia: null })
})

async function salvar() {
  salvando.value = true
  try {
    const corpo = { ...c, saldoInicial: c.saldoInicial ?? 0 }
    const salva = props.conta
      ? await api.put<Conta>(`/contas/${props.conta.id}`, corpo)
      : await api.post<Conta>('/contas', corpo)
    if (vale.value && recargaMudou.value) {
      await api.put(`/contas/${salva.id}/recarga`, {
        valor: recarga.valor || null,
        vencimento: recarga.vencimento,
        diaUtilNumero: recarga.vencimento === 'DiaUtil' ? recarga.diaUtilNumero : null,
        dia: recarga.vencimento === 'DiaFixo' ? recarga.dia : null,
      })
    }
    aberto.value = false
    alterado('Conta salva')
  } catch (e) {
    erro(e)
    alterado() // a conta pode ter sido salva mesmo se a recarga falhou
  } finally {
    salvando.value = false
  }
}

async function excluir() {
  try {
    await api.post(`/contas/${props.conta!.id}/desativar`, { motivo: motivo.value || null })
    aberto.value = false
    alterado('Conta excluída (desativada). O histórico continua guardado.')
  } catch (e) {
    erro(e)
  }
}

async function reativar() {
  try {
    await api.post(`/contas/${props.conta!.id}/reativar`)
    aberto.value = false
    alterado('Conta reativada')
  } catch (e) {
    erro(e)
  }
}
</script>

<template>
  <v-dialog v-model="aberto" max-width="500" scrollable>
    <v-card>
      <v-card-title class="pt-4">{{ conta ? 'Editar conta' : c.tipo === 'CartaoCredito' ? 'Novo cartão' : 'Nova conta' }}</v-card-title>
      <v-card-text class="d-flex flex-column ga-3">
        <v-alert v-if="conta && !conta.ativa" type="info" variant="tonal" density="compact">
          Conta excluída (desativada): não aparece nas listas nem nos formulários.
        </v-alert>
        <v-text-field v-model="c.nome" label="Nome" placeholder="Ex.: Nubank, Itaú, Carteira" />
        <v-select v-model="c.tipo" :items="TIPOS_CONTA" label="Tipo" />

        <template v-if="c.tipo === 'CartaoCredito'">
          <div class="d-flex ga-3">
            <v-text-field v-model.number="c.diaFechamento" type="number" min="1" max="31" label="Dia do fechamento" />
            <v-text-field v-model.number="c.diaVencimento" type="number" min="1" max="31" label="Dia do vencimento" />
          </div>
          <div class="text-caption text-medium-emphasis">Compras a partir do dia do fechamento entram na fatura seguinte.</div>
          <CampoValor v-model="c.limite" label="Limite (opcional)" livre />
        </template>
        <template v-else>
          <CampoValor v-model="c.saldoInicial" :label="vale ? 'Saldo atual do vale' : 'Saldo atual (ponto de partida)'" livre />
          <div class="text-caption text-medium-emphasis mt-n2">
            {{ vale
              ? 'O vale só serve para compras (mercado, restaurante...): não paga contas nem faturas e não entra no saldo disponível.'
              : 'Coloque o saldo de hoje no banco; daqui pra frente o sistema calcula.' }}
          </div>
        </template>

        <!-- Recarga do vale: entra sozinha na data -->
        <v-card v-if="vale" variant="tonal" color="warning" class="pa-3" data-testid="recarga">
          <div class="d-flex align-center ga-2 mb-2">
            <v-icon size="18">mdi-calendar-sync</v-icon>
            <span class="font-weight-bold">Recarga automática</span>
          </div>
          <CampoValor v-model="recarga.valor" label="Valor que cai todo mês" livre />
          <div class="d-flex ga-3 mt-2">
            <v-select v-model="recarga.vencimento" :items="VENCIMENTOS" label="Quando cai" density="comfortable" hide-details />
            <v-text-field v-if="recarga.vencimento === 'DiaUtil'" v-model.number="recarga.diaUtilNumero" type="number" min="1" max="23"
              label="Qual dia útil" density="comfortable" hide-details style="max-width: 130px" />
            <v-text-field v-if="recarga.vencimento === 'DiaFixo'" v-model.number="recarga.dia" type="number" min="1" max="31"
              label="Dia" density="comfortable" hide-details style="max-width: 100px" />
          </div>
          <div class="text-caption mt-2">
            <template v-if="recarga.valor">
              {{ brl(recarga.valor) }} {{ quandoCai(recarga.vencimento, recarga.diaUtilNumero, recarga.dia) }}, somado ao saldo sozinho.
              <template v-if="conta?.recarga?.proxima && !recargaMudou"> Próxima: {{ dataBr(conta.recarga.proxima) }}.</template>
            </template>
            <template v-else-if="conta?.recarga">Sem valor = a recarga para (as já recebidas ficam no histórico).</template>
            <template v-else>Deixe em branco se não tiver recarga fixa.</template>
          </div>
        </v-card>

        <div>
          <div class="text-caption mb-1">Cor</div>
          <div class="d-flex flex-wrap ga-1">
            <v-btn v-for="cor in PALETA" :key="cor" :color="cor" icon size="x-small" :variant="c.cor === cor ? 'elevated' : 'flat'"
              :aria-label="'Cor ' + cor" @click="c.cor = cor">
              <v-icon v-if="c.cor === cor" color="white">mdi-check</v-icon>
            </v-btn>
          </div>
        </div>

        <v-expand-transition>
          <v-alert v-if="excluindo" type="warning" variant="tonal" density="compact" data-testid="confirmar-exclusao">
            <div class="mb-2">A conta some das listas e dos formulários, mas nada é apagado: o histórico continua e dá para reativar.</div>
            <v-text-field v-model="motivo" label="Motivo (opcional)" density="compact" hide-details bg-color="surface" />
            <div class="d-flex justify-end ga-2 mt-2">
              <v-btn variant="text" size="small" @click="excluindo = false">Não</v-btn>
              <v-btn color="error" variant="flat" size="small" @click="excluir">Excluir conta</v-btn>
            </div>
          </v-alert>
        </v-expand-transition>
      </v-card-text>
      <v-card-actions>
        <v-btn v-if="conta?.ativa && !excluindo" color="error" variant="text" prepend-icon="mdi-delete-outline" @click="excluindo = true">Excluir</v-btn>
        <v-btn v-if="conta && !conta.ativa" color="primary" variant="text" prepend-icon="mdi-restore" @click="reativar">Reativar</v-btn>
        <v-spacer />
        <v-btn variant="text" @click="aberto = false">Voltar</v-btn>
        <v-btn color="primary" variant="flat" :loading="salvando" @click="salvar">Salvar</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
