const moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
const numero = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const nomesMes = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro']
const diasSemana = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb']

export const brl = (v: number | null | undefined) => moeda.format(v ?? 0)
export const num = (v: number | null | undefined) => numero.format(v ?? 0)

/** "2026-10-13" no fuso local (sem a armadilha do UTC do `toISOString`). */
export function hojeIso() {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export const mesIso = (dataIso: string) => dataIso.slice(0, 7)

export function somarMeses(mes: string, n: number) {
  const [a, m] = mes.split('-').map(Number)
  const d = new Date(a, m - 1 + n, 1)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
}

export function nomeMes(mes: string, curto = false) {
  const [a, m] = mes.split('-').map(Number)
  const nome = nomesMes[m - 1]
  return curto ? `${nome.slice(0, 3)}/${String(a).slice(2)}` : `${nome[0].toUpperCase()}${nome.slice(1)} de ${a}`
}

export function dataBr(iso: string | null | undefined, comSemana = false) {
  if (!iso) return ''
  const [a, m, d] = iso.slice(0, 10).split('-').map(Number)
  const txt = `${String(d).padStart(2, '0')}/${String(m).padStart(2, '0')}/${a}`
  if (!comSemana) return txt
  return `${diasSemana[new Date(a, m - 1, d).getDay()]}, ${String(d).padStart(2, '0')}/${String(m).padStart(2, '0')}`
}

export function dataHoraBr(iso: string) {
  const d = new Date(iso.endsWith('Z') ? iso : iso + 'Z')
  return d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })
}

/** Aceita "1.234,56", "1234,5", "1234.56", "R$ 10". */
export function parseValor(txt: string | number | null | undefined): number | null {
  if (txt === null || txt === undefined || txt === '') return null
  if (typeof txt === 'number') return txt
  let s = txt.replace(/[^\d,.-]/g, '')
  if (s.includes(',')) s = s.replace(/\./g, '').replace(',', '.')
  const n = Number(s)
  return Number.isFinite(n) ? Math.round(n * 100) / 100 : null
}

export function corValor(tipo: string, valor = 1) {
  if (tipo === 'Transferencia') return 'medium-emphasis'
  const positivo = (tipo === 'Receita') === valor >= 0
  return positivo ? 'text-success' : 'text-error'
}
