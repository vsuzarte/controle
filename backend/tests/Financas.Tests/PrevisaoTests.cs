namespace Financas.Tests;

/// <summary>Previsão por períodos de pagamento, "pagar com" e o quadro.</summary>
public class PrevisaoTests
{
    [Fact]
    public async Task Previsao_mostra_o_periodo_atual_e_mais_um_mes_de_pagamentos()
    {
        using var a = new Ambiente(); // hoje 15/10, banco com 1000
        await a.Pagamento("Salário", 4000, D(2026, 11, 1), RegraVencimento.DiaUtil, 5);  // 09/11 (antes: 07/10)
        await a.Pagamento("Adiantamento", 2000, D(2026, 10, 20));
        await a.Recorrente(TipoLancamento.Despesa, a.Energia, 200, D(2026, 10, 25));
        await a.Despesa(100, D(2026, 10, 1), conta: a.Cartao);  // fatura de outubro, venceu em 10/10 sem pagar
        await a.Despesa(300, D(2026, 10, 5), conta: a.Cartao);  // fatura de novembro, vence 10/11 (próximo ciclo)

        var p = await a.Previsao.Obter();
        Assert.True(p.PorSalario);
        Assert.Equal(D(2026, 10, 7), p.Inicio);
        Assert.Equal(D(2026, 11, 19), p.Fim); // até antes do adiantamento de novembro
        Assert.Equal(3, p.Periodos.Count);

        var (atual, proximo) = (p.Periodos[0], p.Periodos[1]);
        Assert.True(atual.Atual);
        Assert.Equal(D(2026, 10, 19), atual.Fim);
        Assert.Contains(atual.Itens, i => i.Tipo == TipoItemPrevisao.Fatura && i.Atrasado && i.Valor == 100);
        Assert.Equal(900, atual.SaldoFim);

        Assert.Equal("Adiantamento", proximo.Titulo);
        Assert.Equal(D(2026, 10, 20), proximo.Inicio);
        Assert.Equal(900, proximo.SaldoInicio);
        Assert.Contains(proximo.Itens, i => i.Tipo == TipoItemPrevisao.Entrada && i.Valor == 2000);
        Assert.Contains(proximo.Itens, i => i.Tipo == TipoItemPrevisao.Conta && i.Valor == 200);
        Assert.DoesNotContain(proximo.Itens, i => i.Tipo == TipoItemPrevisao.Fatura); // vence 10/11: quem paga é o salário
        Assert.Equal(D(2026, 11, 8), proximo.Fim);
        Assert.Equal(900 + 2000 - 200, proximo.SaldoFim);

        // O salário de 09/11 cobre o que vence até antes do adiantamento de 20/11.
        var salario = p.Periodos[2];
        Assert.Equal("Salário", salario.Titulo);
        Assert.Equal((D(2026, 11, 9), D(2026, 11, 19)), (salario.Inicio, salario.Fim));
        Assert.Contains(salario.Itens, i => i.Tipo == TipoItemPrevisao.Entrada && i.Valor == 4000);
        Assert.Contains(salario.Itens, i => i.Tipo == TipoItemPrevisao.Fatura && i.Valor == 300);
        Assert.Equal(2700 + 4000 - 300, salario.SaldoFim);

        // A janela anda com a data: depois do salário, começa nele.
        var seguinte = await a.Previsao.Obter(D(2026, 11, 9));
        Assert.Equal(D(2026, 11, 9), seguinte.Inicio);
        Assert.Equal(2700, seguinte.SaldoInicio);
        Assert.Equal(3, seguinte.Periodos.Count);
    }

    [Fact]
    public async Task Periodo_de_hoje_todo_pago_aparece_quitado_e_a_janela_avanca()
    {
        using var a = new Ambiente(); // hoje 15/10
        await a.Pagamento("Salário", 4000, D(2026, 11, 1), RegraVencimento.DiaUtil, 5); // 07/10 (virtual), 09/11, 07/12
        await a.Pagamento("Adiantamento", 2000, D(2026, 10, 20));                      // 20/10, 20/11
        var luz = await a.Recorrente(TipoLancamento.Despesa, a.Energia, 200, D(2026, 10, 18));

        // Luz de 18/10 ainda pendente: o período de hoje continua aberto.
        var antes = await a.Previsao.Obter();
        Assert.False(antes.Periodos[0].Quitado);
        Assert.Equal(D(2026, 11, 19), antes.Fim);

        var l = await a.Db.Lancamentos.SingleAsync(x => x.RegraId == luz.Id && x.Sequencia == 1);
        await a.Lancamentos.Confirmar(l.Id, new ConfirmarDto(D(2026, 10, 15), 200, null, null));

        var depois = await a.Previsao.Obter();
        Assert.True(depois.Periodos[0].Quitado);
        Assert.Equal(4, depois.Periodos.Count);           // quitado + um mês de pagamentos
        Assert.Equal(D(2026, 10, 20), depois.Inicio);     // a previsão já começa no próximo pagamento
        Assert.Equal(D(2026, 12, 6), depois.Fim);         // até antes do salário de dezembro (07/12)
        Assert.Equal(800, depois.SaldoInicio);

        // Configurável: só até o próximo pagamento e sem pular o período quitado.
        var cfg = await a.ConfigPrevisao.Salvar(new ConfigPrevisaoDto(1, false));
        Assert.Equal(3, cfg.Padrao);
        var curta = await a.Previsao.Obter();
        Assert.Single(curta.Periodos);
        Assert.False(curta.Periodos[0].Quitado);
        Assert.Equal(D(2026, 10, 19), curta.Fim);

        // Voltar ao padrão.
        await a.ConfigPrevisao.Salvar(new ConfigPrevisaoDto(null, true));
        Assert.Equal(D(2026, 12, 6), (await a.Previsao.Obter()).Fim);
    }

    [Fact]
    public async Task Intervalo_escolhido_divide_pelos_pagamentos_dentro_dele()
    {
        using var a = new Ambiente();
        await a.Pagamento("Adiantamento", 2000, D(2026, 10, 20));
        await a.Recorrente(TipoLancamento.Despesa, a.Energia, 200, D(2026, 10, 25));

        var p = await a.Previsao.Obter(de: D(2026, 10, 15), ate: D(2026, 11, 30));
        Assert.True(p.Personalizado);
        Assert.Equal([D(2026, 10, 15), D(2026, 10, 20), D(2026, 11, 20)], p.Periodos.Select(x => x.Inicio).ToList());
        Assert.Equal(D(2026, 11, 30), p.Fim);
        Assert.Equal(1000 + 2000 * 2 - 200 * 2, p.SaldoFim);
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Previsao.Obter(de: D(2026, 11, 1), ate: D(2026, 10, 1)));
    }

    [Fact]
    public async Task Conta_que_vence_no_fim_de_semana_e_paga_com_o_dinheiro_do_proximo_dia_util()
    {
        using var a = new Ambiente(); // hoje 15/10/2026
        await a.Pagamento("Salário", 4000, D(2026, 11, 1), RegraVencimento.DiaUtil, 5); // 09/11 (segunda)
        await a.Pagamento("Adiantamento", 2000, D(2026, 10, 20));
        var escola = new Categoria { Nome = "Educação", Tipo = TipoCategoria.Despesa };
        a.Db.Categorias.Add(escola);
        await a.Db.SaveChangesAsync();
        await a.Recorrente(TipoLancamento.Despesa, escola, 814.67m, D(2026, 11, 7)); // sábado

        Assert.Equal(D(2026, 11, 9), Calendario.ProximoDiaUtil(D(2026, 11, 7)));
        Assert.Equal(D(2026, 11, 3), Calendario.ProximoDiaUtil(D(2026, 11, 2))); // Finados

        var p = await a.Previsao.Obter();
        var adiantamento = p.Periodos.Single(x => x.Inicio == D(2026, 10, 20));
        var salario = p.Periodos.Single(x => x.Inicio == D(2026, 11, 9));
        Assert.DoesNotContain(adiantamento.Itens, i => i.Descricao == "Educação");
        var item = Assert.Single(salario.Itens, i => i.Descricao == "Educação");
        Assert.Equal(D(2026, 11, 7), item.Data);
        Assert.Equal(D(2026, 11, 9), item.PagavelAte);

        var alerta = (await a.Resumo.Obter()).Alertas.Single(x => x.Descricao == "Educação");
        Assert.Equal(D(2026, 11, 9), alerta.Data);
        Assert.Equal(D(2026, 11, 7), alerta.VenceEm);
    }

    [Fact]
    public async Task Conta_marcada_para_pagar_com_o_salario_vai_para_o_periodo_dele_e_avisa_o_atraso()
    {
        using var a = new Ambiente(); // hoje 15/10/2026
        var salario = await a.Pagamento("Salário", 4000, D(2026, 11, 1), RegraVencimento.DiaUtil, 5); // 09/11
        await a.Pagamento("Adiantamento", 2000, D(2026, 10, 20));
        var escola = await a.Recorrente(TipoLancamento.Despesa, a.Energia, 800, D(2026, 11, 5)); // quinta, antes do salário

        // Automático: vence 05/11, cai no período do adiantamento.
        var auto = await a.Previsao.Obter();
        Assert.Contains(auto.Periodos.Single(p => p.Inicio == D(2026, 10, 20)).Itens, i => i.RegraId == escola.Id);

        // Não dá para "pagar com" outra conta.
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Regras.Editar(escola.Id, new EditarRegraDto("Escola", a.Energia.Id,
            a.Banco.Id, FormaPagamento.Boleto, 800, false, null, null, false, null, PagoComRegraId: escola.Id)));

        await a.Regras.Editar(escola.Id, new EditarRegraDto("Escola", a.Energia.Id, a.Banco.Id, FormaPagamento.Boleto, 800, false,
            null, null, false, null, PagoComRegraId: salario.Id));

        var p = await a.Previsao.Obter();
        Assert.DoesNotContain(p.Periodos.Single(x => x.Inicio == D(2026, 10, 20)).Itens, i => i.RegraId == escola.Id);
        var item = Assert.Single(p.Periodos.Single(x => x.Inicio == D(2026, 11, 9)).Itens, i => i.RegraId == escola.Id);
        Assert.Equal("Salário", item.PagoCom);
        Assert.Equal(D(2026, 11, 9), item.PagarEm);
        Assert.Equal(4, item.DiasDepoisDoVencimento);
        Assert.False(item.Atrasado);
    }

    [Fact]
    public async Task Mover_no_quadro_vale_so_para_aquela_conta_ou_fatura()
    {
        using var a = new Ambiente(); // hoje 15/10/2026, banco 1000
        await a.Pagamento("Salário", 4000, D(2026, 11, 1), RegraVencimento.DiaUtil, 5); // 09/11
        await a.Pagamento("Adiantamento", 2000, D(2026, 10, 20));
        var luz = await a.Recorrente(TipoLancamento.Despesa, a.Energia, 300, D(2026, 10, 23));
        await a.Despesa(500, D(2026, 9, 20), conta: a.Cartao); // fatura de outubro, venceu 10/10
        var outubro = await a.Db.Lancamentos.SingleAsync(l => l.RegraId == luz.Id && l.Sequencia == 1);
        var novembro = await a.Db.Lancamentos.SingleAsync(l => l.RegraId == luz.Id && l.Sequencia == 2);

        // Luz de outubro (23/10) vai para o salário de 09/11; a de novembro continua no lugar.
        await a.Quadro.Mover(new MoverDto(outubro.Id, null, null, D(2026, 11, 9)));
        // Fatura atrasada vai para o adiantamento.
        await a.Quadro.Mover(new MoverDto(null, a.Cartao.Id, D(2026, 10, 1), D(2026, 10, 20)));

        var p = await a.Previsao.Obter();
        var adiant = p.Periodos.Single(x => x.Inicio == D(2026, 10, 20));
        var salario = p.Periodos.Single(x => x.Inicio == D(2026, 11, 9));
        Assert.DoesNotContain(p.Periodos[0].Itens, i => i.Tipo == TipoItemPrevisao.Fatura);
        var fatura = Assert.Single(adiant.Itens, i => i.Tipo == TipoItemPrevisao.Fatura);
        Assert.True(fatura.Movido);
        Assert.Equal(7, fatura.DiasDepoisDoVencimento); // vencia sáb 10/10; 12/10 feriado: prazo sem juros 13/10
        Assert.True(fatura.Atrasado);
        Assert.DoesNotContain(adiant.Itens, i => i.LancamentoId == outubro.Id);
        var movida = Assert.Single(salario.Itens, i => i.LancamentoId == outubro.Id);
        Assert.True(movida.Movido);
        Assert.Equal(17, movida.DiasDepoisDoVencimento); // vence sexta 23/10
        Assert.Equal(D(2026, 11, 9), movida.PagarEm);
        // Sem nada pendente até 19/10, o período de hoje fica quitado e a janela avança até o adiantamento de 20/11,
        // onde a luz de novembro aparece no lugar de sempre (mover a de outubro não mexeu nela).
        Assert.True(p.Periodos[0].Quitado);
        Assert.Contains(p.Periodos.Single(x => x.Inicio == D(2026, 11, 20)).Itens, i => i.LancamentoId == novembro.Id && !i.Movido);
        Assert.Equal(1000 - 0, p.Periodos[0].SaldoFim); // nada mais sai até 19/10
        Assert.Equal(1000 + 2000 - 500, adiant.SaldoFim);

        // Nada no lançamento muda: é só planejamento.
        var depois = await a.Db.Lancamentos.FindAsync(outubro.Id);
        Assert.Equal((D(2026, 10, 23), 300m, StatusLancamento.Previsto), (depois!.Data, depois.Valor, depois.Status));

        // Voltar ao automático.
        await a.Quadro.Mover(new MoverDto(outubro.Id, null, null, null));
        Assert.Contains((await a.Previsao.Obter()).Periodos.Single(x => x.Inicio == D(2026, 10, 20)).Itens, i => i.LancamentoId == outubro.Id);

        // O que já foi pago não se move.
        await a.Lancamentos.Confirmar(novembro.Id, new ConfirmarDto(D(2026, 10, 15), 300, null, null));
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Quadro.Mover(new MoverDto(novembro.Id, null, null, D(2026, 10, 20))));
    }

    [Fact]
    public async Task Sem_salario_cadastrado_a_previsao_e_o_mes()
    {
        using var a = new Ambiente();
        var p = await a.Previsao.Obter();
        Assert.False(p.PorSalario);
        Assert.Equal(D(2026, 10, 1), p.Inicio);
        Assert.Equal(D(2026, 10, 31), p.Fim);
        Assert.Equal(1000, p.SaldoFim);
    }
}
