using Financas.Api.Dominio;
using Financas.Api.Servicos;
using Microsoft.EntityFrameworkCore;
using static Financas.Tests.Ambiente;

namespace Financas.Tests;

/// <summary>Dia útil, juros, vale-refeição, previsão por ciclo de salário e relatórios.</summary>
public class NovidadesTests
{
    private static Task<Regra> Salario(Ambiente a, string nome, decimal valor, DateOnly inicio, RegraVencimento? venc = null, int? diaUtil = null) =>
        a.Regras.Criar(new RegraDto(ModoRegra.Recorrente, TipoLancamento.Receita, nome, a.Salario.Id, a.Banco.Id, FormaPagamento.Pix,
            valor, false, null, Frequencia.Mensal, 1, inicio, null, null, venc, diaUtil));

    [Fact]
    public void Dias_uteis_pulam_fim_de_semana_e_feriados_nacionais()
    {
        Assert.Equal(D(2026, 4, 5), Calendario.Pascoa(2026));
        Assert.True(Calendario.Feriado(D(2026, 4, 3)));   // Sexta-feira Santa
        Assert.True(Calendario.Feriado(D(2026, 2, 16)));  // Carnaval
        Assert.True(Calendario.Feriado(D(2026, 11, 20))); // Consciência Negra
        // Novembro/2026: dia 1 é domingo e dia 2 é Finados -> 3,4,5,6,9.
        Assert.Equal(D(2026, 11, 9), Calendario.DataNoMes(D(2026, 11, 1), RegraVencimento.DiaUtil, 5));
        Assert.Equal(D(2026, 10, 7), Calendario.DataNoMes(D(2026, 10, 1), RegraVencimento.DiaUtil, 5));
        Assert.Equal(D(2026, 10, 30), Calendario.DataNoMes(D(2026, 10, 1), RegraVencimento.UltimoDiaUtil, null));
        Assert.Equal(D(2027, 2, 28), Calendario.DataNoMes(D(2027, 2, 1), RegraVencimento.UltimoDia, null));
    }

    [Fact]
    public async Task Regra_no_quinto_dia_util_gera_as_datas_certas()
    {
        using var a = new Ambiente();
        // Pedido em 15/10, quinto dia útil de outubro já passou (07/10): começa em novembro.
        var r = await Salario(a, "Salário", 4000, D(2026, 10, 15), RegraVencimento.DiaUtil, 5);
        var datas = await a.Db.Lancamentos.Where(l => l.RegraId == r.Id).OrderBy(l => l.Sequencia).Select(l => l.Data).ToListAsync();
        Assert.Equal([D(2026, 11, 9), D(2026, 12, 7)], datas);
    }

    [Fact]
    public async Task Mudar_regra_para_dia_util_recalcula_so_os_previstos_deste_mes_em_diante()
    {
        using var a = new Ambiente();
        var r = await Salario(a, "Salário", 4000, D(2026, 9, 9));
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

    [Fact]
    public async Task Previsao_mostra_o_periodo_atual_e_mais_um_mes_de_pagamentos()
    {
        using var a = new Ambiente(); // hoje 15/10, banco com 1000
        await Salario(a, "Salário", 4000, D(2026, 11, 1), RegraVencimento.DiaUtil, 5);  // 09/11 (antes: 07/10)
        await Salario(a, "Adiantamento", 2000, D(2026, 10, 20));
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
        await Salario(a, "Salário", 4000, D(2026, 11, 1), RegraVencimento.DiaUtil, 5); // 07/10 (virtual), 09/11, 07/12
        await Salario(a, "Adiantamento", 2000, D(2026, 10, 20));                      // 20/10, 20/11
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
        var cfg = await a.Previsao.SalvarConfig(new ConfigPrevisaoDto(1, false));
        Assert.Equal(3, cfg.Padrao);
        var curta = await a.Previsao.Obter();
        Assert.Single(curta.Periodos);
        Assert.False(curta.Periodos[0].Quitado);
        Assert.Equal(D(2026, 10, 19), curta.Fim);

        // Voltar ao padrão.
        await a.Previsao.SalvarConfig(new ConfigPrevisaoDto(null, true));
        Assert.Equal(D(2026, 12, 6), (await a.Previsao.Obter()).Fim);
    }

    [Fact]
    public async Task Intervalo_escolhido_divide_pelos_pagamentos_dentro_dele()
    {
        using var a = new Ambiente();
        await Salario(a, "Adiantamento", 2000, D(2026, 10, 20));
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
        await Salario(a, "Salário", 4000, D(2026, 11, 1), RegraVencimento.DiaUtil, 5); // 09/11 (segunda)
        await Salario(a, "Adiantamento", 2000, D(2026, 10, 20));
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
        var salario = await Salario(a, "Salário", 4000, D(2026, 11, 1), RegraVencimento.DiaUtil, 5); // 09/11
        await Salario(a, "Adiantamento", 2000, D(2026, 10, 20));
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
        await Salario(a, "Salário", 4000, D(2026, 11, 1), RegraVencimento.DiaUtil, 5); // 09/11
        await Salario(a, "Adiantamento", 2000, D(2026, 10, 20));
        var luz = await a.Recorrente(TipoLancamento.Despesa, a.Energia, 300, D(2026, 10, 23));
        await a.Despesa(500, D(2026, 9, 20), conta: a.Cartao); // fatura de outubro, venceu 10/10
        var outubro = await a.Db.Lancamentos.SingleAsync(l => l.RegraId == luz.Id && l.Sequencia == 1);
        var novembro = await a.Db.Lancamentos.SingleAsync(l => l.RegraId == luz.Id && l.Sequencia == 2);

        // Luz de outubro (23/10) vai para o salário de 09/11; a de novembro continua no lugar.
        await a.Previsao.Mover(new MoverDto(outubro.Id, null, null, D(2026, 11, 9)));
        // Fatura atrasada vai para o adiantamento.
        await a.Previsao.Mover(new MoverDto(null, a.Cartao.Id, D(2026, 10, 1), D(2026, 10, 20)));

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
        await a.Previsao.Mover(new MoverDto(outubro.Id, null, null, null));
        Assert.Contains((await a.Previsao.Obter()).Periodos.Single(x => x.Inicio == D(2026, 10, 20)).Itens, i => i.LancamentoId == outubro.Id);

        // O que já foi pago não se move.
        await a.Lancamentos.Confirmar(novembro.Id, new ConfirmarDto(D(2026, 10, 15), 300, null, null));
        await Assert.ThrowsAsync<RegraNegocioException>(() => a.Previsao.Mover(new MoverDto(novembro.Id, null, null, D(2026, 10, 20))));
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

    [Fact]
    public async Task Vencimentos_seguem_a_janela_da_previsao_e_incluem_faturas()
    {
        using var a = new Ambiente();
        await Salario(a, "Salário", 4000, D(2026, 10, 7), RegraVencimento.DiaUtil, 5);
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
