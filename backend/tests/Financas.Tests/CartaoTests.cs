namespace Financas.Tests;

/// <summary>Cartão de crédito: fatura certa, limite, pagamento com juros e visão de todos.</summary>
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

    [Fact]
    public async Task Pagar_fatura_com_juros_separa_o_juro_e_quita_a_fatura()
    {
        using var a = new Ambiente();
        await a.Despesa(500, D(2026, 9, 20), conta: a.Cartao); // fatura de outubro, venceu dia 10
        await a.Cartoes.Pagar(a.Cartao.Id, D(2026, 10, 1), new PagarFaturaDto(a.Banco.Id, 530, D(2026, 10, 15), 30));

        var fatura = await a.Cartoes.Fatura(a.Cartao.Id, D(2026, 10, 1));
        Assert.Equal(StatusFatura.Paga, fatura.Status);
        Assert.Equal(500, fatura.Pago);
        var juros = await a.Db.Lancamentos.Include(l => l.Categoria).SingleAsync(l => l.Juros != null);
        Assert.Equal(30, juros.Valor);
        Assert.Equal("Tarifas e juros", juros.Categoria!.Nome);
        Assert.Equal(1000 - 530, (await a.Resumo.Obter()).SaldoDisponivel);
        Assert.Equal(30, (await a.Relatorios.Comparativo(D(2026, 10, 1), 2)).Juros.Valores[^1]);
    }

    [Fact]
    public async Task Todos_os_cartoes_somam_faturas_e_limites()
    {
        using var a = new Ambiente();
        a.Cartao.Limite = 2000;
        var outro = new Conta { Nome = "Outro", Tipo = TipoConta.CartaoCredito, DiaFechamento = 20, DiaVencimento = 28, Limite = 1000 };
        a.Db.Contas.Add(outro);
        await a.Db.SaveChangesAsync();
        await a.Despesa(100, D(2026, 10, 1), conta: a.Cartao);  // fatura vencida
        await a.Despesa(250, D(2026, 10, 12), conta: outro);    // fatura aberta

        var t = await a.Cartoes.Todos();
        Assert.Equal(2, t.Cartoes.Count);
        Assert.Equal(100, t.TotalAPagar);
        Assert.Equal(250, t.TotalEmAberto);
        Assert.Equal(3000, t.LimiteTotal);
        Assert.Equal(350, t.LimiteUsadoTotal);
    }
}
