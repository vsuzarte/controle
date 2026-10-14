<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../api'
import { alterado, cartoes, erro, estado, pagarFatura, verHistorico } from '../estado'
import { brl, dataBr, nomeMes } from '../formato'
import type { Conta, Fatura, StatusFatura, TodosCartoes } from '../tipos'
import CampoValor from '../components/CampoValor.vue'
import LancamentoLinha from '../components/LancamentoLinha.vue'

const route = useRoute()
const router = useRouter()
/** "todos" = visão de todos os cartões juntos. */
const aba = ref<string>((route.query.cartao as string) ?? 'todos')
const todos = ref<TodosCartoes | null>(null)
const faturas = ref<Fatura[]>([])
const mes = ref<string | null>((route.query.mes as string) ?? null)
const fatura = ref<Fatura | null>(null)
const cartao = computed<Conta | undefined>(() => estado.contas.find((c) => c.id === aba.value))

async function carregar() {
  try {
    if (aba.value === 'todos') {
      todos.value = await api.get<TodosCartoes>('/cartoes/todos')
      router.replace({ query: {} })
      return
    }
    faturas.value = await api.get<Fatura[]>(`/cartoes/${aba.value}/faturas`)
    if (!mes.value || !faturas.value.some((f) => f.mes.startsWith(mes.value!))) {
      const atual = faturas.value.find((f) => f.status === 'Aberta') ?? faturas.value[0]
      mes.value = atual?.mes.slice(0, 7) ?? null
    }
    await carregarFatura()
  } catch (e) {
    erro(e)
  }
}

async function carregarFatura() {
  if (aba.value === 'todos' || !mes.value) { fatura.value = null; return }
  fatura.value = await api.get<Fatura>(`/cartoes/${aba.value}/faturas/${mes.value}`)
  router.replace({ query: { cartao: aba.value, mes: mes.value } })
}

onMounted(carregar)
watch(aba, () => { mes.value = null; carregar() })
watch(mes, (m, antigo) => { if (antigo && m) carregarFatura().catch(erro) })
watch(() => estado.versao, carregar)

const cores: Record<StatusFatura, string> = { Aberta: 'info', Fechada: 'warning', Paga: 'success', Vencida: 'error', Futura: 'grey' }
const ordenadas = computed(() => [...faturas.value].reverse())

function corLimite(usado: number, limite: number | null) {
  if (!limite) return 'primary'
  const pct = usado / limite
  return pct > 1 ? 'error' : pct >= 0.8 ? 'warning' : 'primary'
}
const pct = (usado: number, limite: number | null) => (limite ? Math.min(100, (usado / limite) * 100) : 0)

function pagar(f: Fatura) {
  pagarFatura({ cartaoId: f.cartaoId, cartao: f.cartao, mes: f.mes, vencimento: f.vencimento, restante: f.restante })
}

// ----- Ajuste de limite -----
const limiteDlg = ref(false)
const lim = reactive({ valor: null as number | null, motivo: '' })
const salvandoLimite = ref(false)
function abrirLimite() {
  Object.assign(lim, { valor: cartao.value?.limite ?? null, motivo: '' })
  limiteDlg.value = true
}
async function salvarLimite() {
  salvandoLimite.value = true
  try {
    await api.put(`/contas/${aba.value}/limite`, { limite: lim.valor, motivo: lim.motivo || null })
    limiteDlg.value = false
    alterado('Limite atualizado')
  } catch (e) {
    erro(e)
  } finally {
    salvandoLimite.value = false
  }
}
</script>

<template>
  <div class="pagina">
    <v-card v-if="!cartoes.length" class="pa-8 text-center">
      <v-icon size="48" class="mb-2">mdi-credit-card-plus-outline</v-icon>
      <div class="mb-3">Nenhum cartão cadastrado.</div>
      <v-btn color="primary" to="/cadastros">Cadastrar cartão</v-btn>
    </v-card>

    <template v-else>
      <v-tabs v-model="aba" color="primary" class="mb-4" show-arrows>
        <v-tab value="todos" prepend-icon="mdi-credit-card-multiple-outline">Todos</v-tab>
        <v-tab v-for="c in cartoes" :key="c.id" :value="c.id" prepend-icon="mdi-credit-card">{{ c.nome }}</v-tab>
      </v-tabs>

      <!-- ===== Todos os cartões ===== -->
      <template v-if="aba === 'todos' && todos">
        <v-card class="destaque destaque--indigo mb-4 pa-6" rounded="xl" data-testid="todos-cartoes">
          <div class="rotulo" style="opacity: 0.85">Faturas fechadas a pagar</div>
          <div class="grande" data-testid="total-a-pagar">{{ brl(todos.totalAPagar) }}</div>
          <v-row dense class="mt-4 text-body-2">
            <v-col cols="6" sm="4">
              <div style="opacity: 0.75">Em aberto (ainda recebendo compras)</div>
              <b class="valor" data-testid="total-em-aberto">{{ brl(todos.totalEmAberto) }}</b>
            </v-col>
            <v-col cols="6" sm="4">
              <div style="opacity: 0.75">Soma das duas</div>
              <b class="valor">{{ brl(todos.totalAPagar + todos.totalEmAberto) }}</b>
            </v-col>
            <v-col v-if="todos.limiteTotal" cols="12" sm="4">
              <div style="opacity: 0.75">Limite usado</div>
              <b class="valor">{{ brl(todos.limiteUsadoTotal) }}</b> de {{ brl(todos.limiteTotal) }}
            </v-col>
          </v-row>
        </v-card>

        <v-row>
          <v-col v-for="c in todos.cartoes" :key="c.id" cols="12" md="6">
            <v-card class="h-100" data-testid="cartao-resumo" :aria-label="c.nome">
              <v-card-item>
                <template #prepend>
                  <v-avatar :color="c.cor ?? 'secondary'" rounded="lg"><v-icon color="white">mdi-credit-card</v-icon></v-avatar>
                </template>
                <v-card-title class="font-weight-bold">{{ c.nome }}</v-card-title>
                <v-card-subtitle>Fecha dia {{ c.diaFechamento }} · vence dia {{ c.diaVencimento }}</v-card-subtitle>
                <template #append><v-btn variant="text" size="small" @click="aba = c.id">Ver fatura</v-btn></template>
              </v-card-item>
              <v-card-text class="pt-0">
                <div v-for="f in c.aPagar" :key="f.mes" class="d-flex align-center ga-2 py-2">
                  <v-chip :color="cores[f.status]" size="small" variant="tonal" label>{{ f.status }}</v-chip>
                  <div>
                    <div class="font-weight-medium text-capitalize">{{ nomeMes(f.mes.slice(0, 7)) }}</div>
                    <div class="text-caption text-medium-emphasis">vence {{ dataBr(f.vencimento) }}</div>
                  </div>
                  <v-spacer />
                  <span class="valor">{{ brl(f.restante) }}</span>
                  <v-btn size="small" color="primary" variant="tonal" @click="pagar(f)">Pagar</v-btn>
                </div>
                <div v-if="c.aberta" class="d-flex align-center ga-2 py-2">
                  <v-chip color="info" size="small" variant="tonal" label>Aberta</v-chip>
                  <div>
                    <div class="font-weight-medium text-capitalize">{{ nomeMes(c.aberta.mes.slice(0, 7)) }}</div>
                    <div class="text-caption text-medium-emphasis">fecha {{ dataBr(c.aberta.fechamento) }} · vence {{ dataBr(c.aberta.vencimento) }}</div>
                  </div>
                  <v-spacer />
                  <span class="valor">{{ brl(c.aberta.restante) }}</span>
                </div>
                <div v-if="c.limite" class="mt-3">
                  <div class="d-flex text-caption mb-1">
                    <span>Limite usado {{ brl(c.limiteUsado) }}</span><v-spacer />
                    <span :class="(c.limiteDisponivel ?? 0) < 0 ? 'text-error' : ''">Disponível {{ brl(c.limiteDisponivel ?? 0) }}</span>
                  </div>
                  <v-progress-linear :model-value="pct(c.limiteUsado, c.limite)" :color="corLimite(c.limiteUsado, c.limite)" height="8" rounded />
                </div>
              </v-card-text>
            </v-card>
          </v-col>
        </v-row>
      </template>

      <!-- ===== Um cartão ===== -->
      <template v-else-if="aba !== 'todos'">
        <v-card v-if="cartao" class="mb-4 pa-4" data-testid="limite">
          <div class="d-flex align-center flex-wrap ga-2">
            <div>
              <div class="rotulo">Limite</div>
              <div v-if="cartao.limite" class="text-body-1">
                <b class="valor" data-testid="limite-disponivel">{{ brl(cartao.limite - (cartao.limiteUsado ?? 0)) }}</b> disponível
                <span class="text-medium-emphasis">· usado {{ brl(cartao.limiteUsado ?? 0) }} de {{ brl(cartao.limite) }}</span>
              </div>
              <div v-else class="text-medium-emphasis">Sem limite cadastrado</div>
            </div>
            <v-spacer />
            <v-btn variant="tonal" prepend-icon="mdi-tune-variant" @click="abrirLimite">Ajustar limite</v-btn>
            <v-btn variant="text" icon="mdi-history" aria-label="Histórico do cartão" @click="verHistorico(cartao.id, cartao.nome)" />
          </div>
          <v-progress-linear v-if="cartao.limite" class="mt-3" height="8" rounded
            :model-value="pct(cartao.limiteUsado ?? 0, cartao.limite)" :color="corLimite(cartao.limiteUsado ?? 0, cartao.limite)" />
          <div v-if="cartao.limite" class="text-caption text-medium-emphasis mt-2">
            Conta como o banco: compras não pagas e o total das parcelas futuras. Pagar a fatura libera o limite.
          </div>
        </v-card>

        <v-slide-group v-model="mes" show-arrows class="mb-3" mandatory>
          <v-slide-group-item v-for="f in ordenadas" :key="f.mes" v-slot="{ isSelected, toggle }" :value="f.mes.slice(0, 7)">
            <v-card :variant="isSelected ? 'tonal' : 'outlined'" :color="isSelected ? 'primary' : undefined" class="ma-1 pa-2 text-center" width="110" @click="toggle">
              <div class="text-caption text-capitalize">{{ nomeMes(f.mes.slice(0, 7), true) }}</div>
              <div class="valor text-body-2">{{ brl(f.total) }}</div>
              <v-chip :color="cores[f.status]" size="x-small" variant="flat" class="mt-1">{{ f.status }}</v-chip>
            </v-card>
          </v-slide-group-item>
        </v-slide-group>

        <v-card v-if="fatura" class="destaque destaque--indigo mb-4 pa-6" rounded="xl" data-testid="fatura">
          <div class="d-flex align-center">
            <v-icon size="28">mdi-integrated-circuit-chip</v-icon>
            <v-spacer />
            <v-chip :color="cores[fatura.status]" size="small" variant="flat" label>{{ fatura.status }}</v-chip>
          </div>
          <div class="rotulo mt-5" style="opacity: 0.85">Fatura de {{ nomeMes(fatura.mes.slice(0, 7)) }}</div>
          <div class="grande" data-testid="fatura-total">{{ brl(fatura.total) }}</div>
          <v-row dense class="mt-4 text-body-2">
            <v-col cols="6" sm="3"><div style="opacity: 0.75">Fecha</div><b>{{ dataBr(fatura.fechamento) }}</b></v-col>
            <v-col cols="6" sm="3"><div style="opacity: 0.75">Vence</div><b>{{ dataBr(fatura.vencimento) }}</b></v-col>
            <v-col cols="6" sm="3"><div style="opacity: 0.75">Pago</div><b class="valor">{{ brl(fatura.pago) }}</b></v-col>
            <v-col cols="6" sm="3"><div style="opacity: 0.75">Falta</div><b class="valor" data-testid="fatura-restante">{{ brl(fatura.restante) }}</b></v-col>
          </v-row>
          <v-btn v-if="fatura.restante > 0" color="white" variant="flat" class="mt-4 text-indigo-darken-3" prepend-icon="mdi-cash-check" @click="pagar(fatura)">
            Pagar fatura
          </v-btn>
        </v-card>

        <v-card v-if="fatura">
          <v-card-title class="text-subtitle-1 font-weight-bold">Compras nesta fatura</v-card-title>
          <v-list class="py-0">
            <div v-if="!fatura.lancamentos.length" class="pa-4 text-medium-emphasis">Nenhuma compra nesta fatura.</div>
            <LancamentoLinha v-for="l in fatura.lancamentos" :key="l.id" :l="l" mostrar-data />
          </v-list>
          <template v-if="fatura.pagamentos.length">
            <v-divider />
            <v-card-title class="text-subtitle-2">Pagamentos</v-card-title>
            <v-list class="py-0">
              <LancamentoLinha v-for="l in fatura.pagamentos" :key="l.id" :l="l" mostrar-data />
            </v-list>
          </template>
        </v-card>
      </template>
    </template>

    <v-dialog v-model="limiteDlg" max-width="420">
      <v-card>
        <v-card-title class="pt-4">Ajustar limite</v-card-title>
        <v-card-subtitle>{{ cartao?.nome }}</v-card-subtitle>
        <v-card-text class="d-flex flex-column ga-3 pt-4">
          <CampoValor v-model="lim.valor" label="Novo limite" livre autofocus />
          <v-text-field v-model="lim.motivo" label="Motivo (opcional)" placeholder="Ex.: banco aumentou o limite" />
          <div class="text-caption text-medium-emphasis">Cada ajuste fica no histórico do cartão. Deixe vazio para ficar sem limite.</div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="limiteDlg = false">Voltar</v-btn>
          <v-btn color="primary" variant="flat" :loading="salvandoLimite" @click="salvarLimite">Salvar limite</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
