namespace Financas.Api.Funcionalidades.Relatorios;

/// <summary>Uma linha do comparativo: um valor por mês, na mesma ordem de <see cref="ComparativoView.Meses"/>.</summary>
public record SerieMensal(Guid? Id, string Nome, string? Cor, string? Icone, List<decimal> Valores);
