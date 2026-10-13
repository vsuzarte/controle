using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Servicos;

public enum StatusFatura { Aberta, Fechada, Paga, Vencida, Futura }

public record FaturaView(
    Guid CartaoId,
    string Cartao,
    DateOnly Mes,
    DateOnly Fechamento,
    DateOnly Vencimento,
    decimal Total,
    decimal Pago,
    decimal Restante,
    StatusFatura Status,
    List<LancamentoView> Lancamentos,
    List<LancamentoView> Pagamentos);

/// <summary>
/// A fatura não é uma tabela: é a soma dos lançamentos do cartão com aquele FaturaMes.
/// Pagar a fatura é uma transferência da conta bancária para o cartão. O gasto já foi
/// contado por categoria na compra, então o pagamento não é despesa (sem contar duas vezes).
/// </summary>
public class CartaoService(AppDbContext db, IRelogio relogio)
{
    public async Task<FaturaView> Fatura(Guid cartaoId, DateOnly mes)
    {
        var cartao = await Cartao(cartaoId);
        mes = Calendario.InicioMes(mes);
        var itens = await db.Lancamentos
            .Include(l => l.Conta).Include(l => l.Categoria).Include(l => l.Regra).Include(l => l.ContaDestino)
            .Where(l => l.FaturaMes == mes && (l.ContaId == cartaoId || l.ContaDestinoId == cartaoId))
            .OrderBy(l => l.Data).ThenBy(l => l.CriadoEm)
            .ToListAsync();
        return Montar(cartao, mes, itens);
    }

    /// <summary>Faturas com movimento ou ainda não pagas, da mais recente para a mais antiga.</summary>
    public async Task<List<FaturaView>> Faturas(Guid cartaoId)
    {
        var cartao = await Cartao(cartaoId);
        var itens = await db.Lancamentos
            .Include(l => l.Conta).Include(l => l.Categoria).Include(l => l.Regra).Include(l => l.ContaDestino)
            .Where(l => l.FaturaMes != null && (l.ContaId == cartaoId || l.ContaDestinoId == cartaoId))
            .ToListAsync();
        var atual = Calendario.FaturaDaCompra(cartao, relogio.Hoje);
        return itens.Select(l => l.FaturaMes!.Value).Append(atual).Distinct()
            .OrderByDescending(m => m)
            .Select(m => Montar(cartao, m, itens.Where(l => l.FaturaMes == m).OrderBy(l => l.Data).ToList()))
            .ToList();
    }

    public async Task<Lancamento> Pagar(Guid cartaoId, DateOnly mes, PagarFaturaDto dto)
    {
        var cartao = await Cartao(cartaoId);
        var origem = await db.Contas.FindAsync(dto.ContaOrigemId) ?? throw new RegraNegocioException("Conta de origem não encontrada.");
        if (origem.EhCartao) throw new RegraNegocioException("A fatura deve ser paga a partir de uma conta, não de outro cartão.");
        if (!origem.Ativa) throw new RegraNegocioException($"A conta '{origem.Nome}' está desativada.");
        if (dto.Data > relogio.Hoje) throw new RegraNegocioException("A data do pagamento não pode ser futura.");

        mes = Calendario.InicioMes(mes);
        var pagamento = new Lancamento
        {
            Tipo = TipoLancamento.Transferencia,
            Status = StatusLancamento.Realizado,
            Descricao = $"Pagamento fatura {cartao.Nome} {mes:MM/yyyy}",
            Valor = Validar.ValorPositivo(dto.Valor),
            Data = dto.Data,
            ContaId = origem.Id,
            ContaDestinoId = cartao.Id,
            FormaPagamento = FormaPagamento.Transferencia,
            FaturaMes = mes,
        };
        db.Lancamentos.Add(pagamento);
        db.DefinirAuditoria("Pagamento de fatura");
        await db.SaveChangesAsync();
        return pagamento;
    }

    private FaturaView Montar(Conta cartao, DateOnly mes, List<Lancamento> itens)
    {
        var validos = itens.Where(l => l.Valido).ToList();
        var compras = validos.Where(l => l.ContaId == cartao.Id && l.Tipo != TipoLancamento.Transferencia).ToList();
        var pagamentos = validos.Where(l => l.Tipo == TipoLancamento.Transferencia && l.ContaDestinoId == cartao.Id).ToList();

        var total = compras.Sum(l => l.Tipo == TipoLancamento.Despesa ? l.Valor : -l.Valor);
        var pago = pagamentos.Where(p => p.Status == StatusLancamento.Realizado).Sum(p => p.Valor);
        var restante = Math.Max(0, total - pago);
        var fechamento = Calendario.FechamentoDaFatura(cartao, mes);
        var vencimento = Calendario.VencimentoDaFatura(cartao, mes);
        var hoje = relogio.Hoje;

        var status = total > 0 && restante == 0 ? StatusFatura.Paga
            : vencimento < hoje && restante > 0 ? StatusFatura.Vencida
            : hoje >= fechamento ? StatusFatura.Fechada
            : Calendario.FaturaDaCompra(cartao, hoje) == mes ? StatusFatura.Aberta
            : StatusFatura.Futura;

        return new FaturaView(cartao.Id, cartao.Nome, mes, fechamento, vencimento, total, pago, restante, status,
            itens.Where(l => l.ContaId == cartao.Id && l.Tipo != TipoLancamento.Transferencia).Select(LancamentoView.De).ToList(),
            itens.Where(l => l.Tipo == TipoLancamento.Transferencia).Select(LancamentoView.De).ToList());
    }

    private async Task<Conta> Cartao(Guid id)
    {
        var c = await db.Contas.FindAsync(id) ?? throw new RegraNegocioException("Cartão não encontrado.");
        return c.EhCartao ? c : throw new RegraNegocioException($"'{c.Nome}' não é um cartão de crédito.");
    }
}
