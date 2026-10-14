<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, sessao } from '../api'
import { alterado, erro, estado, verHistorico } from '../estado'
import { brl } from '../formato'
import { TIPOS_CONTA, type Categoria, type Conta, type TipoCategoria, type TipoConta } from '../tipos'
import CampoValor from '../components/CampoValor.vue'

const aba = ref('contas')

const PALETA = ['#1E88E5', '#43A047', '#E53935', '#FB8C00', '#8E24AA', '#00ACC1', '#FDD835', '#6D4C41', '#D81B60', '#3949AB', '#546E7A', '#2E7D32']
const ICONES = [
  'mdi-cart', 'mdi-home', 'mdi-flash', 'mdi-water', 'mdi-wifi', 'mdi-car', 'mdi-gas-station', 'mdi-bus', 'mdi-medical-bag',
  'mdi-pill', 'mdi-silverware-fork-knife', 'mdi-food', 'mdi-gamepad-variant', 'mdi-tshirt-crew', 'mdi-play-box-multiple',
  'mdi-shopping', 'mdi-school', 'mdi-bank', 'mdi-hand-coin', 'mdi-paw', 'mdi-gift', 'mdi-airplane', 'mdi-dumbbell',
  'mdi-baby-carriage', 'mdi-tools', 'mdi-briefcase', 'mdi-beach', 'mdi-cash-plus', 'mdi-cash-refund', 'mdi-dots-horizontal',
]

// ---------- contas ----------
const contaDlg = ref(false)
const contaEdit = ref<Conta | null>(null)
const c = reactive({ nome: '', tipo: 'Corrente' as TipoConta, saldoInicial: 0 as number | null, cor: PALETA[0] as string | null, diaFechamento: null as number | null, diaVencimento: null as number | null, limite: null as number | null, ativa: true })

function abrirConta(conta?: Conta) {
  contaEdit.value = conta ?? null
  Object.assign(c, conta
    ? { nome: conta.nome, tipo: conta.tipo, saldoInicial: conta.saldoInicial, cor: conta.cor, diaFechamento: conta.diaFechamento, diaVencimento: conta.diaVencimento, limite: conta.limite, ativa: conta.ativa }
    : { nome: '', tipo: 'Corrente', saldoInicial: 0, cor: PALETA[0], diaFechamento: null, diaVencimento: null, limite: null, ativa: true })
  contaDlg.value = true
}

async function salvarConta() {
  try {
    const corpo = { ...c, saldoInicial: c.saldoInicial ?? 0 }
    if (contaEdit.value) await api.put(`/contas/${contaEdit.value.id}`, corpo)
    else await api.post('/contas', corpo)
    contaDlg.value = false
    alterado('Conta salva')
  } catch (e) {
    erro(e)
  }
}

// ---------- categorias ----------
const catDlg = ref(false)
const catEdit = ref<Categoria | null>(null)
const k = reactive({ nome: '', tipo: 'Despesa' as TipoCategoria, grupo: '' as string | null, cor: PALETA[0] as string | null, icone: 'mdi-tag' as string | null, ativa: true })
const verInativas = ref(false)

const grupos = computed(() => [...new Set(estado.categorias.map((x) => x.grupo).filter(Boolean))] as string[])
const categoriasPorTipo = computed(() =>
  (['Despesa', 'Receita'] as TipoCategoria[]).map((tipo) => ({
    tipo,
    itens: estado.categorias.filter((x) => x.tipo === tipo && (verInativas.value || x.ativa)),
  })),
)

function abrirCategoria(cat?: Categoria, tipo: TipoCategoria = 'Despesa') {
  catEdit.value = cat ?? null
  Object.assign(k, cat
    ? { nome: cat.nome, tipo: cat.tipo, grupo: cat.grupo, cor: cat.cor, icone: cat.icone, ativa: cat.ativa }
    : { nome: '', tipo, grupo: null, cor: PALETA[Math.floor(Math.random() * PALETA.length)], icone: 'mdi-tag', ativa: true })
  catDlg.value = true
}

async function salvarCategoria() {
  try {
    if (catEdit.value) await api.put(`/categorias/${catEdit.value.id}`, k)
    else await api.post('/categorias', k)
    catDlg.value = false
    alterado('Categoria salva')
  } catch (e) {
    erro(e)
  }
}

async function sair() {
  await api.post('/auth/logout')
  sessao.precisaLogin = true
}

const iconeConta = (t: TipoConta) => TIPOS_CONTA.find((x) => x.value === t)?.icone ?? 'mdi-bank'
</script>

<template>
  <div class="pagina">
    <v-tabs v-model="aba" color="primary" class="mb-3">
      <v-tab value="contas" prepend-icon="mdi-bank-outline">Contas e cartões</v-tab>
      <v-tab value="categorias" prepend-icon="mdi-tag-multiple-outline">Categorias</v-tab>
      <v-tab value="sistema" prepend-icon="mdi-information-outline">Sistema</v-tab>
    </v-tabs>

    <v-window v-model="aba">
      <v-window-item value="contas">
        <div class="d-flex mb-2"><v-spacer /><v-btn color="primary" prepend-icon="mdi-plus" @click="abrirConta()">Nova conta / cartão</v-btn></div>
        <v-card>
          <v-list>
            <v-list-item v-for="x in estado.contas" :key="x.id" :class="{ 'text-disabled': !x.ativa }" @click="abrirConta(x)">
              <template #prepend><v-avatar :color="x.cor ?? 'grey'" variant="tonal"><v-icon>{{ iconeConta(x.tipo) }}</v-icon></v-avatar></template>
              <v-list-item-title>{{ x.nome }} <v-chip v-if="!x.ativa" size="x-small">inativa</v-chip></v-list-item-title>
              <v-list-item-subtitle>
                {{ TIPOS_CONTA.find((t) => t.value === x.tipo)?.title }}
                <template v-if="x.tipo === 'CartaoCredito'"> · fecha dia {{ x.diaFechamento }}, vence dia {{ x.diaVencimento }}<template v-if="x.limite"> · limite {{ brl(x.limite) }}</template></template>
              </v-list-item-subtitle>
              <template #append>
                <span class="valor" :class="x.saldo < 0 ? 'text-error' : ''">{{ brl(x.saldo) }}</span>
                <v-btn icon="mdi-history" variant="text" size="small" @click.stop="verHistorico(x.id, x.nome)" />
              </template>
            </v-list-item>
          </v-list>
        </v-card>
        <div class="text-caption text-medium-emphasis mt-2">
          No cartão, o saldo negativo é o que você deve (compras lançadas menos pagamentos de fatura).
        </div>
      </v-window-item>

      <v-window-item value="categorias">
        <div class="d-flex mb-2 align-center">
          <v-switch v-model="verInativas" label="Mostrar inativas" hide-details density="compact" color="primary" />
          <v-spacer />
          <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirCategoria()">Nova categoria</v-btn>
        </div>
        <v-row dense>
          <v-col v-for="g in categoriasPorTipo" :key="g.tipo" cols="12" md="6">
            <v-card>
              <v-card-title class="text-subtitle-1 font-weight-bold">{{ g.tipo === 'Despesa' ? 'Despesas' : 'Receitas' }}</v-card-title>
              <v-list density="comfortable">
                <v-list-item v-for="x in g.itens" :key="x.id" :class="{ 'text-disabled': !x.ativa }" :subtitle="x.grupo ?? undefined" @click="abrirCategoria(x)">
                  <template #prepend><v-avatar :color="x.cor ?? 'grey'" variant="tonal" size="34"><v-icon size="18">{{ x.icone ?? 'mdi-tag' }}</v-icon></v-avatar></template>
                  <v-list-item-title>{{ x.nome }} <v-chip v-if="!x.ativa" size="x-small">inativa</v-chip></v-list-item-title>
                </v-list-item>
              </v-list>
            </v-card>
          </v-col>
        </v-row>
      </v-window-item>

      <v-window-item value="sistema">
        <v-card class="pa-4 mb-3">
          <div class="text-subtitle-1 font-weight-bold mb-2">Seus dados</div>
          <ul class="text-body-2 pl-4">
            <li>Ficam num único arquivo SQLite, na pasta <code>dados/</code> ao lado do docker-compose.</li>
            <li>Uma cópia é feita automaticamente todo dia em <code>backups/</code> (mantém os 30 últimos).</li>
            <li>Nada é apagado: cancelamentos, correções e remoções da previsão ficam no histórico.</li>
          </ul>
          <v-btn class="mt-3" variant="tonal" prepend-icon="mdi-history" @click="verHistorico('', 'Últimas alterações')">Ver últimas alterações</v-btn>
        </v-card>
        <v-card class="pa-4">
          <div class="text-subtitle-1 font-weight-bold mb-2">Atalhos</div>
          <div class="text-body-2">Tecla <kbd>N</kbd> abre um novo lançamento em qualquer tela.</div>
          <div class="text-body-2">No celular, use "Adicionar à tela inicial" no navegador para abrir como app.</div>
          <v-btn class="mt-3" variant="text" color="error" prepend-icon="mdi-logout" @click="sair">Sair</v-btn>
        </v-card>
      </v-window-item>
    </v-window>

    <v-dialog v-model="contaDlg" max-width="480">
      <v-card>
        <v-card-title class="pt-4">{{ contaEdit ? 'Editar conta' : 'Nova conta' }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="c.nome" label="Nome" placeholder="Ex.: Nubank, Itaú, Carteira" />
          <v-select v-model="c.tipo" :items="TIPOS_CONTA" label="Tipo" />
          <template v-if="c.tipo === 'CartaoCredito'">
            <div class="d-flex ga-3">
              <v-text-field v-model.number="c.diaFechamento" type="number" min="1" max="31" label="Dia do fechamento" />
              <v-text-field v-model.number="c.diaVencimento" type="number" min="1" max="31" label="Dia do vencimento" />
            </div>
            <div class="text-caption text-medium-emphasis">Compras a partir do dia do fechamento entram na fatura seguinte.</div>
            <CampoValor v-model="c.limite" label="Limite (opcional)" livre />
            <div class="text-caption text-medium-emphasis mt-n2">Mudou o limite no banco? Ajuste aqui ou em Cartões → Ajustar limite.</div>
          </template>
          <template v-else>
            <CampoValor v-model="c.saldoInicial" :label="c.tipo === 'Beneficio' ? 'Saldo atual do vale' : 'Saldo atual (ponto de partida)'" livre />
            <div v-if="c.tipo === 'Beneficio'" class="text-caption text-medium-emphasis mt-n2">
              O vale só serve para compras (mercado, restaurante...): não paga contas nem faturas e não entra no saldo disponível.
              Cadastre a recarga mensal em Contas do mês como uma entrada nesta conta.
            </div>
            <div v-else class="text-caption text-medium-emphasis mt-n2">Coloque o saldo de hoje no banco; daqui pra frente o sistema calcula.</div>
          </template>
          <div>
            <div class="text-caption mb-1">Cor</div>
            <div class="d-flex flex-wrap ga-1">
              <v-btn v-for="cor in PALETA" :key="cor" :color="cor" icon size="x-small" :variant="c.cor === cor ? 'elevated' : 'flat'" @click="c.cor = cor">
                <v-icon v-if="c.cor === cor" color="white">mdi-check</v-icon>
              </v-btn>
            </div>
          </div>
          <v-switch v-if="contaEdit" v-model="c.ativa" label="Ativa" color="primary" hide-details density="compact" />
        </v-card-text>
        <v-card-actions><v-spacer /><v-btn variant="text" @click="contaDlg = false">Voltar</v-btn><v-btn color="primary" variant="flat" @click="salvarConta">Salvar</v-btn></v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="catDlg" max-width="480" scrollable>
      <v-card>
        <v-card-title class="pt-4">{{ catEdit ? 'Editar categoria' : 'Nova categoria' }}</v-card-title>
        <v-card-text class="d-flex flex-column ga-3">
          <v-text-field v-model="k.nome" label="Nome" />
          <v-btn-toggle v-model="k.tipo" mandatory divided variant="outlined" color="primary">
            <v-btn value="Despesa">Despesa</v-btn>
            <v-btn value="Receita">Receita</v-btn>
          </v-btn-toggle>
          <v-combobox v-model="k.grupo" :items="grupos" label="Grupo (opcional)" clearable variant="outlined" density="comfortable" hide-details />
          <div>
            <div class="text-caption mb-1">Cor</div>
            <div class="d-flex flex-wrap ga-1">
              <v-btn v-for="cor in PALETA" :key="cor" :color="cor" icon size="x-small" :variant="k.cor === cor ? 'elevated' : 'flat'" @click="k.cor = cor">
                <v-icon v-if="k.cor === cor" color="white">mdi-check</v-icon>
              </v-btn>
            </div>
          </div>
          <div>
            <div class="text-caption mb-1">Ícone</div>
            <div class="d-flex flex-wrap ga-1">
              <v-btn v-for="ic in ICONES" :key="ic" :icon="ic" size="small" :variant="k.icone === ic ? 'tonal' : 'text'" :color="k.icone === ic ? k.cor ?? 'primary' : undefined" @click="k.icone = ic" />
            </div>
          </div>
          <v-switch v-if="catEdit" v-model="k.ativa" label="Ativa (inativas somem dos formulários, mas o histórico fica)" color="primary" hide-details density="compact" />
        </v-card-text>
        <v-card-actions><v-spacer /><v-btn variant="text" @click="catDlg = false">Voltar</v-btn><v-btn color="primary" variant="flat" @click="salvarCategoria">Salvar</v-btn></v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
