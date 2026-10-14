namespace Financas.Api.Funcionalidades.Compras;

/// <summary>Importar a nota: o link (o servidor lê a nota de novo), com que conta pagou, o nome da loja e a categoria de cada item.</summary>
public record ImportarCompraDto(string Url, Guid ContaId, string? Loja, List<CategoriaDoItemDto> Itens);
