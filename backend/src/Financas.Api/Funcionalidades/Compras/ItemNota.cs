namespace Financas.Api.Funcionalidades.Compras;

/// <summary>Item como veio na nota.</summary>
public record ItemNota(int Sequencia, string Descricao, string? Codigo, decimal Quantidade, string? Unidade, decimal ValorUnitario, decimal ValorTotal);
