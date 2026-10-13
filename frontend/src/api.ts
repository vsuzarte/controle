import { reactive } from 'vue'

export const sessao = reactive({ precisaLogin: false })

export class ErroApi extends Error {}

async function req<T>(metodo: string, url: string, corpo?: unknown): Promise<T> {
  const resp = await fetch('/api' + url, {
    method: metodo,
    headers: corpo !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: corpo !== undefined ? JSON.stringify(corpo) : undefined,
    credentials: 'same-origin',
  })
  if (resp.status === 401 && !url.startsWith('/auth/')) {
    sessao.precisaLogin = true
    throw new ErroApi('Sessão expirada. Entre novamente.')
  }
  const texto = await resp.text()
  const dados = texto ? JSON.parse(texto) : undefined
  if (!resp.ok) throw new ErroApi(dados?.erro ?? `Erro ${resp.status}`)
  return dados as T
}

export const api = {
  get: <T>(url: string) => req<T>('GET', url),
  post: <T = unknown>(url: string, corpo: unknown = {}) => req<T>('POST', url, corpo),
  put: <T = unknown>(url: string, corpo: unknown) => req<T>('PUT', url, corpo),
}

/** Monta query string ignorando valores vazios. */
export function qs(params: Record<string, string | null | undefined>) {
  const p = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v) p.set(k, v)
  const s = p.toString()
  return s ? '?' + s : ''
}
