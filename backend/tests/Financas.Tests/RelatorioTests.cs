namespace Financas.Tests;

/// <summary>Comparativo de meses.</summary>
public class RelatorioTests
{
    [Fact]
    public async Task Comparativo_mostra_cada_mes_por_categoria_e_conta_fixa()
    {
        using var a = new Ambiente();
        var luz = await a.Recorrente(TipoLancamento.Despesa, a.Energia, 150, D(2026, 9, 10));
        foreach (var l in await a.Db.Lancamentos.Where(l => l.RegraId == luz.Id && l.Data <= D(2026, 10, 31)).ToListAsync())
            await a.Lancamentos.Confirmar(l.Id, new ConfirmarDto(l.Data, l.Data.Month == 9 ? 150 : 180, null, null));
        await a.Despesa(400, D(2026, 9, 3));
        await a.Despesa(250, D(2026, 10, 3));

        var c = await a.Relatorios.Comparativo(D(2026, 10, 1), 2);
        Assert.Equal([D(2026, 9, 1), D(2026, 10, 1)], c.Meses);
        Assert.Equal([150m, 180m], c.ContasFixas.Single().Valores);
        Assert.Equal([400m, 250m], c.Categorias.Single(x => x.Id == a.Mercado.Id).Valores);
        Assert.Equal([550m, 430m], c.Total.Valores);
    }
}
