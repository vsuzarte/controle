namespace Financas.Tests;

/// <summary>Tela inicial: alertas de vencimento.</summary>
public class ResumoTests
{
    [Fact]
    public async Task Conta_vencida_aparece_nos_alertas_com_dias_de_atraso()
    {
        using var a = new Ambiente();
        await a.Recorrente(TipoLancamento.Despesa, a.Energia, 180m, D(2026, 10, 10));
        await a.Despesa(90m, D(2026, 10, 15), status: StatusLancamento.Previsto);

        var alertas = (await a.Resumo.Obter()).Alertas;
        var vencida = Assert.Single(alertas, x => x.Tipo == TipoAlerta.Vencido);
        // Venceu sábado 10/10; segunda 12/10 é feriado: dava para pagar sem atraso até terça 13/10.
        Assert.Equal(2, vencida.Dias);
        Assert.Equal(D(2026, 10, 10), vencida.VenceEm);
        Assert.Single(alertas, x => x.Tipo == TipoAlerta.Hoje);
    }

    [Fact]
    public async Task Vencimentos_seguem_a_janela_da_previsao_e_incluem_faturas()
    {
        using var a = new Ambiente();
        await a.Pagamento("Salário", 4000, D(2026, 10, 7), RegraVencimento.DiaUtil, 5);
        await a.Recorrente(TipoLancamento.Despesa, a.Energia, 200, D(2026, 11, 3)); // depois do mês, antes do salário
        await a.Despesa(300, D(2026, 10, 5), conta: a.Cartao);                    // fatura vence 10/11
        await a.Despesa(50, D(2026, 9, 25), conta: a.Cartao);                     // fatura venceu 10/10

        // Um pagamento por mês (5º dia útil): período atual (07/10 a 08/11) + o seguinte (09/11 a 06/12).
        var r = await a.Resumo.Obter();
        Assert.Equal(D(2026, 12, 6), r.Previsao.Fim);
        Assert.Contains(r.Alertas, x => x.Descricao == "Energia" && x.Data == D(2026, 11, 3));
        Assert.Contains(r.Alertas, x => x.CartaoId == a.Cartao.Id && x.Tipo == TipoAlerta.Vencido);
        Assert.Contains(r.Alertas, x => x.CartaoId == a.Cartao.Id && x.Data == D(2026, 11, 10));
        Assert.DoesNotContain(r.Alertas, x => x.Data > D(2026, 12, 6));
    }
}
