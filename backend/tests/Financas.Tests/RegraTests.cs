namespace Financas.Tests;

/// <summary>Contas do mês e parcelamentos: geração sem duplicar, dia útil, desativação.</summary>
public class RegraTests
{
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

    [Theory]
    [InlineData(100, 3)]
    [InlineData(1200, 7)]
    [InlineData(99.99, 4)]
    public void Parcelas_nao_exatas_somam_o_total(decimal total, int n)
    {
        var parcelas = GeradorDeOcorrencias.DividirParcelas(total, n);
        Assert.Equal(n, parcelas.Length);
        Assert.Equal(total, parcelas.Sum());
        Assert.All(parcelas, p => Assert.Equal(p, decimal.Round(p, 2)));
    }

    [Fact]
    public async Task Regra_no_quinto_dia_util_gera_as_datas_certas()
    {
        using var a = new Ambiente();
        // Pedido em 15/10, quinto dia útil de outubro já passou (07/10): começa em novembro.
        var r = await a.Pagamento("Salário", 4000, D(2026, 10, 15), RegraVencimento.DiaUtil, 5);
        var datas = await a.Db.Lancamentos.Where(l => l.RegraId == r.Id).OrderBy(l => l.Sequencia).Select(l => l.Data).ToListAsync();
        Assert.Equal([D(2026, 11, 9), D(2026, 12, 7)], datas);
    }

    [Fact]
    public async Task Mudar_regra_para_dia_util_recalcula_so_os_previstos_deste_mes_em_diante()
    {
        using var a = new Ambiente();
        var r = await a.Pagamento("Salário", 4000, D(2026, 9, 9));
        var setembro = await a.Db.Lancamentos.SingleAsync(l => l.RegraId == r.Id && l.Sequencia == 1);
        await a.Lancamentos.Confirmar(setembro.Id, new ConfirmarDto(D(2026, 9, 9), 4000, null, null));

        await a.Regras.Editar(r.Id, new EditarRegraDto("Salário", a.Salario.Id, a.Banco.Id, FormaPagamento.Pix, 4000, false, null, null,
            false, "5º dia útil", RegraVencimento.DiaUtil, 5));

        var datas = await a.Db.Lancamentos.Where(l => l.RegraId == r.Id).OrderBy(l => l.Sequencia)
            .Select(l => new { l.Data, l.Status }).ToListAsync();
        Assert.Equal(D(2026, 9, 9), datas[0].Data); // já recebido: não muda
        Assert.Equal(D(2026, 10, 7), datas[1].Data);
        Assert.Equal(D(2026, 11, 9), datas[2].Data);
        Assert.Equal(D(2026, 12, 7), datas[3].Data);
        Assert.Contains(await a.Db.AuditLogs.ToListAsync(), l => l.Motivo == "5º dia útil");
    }
}
