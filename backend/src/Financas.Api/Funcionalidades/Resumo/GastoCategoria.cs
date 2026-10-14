using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Resumo;

/// <summary>Gasto = tudo da categoria no mês; Variavel = só o do dia a dia (o que o orçamento controla).</summary>
public record GastoCategoria(Guid CategoriaId, string Categoria, string? Cor, decimal Gasto, decimal Variavel, decimal? Orcado);
