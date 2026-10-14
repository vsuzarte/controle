namespace Financas.Api.Funcionalidades.Compras;

/// <summary>
/// Um produto (pelo nome na nota ou no lançamento) no período: quanto gastou, quantas vezes comprou, quanto levou,
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
    string LojaMenorPreco,
    /// <summary>"Nota" (item de nota fiscal; Onde = loja) ou "Lancamento" (lançado à mão; Onde = conta).</summary>
    string Origem = ComprasConsultas.OrigemNota);
