using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Previsao;

/// <summary>
/// Dias de pagamento: entradas recorrentes marcadas como "dia de pagamento" (por padrão, as da categoria Salário).
/// São elas que dividem a previsão em períodos; vêm do cadastro, nunca do código.
/// </summary>
public class DiasDePagamento(AppDbContext db)
{
    /// <summary>
    /// Datas de pagamento entre as datas. Usa a data real quando a ocorrência já existe e, antes do início
    /// da regra, a data que ela teria (só para marcar onde começa o ciclo).
    /// </summary>
    public async Task<List<DiaPagamento>> Entre(DateOnly de, DateOnly ate)
    {
        var salarios = await Regras();
        if (salarios.Count == 0) return [];

        var ids = salarios.Select(r => r.Id).ToList();
        var reais = await db.Lancamentos
            .Where(l => l.RegraId != null && ids.Contains(l.RegraId.Value) && l.Status != StatusLancamento.Cancelado)
            .Select(l => new { RegraId = l.RegraId!.Value, l.Sequencia, l.Data })
            .ToListAsync();

        var dias = new List<DiaPagamento>();
        foreach (var r in salarios)
        {
            for (var n = -60; n < 2000; n++)
            {
                var prevista = Calendario.Ocorrencia(r, n);
                if (prevista > ate.AddMonths(1) || (r.DataFim is { } f && prevista > f)) break;
                var data = reais.FirstOrDefault(x => x.RegraId == r.Id && x.Sequencia == n)?.Data ?? prevista;
                if (data >= de && data <= ate) dias.Add(new DiaPagamento(data, r.Descricao, r.Id));
            }
        }
        return dias.OrderBy(d => d.Data).ToList();
    }

    /// <summary>Quantas entradas marcadas como dia de pagamento estão ativas (cada uma = um pagamento por mês).</summary>
    public async Task<int> PorMes() => Math.Max(1, (await Regras()).Count);

    public async Task<List<Regra>> Regras()
    {
        var candidatas = await db.Regras.Include(r => r.Categoria).Include(r => r.Conta)
            .Where(r => r.Ativa && r.Modo == ModoRegra.Recorrente && r.Tipo == TipoLancamento.Receita)
            .ToListAsync();
        return candidatas.Where(r => r.Conta!.EhDisponivel && (r.IniciaPeriodo ?? EhSalario(r.Categoria!.Nome))).ToList();
    }

    public static bool EhSalario(string categoria) =>
        categoria.Trim().StartsWith("salário", StringComparison.OrdinalIgnoreCase)
        || categoria.Trim().StartsWith("salario", StringComparison.OrdinalIgnoreCase);
}
