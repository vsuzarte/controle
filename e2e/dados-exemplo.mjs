// Zera o ambiente de testes e preenche com um mês de exemplo (para olhar o visual ou demonstrar).
// Uso: docker compose -f docker-compose.e2e.yml up -d --build app  →  node e2e/dados-exemplo.mjs
// Só funciona no ambiente E2E (o endpoint de reset não existe no sistema de verdade).
const base = process.env.BASE_URL ?? 'http://localhost:8090'
let cookie = ''

async function api(metodo, url, corpo) {
  const r = await fetch(base + url, {
    method: metodo,
    headers: { 'Content-Type': 'application/json', cookie },
    body: corpo ? JSON.stringify(corpo) : undefined,
  })
  const setCookie = r.headers.get('set-cookie')
  if (setCookie) cookie = setCookie.split(';')[0]
  const texto = await r.text()
  if (!r.ok) throw new Error(`${metodo} ${url} → ${r.status} ${texto}`)
  return texto ? JSON.parse(texto) : null
}

await api('POST', '/api/e2e/reset')
await api('POST', '/api/auth/login', { senha: process.env.SENHA_E2E ?? 'e2e' })

const contas = await api('GET', '/api/contas')
const categorias = await api('GET', '/api/categorias')
const cat = (nome) => categorias.find((c) => c.nome.startsWith(nome)).id
const cc = contas.find((c) => c.nome === 'Conta corrente').id
await api('PUT', `/api/contas/${cc}`, { nome: 'Conta corrente', tipo: 'Corrente', saldoInicial: 3250.4, cor: '#1E88E5', ativa: true })
const nu = (await api('POST', '/api/contas', { nome: 'Nubank', tipo: 'CartaoCredito', saldoInicial: 0, cor: '#8E24AA', diaFechamento: 3, diaVencimento: 10, ativa: true })).id

const fixa = (tipo, descricao, categoria, conta, valor, inicio, variavel = false) =>
  api('POST', '/api/regras', { modo: 'Recorrente', tipo, descricao, categoriaId: cat(categoria), contaId: conta, formaPagamento: 'Boleto', valor, valorVariavel: variavel, frequencia: 'Mensal', intervalo: 1, dataInicio: inicio })
const gasto = (descricao, valor, data, conta, categoria, parcelas = null) =>
  api('POST', '/api/lancamentos', { tipo: 'Despesa', descricao, valor, data, contaId: conta, categoriaId: cat(categoria), formaPagamento: 'Pix', parcelas })

await fixa('Receita', 'Salário', 'Salário', cc, 6800, '2026-10-05')
await fixa('Despesa', 'Aluguel', 'Moradia', cc, 1850, '2026-10-10')
await fixa('Despesa', 'Luz', 'Energia', cc, 210, '2026-10-18', true)
await fixa('Despesa', 'Spotify', 'Assinaturas', nu, 21.9, '2026-10-08')
for (const d of ['05', '12', '19', '26']) await gasto('Mercado', 380, `2026-09-${d}`, cc, 'Mercado')
await gasto('Cinema', 90, '2026-09-14', nu, 'Lazer')
await gasto('Uber', 140, '2026-09-20', cc, 'Transporte')
await gasto('Mercado', 412.37, '2026-10-04', nu, 'Mercado')
await gasto('Feira', 96.5, '2026-10-11', cc, 'Mercado')
await gasto('iFood', 68.9, '2026-10-13', nu, 'Restaurantes')
await gasto('Show', 240, '2026-10-09', nu, 'Lazer')
await gasto('Gasolina', 230, '2026-10-07', cc, 'Transporte')
await gasto('Notebook', 4200, '2026-10-01', nu, 'Lazer', 10)
console.log('Pronto: abra', base, '(senha: e2e)')
