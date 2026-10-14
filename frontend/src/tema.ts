import { ref, watch } from 'vue'

/** Tema escolhido pelo usuário. "auto" acompanha o modo claro/escuro do sistema (Windows, celular). */
export type PreferenciaTema = 'auto' | 'claro' | 'escuro'

const midia = window.matchMedia('(prefers-color-scheme: dark)')
const lida = (() => { try { return localStorage.getItem('fin.tema') } catch { return null } })()

export const preferenciaTema = ref<PreferenciaTema>(lida === 'claro' || lida === 'escuro' ? lida : 'auto')

/** Nome do tema do Vuetify para a preferência atual. */
export function temaEfetivo(p: PreferenciaTema = preferenciaTema.value): 'claro' | 'escuro' {
  return p === 'auto' ? (midia.matches ? 'escuro' : 'claro') : p
}

/** Liga a preferência ao tema global do Vuetify (chamado uma vez, no início). */
export function aplicarTema(definir: (nome: 'claro' | 'escuro') => void) {
  watch(preferenciaTema, (p) => {
    try { localStorage.setItem('fin.tema', p) } catch { /* sem storage */ }
    definir(temaEfetivo(p))
  })
  midia.addEventListener('change', () => { if (preferenciaTema.value === 'auto') definir(temaEfetivo()) })
}

const ORDEM: PreferenciaTema[] = ['auto', 'claro', 'escuro']
export const TEMAS: Record<PreferenciaTema, { titulo: string; icone: string }> = {
  auto: { titulo: 'Tema automático', icone: 'mdi-theme-light-dark' },
  claro: { titulo: 'Tema claro', icone: 'mdi-white-balance-sunny' },
  escuro: { titulo: 'Tema escuro', icone: 'mdi-weather-night' },
}

export function proximoTema() {
  preferenciaTema.value = ORDEM[(ORDEM.indexOf(preferenciaTema.value) + 1) % ORDEM.length]
}
