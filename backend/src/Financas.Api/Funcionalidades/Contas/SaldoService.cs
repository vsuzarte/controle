using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>Saldo das contas a partir do que foi realizado (previstos não mexem no saldo).</summary>
public class SaldoService(AppDbContext db)
{
    /// <summary>Movimento líquido realizado de cada conta até a data (sem o saldo inicial).</summary>
    public async Task<Dictionary<Guid, decimal>> Saldos(DateOnly ate)
    {
        var movimentos = await db.Lancamentos
            .Where(l => l.Status == StatusLancamento.Realizado && l.Data <= ate)
            .Select(l => new { l.Tipo, l.Valor, l.ContaId, l.ContaDestinoId })
            .ToListAsync();

        var saldos = new Dictionary<Guid, decimal>();
        void Somar(Guid conta, decimal v) => saldos[conta] = saldos.GetValueOrDefault(conta) + v;
        foreach (var m in movimentos)
        {
            switch (m.Tipo)
            {
                case TipoLancamento.Receita: Somar(m.ContaId, m.Valor); break;
                case TipoLancamento.Despesa: Somar(m.ContaId, -m.Valor); break;
                case TipoLancamento.Transferencia:
                    Somar(m.ContaId, -m.Valor);
                    Somar(m.ContaDestinoId!.Value, m.Valor);
                    break;
            }
        }
        return saldos;
    }

    /// <summary>Saldo de uma conta no fim do dia informado, com o saldo inicial.</summary>
    public async Task<decimal> SaldoDaConta(Conta conta, DateOnly ate)
    {
        var movimentos = await db.Lancamentos
            .Where(l => l.Status == StatusLancamento.Realizado && l.Data <= ate && (l.ContaId == conta.Id || l.ContaDestinoId == conta.Id))
            .Select(l => new { l.Tipo, l.Valor, l.ContaId })
            .ToListAsync();
        return conta.SaldoInicial + movimentos.Sum(m =>
            m.Tipo == TipoLancamento.Receita ? m.Valor
            : m.Tipo == TipoLancamento.Despesa ? -m.Valor
            : m.ContaId == conta.Id ? -m.Valor : m.Valor);
    }
}
