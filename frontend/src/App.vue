<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useDisplay } from 'vuetify'
import { api, sessao } from './api'
import { carregarCadastros, erro, estado, novoLancamento } from './estado'
import { definirHoje, mesIso } from './formato'
import { TEMAS, preferenciaTema, proximoTema } from './tema'
import LancamentoForm from './components/LancamentoForm.vue'
import ConfirmarDialog from './components/ConfirmarDialog.vue'
import HistoricoDialog from './components/HistoricoDialog.vue'
import CompraDialog from './components/CompraDialog.vue'
import MoverParcelasDialog from './components/MoverParcelasDialog.vue'
import NotaFiscalDialog from './components/NotaFiscalDialog.vue'
import PagarFaturaDialog from './components/PagarFaturaDialog.vue'

const { mdAndUp, height } = useDisplay()
/** Tela baixa (notebook, janela reduzida): menu mais compacto para caber sem barra de rolagem. */
const menuCompacto = computed(() => height.value < 820)
const route = useRoute()
const pronto = ref(false)
const senha = ref('')
const entrando = ref(false)
const erroLogin = ref('')
const mais = ref(false)
/** Menu lateral só com ícones (quem já conhece o sistema). Lembrado neste aparelho. */
const recolhido = ref((() => { try { return localStorage.getItem('fin.menu.recolhido') === '1' } catch { return false } })())
watch(recolhido, (v) => { try { localStorage.setItem('fin.menu.recolhido', v ? '1' : '0') } catch { /* sem storage */ } })

const menu = [
  { to: '/', titulo: 'Início', icone: 'mdi-home-variant-outline', iconeAtivo: 'mdi-home-variant' },
  { to: '/lancamentos', titulo: 'Lançamentos', icone: 'mdi-swap-vertical', iconeAtivo: 'mdi-swap-vertical-bold' },
  { to: '/extrato', titulo: 'Extrato', icone: 'mdi-text-box-outline', iconeAtivo: 'mdi-text-box' },
  { to: '/contas-do-mes', titulo: 'Contas do mês', icone: 'mdi-calendar-blank-outline', iconeAtivo: 'mdi-calendar-blank' },
  { to: '/previsao', titulo: 'Previsão', icone: 'mdi-calendar-clock-outline', iconeAtivo: 'mdi-calendar-clock' },
  { to: '/planejamento', titulo: 'Planejamento', icone: 'mdi-chart-donut', iconeAtivo: 'mdi-chart-donut' },
  { to: '/cartoes', titulo: 'Cartões', icone: 'mdi-credit-card-outline', iconeAtivo: 'mdi-credit-card' },
  { to: '/relatorios', titulo: 'Relatórios', icone: 'mdi-chart-line', iconeAtivo: 'mdi-chart-line' },
  { to: '/cadastros', titulo: 'Ajustes', icone: 'mdi-cog-outline', iconeAtivo: 'mdi-cog' },
]
// Celular: Início e Lançamentos à esquerda do +, Contas do mês à direita; o resto em "Mais".
const naBarra = ['/', '/lancamentos', '/contas-do-mes']
const barraEsquerda = menu.filter((m) => naBarra.slice(0, 2).includes(m.to))
const barraDireita = menu.filter((m) => m.to === naBarra[2])
const menuMais = menu.filter((m) => !naBarra.includes(m.to))
const maisAtivo = computed(() => menuMais.some((m) => m.to === route.path))
const ativo = (to: string) => route.path === to

async function iniciar() {
  const st = await api.get<{ exigeLogin: boolean; autenticado: boolean; hoje: string }>('/auth/status')
  definirHoje(st.hoje)
  estado.mes = mesIso(st.hoje)
  sessao.precisaLogin = !st.autenticado
  if (st.autenticado) {
    await carregarCadastros()
    pronto.value = true
  }
}

async function entrar() {
  entrando.value = true
  erroLogin.value = ''
  try {
    await api.post('/auth/login', { senha: senha.value })
    senha.value = ''
    await iniciar()
  } catch (e) {
    erroLogin.value = (e as Error).message
  } finally {
    entrando.value = false
  }
}

watch(() => sessao.precisaLogin, (v) => { if (v) pronto.value = false })

// Atalho: "N" abre um novo lançamento em qualquer tela (fora de campos de texto).
function atalho(e: KeyboardEvent) {
  const alvo = e.target as HTMLElement
  if (['INPUT', 'TEXTAREA', 'SELECT'].includes(alvo.tagName) || alvo.isContentEditable) return
  if (e.key.toLowerCase() === 'n' && !e.ctrlKey && !e.metaKey && !e.altKey && pronto.value && !estado.formulario.aberto) {
    e.preventDefault()
    novoLancamento()
  }
}

onMounted(() => {
  iniciar().catch(erro)
  window.addEventListener('keydown', atalho)
})
</script>

<template>
  <v-app>
    <!-- ===== Login ===== -->
    <v-main v-if="sessao.precisaLogin">
      <v-container class="fill-height" style="max-width: 420px">
        <div class="w-100">
          <v-card class="destaque pa-8 mb-6" rounded="xl">
            <v-icon size="40">mdi-chart-timeline-variant-shimmer</v-icon>
            <div class="text-h5 font-weight-bold mt-4">Finanças</div>
            <div style="opacity: 0.8">Seu dinheiro, sob controle.</div>
          </v-card>
          <v-form @submit.prevent="entrar">
            <v-text-field v-model="senha" label="Senha" type="password" autofocus :error-messages="erroLogin" prepend-inner-icon="mdi-lock-outline" />
            <v-btn type="submit" color="primary" variant="flat" block size="x-large" class="mt-4" :loading="entrando">Entrar</v-btn>
          </v-form>
        </div>
      </v-container>
    </v-main>

    <template v-else>
      <!-- ===== Computador: barra lateral ===== -->
      <v-navigation-drawer v-if="mdAndUp" permanent :rail="recolhido" width="240" rail-width="76" color="background" border="0">
        <!-- Logo, ícones do menu e botões de baixo na mesma coluna (aberto: x=32; recolhido: centro do trilho). -->
        <v-list-item :class="menuCompacto ? 'py-3 logo' : 'py-6 logo'" :style="{ paddingInline: recolhido ? '18px' : '12px' }">
          <template #prepend>
            <v-avatar class="destaque" rounded="lg" size="40"><v-icon color="white">mdi-chart-timeline-variant-shimmer</v-icon></v-avatar>
          </template>
          <v-list-item-title class="text-h6 font-weight-bold">Finanças</v-list-item-title>
        </v-list-item>
        <v-list nav color="primary" class="px-3 py-0" bg-color="transparent" :density="menuCompacto ? 'compact' : 'default'">
          <v-tooltip v-for="m in menu" :key="m.to" :text="m.titulo" location="end" :disabled="!recolhido">
            <template #activator="{ props: dica }">
              <v-list-item
                v-bind="dica"
                :to="m.to"
                :prepend-icon="ativo(m.to) ? m.iconeAtivo : m.icone"
                :title="m.titulo"
                :aria-label="m.titulo"
                rounded="lg"
                :active="ativo(m.to)"
                :class="menuCompacto ? 'mb-0' : 'mb-1'"
              />
            </template>
          </v-tooltip>
        </v-list>
        <template #append>
          <div :class="recolhido ? (menuCompacto ? 'pa-2 d-flex flex-column align-center ga-1' : 'pa-3 d-flex flex-column align-center ga-2') : (menuCompacto ? 'pa-3' : 'pa-4')">
            <v-tooltip v-if="recolhido" text="Lançar (N)" location="end">
              <template #activator="{ props: dica }">
                <v-btn v-bind="dica" icon="mdi-plus" color="primary" variant="flat" aria-label="Lançar" @click="novoLancamento()" />
              </template>
            </v-tooltip>
            <template v-else>
              <v-btn block color="primary" variant="flat" :size="menuCompacto ? 'large' : 'x-large'" prepend-icon="mdi-plus" @click="novoLancamento()">Lançar</v-btn>
              <div v-if="!menuCompacto" class="text-caption text-center text-medium-emphasis mt-2">ou aperte <v-chip size="x-small" label>N</v-chip></div>
            </template>
            <div :class="recolhido ? 'd-flex flex-column align-center ga-1' : 'd-flex justify-center ga-2 mt-2'">
              <v-tooltip :text="TEMAS[preferenciaTema].titulo + ' (clique para trocar)'" location="end">
                <template #activator="{ props: dica }">
                  <v-btn v-bind="dica" :icon="TEMAS[preferenciaTema].icone" variant="text" size="small"
                    :aria-label="TEMAS[preferenciaTema].titulo" data-testid="trocar-tema" @click="proximoTema" />
                </template>
              </v-tooltip>
              <v-tooltip :text="recolhido ? 'Abrir menu' : 'Recolher menu'" location="end">
                <template #activator="{ props: dica }">
                  <v-btn v-bind="dica" :icon="recolhido ? 'mdi-chevron-double-right' : 'mdi-chevron-double-left'" variant="text" size="small"
                    :aria-label="recolhido ? 'Abrir menu' : 'Recolher menu'" @click="recolhido = !recolhido" />
                </template>
              </v-tooltip>
            </div>
          </div>
        </template>
      </v-navigation-drawer>

      <v-main>
        <router-view v-if="pronto" v-slot="{ Component }">
          <transition name="pagina" mode="out-in">
            <component :is="Component" :key="route.path" />
          </transition>
        </router-view>
        <div v-else class="d-flex justify-center pa-16"><v-progress-circular indeterminate color="primary" /></div>
      </v-main>

      <!-- ===== Celular: barra inferior ===== -->
      <v-bottom-navigation v-if="!mdAndUp" grow color="primary" height="68" elevation="8" aria-label="Navegação">
        <v-btn v-for="m in barraEsquerda" :key="m.to" :to="m.to" :active="ativo(m.to)">
          <v-icon>{{ ativo(m.to) ? m.iconeAtivo : m.icone }}</v-icon><span>{{ m.titulo }}</span>
        </v-btn>
        <v-btn class="botao-mais" aria-label="Novo lançamento" @click="novoLancamento()"><v-icon>mdi-plus</v-icon></v-btn>
        <v-btn v-for="m in barraDireita" :key="m.to" :to="m.to" :active="ativo(m.to)">
          <v-icon>{{ ativo(m.to) ? m.iconeAtivo : m.icone }}</v-icon><span>Contas</span>
        </v-btn>
        <v-btn :active="maisAtivo" @click="mais = true">
          <v-icon>mdi-dots-grid</v-icon><span>Mais</span>
        </v-btn>
      </v-bottom-navigation>

      <v-bottom-sheet v-model="mais">
        <v-card rounded="t-xl" class="pb-6">
          <v-list nav class="pa-3">
            <v-list-item :title="TEMAS[preferenciaTema].titulo" subtitle="Toque para trocar (automático, claro, escuro)" rounded="lg" @click="proximoTema">
              <template #prepend><v-avatar color="secondary" variant="tonal" rounded="lg"><v-icon>{{ TEMAS[preferenciaTema].icone }}</v-icon></v-avatar></template>
            </v-list-item>
            <v-list-item v-for="m in menuMais" :key="m.to" :to="m.to" :title="m.titulo" rounded="lg" @click="mais = false">
              <template #prepend><v-avatar color="primary" variant="tonal" rounded="lg"><v-icon>{{ m.iconeAtivo }}</v-icon></v-avatar></template>
            </v-list-item>
          </v-list>
        </v-card>
      </v-bottom-sheet>

      <LancamentoForm v-if="pronto" />
      <ConfirmarDialog v-if="pronto" />
      <HistoricoDialog v-if="pronto" />
      <PagarFaturaDialog v-if="pronto" />
      <MoverParcelasDialog v-if="pronto" />
      <NotaFiscalDialog v-if="pronto" />
      <CompraDialog v-if="pronto" />
    </template>

    <v-snackbar v-model="estado.aviso.mostrar" :color="estado.aviso.cor" timeout="3500" location="top" rounded="lg">
      {{ estado.aviso.texto }}
    </v-snackbar>
  </v-app>
</template>

<style>
/* Menu recolhido: ícone no centro do trilho de 76px (o padrão do Vuetify deixa 6px para a esquerda). */
.v-navigation-drawer--rail .v-list .v-list-item { padding-inline: 14px !important; }
</style>
