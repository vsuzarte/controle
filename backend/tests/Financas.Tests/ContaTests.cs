namespace Financas.Tests;

/// <summary>Contas: mudar o tipo e desativar ("excluir") só travam com pendências; nada é apagado.</summary>
public class ContaTests
{
    private static ContaDto Dto(Conta c, TipoConta? tipo = null, bool ativa = true) =>
        new(c.Nome, tipo ?? c.Tipo, c.SaldoInicial, c.Cor, c.DiaFechamento, c.DiaVencimento, ativa, c.Limite);

    [Fact]
    public async Task Lancamento_cancelado_nao_impede_mudar_o_tipo()
    {
        using var a = new Ambiente();
        var l = await a.Despesa(50, D(2026, 10, 10));
        await a.Lancamentos.Cancelar(l.Id, "lançado na conta errada");

        // Só havia um lançamento cancelado: pode virar até vale.
        var conta = await a.Contas.Editar(a.Banco.Id, Dto(a.Banco, TipoConta.Beneficio));
        Assert.Equal(TipoConta.Beneficio, conta.Tipo);
    }

    [Fact]
    public async Task Conta_com_historico_muda_entre_tipos_comuns_mas_nao_vira_cartao_ou_vale()
    {
        using var a = new Ambiente();
        await a.Despesa(50, D(2026, 10, 10));

        Assert.Equal(TipoConta.CarteiraDigital, (await a.Contas.Editar(a.Banco.Id, Dto(a.Banco, TipoConta.CarteiraDigital))).Tipo);
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => a.Contas.Editar(a.Banco.Id, Dto(a.Banco, TipoConta.Beneficio)));
        Assert.Contains("Crie uma conta nova", erro.Message);
    }

    [Fact]
    public async Task Pendencias_impedem_mudar_tipo_e_desativar_ate_serem_resolvidas()
    {
        using var a = new Ambiente();
        var previsto = await a.Despesa(80, D(2026, 10, 25), status: StatusLancamento.Previsto);

        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => a.Contas.Editar(a.Banco.Id, Dto(a.Banco, TipoConta.Poupanca)));
        Assert.Contains("1 lançamento(s) previsto(s)", erro.Message);
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Contas.Desativar(a.Banco.Id, null));
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Contas.Editar(a.Banco.Id, Dto(a.Banco, ativa: false)));

        await a.Lancamentos.Cancelar(previsto.Id, "não vai acontecer");
        var desativada = await a.Contas.Desativar(a.Banco.Id, "fechei a conta");
        Assert.False(desativada.Ativa);
        Assert.NotNull(await a.Db.Contas.FindAsync(a.Banco.Id)); // desativar não apaga
        Assert.Contains(await a.Db.AuditLogs.ToListAsync(), x => x.EntidadeId == a.Banco.Id && x.Operacao == "Desativação");

        Assert.True((await a.Contas.Reativar(a.Banco.Id)).Ativa);
    }

    [Fact]
    public async Task Conta_do_mes_ativa_e_fatura_sem_pagar_sao_pendencias()
    {
        using var a = new Ambiente();
        var aluguel = await a.Recorrente(TipoLancamento.Despesa, a.Mercado, 900, D(2026, 11, 5));
        await a.Regras.Desativar(aluguel.Id, new DesativarRegraDto(false, null));
        var erro = await Assert.ThrowsAsync<RegraNegocioException>(() => a.Contas.Desativar(a.Banco.Id, null));
        Assert.Contains("previsto", erro.Message); // a regra parou, mas os previstos já gerados continuam

        await a.Despesa(120, D(2026, 10, 14), conta: a.Cartao);
        erro = await Assert.ThrowsAsync<RegraNegocioException>(() => a.Contas.Desativar(a.Cartao.Id, null));
        Assert.Contains("fatura sem pagar", erro.Message);
    }

    [Fact]
    public async Task Extrato_mostra_cada_movimento_com_o_saldo_depois_dele()
    {
        using var a = new Ambiente();
        var extratos = new ExtratoService(a.Db, new SaldoService(a.Db), a.Regras);
        await a.Despesa(100, D(2026, 9, 20));                                   // mês anterior: entra no saldo inicial
        var mercado = await a.Despesa(50, D(2026, 10, 2));
        await a.Lancamentos.Criar(new NovoLancamentoDto(TipoLancamento.Receita, "Freela", 300, D(2026, 10, 5),
            a.Banco.Id, null, a.Salario.Id, FormaPagamento.Pix, null, null, null, null));
        await a.Lancamentos.Criar(new NovoLancamentoDto(TipoLancamento.Transferencia, "Guardar", 200, D(2026, 10, 6),
            a.Banco.Id, a.Poupanca.Id, null, FormaPagamento.Transferencia, null, null, null, null));
        await a.Lancamentos.Estornar(mercado.Id, new EstornoDto(10, D(2026, 10, 7), "troca"));
        await a.Despesa(80, D(2026, 10, 25), status: StatusLancamento.Previsto);  // previsto: não mexe no saldo
        await a.Despesa(60, D(2026, 10, 3), conta: a.Poupanca);                  // outra conta: não aparece

        var e = await extratos.Obter(a.Banco.Id, D(2026, 10, 1));
        Assert.Equal(900m, e.SaldoInicio);
        Assert.Equal([-50m, 300m, -200m, 10m, -80m], e.Linhas.Select(l => l.Efeito));
        Assert.Equal([850m, 1150m, 950m, 960m, null], e.Linhas.Select(l => l.Saldo));
        Assert.Equal((310m, 250m, 960m, 880m), (e.Entrou, e.Saiu, e.SaldoFim, e.SaldoPrevisto));
        Assert.Equal(960m, (await a.Contas.Listar()).Single(c => c.Id == a.Banco.Id).Saldo); // bate com o saldo da conta

        var poupanca = await extratos.Obter(a.Poupanca.Id, D(2026, 10, 1));
        Assert.Equal([-60m, 200m], poupanca.Linhas.Select(l => l.Efeito)); // a transferência entra na poupança
    }
}
