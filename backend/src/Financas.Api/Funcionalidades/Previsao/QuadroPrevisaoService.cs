using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Cartoes;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Previsao;

/// <summary>Quadro da previsão: mover uma conta ou fatura para ser paga com o dinheiro de outro período.</summary>
public class QuadroPrevisaoService(AppDbContext db, CartaoService cartoes)
{
    /// <summary>
    /// Planeja pagar uma conta (só esta ocorrência) ou fatura em outra data. Sem data, volta ao automático.
    /// Só muda a previsão: o lançamento, o vencimento e o saldo não são tocados.
    /// </summary>
    public async Task Mover(MoverDto dto)
    {
        PagamentoPlanejado? atual;
        if (dto.LancamentoId is { } id)
        {
            var l = await db.Lancamentos.Include(x => x.Conta).SingleOrDefaultAsync(x => x.Id == id)
                    ?? throw new RegraNegocioException("Conta não encontrada.");
            if (l.Status != StatusLancamento.Previsto || l.Tipo != TipoLancamento.Despesa || !l.Conta!.EhDisponivel)
                throw new RegraNegocioException("Só contas ainda não pagas podem ser movidas.");
            atual = await db.PagamentosPlanejados.SingleOrDefaultAsync(p => p.LancamentoId == id);
            atual ??= dto.PagarEm is null ? null : db.PagamentosPlanejados.Add(new PagamentoPlanejado { LancamentoId = id }).Entity;
        }
        else if (dto.CartaoId is { } cartao && dto.FaturaMes is { } mes)
        {
            mes = Calendario.InicioMes(mes);
            var fatura = await cartoes.Fatura(cartao, mes);
            if (fatura.Restante <= 0) throw new RegraNegocioException("Esta fatura já está paga.");
            atual = await db.PagamentosPlanejados.SingleOrDefaultAsync(p => p.CartaoId == cartao && p.FaturaMes == mes);
            atual ??= dto.PagarEm is null ? null : db.PagamentosPlanejados.Add(new PagamentoPlanejado { CartaoId = cartao, FaturaMes = mes }).Entity;
        }
        else throw new RegraNegocioException("Informe a conta ou a fatura.");

        if (atual is null) return;
        if (dto.PagarEm is { } data)
        {
            atual.PagarEm = data;
            atual.AtualizadoEm = DateTime.UtcNow;
        }
        else db.PagamentosPlanejados.Remove(atual);
        await db.SaveChangesAsync();
    }
}
