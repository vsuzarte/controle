namespace Financas.Api.Funcionalidades.Compras;

/// <summary>
/// Um produto (pelo nome na nota) no período: quanto gastou, quantas vezes comprou, quanto levou,
/// preço médio por unidade, último preço e onde estava mais barato.
/// </summary>
public record ProdutoView(
    string Descricao,
    int Vezes,
    decimal Quantidade,
    string? Unidade,
    decimal Total,
    decimal PrecoMedio,
    decimal UltimoPreco,
    DateOnly UltimaCompra,
    string UltimaLoja,
    decimal MenorPreco,
    string LojaMenorPreco);
