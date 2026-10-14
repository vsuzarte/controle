using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Compras;

/// <summary>Leitura das compras importadas: a nota com os itens e a busca "o que comprei".</summary>
public class ComprasConsultas(AppDbContext db)
{
    public async Task<CompraView> Obter(Guid id)
    {
        var c = await db.Compras.Include(x => x.Itens).ThenInclude(i => i.Categoria).SingleOrDefaultAsync(x => x.Id == id)
                ?? throw new RegraNegocioException("Compra não encontrada.");
        var conta = await db.Contas.FindAsync(c.ContaId);
        return new CompraView(c.Id, c.Loja, c.Emissao, c.Total, c.Desconto, c.FormaPagamentoNota, c.ContaId, conta?.Nome ?? "",
            c.Itens.OrderBy(i => i.Sequencia).Select(i => new ItemCompraView(i.Sequencia, i.Descricao, i.Codigo, i.Quantidade, i.Unidade,
                i.ValorUnitario, i.ValorPago, i.CategoriaId, i.Categoria!.Nome, i.Categoria.Cor)).ToList());
    }

    public async Task<List<CompraResumoView>> Recentes(int limite = 30)
    {
        var compras = await db.Compras.OrderByDescending(c => c.Emissao).Take(Math.Clamp(limite, 1, 200))
            .Select(c => new { c.Id, c.Loja, c.Emissao, c.Total, Itens = c.Itens.Count, c.ContaId }).ToListAsync();
        var contas = await db.Contas.ToDictionaryAsync(c => c.Id, c => c.Nome);
        return compras.Select(c => new CompraResumoView(c.Id, c.Loja, c.Emissao, c.Total, c.Itens, contas.GetValueOrDefault(c.ContaId, ""))).ToList();
    }

    /// <summary>Produtos comprados no período, do que mais gastou para o que menos. Com busca, só os que contêm o texto.</summary>
    public async Task<List<ProdutoView>> Produtos(string? busca, DateOnly de, DateOnly ate)
    {
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.ToDateTime(TimeOnly.MaxValue);
        var q = db.ItensCompra.Where(i => i.Compra!.Emissao >= inicio && i.Compra.Emissao <= fim);
        if (!string.IsNullOrWhiteSpace(busca)) q = q.Where(i => EF.Functions.Like(i.Descricao, $"%{busca.Trim()}%"));
        var itens = await q.Select(i => new { i.Descricao, i.Quantidade, i.Unidade, i.ValorPago, i.Compra!.Emissao, i.Compra.Loja }).ToListAsync();

        decimal Unitario(decimal valor, decimal qtd) => qtd > 0 ? decimal.Round(valor / qtd, 2) : valor;
        return itens.GroupBy(i => i.Descricao)
            .Select(g =>
            {
                var ultima = g.MaxBy(i => i.Emissao)!;
                var maisBarata = g.MinBy(i => Unitario(i.ValorPago, i.Quantidade))!;
                var quantidade = g.Sum(i => i.Quantidade);
                return new ProdutoView(g.Key, g.Count(), quantidade, ultima.Unidade, g.Sum(i => i.ValorPago),
                    Unitario(g.Sum(i => i.ValorPago), quantidade), Unitario(ultima.ValorPago, ultima.Quantidade),
                    DateOnly.FromDateTime(ultima.Emissao), ultima.Loja, Unitario(maisBarata.ValorPago, maisBarata.Quantidade), maisBarata.Loja);
            })
            .OrderByDescending(p => p.Total).ThenBy(p => p.Descricao)
            .Take(200)
            .ToList();
    }
}
