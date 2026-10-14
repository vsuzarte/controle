namespace Financas.Tests;

/// <summary>Orçamento do mês: sugestão, dispensa e o que conta como gasto variável.</summary>
public class PlanejamentoTests
{
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
