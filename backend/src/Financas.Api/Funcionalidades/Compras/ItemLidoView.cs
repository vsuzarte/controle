namespace Financas.Api.Funcionalidades.Compras;

/// <summary>Item para conferir: valor com a parte do desconto (ValorPago) e a categoria sugerida.</summary>
public record ItemLidoView(
    int Sequencia,
    string Descricao,
    string? Codigo,
    decimal Quantidade,
    string? Unidade,
    decimal ValorUnitario,
    decimal ValorTotal,
    decimal ValorPago,
    Guid CategoriaId);
