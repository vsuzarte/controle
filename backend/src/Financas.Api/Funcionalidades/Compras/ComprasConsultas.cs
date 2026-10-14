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

    /// <summary>
    /// Tudo o que foi comprado no período, do que mais gastou para o que menos: itens das notas fiscais e também as
    /// compras lançadas à mão (despesas avulsas, sem nota). Com busca, só o que contém o texto.
    /// </summary>
    public async Task<List<ProdutoView>> Produtos(string? busca, DateOnly de, DateOnly ate)
    {
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.ToDateTime(TimeOnly.MaxValue);
        var texto = string.IsNullOrWhiteSpace(busca) ? null : $"%{busca.Trim()}%";

        var qItens = db.ItensCompra.Where(i => i.Compra!.Emissao >= inicio && i.Compra.Emissao <= fim);
        if (texto is not null) qItens = qItens.Where(i => EF.Functions.Like(i.Descricao, texto));
        var daNota = (await qItens.Select(i => new { i.Descricao, i.Quantidade, i.Unidade, i.ValorPago, i.Compra!.Emissao, i.Compra.Loja }).ToListAsync())
            .Select(i => new LinhaCompra(i.Descricao, i.Quantidade, i.Unidade, i.ValorPago, i.Emissao, i.Loja, OrigemNota));

        // Compras lançadas à mão: despesas realizadas avulsas (sem nota, sem conta do mês/parcela, sem estorno).
        var qLanc = db.Lancamentos.Where(l => l.Tipo == TipoLancamento.Despesa && l.Status == StatusLancamento.Realizado
            && l.CompraId == null && l.RegraId == null && l.EstornoDeId == null && l.Data >= de && l.Data <= ate);
        if (texto is not null) qLanc = qLanc.Where(l => EF.Functions.Like(l.Descricao, texto));
        var manuais = (await qLanc.Select(l => new { l.Descricao, l.Valor, l.Data, Conta = l.Conta!.Nome }).ToListAsync())
            .Select(l => new LinhaCompra(l.Descricao.Trim(), 1, null, l.Valor, l.Data.ToDateTime(TimeOnly.MinValue), l.Conta, OrigemLancamento));

        decimal Unitario(decimal valor, decimal qtd) => qtd > 0 ? decimal.Round(valor / qtd, 2) : valor;
        return daNota.Concat(manuais)
            .GroupBy(c => (c.Origem, Chave: c.Descricao.ToUpperInvariant()))
            .Select(g =>
            {
                var ultima = g.MaxBy(i => i.Quando)!;
                var maisBarata = g.MinBy(i => Unitario(i.Valor, i.Quantidade))!;
                var quantidade = g.Sum(i => i.Quantidade);
                return new ProdutoView(ultima.Descricao, g.Count(), quantidade, ultima.Unidade, g.Sum(i => i.Valor),
                    Unitario(g.Sum(i => i.Valor), quantidade), Unitario(ultima.Valor, ultima.Quantidade),
                    DateOnly.FromDateTime(ultima.Quando), ultima.Onde, Unitario(maisBarata.Valor, maisBarata.Quantidade), maisBarata.Onde,
                    g.Key.Origem);
            })
            .OrderByDescending(p => p.Total).ThenBy(p => p.Descricao)
            .Take(300)
            .ToList();
    }

    public const string OrigemNota = "Nota";
    public const string OrigemLancamento = "Lancamento";

    /// <summary>Uma compra (item de nota ou lançamento à mão) no formato comum da busca.</summary>
    private record LinhaCompra(string Descricao, decimal Quantidade, string? Unidade, decimal Valor, DateTime Quando, string Onde, string Origem);
}
