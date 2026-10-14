using Financas.Api.Dados;
using Financas.Api.Dominio;
using Financas.Api.Funcionalidades.Regras;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Lancamentos;

/// <summary>Consultas de lançamentos para as telas (só leitura). As alterações ficam no <see cref="LancamentoService"/>.</summary>
public class LancamentoConsultas(AppDbContext db, IRelogio relogio, RegraService regras)
{
    public async Task<List<LancamentoView>> Listar(string? mes, Guid? contaId, Guid? categoriaId,
        TipoLancamento? tipo, StatusLancamento? status, string? busca, DateOnly? desde = null)
    {
        var q = db.Lancamentos.Include(l => l.Conta).Include(l => l.ContaDestino)
            .Include(l => l.Categoria).Include(l => l.Regra).AsQueryable();
        if (!string.IsNullOrEmpty(mes))
        {
            var inicio = Calendario.ParseMes(mes);
            var fim = Calendario.FimMes(inicio);
            await regras.Sincronizar(fim);
            // "desde": busca em vários meses (do mês escolhido para trás até essa data).
            var de = desde is { } d && d < inicio ? d : inicio;
            q = q.Where(l => l.Data >= de && l.Data <= fim);
        }
        if (contaId is { } c) q = q.Where(l => l.ContaId == c || l.ContaDestinoId == c);
        if (categoriaId is { } cat) q = q.Where(l => l.CategoriaId == cat);
        if (tipo is { } t) q = q.Where(l => l.Tipo == t);
        if (status is { } s) q = q.Where(l => l.Status == s);
        var texto = string.IsNullOrWhiteSpace(busca) ? null : $"%{busca.Trim()}%";
        // A busca olha a descrição do lançamento e também os itens das notas fiscais (ex.: "coca" acha a compra do mercado).
        if (texto is not null)
            q = q.Where(l => EF.Functions.Like(l.Descricao, texto)
                             || db.ItensCompra.Any(i => i.LancamentoId == l.Id && EF.Functions.Like(i.Descricao, texto)));
        var lista = await q.OrderByDescending(l => l.Data).ThenByDescending(l => l.CriadoEm).Take(2000).ToListAsync();
        if (texto is null) return lista.Select(LancamentoView.De).ToList();

        var ids = lista.Where(l => l.CompraId != null).Select(l => l.Id).ToList();
        var itens = (await db.ItensCompra.Where(i => i.LancamentoId != null && ids.Contains(i.LancamentoId.Value) && EF.Functions.Like(i.Descricao, texto))
                .OrderBy(i => i.Sequencia).ToListAsync())
            .GroupBy(i => i.LancamentoId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(i => new ItemEncontradoView(i.Descricao, i.Quantidade, i.Unidade, i.ValorPago)).ToList());
        return lista.Select(l => LancamentoView.De(l) with { ItensEncontrados = itens.GetValueOrDefault(l.Id) }).ToList();
    }

    /// <summary>
    /// Previstos próximos da mesma categoria: usado pelo formulário para sugerir
    /// "confirmar o previsto" em vez de criar um lançamento duplicado.
    /// </summary>
    public async Task<List<LancamentoView>> Pendentes(Guid categoriaId)
    {
        var de = relogio.Hoje.AddDays(-45);
        var ate = relogio.Hoje.AddDays(20);
        var lista = await db.Lancamentos.Include(l => l.Conta).Include(l => l.Categoria).Include(l => l.Regra)
            .Where(l => l.CategoriaId == categoriaId && l.Status == StatusLancamento.Previsto && l.Data >= de && l.Data <= ate)
            .OrderBy(l => l.Data).ToListAsync();
        return lista.Select(LancamentoView.De).ToList();
    }

    public async Task<LancamentoView> Obter(Guid id)
    {
        var l = await db.Lancamentos.Include(x => x.Conta).Include(x => x.ContaDestino)
            .Include(x => x.Categoria).Include(x => x.Regra).SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new RegraNegocioException("Lançamento não encontrado.");
        return LancamentoView.De(l);
    }
}
