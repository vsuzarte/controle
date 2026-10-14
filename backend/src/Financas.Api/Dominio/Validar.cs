namespace Financas.Api.Dominio;

/// <summary>Validações de entrada usadas por todas as funcionalidades. Erro vira mensagem para o usuário.</summary>
internal static class Validar
{
    public static string Texto(string? s, string campo) =>
        string.IsNullOrWhiteSpace(s) ? throw new RegraNegocioException($"Preencha o campo {campo}.") : s.Trim();

    public static string? Opcional(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static decimal? Limite(decimal? v)
    {
        if (v is null) return null;
        var arredondado = decimal.Round(v.Value, 2, MidpointRounding.AwayFromZero);
        return arredondado < 0 ? throw new RegraNegocioException("O limite não pode ser negativo.") : arredondado;
    }

    /// <summary>Juros/multa embutidos num pagamento: maior que zero e menor que o valor pago.</summary>
    public static decimal? Juros(decimal? juros, decimal valorPago)
    {
        if (juros is null || juros == 0) return null;
        var j = decimal.Round(juros.Value, 2, MidpointRounding.AwayFromZero);
        if (j < 0) throw new RegraNegocioException("Os juros não podem ser negativos.");
        if (j >= valorPago) throw new RegraNegocioException("Os juros devem ser menores que o valor pago.");
        return j;
    }

    public static decimal ValorPositivo(decimal v)
    {
        var arredondado = decimal.Round(v, 2, MidpointRounding.AwayFromZero);
        if (arredondado <= 0) throw new RegraNegocioException("O valor deve ser maior que zero.");
        if (arredondado > 99_999_999m) throw new RegraNegocioException("Valor muito alto.");
        return arredondado;
    }
}
