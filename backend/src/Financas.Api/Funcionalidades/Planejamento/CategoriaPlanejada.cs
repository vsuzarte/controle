using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Planejamento;

public record CategoriaPlanejada(
    Guid CategoriaId,
    string Categoria,
    string? Cor,
    string? Icone,
    string? Grupo,
    decimal? Orcado,
    decimal? Sugerido,
    decimal Gasto,
    bool Editado,
    bool Dispensado,
    /// <summary>O que entra no resultado previsto: o maior entre orçado e já gasto (dispensado conta só o gasto).</summary>
    decimal Considerado);
