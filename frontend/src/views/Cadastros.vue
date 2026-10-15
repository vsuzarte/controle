<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { api, sessao } from '../api'
import { PALETA } from '../cores'
import { alterado, erro, estado, verHistorico } from '../estado'
import { brl, dataBr } from '../formato'
import { quandoCai } from '../regras'
import { TIPOS_CONTA, type Categoria, type Conta, type TipoCategoria, type TipoConta } from '../tipos'
import ContaDialog from '../components/ContaDialog.vue'

const aba = ref('contas')

const ICONES = [
  'mdi-cart', 'mdi-home', 'mdi-flash', 'mdi-water', 'mdi-wifi', 'mdi-car', 'mdi-gas-station', 'mdi-bus', 'mdi-medical-bag',
  'mdi-pill', 'mdi-silverware-fork-knife', 'mdi-food', 'mdi-gamepad-variant', 'mdi-tshirt-crew', 'mdi-play-box-multiple',
  'mdi-shopping', 'mdi-school', 'mdi-bank', 'mdi-hand-coin', 'mdi-paw', 'mdi-gift', 'mdi-airplane', 'mdi-dumbbell',
  'mdi-baby-carriage', 'mdi-tools', 'mdi-briefcase', 'mdi-beach', 'mdi-cash-plus', 'mdi-cash-refund', 'mdi-dots-horizontal',
]

// ---------- contas ----------
const contaDlg = ref(false)
const contaEdit = ref<Conta | null>(null)
const verContasInativas = ref(false)
const contasVisiveis = computed(() => estado.contas.filter((x) => x.ativa || verContasInativas.value))
const contasInativas = computed(() => estado.contas.filter((x) => !x.ativa).length)

function abrirConta(conta?: Conta) {
  contaEdit.value = conta ?? null
  contaDlg.value = true
}

const tipoConta = (t: TipoConta) => TIPOS_CONTA.find((x) => x.value === t)
const usoLimite = (x: Conta) => (x.limite ? Math.min(100, ((x.limiteUsado ?? 0) / x.limite) * 100) : 0)

// ---------- categorias ----------
const catDlg = ref(false)
const catEdit = ref<Categoria | null>(null)
const k = reactive({ nome: '', tipo: 'Despesa' as TipoCategoria, grupo: '' as string | null, cor: PALETA[0] as string | null, icone: 'mdi-tag' as string | null, ativa: true })
const verInativas = ref(false)
const categoriasInativas = computed(() => estado.categorias.filter((x) => !x.ativa).length)

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
</script>

<template>
  <div class="pagina">
    <v-tabs v-model="aba" color="primary" class="mb-3">
      <v-tab value="contas" prepend-icon="mdi-bank-outline">Contas e cartões</v-tab>
      <v-tab value="categorias" prepend-icon="mdi-tag-multiple-outline">Categorias</v-tab>
      <v-tab value="sistema" prepend-icon="mdi-information-outline">Sistema</v-tab>
    </v-tabs>

    <v-window v-model="aba">
      <!-- ===== Contas em cards ===== -->
      <v-window-item value="contas">
        <div class="d-flex flex-wrap align-center ga-2 mb-3">
          <v-btn v-if="contasInativas" variant="tonal" size="small" :prepend-icon="verContasInativas ? 'mdi-eye-off-outline' : 'mdi-eye-outline'"
            @click="verContasInativas = !verContasInativas">
            {{ verContasInativas ? 'Esconder excluídas' : `Mostrar excluídas (${contasInativas})` }}
          </v-btn>
          <v-spacer />
          <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirConta()">Nova conta / cartão</v-btn>
        </div>
        <v-row dense>
          <v-col v-for="x in contasVisiveis" :key="x.id" cols="12" sm="6" lg="4" xl="3">
            <v-card class="h-100 d-flex flex-column" :class="{ inativa: !x.ativa }" data-testid="card-conta-cadastro" :aria-label="x.nome" @click="abrirConta(x)">
              <div class="d-flex align-center ga-3 pa-4 pb-2">
                <v-avatar :color="x.cor ?? 'grey'" variant="tonal" rounded="lg"><v-icon>{{ tipoConta(x.tipo)?.icone ?? 'mdi-bank' }}</v-icon></v-avatar>
                <div class="flex-grow-1" style="min-width: 0">
                  <div class="font-weight-bold text-truncate">{{ x.nome }}</div>
                  <div class="text-caption text-medium-emphasis">{{ tipoConta(x.tipo)?.title }}</div>
                </div>
                <v-chip v-if="!x.ativa" size="x-small" label>excluída</v-chip>
                <v-btn icon="mdi-text-box-outline" variant="text" size="small" title="Extrato" :aria-label="'Extrato de ' + x.nome" :to="{ path: '/extrato', query: { conta: x.id } }" @click.stop />
                <v-btn icon="mdi-history" variant="text" size="small" :aria-label="'Histórico de ' + x.nome" @click.stop="verHistorico(x.id, x.nome)" />
              </div>
              <div class="px-4 pb-4 mt-auto">
                <template v-if="x.tipo === 'CartaoCredito'">
                  <div class="text-caption text-medium-emphasis">Fecha dia {{ x.diaFechamento }} · vence dia {{ x.diaVencimento }}</div>
                  <template v-if="x.limite">
                    <v-progress-linear :model-value="usoLimite(x)" :color="usoLimite(x) > 85 ? 'error' : 'primary'" rounded height="6" class="my-2" />
                    <div class="d-flex justify-space-between text-caption">
                      <span>Usado {{ brl(x.limiteUsado ?? 0) }}</span><span>Livre {{ brl(x.limite - (x.limiteUsado ?? 0)) }}</span>
                    </div>
                  </template>
                  <div v-else class="text-caption text-medium-emphasis mt-1">Sem limite cadastrado</div>
                </template>
                <template v-else>
                  <div class="medio" :class="x.saldo < 0 ? 'text-error' : ''">{{ brl(x.saldo) }}</div>
                  <div v-if="x.tipo === 'Beneficio'" class="text-caption text-medium-emphasis" data-testid="recarga-card">
                    <template v-if="x.recarga">
                      Recarga {{ brl(x.recarga.valor) }} {{ quandoCai(x.recarga.vencimento, x.recarga.diaUtilNumero, x.recarga.dia) }}
                      <template v-if="x.recarga.proxima"> · próxima {{ dataBr(x.recarga.proxima).slice(0, 5) }}</template>
                    </template>
                    <template v-else>Sem recarga automática · toque para cadastrar</template>
                  </div>
                </template>
              </div>
            </v-card>
          </v-col>
        </v-row>
      </v-window-item>

      <!-- ===== Categorias em cards pequenos ===== -->
      <v-window-item value="categorias">
        <div class="d-flex flex-wrap align-center ga-2 mb-3">
          <v-btn v-if="categoriasInativas" variant="tonal" size="small" :prepend-icon="verInativas ? 'mdi-eye-off-outline' : 'mdi-eye-outline'"
            @click="verInativas = !verInativas">
            {{ verInativas ? 'Esconder inativas' : `Mostrar inativas (${categoriasInativas})` }}
          </v-btn>
          <v-spacer />
          <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirCategoria()">Nova categoria</v-btn>
        </div>
        <div v-for="g in categoriasPorTipo" :key="g.tipo" class="mb-5">
          <div class="d-flex align-center mb-2">
            <div class="rotulo">{{ g.tipo === 'Despesa' ? 'Despesas' : 'Receitas' }} · {{ g.itens.length }}</div>
            <v-spacer />
            <v-btn variant="text" size="small" prepend-icon="mdi-plus" @click="abrirCategoria(undefined, g.tipo)">
              {{ g.tipo === 'Despesa' ? 'Despesa' : 'Receita' }}
            </v-btn>
          </div>
          <v-row dense>
            <v-col v-for="x in g.itens" :key="x.id" cols="6" sm="4" md="3" xl="2">
              <v-card class="pa-3 d-flex align-center ga-2 h-100" :class="{ inativa: !x.ativa }" data-testid="card-categoria" :aria-label="x.nome" @click="abrirCategoria(x)">
                <v-avatar :color="x.cor ?? 'grey'" variant="tonal" size="36" rounded="lg"><v-icon size="20">{{ x.icone ?? 'mdi-tag' }}</v-icon></v-avatar>
                <div style="min-width: 0">
                  <div class="text-body-2 font-weight-medium text-truncate">{{ x.nome }}</div>
                  <div class="text-caption text-medium-emphasis text-truncate">{{ x.ativa ? x.grupo ?? '' : 'inativa' }}</div>
                </div>
              </v-card>
            </v-col>
          </v-row>
        </div>
      </v-window-item>

      <v-window-item value="sistema">
        <v-card class="pa-4 mb-3">
          <div class="text-subtitle-1 font-weight-bold mb-2">Seus dados</div>
          <ul class="text-body-2 pl-4">
            <li>Ficam num único arquivo SQLite, na pasta <code>dados/</code> ao lado do docker-compose.</li>
            <li>Uma cópia é feita automaticamente todo dia em <code>backups/</code> (mantém os 30 últimos).</li>
            <li>Nada é apagado: cancelamentos, correções, exclusões e remoções da previsão ficam no histórico.</li>
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

    <ContaDialog v-model="contaDlg" :conta="contaEdit" />

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

<style scoped>
.inativa { opacity: 0.55; }
</style>
