import { describe, expect, it } from 'vitest'
import { dataAoMover, faltaNoVale, jurosPorPadrao, pagoAMais, passaDoLimite, separarVencimentos, situacaoCard } from './regras'
import { dataBr, nomeMes, parseValor, somarDias, somarMeses } from './formato'
import type { Alerta } from './tipos'

const alerta = (a: Partial<Alerta>): Alerta => ({
  tipo: 'Proximo', dias: 3, descricao: 'Conta', valor: 100, data: '2026-10-18', tipoLancamento: 'Despesa',
  lancamentoId: crypto.randomUUID(), cartaoId: null, faturaMes: null, regraId: null, venceEm: null, ...a,
})

describe('juros no pagamento', () => {
  it('calcula só o que foi pago a mais, em centavos exatos', () => {
    expect(pagoAMais(525, 500)).toBe(25)
    expect(pagoAMais(193.47, 180)).toBe(13.47)
    expect(pagoAMais(0.3, 0.1)).toBe(0.2)
    expect(pagoAMais(480, 500)).toBe(0)
    expect(pagoAMais(null, 500)).toBe(0)
  })

  it('marca juros por padrão só em conta de valor fixo paga atrasada', () => {
    expect(jurosPorPadrao(false, '2026-10-05', '2026-10-15')).toBe(true)
    expect(jurosPorPadrao(true, '2026-10-05', '2026-10-15')).toBe(false) // luz: valor muda todo mês
    expect(jurosPorPadrao(false, '2026-10-15', '2026-10-15')).toBe(false) // pago no dia
  })
})

describe('vale e limite', () => {
  it('falta no vale considera saldo negativo como zero', () => {
    expect(faltaNoVale(80, 50)).toBe(30)
    expect(faltaNoVale(40, 50)).toBe(0)
    expect(faltaNoVale(80, -10)).toBe(80)
    expect(faltaNoVale(null, 50)).toBe(0)
  })

  it('avisa só quando a compra passa do limite disponível', () => {
    expect(passaDoLimite(600, 500, 0)).toBe(500)
    expect(passaDoLimite(300, 1000, 750)).toBe(250)
    expect(passaDoLimite(250, 1000, 750)).toBeNull()
    expect(passaDoLimite(600, null, 0)).toBeNull() // sem limite cadastrado
  })
})

describe('vencimentos da tela inicial', () => {
  it('agrupa atrasados da mesma conta e separa semana, depois e a receber', () => {
    const v = separarVencimentos([
      alerta({ tipo: 'Vencido', dias: 48, descricao: 'Internet', valor: 100, regraId: 'r1' }),
      alerta({ tipo: 'Vencido', dias: 17, descricao: 'Internet', valor: 100, regraId: 'r1' }),
      alerta({ tipo: 'Vencido', dias: 5, descricao: 'Fatura Nubank', valor: 300, cartaoId: 'c1', lancamentoId: null }),
      alerta({ tipo: 'Hoje', dias: 0, descricao: 'Luz', valor: 180 }),
      alerta({ dias: 13, descricao: 'Escola', valor: 814.67 }),
      alerta({ dias: 5, descricao: 'Salário', valor: 4000, tipoLancamento: 'Receita' }),
    ])
    expect(v.atrasadas).toHaveLength(2)
    expect(v.atrasadas[0]).toMatchObject({ chave: 'r1', total: 200 })
    expect(v.atrasadas[0].itens).toHaveLength(2)
    expect(v.semana.map((a) => a.descricao)).toEqual(['Luz'])
    expect(v.depois.map((a) => a.descricao)).toEqual(['Escola'])
    expect(v.aReceber.map((a) => a.descricao)).toEqual(['Salário'])
    expect(v.total).toBe(1494.67) // salário não entra no total a pagar
  })
})

describe('formato', () => {
  it('entende valores digitados ou colados', () => {
    expect(parseValor('1.234,56')).toBe(1234.56)
    expect(parseValor('1234.56')).toBe(1234.56)
    expect(parseValor('R$ 10')).toBe(10)
    expect(parseValor('')).toBeNull()
  })

  it('soma dias e meses atravessando o fim do mês e do ano', () => {
    expect(somarDias('2026-10-31', 1)).toBe('2026-11-01')
    expect(somarDias('2026-11-09', -1)).toBe('2026-11-08')
    expect(somarMeses('2026-12', 1)).toBe('2027-01')
    expect(nomeMes('2026-10')).toBe('Outubro de 2026')
    expect(nomeMes('2026-10', true)).toBe('out/26')
    expect(dataBr('2026-10-07')).toBe('07/10/2026')
  })
})

describe('quadro da previsão', () => {
  const adiantamento = { inicio: '2026-10-20', fim: '2026-11-08', atual: false }
  const salario = { inicio: '2026-11-09', fim: '2026-11-19', atual: false }
  const agora = { inicio: '2026-10-07', fim: '2026-10-19', atual: true }

  it('cor do card: atrasada, depois do prazo ou no prazo', () => {
    expect(situacaoCard({ atrasado: true, diasDepoisDoVencimento: 7 }).cor).toBe('error')
    expect(situacaoCard({ atrasado: false, diasDepoisDoVencimento: 1 })).toEqual({ cor: 'warning', texto: '+1 dia do vencimento' })
    expect(situacaoCard({ atrasado: false, diasDepoisDoVencimento: null }).cor).toBe('success')
  })

  it('soltar no período de outro pagamento grava a data desse pagamento', () => {
    const escola = { data: '2026-11-05', pagavelAte: null, pagoCom: null }
    expect(dataAoMover(escola, salario, '2026-10-15')).toBe('2026-11-09')
  })

  it('voltar para o lugar natural (inclusive fim de semana) volta ao automático', () => {
    expect(dataAoMover({ data: '2026-11-05', pagavelAte: null, pagoCom: null }, adiantamento, '2026-10-15')).toBeNull()
    expect(dataAoMover({ data: '2026-11-07', pagavelAte: '2026-11-09', pagoCom: null }, salario, '2026-10-15')).toBeNull()
    // atrasada pertence ao período de hoje
    expect(dataAoMover({ data: '2026-09-28', pagavelAte: null, pagoCom: null }, agora, '2026-10-15')).toBeNull()
  })

  it('adiantar para o período de hoje planeja para hoje', () => {
    expect(dataAoMover({ data: '2026-10-28', pagavelAte: null, pagoCom: null }, agora, '2026-10-15')).toBe('2026-10-15')
  })
})
