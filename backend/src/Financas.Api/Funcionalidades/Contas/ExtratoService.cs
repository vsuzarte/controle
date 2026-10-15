using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Cartoes;
using Financas.Api.Funcionalidades.Lancamentos;
using Financas.Api.Funcionalidades.Regras;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>Extrato da conta, como o do banco: tudo o que passou por ela no mês, com o saldo depois de cada movimento.</summary>
public class ExtratoService(AppDbContext db, SaldoService saldos, RegraService regras, CartaoService cartoes)
{
    public async Task<ExtratoView> Obter(Guid contaId, DateOnly mes)
    {
        var conta = await db.Contas.FindAsync(contaId) ?? throw new RegraNegocioException("Conta não encontrada.");
        var inicio = Calendario.InicioMes(mes);
        var fim = Calendario.FimMes(inicio);
        await regras.Sincronizar(fim);

        var saldoInicio = await saldos.SaldoDaConta(conta, inicio.AddDays(-1));
        var lancamentos = await db.Lancamentos
            .Include(l => l.Conta).Include(l => l.ContaDestino).Include(l => l.Categoria).Include(l => l.Regra)
            .Where(l => (l.ContaId == contaId || l.ContaDestinoId == contaId) && l.Data >= inicio && l.Data <= fim)
            .OrderBy(l => l.Data).ThenBy(l => l.CriadoEm)
            .ToListAsync();

        // Saldo corrido na ordem em que as coisas aconteceram; só o que foi realizado mexe no saldo.
        var saldo = saldoInicio;
        decimal entrou = 0, saiu = 0, previsto = 0;
        var linhas = new List<ExtratoLinhaView>();
        foreach (var l in lancamentos)
        {
            var efeito = Efeito(l, contaId);
            decimal? depois = null;
            if (l.Status == StatusLancamento.Realizado)
            {
                saldo += efeito;
                depois = saldo;
                if (efeito >= 0) entrou += efeito; else saiu -= efeito;
            }
            else if (l.Status == StatusLancamento.Previsto) previsto += efeito;
            // No cartão não há "saldo depois": o que vale é o limite (no resumo) e a fatura de cada compra.
            linhas.Add(new ExtratoLinhaView(LancamentoView.De(l), efeito, conta.EhCartao ? null : depois));
        }
        return new ExtratoView(conta.Id, conta.Nome, conta.Tipo, inicio, saldoInicio, entrou, saiu, saldo, saldo + previsto, linhas,
            conta.EhCartao ? await ResumoDoCartao(conta) : null);
    }

    private async Task<CartaoNoExtratoView> ResumoDoCartao(Conta cartao)
    {
        var usado = (await cartoes.LimitesUsados()).GetValueOrDefault(cartao.Id);
        var faturas = await cartoes.Faturas(cartao.Id);
        var aberta = faturas.FirstOrDefault(f => f.Status == StatusFatura.Aberta);
        var aPagar = faturas.Where(f => f.Restante > 0 && f.Status is StatusFatura.Fechada or StatusFatura.Vencida).MinBy(f => f.Vencimento);
        static FaturaView? Leve(FaturaView? f) => f is null ? null : f with { Lancamentos = [], Pagamentos = [] };
        return new CartaoNoExtratoView(cartao.Limite, usado, cartao.Limite is { } l ? l - usado : null, Leve(aberta), Leve(aPagar));
    }

    /// <summary>Quanto o lançamento mexe nesta conta: receita entra, despesa sai, transferência sai da origem e entra no destino.</summary>
    public static decimal Efeito(Lancamento l, Guid contaId) => l.Tipo switch
    {
        TipoLancamento.Receita => l.Valor,
        TipoLancamento.Despesa => -l.Valor,
        _ => l.ContaId == contaId ? -l.Valor : l.Valor,
    };
}
