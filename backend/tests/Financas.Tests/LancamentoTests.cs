namespace Financas.Tests;

/// <summary>Confirmar, transferir, estornar e pagar com juros.</summary>
public class LancamentoTests
{
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

    [Fact]
    public async Task Pagar_conta_com_juros_guarda_a_diferenca()
    {
        using var a = new Ambiente();
        var r = await a.Recorrente(TipoLancamento.Despesa, a.Energia, 200, D(2026, 10, 10));
        var luz = await a.Db.Lancamentos.SingleAsync(l => l.RegraId == r.Id && l.Sequencia == 1);

        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            a.Lancamentos.Confirmar(luz.Id, new ConfirmarDto(D(2026, 10, 15), 215, null, null, 215)));
        await a.Lancamentos.Confirmar(luz.Id, new ConfirmarDto(D(2026, 10, 15), 215.40m, null, null, 15.40m));

        var pago = await a.Db.Lancamentos.FindAsync(luz.Id);
        Assert.Equal(215.40m, pago!.Valor);
        Assert.Equal(15.40m, pago.Juros);
        var rel = await a.Relatorios.Comparativo(D(2026, 10, 1), 3);
        Assert.Equal(15.40m, rel.Juros.Valores[^1]);
    }
}
