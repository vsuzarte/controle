using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Previsao;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Regras;

/// <summary>Lista das contas do mês e parcelamentos para a tela (só leitura).</summary>
public class RegraConsultas(AppDbContext db, IRelogio relogio)
{
    public async Task<List<RegraView>> Listar()
    {
        var regras = await db.Regras.Include(r => r.Categoria).Include(r => r.Conta)
            .OrderByDescending(r => r.Ativa).ThenBy(r => r.Modo).ThenBy(r => r.Descricao).ToListAsync();
        var hoje = relogio.Hoje;
        var lancs = await db.Lancamentos.Where(l => l.RegraId != null)
            .Select(l => new { l.RegraId, l.Status, l.Data, l.Valor }).ToListAsync();
        return regras.Select(r =>
        {
            var meus = lancs.Where(l => l.RegraId == r.Id).ToList();
            var proximo = meus.Where(l => l.Status == StatusLancamento.Previsto).OrderBy(l => l.Data).FirstOrDefault();
            return new RegraView(
                r.Id, r.Modo, r.Tipo, r.Descricao, r.CategoriaId, r.Categoria!.Nome, r.Categoria.Cor,
                r.ContaId, r.Conta!.Nome, r.FormaPagamento, r.Valor, r.ValorVariavel, r.Parcelas,
                r.Frequencia, r.Intervalo, r.DataInicio, r.DataFim, r.Ativa, r.Observacao,
                r.Vencimento ?? RegraVencimento.DiaFixo, r.DiaUtilNumero,
                r.PagoComRegraId, regras.FirstOrDefault(x => x.Id == r.PagoComRegraId)?.Descricao,
                r.Tipo == TipoLancamento.Receita && (r.IniciaPeriodo ?? DiasDePagamento.EhSalario(r.Categoria.Nome)),
                meus.Count(l => l.Status == StatusLancamento.Realizado),
                meus.Count(l => l.Status == StatusLancamento.Previsto),
                proximo?.Data,
                proximo?.Valor,
                meus.Count(l => l.Status == StatusLancamento.Previsto && l.Data < hoje));
        }).ToList();
    }
}
