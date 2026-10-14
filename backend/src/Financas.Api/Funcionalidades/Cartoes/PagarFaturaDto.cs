namespace Financas.Api.Funcionalidades.Cartoes;

/// <summary>Juros: parte do valor que foi juros/multa (vira uma despesa separada em "Tarifas e juros").</summary>
public record PagarFaturaDto(Guid ContaOrigemId, decimal Valor, DateOnly Data, decimal? Juros = null);
