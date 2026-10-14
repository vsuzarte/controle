namespace Financas.Tests;

/// <summary>Vale-refeição: só compra, completa com outra conta e recarrega sozinho.</summary>
public class ValeTests
{
    [Fact]
    public async Task Vale_refeicao_so_compra_e_completa_com_outra_conta()
    {
        using var a = new Ambiente();
        var vr = new Conta { Nome = "VR", Tipo = TipoConta.Beneficio, SaldoInicial = 100 };
        a.Db.Contas.Add(vr);
        await a.Db.SaveChangesAsync();

        // Não paga contas nem é transferido.
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Recorrente(TipoLancamento.Despesa, a.Energia, 50, D(2026, 10, 20), vr));
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Lancamentos.Criar(new NovoLancamentoDto(TipoLancamento.Transferencia,
            "x", 10, D(2026, 10, 15), vr.Id, a.Banco.Id, null, null, null, null, null, null)));
        await Assert.ThrowsAsync<RegraNegocioException>(() =>
            a.Cartoes.Pagar(a.Cartao.Id, D(2026, 10, 1), new PagarFaturaDto(vr.Id, 10, D(2026, 10, 15))));

        // Compra de 130 com 100 no vale: 100 no VR e 30 no banco, mesma categoria.
        var criados = await a.Lancamentos.Criar(new NovoLancamentoDto(TipoLancamento.Despesa, "Almoço", 130, D(2026, 10, 15),
            vr.Id, null, a.Mercado.Id, FormaPagamento.CartaoDebito, null, null, null, null, a.Banco.Id));
        Assert.Equal(2, criados.Count);
        Assert.Equal(100, criados[0].Valor);
        Assert.Equal(30, criados[1].Valor);
        Assert.Equal(a.Banco.Id, criados[1].ContaId);
        Assert.Equal(a.Mercado.Id, criados[1].CategoriaId);

        var resumo = await a.Resumo.Obter();
        Assert.Equal(1000 - 30, resumo.SaldoDisponivel); // o vale não entra no dinheiro que paga contas
        Assert.Equal(0, resumo.Beneficios.Single().Saldo);
        Assert.Equal(130, resumo.GastosPorCategoria.Single(g => g.CategoriaId == a.Mercado.Id).Gasto);
    }

    [Fact]
    public async Task Recarga_do_vale_e_compra_futura_no_cartao_se_confirmam_sozinhas()
    {
        using var a = new Ambiente();
        var vr = new Conta { Nome = "VR", Tipo = TipoConta.Beneficio };
        a.Db.Contas.Add(vr);
        await a.Db.SaveChangesAsync();
        await a.Recorrente(TipoLancamento.Receita, a.Salario, 800, D(2026, 10, 20), vr);
        var compra = await a.Despesa(90, D(2026, 10, 20), conta: a.Cartao);
        Assert.Equal(StatusLancamento.Previsto, compra.Status);

        a.Relogio.Hoje = D(2026, 10, 20);
        await a.Regras.Sincronizar();

        Assert.Equal(StatusLancamento.Realizado, (await a.Db.Lancamentos.FindAsync(compra.Id))!.Status);
        Assert.Equal(StatusLancamento.Realizado,
            (await a.Db.Lancamentos.SingleAsync(l => l.ContaId == vr.Id && l.Data == D(2026, 10, 20))).Status);
    }
}
