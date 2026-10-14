// Zera o ambiente de testes e preenche com ~6 meses de dados fictícios, para demonstrar o sistema.
// Uso: docker compose -f docker-compose.e2e.yml up -d --build app  →  node e2e/dados-exemplo.mjs
// Só funciona no ambiente E2E (o endpoint de reset não existe no sistema de verdade). Hoje lá é 15/10/2026.
const base = process.env.BASE_URL ?? 'http://localhost:8090'
const HOJE = '2026-10-15'
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

// Números "aleatórios" sempre iguais (mesma demonstração a cada execução).
let semente = 20261015
const aleatorio = () => ((semente = (semente * 1103515245 + 12345) % 2147483648) / 2147483648)
const entre = (min, max) => Math.round((min + aleatorio() * (max - min)) * 100) / 100
const dia = (mes, d) => `2026-${String(mes).padStart(2, '0')}-${String(d).padStart(2, '0')}`
const MESES = [5, 6, 7, 8, 9, 10]
const ultimoDia = (mes) => (mes === 10 ? 14 : 28) // outubro: só até ontem

await api('POST', '/api/e2e/reset')
await api('POST', '/api/auth/login', { senha: process.env.SENHA_E2E ?? 'e2e' })

// ---------------- Cadastros ----------------
const categorias = await api('GET', '/api/categorias')
const cat = (nome) => categorias.find((c) => c.nome.startsWith(nome)).id
const beneficios = (await api('POST', '/api/categorias', { nome: 'Benefícios', tipo: 'Receita', grupo: 'Entradas', cor: '#F59E0B', icone: 'mdi-food', ativa: true })).id
categorias.push({ id: beneficios, nome: 'Benefícios' })

const contas = await api('GET', '/api/contas')
const banco = contas.find((c) => c.nome === 'Conta corrente').id
const dinheiro = contas.find((c) => c.nome === 'Dinheiro').id
await api('PUT', `/api/contas/${banco}`, { nome: 'Banco Inter', tipo: 'Corrente', saldoInicial: 0, cor: '#F97316', ativa: true })
await api('PUT', `/api/contas/${dinheiro}`, { nome: 'Carteira', tipo: 'Dinheiro', saldoInicial: 180, cor: '#43A047', ativa: true })
const conta = (nome, tipo, extra = {}) => api('POST', '/api/contas', { nome, tipo, saldoInicial: 0, ativa: true, ...extra }).then((c) => c.id)
const reserva = await conta('Reserva', 'Poupanca', { saldoInicial: 800, cor: '#0EA5E9' })
const nubank = await conta('Nubank', 'CartaoCredito', { cor: '#8E24AA', diaFechamento: 3, diaVencimento: 10, limite: 6500 })
const itau = await conta('Itaú Visa', 'CartaoCredito', { cor: '#1E3A8A', diaFechamento: 20, diaVencimento: 28, limite: 4800 })
const vr = await conta('VR Alelo', 'Beneficio', { saldoInicial: 500, cor: '#F59E0B' })

// ---------------- Contas do mês e pagamentos ----------------
const regra = (tipo, descricao, categoria, contaId, valor, inicio, extra = {}) =>
  api('POST', '/api/regras', {
    modo: 'Recorrente', tipo, descricao, categoriaId: cat(categoria), contaId, formaPagamento: 'Pix', valor,
    valorVariavel: false, frequencia: 'Mensal', intervalo: 1, dataInicio: inicio, ...extra,
  })

const salario = await regra('Receita', 'Salário', 'Salário', banco, 5200, '2026-05-01', { vencimento: 'DiaUtil', diaUtilNumero: 5, iniciaPeriodo: true, formaPagamento: 'Transferencia' })
await regra('Receita', 'Adiantamento', 'Salário', banco, 2800, '2026-05-20', { iniciaPeriodo: true, formaPagamento: 'Transferencia' })
await regra('Receita', 'Recarga VR', 'Benefícios', vr, 520, '2026-05-01', { vencimento: 'UltimoDiaUtil', iniciaPeriodo: false })

await regra('Despesa', 'Aluguel', 'Moradia', banco, 2100, '2026-05-10')
await regra('Despesa', 'Condomínio', 'Moradia', banco, 480, '2026-05-08', { formaPagamento: 'Boleto' })
await regra('Despesa', 'Luz', 'Energia', banco, 210, '2026-05-18', { valorVariavel: true, formaPagamento: 'Boleto' })
await regra('Despesa', 'Água', 'Água', banco, 95, '2026-05-22', { valorVariavel: true, formaPagamento: 'Boleto' })
await regra('Despesa', 'Internet', 'Internet', banco, 119.9, '2026-05-12', { formaPagamento: 'Boleto' })
await regra('Despesa', 'Celular', 'Internet', banco, 59.9, '2026-05-15')
await regra('Despesa', 'Escola das crianças', 'Educação', banco, 980, '2026-05-07', { formaPagamento: 'Boleto', pagoComRegraId: salario })
await regra('Despesa', 'Seguro do carro', 'Transporte', banco, 214.5, '2026-05-25', { formaPagamento: 'DebitoAutomatico' })
await regra('Despesa', 'Academia', 'Saúde', nubank, 129.9, '2026-05-05')
await regra('Despesa', 'Spotify', 'Assinaturas', nubank, 21.9, '2026-05-08')
await regra('Despesa', 'Netflix', 'Assinaturas', itau, 55.9, '2026-05-17')

// ---------------- Gastos do dia a dia ----------------
const gasto = (descricao, valor, data, contaId, categoria, extra = {}) =>
  api('POST', '/api/lancamentos', { tipo: 'Despesa', descricao, valor, data, contaId, categoriaId: cat(categoria), formaPagamento: 'Pix', ...extra })

for (const m of MESES) {
  const fim = ultimoDia(m)
  // Cresce um pouco a cada mês, para os relatórios mostrarem tendência.
  const alta = 1 + (m - 5) * 0.06
  for (const d of [3, 10, 17, 24]) if (d <= fim) await gasto('Mercado', entre(260, 420) * alta, dia(m, d), d % 2 ? nubank : banco, 'Mercado')
  for (const d of [6, 20]) if (d <= fim) await gasto('Feira', entre(55, 110), dia(m, d), banco, 'Mercado')
  for (const d of [2, 9, 15, 23, 27]) if (d <= fim) await gasto('iFood', entre(42, 98) * alta, dia(m, d), nubank, 'Restaurantes')
  for (const d of [4, 11, 18, 25]) if (d <= fim) await gasto('Uber', entre(18, 46), dia(m, d), itau, 'Transporte')
  for (const d of [7, 21]) if (d <= fim) await gasto('Gasolina', entre(190, 265), dia(m, d), itau, 'Transporte')
  if (13 <= fim) await gasto('Farmácia', entre(38, 160), dia(m, 13), banco, 'Saúde')
  if (16 <= fim) await gasto(['Cinema', 'Show', 'Barzinho', 'Parque', 'Teatro', 'Boliche'][m - 5], entre(70, 260), dia(m, 16), nubank, 'Lazer')
  for (const d of [1, 2, 5, 6, 8, 9, 12, 13, 14, 19, 22, 26]) if (d <= fim) await gasto('Almoço', entre(26, 52), dia(m, d), vr, 'Restaurantes', { formaPagamento: 'CartaoDebito' })
  if (12 <= fim) await gasto('Padaria', entre(14, 38), dia(m, 12), dinheiro, 'Mercado', { formaPagamento: 'Dinheiro' })
  if (m !== 10) await api('POST', '/api/lancamentos', { tipo: 'Transferencia', descricao: 'Guardar na reserva', valor: 300, data: dia(m, 9), contaId: banco, contaDestinoId: reserva, formaPagamento: 'Transferencia' })
}
// Extras e compras grandes
await gasto('Tênis de corrida', 389.9, '2026-06-14', nubank, 'Roupas')
await gasto('Jaqueta', 249.9, '2026-09-05', itau, 'Roupas')
await gasto('Presente aniversário', 180, '2026-07-19', nubank, 'Compras diversas')
await gasto('Consulta dentista', 280, '2026-08-11', banco, 'Saúde')
await gasto('Geladeira', 3600, '2026-06-14', itau, 'Compras diversas', { parcelas: 12 })
await gasto('Notebook', 4200, '2026-08-02', nubank, 'Compras diversas', { parcelas: 10 })
const [caixaDeSom] = await gasto('Caixa de som', 459, '2026-09-21', nubank, 'Lazer')
const [duplicado] = await gasto('Mercado', 312.4, '2026-09-24', banco, 'Mercado')
for (const [mes, valor] of [[7, 1200], [9, 1450]])
  await api('POST', '/api/lancamentos', { tipo: 'Receita', descricao: 'Freela site', valor, data: dia(mes, 23), contaId: banco, categoriaId: cat('Renda extra'), formaPagamento: 'Pix' })

// Estorno (devolução) e cancelamento (lançado em dobro), para mostrar o histórico.
await api('POST', `/api/lancamentos/${caixaDeSom}/estornar`, { valor: 459, data: '2026-09-28', motivo: 'Devolvida com defeito' })
await api('POST', `/api/lancamentos/${duplicado}/cancelar`, { motivo: 'Lancei duas vezes' })

// ---------------- Pagar o que já passou ----------------
await api('GET', '/api/resumo') // gera recorrências e confirma cartão/débito automático/recarga
const VALORES = {
  Luz: { 5: 168.4, 6: 175.1, 7: 189.33, 8: 214.7, 9: 236.18 },
  Água: { 5: 88.2, 6: 91.45, 7: 86.9, 8: 95.3, 9: 104.75 },
}
const pendentes = (await api('GET', '/api/lancamentos?status=Previsto')).filter((l) => l.data < HOJE)
for (const l of pendentes) {
  const mes = Number(l.data.slice(5, 7))
  // Internet de setembro e outubro fica sem pagar (aparece como 2 meses atrasados).
  if (l.descricao === 'Internet' && mes >= 9) continue
  let valor = VALORES[l.descricao]?.[mes] ?? l.valor
  let data = l.data
  let juros = null
  if (l.descricao === 'Luz' && mes === 8) { data = '2026-08-27'; juros = 6.44; valor += juros } // paga atrasada, com multa
  await api('POST', `/api/lancamentos/${l.id}/confirmar`, { data, valor: Math.round(valor * 100) / 100, juros })
}

// Faturas vencidas: todas pagas; a de agosto do Itaú, com atraso e juros.
for (const cartao of [nubank, itau]) {
  for (const f of await api('GET', `/api/cartoes/${cartao}/faturas`)) {
    if (f.restante <= 0 || f.vencimento >= HOJE) continue
    const atrasada = cartao === itau && f.mes.startsWith('2026-08')
    const juros = atrasada ? 38.5 : null
    await api('POST', `/api/cartoes/${cartao}/faturas/${f.mes.slice(0, 7)}/pagar`, {
      contaOrigemId: banco, valor: Math.round((f.restante + (juros ?? 0)) * 100) / 100,
      data: atrasada ? '2026-09-02' : f.vencimento, juros,
    })
  }
}

// ---------------- Planejamento e previsão ----------------
// Água de outubro movida no quadro para o salário de novembro (fica amarela: depois do vencimento).
const agua = (await api('GET', '/api/lancamentos?status=Previsto')).find((l) => l.descricao === 'Água' && l.data.startsWith('2026-10'))
await api('PUT', '/api/previsao/mover', { lancamentoId: agua.id, pagarEm: '2026-11-09' })
await api('POST', '/api/previsao/periodos', { nome: 'Até o Natal', de: HOJE, ate: '2026-12-25' })
await api('GET', '/api/planejamento/2026-10') // cria a sugestão de orçamento do mês

// Saldo do banco hoje: ajusta o saldo inicial para ficar num valor realista.
const SALDO_HOJE = 2380.55
const atual = (await api('GET', '/api/contas')).find((c) => c.id === banco)
await api('PUT', `/api/contas/${banco}`, { nome: 'Banco Inter', tipo: 'Corrente', saldoInicial: Math.round((SALDO_HOJE - (atual.saldo - atual.saldoInicial)) * 100) / 100, cor: '#F97316', ativa: true })

const resumo = await api('GET', '/api/resumo')
const lancs = await api('GET', '/api/lancamentos')
console.log(`Pronto: ${lancs.length} lançamentos. Saldo disponível R$ ${resumo.saldoDisponivel.toFixed(2)}.`)
console.log(`Abra ${base} (senha: e2e). "Hoje" nesse ambiente é 15/10/2026.`)
