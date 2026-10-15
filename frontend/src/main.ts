import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import { pt } from 'vuetify/locale'
import '@fontsource-variable/plus-jakarta-sans'
import '@mdi/font/css/materialdesignicons.css'
import 'vuetify/styles'
import './estilo.css'
import App from './App.vue'
import { aplicarTema, temaEfetivo } from './tema'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: () => import('./views/Inicio.vue') },
    { path: '/lancamentos', component: () => import('./views/Lancamentos.vue') },
    { path: '/planejamento', component: () => import('./views/Planejamento.vue') },
    { path: '/cartoes', component: () => import('./views/Cartoes.vue') },
    { path: '/previsao', component: () => import('./views/Previsao.vue') },
    { path: '/relatorios', component: () => import('./views/Relatorios.vue') },
    { path: '/extrato', component: () => import('./views/Extrato.vue') },
    { path: '/contas-do-mes', component: () => import('./views/ContasDoMes.vue') },
    { path: '/recorrencias', redirect: '/contas-do-mes' },
    { path: '/cadastros', component: () => import('./views/Cadastros.vue') },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})


const vuetify = createVuetify({
  locale: { locale: 'pt', messages: { pt } },
  theme: {
    defaultTheme: temaEfetivo(),
    themes: {
      claro: {
        dark: false,
        colors: {
          primary: '#0E9F6E', secondary: '#6366F1', background: '#F4F6FA', surface: '#FFFFFF',
          'surface-variant': '#EEF1F6', 'on-surface-variant': '#475569',
          success: '#10B981', error: '#EF4444', warning: '#F59E0B', info: '#3B82F6',
        },
      },
      escuro: {
        dark: true,
        colors: {
          primary: '#34D399', secondary: '#818CF8', background: '#0B0F14', surface: '#141B23',
          'surface-variant': '#1E2732', 'on-surface-variant': '#A3B1C2',
          success: '#34D399', error: '#F87171', warning: '#FBBF24', info: '#60A5FA',
        },
      },
    },
  },
  defaults: {
    VCard: { rounded: 'xl', elevation: 0, class: 'cartao' },
    VTextField: { variant: 'solo-filled', flat: true, density: 'comfortable', hideDetails: 'auto', rounded: 'lg' },
    VSelect: { variant: 'solo-filled', flat: true, density: 'comfortable', hideDetails: 'auto', rounded: 'lg' },
    VAutocomplete: { variant: 'solo-filled', flat: true, density: 'comfortable', hideDetails: 'auto', rounded: 'lg' },
    VCombobox: { variant: 'solo-filled', flat: true, density: 'comfortable', hideDetails: 'auto', rounded: 'lg' },
    VTextarea: { variant: 'solo-filled', flat: true, density: 'comfortable', hideDetails: 'auto', rounded: 'lg', rows: 2, autoGrow: true },
    VBtn: { rounded: 'lg', class: 'text-none', style: 'letter-spacing: 0' },
    VChip: { rounded: 'lg' },
    VDialog: { transition: 'dialog-bottom-transition' },
  },
})

aplicarTema((nome) => { vuetify.theme.global.name.value = nome })

createApp(App).use(router).use(vuetify).mount('#app')
