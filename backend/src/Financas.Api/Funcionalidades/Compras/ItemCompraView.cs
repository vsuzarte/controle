namespace Financas.Api.Funcionalidades.Compras;

public record ItemCompraView(int Sequencia, string Descricao, string? Codigo, decimal Quantidade, string? Unidade,
    decimal ValorUnitario, decimal ValorPago, Guid CategoriaId, string Categoria, string? CategoriaCor);
