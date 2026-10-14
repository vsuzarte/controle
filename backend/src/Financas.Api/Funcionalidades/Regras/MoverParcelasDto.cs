namespace Financas.Api.Funcionalidades.Regras;

/// <summary>Quantos meses mover todas as parcelas (positivo = para frente).</summary>
public record MoverParcelasDto(int Meses, string? Motivo = null);
