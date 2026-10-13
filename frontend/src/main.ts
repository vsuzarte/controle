import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import { pt } from 'vuetify/locale'
import '@mdi/font/css/materialdesignicons.css'
import 'vuetify/styles'
import './estilo.css'
import App from './App.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: () => import('./views/Inicio.vue') },
    { path: '/lancamentos', component: () => import('./views/Lancamentos.vue') },
    { path: '/planejamento', component: () => import('./views/Planejamento.vue') },
    { path: '/cartoes', component: () => import('./views/Cartoes.vue') },
    { path: '/recorrencias', component: () => import('./views/Recorrencias.vue') },
    { path: '/cadastros', component: () => import('./views/Cadastros.vue') },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

const escuro = window.matchMedia('(prefers-color-scheme: dark)').matches

const vuetify = createVuetify({
  locale: { locale: 'pt', messages: { pt } },
  theme: {
    defaultTheme: escuro ? 'escuro' : 'claro',
    themes: {
      claro: {
        dark: false,
        colors: {
          primary: '#1E5631', secondary: '#4E6E58', background: '#F4F6F3', surface: '#FFFFFF',
          success: '#2E7D32', error: '#C62828', warning: '#E08A00', info: '#1565C0',
        },
      },
      escuro: {
        dark: true,
        colors: {
          primary: '#7BC88F', secondary: '#9DB8A4', background: '#121512', surface: '#1C201C',
          success: '#81C784', error: '#EF7A7A', warning: '#FFB74D', info: '#64B5F6',
        },
      },
    },
  },
  defaults: {
    VCard: { rounded: 'lg', elevation: 0, border: true },
    VTextField: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VSelect: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VAutocomplete: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' },
    VTextarea: { variant: 'outlined', density: 'comfortable', hideDetails: 'auto', rows: 2, autoGrow: true },
    VBtn: { rounded: 'lg' },
  },
})

window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
  vuetify.theme.global.name.value = e.matches ? 'escuro' : 'claro'
})

createApp(App).use(router).use(vuetify).mount('#app')
