using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Financas.Api.Dados;

/// <summary>Grava decimal (reais) como inteiro em centavos.</summary>
public class CentavosConverter() : ValueConverter<decimal, long>(
    v => (long)decimal.Round(v * 100m, 0, MidpointRounding.AwayFromZero),
    v => v / 100m);
