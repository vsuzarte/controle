namespace Financas.Api.Funcionalidades.Lancamentos;

public record EstornoDto(decimal Valor, DateOnly Data, string? Motivo);
