using Financas.Api.Dominio;
using Financas.Api.Servicos;
using Microsoft.EntityFrameworkCore;
using static Financas.Tests.Ambiente;

namespace Financas.Tests;

public class RegrasDeNegocioTests
{
    [Fact]
    public async Task Corrigir_lancamento_mantem_versao_anterior_na_auditoria()
    {
        using var a = new Ambiente();
        var l = await a.Despesa(150m, D(2026, 10, 10));

        await a.Lancamentos.Editar(l.Id, new EditarLancamentoDto("Despesa", 105m, l.Data, a.Banco.Id, null,
            a.Mercado.Id, FormaPagamento.Pix, null, null, "Valor digitado errado"));

        var log = await a.Db.AuditLogs.Where(x => x.EntidadeId == l.Id && x.Operacao == "Correção").SingleAsync();
        Assert.Contains("150", log.Antes);
        Assert.Contains("105", log.Depois);
        Assert.Equal("Valor digitado errado", log.Motivo);
        Assert.Equal(105m, (await a.Db.Lancamentos.FindAsync(l.Id))!.Valor);
    }

    [Fact]
    public async Task Cancelar_nao_apaga_e_deixa_de_afetar_o_saldo()
    {
        using var a = new Ambiente();
        var l = await a.Despesa(200m, D(2026, 10, 10));
        Assert.Equal(800m, (await a.Resumo.Obter()).SaldoDisponivel);

        await a.Lancamentos.Cancelar(l.Id, "Lançado em duplicidade");

        a.Db.ChangeTracker.Clear();
        var salvo = await a.Db.Lancamentos.FindAsync(l.Id);
        Assert.Equal(StatusLancamento.Cancelado, salvo!.Status);
        Assert.Equal(1000m, (await a.Resumo.Obter()).SaldoDisponivel);
    }

    [Fact]
    public async Task Cancelar_exige_motivo()
    {
        using var a = new Ambiente();
        var l = await a.Despesa(50m, D(2026, 10, 10));
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Lancamentos.Cancelar(l.Id, " "));
    }

    [Fact]
    public async Task Exclusao_fisica_e_bloqueada_no_codigo_e_no_banco()
    {
        using var a = new Ambiente();
        var l = await a.Despesa(50m, D(2026, 10, 10));

        a.Db.Lancamentos.Remove(l);
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Db.SaveChangesAsync());
        a.Db.ChangeTracker.Clear();

        await Assert.ThrowsAnyAsync<Exception>(() => a.Db.Database.ExecuteSqlRawAsync("DELETE FROM Lancamentos"));
        await Assert.ThrowsAnyAsync<Exception>(() => a.Db.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs"));
        await Assert.ThrowsAnyAsync<Exception>(() => a.Db.Database.ExecuteSqlRawAsync("UPDATE AuditLogs SET Motivo = 'x'"));
        Assert.Equal(1, await a.Db.Lancamentos.CountAsync());
    }

    [Fact]
    public async Task Remover_sugestao_da_previsao_nao_apaga_historico_e_nao_reaparece()
    {
        using var a = new Ambiente();
        await a.Despesa(300m, D(2026, 9, 5), a.Lazer);
        var novembro = D(2026, 11, 1);

        await a.Planejamento.Sugerir(novembro, BaseSugestao.MesAnterior); // outubro vazio: nada sugerido
        a.Relogio.Hoje = D(2026, 10, 15);
        await a.Despesa(250m, D(2026, 10, 5), a.Lazer);
        await a.Planejamento.Sugerir(novembro, BaseSugestao.MesAnterior);
        await a.Planejamento.Dispensar(novembro, a.Lazer.Id, "Não vou viajar");

        await a.Planejamento.Sugerir(novembro, BaseSugestao.MesAnterior);
        var plano = await a.Planejamento.Obter(novembro);

        var lazer = plano.Variaveis.Single(v => v.CategoriaId == a.Lazer.Id);
        Assert.True(lazer.Dispensado);
        Assert.Equal(0m, plano.TotalVariaveis);
        Assert.Equal(2, await a.Db.Lancamentos.CountAsync(l => l.CategoriaId == a.Lazer.Id));

        await a.Planejamento.Restaurar(novembro, a.Lazer.Id);
        Assert.Equal(250m, (await a.Planejamento.Obter(novembro)).TotalVariaveis);
    }

    [Fact]
    public async Task Parcela_continua_na_previsao_mesmo_com_categoria_dispensada()
    {
        using var a = new Ambiente();
        await a.Despesa(1200m, D(2026, 10, 15), a.Lazer, status: StatusLancamento.Previsto, parcelas: 6);
        var novembro = D(2026, 11, 1);
        await a.Planejamento.Dispensar(novembro, a.Lazer.Id, null);

        var plano = await a.Planejamento.Obter(novembro);
        var parcela = Assert.Single(plano.Compromissos);
        Assert.Equal(200m, parcela.Valor);
        Assert.Equal("Despesa (2/6)", parcela.Descricao);
    }

    [Fact]
    public async Task Pagar_conta_recorrente_nao_cria_segunda_despesa()
    {
        using var a = new Ambiente();
        var regra = await a.Recorrente(TipoLancamento.Despesa, a.Energia, 180m, D(2026, 10, 10));
        var outubro = await a.Db.Lancamentos.SingleAsync(l => l.RegraId == regra.Id && l.Sequencia == 1);
        var antes = await a.Db.Lancamentos.CountAsync();

        await a.Lancamentos.Confirmar(outubro.Id, new ConfirmarDto(D(2026, 10, 12), 193.47m, null, FormaPagamento.Pix));

        Assert.Equal(antes, await a.Db.Lancamentos.CountAsync());
        var plano = await a.Planejamento.Obter(D(2026, 10, 1));
        Assert.Equal(193.47m, plano.TotalCompromissos);
        Assert.Equal(1000m - 193.47m, (await a.Resumo.Obter()).SaldoDisponivel);
        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            a.Lancamentos.Confirmar(outubro.Id, new ConfirmarDto(D(2026, 10, 12), 1m, null, null)));
    }

    [Fact]
    public async Task Confirmar_salario_nao_duplica_receita()
    {
        using var a = new Ambiente();
        var regra = await a.Recorrente(TipoLancamento.Receita, a.Salario, 4500m, D(2026, 10, 5), forma: FormaPagamento.Transferencia);
        var outubro = await a.Db.Lancamentos.SingleAsync(l => l.RegraId == regra.Id && l.Sequencia == 1);

        Assert.Equal(4500m, (await a.Planejamento.Obter(D(2026, 10, 1))).TotalReceitas);
        await a.Lancamentos.Confirmar(outubro.Id, new ConfirmarDto(D(2026, 10, 5), 4500m, null, null));

        var plano = await a.Planejamento.Obter(D(2026, 10, 1));
        Assert.Equal(4500m, plano.TotalReceitas);
        Assert.Equal(4500m, plano.ReceitasRecebidas);
    }

    [Fact]
    public async Task Gerar_previsao_e_ocorrencias_duas_vezes_nao_duplica()
    {
        using var a = new Ambiente();
        await a.Despesa(100m, D(2026, 9, 10), a.Lazer);
        var regra = await a.Recorrente(TipoLancamento.Despesa, a.Energia, 180m, D(2026, 10, 10));

        await a.Planejamento.Sugerir(D(2026, 10, 1), BaseSugestao.MesAnterior);
        await a.Planejamento.Sugerir(D(2026, 10, 1), BaseSugestao.MesAnterior);
        await a.Regras.Sincronizar();
        await a.Regras.Sincronizar(D(2027, 3, 31));
        await a.Regras.Sincronizar(D(2027, 3, 31));

        Assert.Equal(1, await a.Db.OrcamentoItens.CountAsync());
        var seqs = await a.Db.Lancamentos.Where(l => l.RegraId == regra.Id).Select(l => l.Sequencia).ToListAsync();
        Assert.Equal(6, seqs.Count); // out/2026 a mar/2027
        Assert.Equal(seqs.Count, seqs.Distinct().Count());
    }

    [Fact]
    public async Task Conta_vencida_aparece_nos_alertas_com_dias_de_atraso()
    {
        using var a = new Ambiente();
        await a.Recorrente(TipoLancamento.Despesa, a.Energia, 180m, D(2026, 10, 10));
        await a.Despesa(90m, D(2026, 10, 15), status: StatusLancamento.Previsto);

        var alertas = (await a.Resumo.Obter()).Alertas;
        var vencida = Assert.Single(alertas, x => x.Tipo == TipoAlerta.Vencido);
        Assert.Equal(5, vencida.Dias);
        Assert.Single(alertas, x => x.Tipo == TipoAlerta.Hoje);
    }

    [Fact]
    public async Task Desativar_recorrencia_para_novas_ocorrencias_sem_apagar_passado()
    {
        using var a = new Ambiente();
        a.Relogio.Hoje = D(2026, 8, 1);
        var regra = await a.Recorrente(TipoLancamento.Despesa, a.Energia, 180m, D(2026, 8, 10));
        var agosto = await a.Db.Lancamentos.SingleAsync(l => l.RegraId == regra.Id && l.Sequencia == 1);
        a.Relogio.Hoje = D(2026, 10, 15);
        await a.Lancamentos.Confirmar(agosto.Id, new ConfirmarDto(D(2026, 8, 10), 180m, null, null));
        await a.Regras.Sincronizar(); // gera até dezembro

        await a.Regras.Desativar(regra.Id, new DesativarRegraDto(true, "Mudei de casa"));
        await a.Regras.Sincronizar(D(2027, 12, 31));

        var todos = await a.Db.Lancamentos.Where(l => l.RegraId == regra.Id).ToListAsync();
        Assert.Equal(5, todos.Count); // ago a dez — nada novo gerado após desativar
        Assert.Equal(StatusLancamento.Realizado, todos.Single(l => l.Sequencia == 1).Status);
        // Atrasadas (set, out) ficam para o usuário decidir; futuras (nov, dez) são canceladas.
        Assert.Equal(StatusLancamento.Previsto, todos.Single(l => l.Sequencia == 2).Status);
        Assert.Equal(StatusLancamento.Previsto, todos.Single(l => l.Sequencia == 3).Status);
        Assert.Equal(StatusLancamento.Cancelado, todos.Single(l => l.Sequencia == 4).Status);
        Assert.Equal(StatusLancamento.Cancelado, todos.Single(l => l.Sequencia == 5).Status);
    }

    [Fact]
    public async Task Transferencia_entre_contas_nao_e_receita_nem_despesa()
    {
        using var a = new Ambiente();
        await a.Lancamentos.Criar(new NovoLancamentoDto(TipoLancamento.Transferencia, "Guardar", 300m, D(2026, 10, 10),
            a.Banco.Id, a.Poupanca.Id, null, null, null, null, null, null));

        var r = await a.Resumo.Obter();
        Assert.Equal(0m, r.EntradasMes);
        Assert.Equal(0m, r.SaidasMes);
        Assert.Equal(1000m, r.SaldoDisponivel);
        Assert.Equal(700m, r.Contas.Single(c => c.Id == a.Banco.Id).Saldo);
        Assert.Equal(300m, r.Contas.Single(c => c.Id == a.Poupanca.Id).Saldo);
    }

    [Fact]
    public async Task Valor_previsto_nao_altera_saldo_real()
    {
        using var a = new Ambiente();
        await a.Despesa(400m, D(2026, 10, 20), status: StatusLancamento.Previsto);

        var r = await a.Resumo.Obter();
        Assert.Equal(1000m, r.SaldoDisponivel);
        Assert.Equal(400m, r.DespesasAPagar);
        Assert.Equal(600m, r.SaldoProjetado);
    }

    [Fact]
    public async Task Estorno_reduz_totais_e_fica_vinculado_ao_original()
    {
        using var a = new Ambiente();
        var compra = await a.Despesa(300m, D(2026, 10, 5));

        var estorno = await a.Lancamentos.Estornar(compra.Id, new EstornoDto(120m, D(2026, 10, 8), "Devolvi uma peça"));

        Assert.Equal(compra.Id, estorno.EstornoDeId);
        var r = await a.Resumo.Obter();
        Assert.Equal(180m, r.SaidasMes);
        Assert.Equal(820m, r.SaldoDisponivel);
        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            a.Lancamentos.Estornar(compra.Id, new EstornoDto(200m, D(2026, 10, 8), null)));
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Lancamentos.Cancelar(compra.Id, "x"));
    }

    [Theory]
    [InlineData(100, 3)]
    [InlineData(1200, 7)]
    [InlineData(99.99, 4)]
    public void Parcelas_nao_exatas_somam_o_total(decimal total, int n)
    {
        var parcelas = RegraService.DividirParcelas(total, n);
        Assert.Equal(n, parcelas.Length);
        Assert.Equal(total, parcelas.Sum());
        Assert.All(parcelas, p => Assert.Equal(p, decimal.Round(p, 2)));
    }

    [Fact]
    public async Task Parcelas_nao_entram_na_base_de_gastos_variaveis()
    {
        using var a = new Ambiente();
        a.Relogio.Hoje = D(2026, 9, 1);
        await a.Despesa(600m, D(2026, 9, 1), a.Lazer, status: StatusLancamento.Previsto, parcelas: 3);
        await a.Despesa(80m, D(2026, 9, 1), a.Lazer);
        a.Relogio.Hoje = D(2026, 10, 15);

        await a.Planejamento.Sugerir(D(2026, 10, 1), BaseSugestao.MesAnterior);
        var item = await a.Db.OrcamentoItens.SingleAsync();
        Assert.Equal(80m, item.Valor);
    }
}

public class CartaoTests
{
    [Theory]
    [InlineData(2026, 10, 2, 2026, 10)] // antes do fechamento (dia 3): vence em 10/out
    [InlineData(2026, 10, 3, 2026, 11)] // no dia do fechamento: próxima fatura
    [InlineData(2026, 10, 20, 2026, 11)]
    [InlineData(2026, 12, 15, 2027, 1)]
    public void Compra_cai_na_fatura_correta(int a, int m, int d, int fa, int fm)
    {
        var cartao = new Conta { Tipo = TipoConta.CartaoCredito, DiaFechamento = 3, DiaVencimento = 10 };
        Assert.Equal(new DateOnly(fa, fm, 1), Calendario.FaturaDaCompra(cartao, new DateOnly(a, m, d)));
    }

    [Fact]
    public void Cartao_que_vence_antes_do_fechamento_usa_mes_seguinte()
    {
        var cartao = new Conta { Tipo = TipoConta.CartaoCredito, DiaFechamento = 25, DiaVencimento = 5 };
        Assert.Equal(new DateOnly(2026, 11, 1), Calendario.FaturaDaCompra(cartao, new DateOnly(2026, 10, 20)));
        Assert.Equal(new DateOnly(2026, 12, 1), Calendario.FaturaDaCompra(cartao, new DateOnly(2026, 10, 25)));
        Assert.Equal(new DateOnly(2026, 10, 25), Calendario.FechamentoDaFatura(cartao, new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public async Task Compra_no_cartao_conta_na_categoria_e_pagamento_da_fatura_nao_duplica()
    {
        using var a = new Ambiente();
        await a.Despesa(250m, D(2026, 10, 5), a.Mercado, a.Cartao);
        await a.Despesa(900m, D(2026, 10, 5), a.Lazer, a.Cartao, parcelas: 3);

        var fatura = await a.Cartoes.Fatura(a.Cartao.Id, D(2026, 11, 1));
        Assert.Equal(550m, fatura.Total); // 250 + 1ª parcela de 300
        Assert.Equal(StatusFatura.Aberta, fatura.Status);
        Assert.Equal(300m, (await a.Cartoes.Fatura(a.Cartao.Id, D(2026, 12, 1))).Total);

        a.Relogio.Hoje = D(2026, 11, 8);
        await a.Cartoes.Pagar(a.Cartao.Id, D(2026, 11, 1), new PagarFaturaDto(a.Banco.Id, 550m, D(2026, 11, 8)));

        var paga = await a.Cartoes.Fatura(a.Cartao.Id, D(2026, 11, 1));
        Assert.Equal(StatusFatura.Paga, paga.Status);
        var r = await a.Resumo.Obter();
        Assert.Equal(450m, r.SaldoDisponivel);
        Assert.Equal(300m, r.SaidasMes); // em novembro só a 2ª parcela é despesa; o pagamento da fatura não é
    }

    [Fact]
    public async Task Limite_usado_conta_parcelas_futuras_e_libera_com_pagamento_e_estorno()
    {
        using var a = new Ambiente();
        await a.Despesa(1200m, D(2026, 10, 5), a.Lazer, a.Cartao, parcelas: 6);   // ocupa os 1.200 na hora
        var mercado = await a.Despesa(300m, D(2026, 10, 6), a.Mercado, a.Cartao);
        await a.Recorrente(TipoLancamento.Despesa, a.Lazer, 55.90m, D(2026, 10, 20), a.Cartao); // futura: não ocupa
        var cancelada = await a.Despesa(999m, D(2026, 10, 7), a.Mercado, a.Cartao);
        await a.Lancamentos.Cancelar(cancelada.Id, "erro");

        Assert.Equal(1500m, (await a.Cartoes.LimitesUsados())[a.Cartao.Id]);

        await a.Lancamentos.Estornar(mercado.Id, new EstornoDto(100m, D(2026, 10, 8), "devolução"));
        await a.Cartoes.Pagar(a.Cartao.Id, D(2026, 11, 1), new PagarFaturaDto(a.Banco.Id, 400m, D(2026, 10, 15)));

        Assert.Equal(1000m, (await a.Cartoes.LimitesUsados())[a.Cartao.Id]);
    }

    [Fact]
    public async Task Recorrencia_no_cartao_e_confirmada_automaticamente_na_data()
    {
        using var a = new Ambiente();
        var regra = await a.Recorrente(TipoLancamento.Despesa, a.Lazer, 55.90m, D(2026, 10, 20), a.Cartao);
        var primeira = await a.Db.Lancamentos.SingleAsync(l => l.RegraId == regra.Id && l.Sequencia == 1);
        Assert.Equal(StatusLancamento.Previsto, primeira.Status);

        a.Relogio.Hoje = D(2026, 10, 21);
        await a.Regras.Sincronizar();
        await a.Db.Entry(primeira).ReloadAsync();
        Assert.Equal(StatusLancamento.Realizado, primeira.Status);
        Assert.Equal(D(2026, 11, 1), primeira.FaturaMes);
    }
}
