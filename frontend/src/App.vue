<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { useDisplay } from 'vuetify'
import { api, sessao } from './api'
import { carregarCadastros, erro, estado, novoLancamento } from './estado'
import LancamentoForm from './components/LancamentoForm.vue'
import ConfirmarDialog from './components/ConfirmarDialog.vue'
import HistoricoDialog from './components/HistoricoDialog.vue'

const { mobile } = useDisplay()
const pronto = ref(false)
const senha = ref('')
const entrando = ref(false)
const erroLogin = ref('')

const menu = [
  { to: '/', titulo: 'Início', icone: 'mdi-view-dashboard-outline' },
  { to: '/lancamentos', titulo: 'Lançamentos', icone: 'mdi-swap-vertical' },
  { to: '/planejamento', titulo: 'Planejamento', icone: 'mdi-calendar-month-outline' },
  { to: '/cartoes', titulo: 'Cartões', icone: 'mdi-credit-card-outline' },
  { to: '/recorrencias', titulo: 'Recorrências', icone: 'mdi-repeat' },
  { to: '/cadastros', titulo: 'Cadastros', icone: 'mdi-cog-outline' },
]
const menuCelular = menu.slice(0, 4)

async function iniciar() {
  const st = await api.get<{ exigeLogin: boolean; autenticado: boolean }>('/auth/status')
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
    <template v-if="sessao.precisaLogin">
      <v-main>
        <div class="d-flex align-center justify-center" style="min-height: 100vh; padding: 16px">
          <v-card width="360" class="pa-6">
            <div class="d-flex align-center ga-3 mb-6">
              <img src="/icone.svg" width="40" height="40" alt="" />
              <div class="text-h6">Finanças</div>
            </div>
            <v-form @submit.prevent="entrar">
              <v-text-field v-model="senha" label="Senha" type="password" autofocus :error-messages="erroLogin" />
              <v-btn type="submit" color="primary" block size="large" class="mt-4" :loading="entrando">Entrar</v-btn>
            </v-form>
          </v-card>
        </div>
      </v-main>
    </template>

    <template v-else>
      <v-navigation-drawer v-if="!mobile" permanent width="230">
        <div class="d-flex align-center ga-3 pa-4">
          <img src="/icone.svg" width="32" height="32" alt="" />
          <div class="text-subtitle-1 font-weight-bold">Finanças</div>
        </div>
        <v-list nav density="comfortable" color="primary">
          <v-list-item v-for="m in menu" :key="m.to" :to="m.to" :prepend-icon="m.icone" :title="m.titulo" exact />
        </v-list>
        <template #append>
          <div class="pa-3">
            <v-btn block color="primary" prepend-icon="mdi-plus" size="large" @click="novoLancamento()">
              Lançar
              <v-tooltip activator="parent" location="top">Atalho: tecla N</v-tooltip>
            </v-btn>
          </div>
        </template>
      </v-navigation-drawer>

      <v-app-bar v-if="mobile" density="compact" flat border="b">
        <template #prepend><img src="/icone.svg" width="28" height="28" alt="" class="ml-3" /></template>
        <v-app-bar-title class="text-subtitle-1 font-weight-bold">Finanças</v-app-bar-title>
        <template #append>
          <v-btn icon="mdi-repeat" to="/recorrencias" aria-label="Recorrências" />
          <v-btn icon="mdi-cog-outline" to="/cadastros" aria-label="Cadastros" />
        </template>
      </v-app-bar>

      <v-main>
        <router-view v-if="pronto" :key="$route.path" />
        <div v-else class="d-flex justify-center pa-12"><v-progress-circular indeterminate color="primary" /></div>
      </v-main>

      <v-bottom-navigation v-if="mobile" grow color="primary">
        <v-btn v-for="m in menuCelular" :key="m.to" :to="m.to" exact>
          <v-icon>{{ m.icone }}</v-icon>
          <span>{{ m.titulo }}</span>
        </v-btn>
      </v-bottom-navigation>

      <v-fab
        v-if="mobile && pronto"
        icon="mdi-plus"
        color="primary"
        size="large"
        app
        location="bottom end"
        style="margin-bottom: 64px"
        aria-label="Novo lançamento"
        @click="novoLancamento()"
      />

      <LancamentoForm v-if="pronto" />
      <ConfirmarDialog v-if="pronto" />
      <HistoricoDialog v-if="pronto" />
    </template>

    <v-snackbar v-model="estado.aviso.mostrar" :color="estado.aviso.cor" timeout="3500" location="top">
      {{ estado.aviso.texto }}
    </v-snackbar>
  </v-app>
</template>
