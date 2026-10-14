using System.Globalization;
using System.Text;

namespace Financas.Api.Dominio;

/// <summary>Comparação de textos como uma pessoa faria: sem diferenciar acento, maiúscula e espaços extras.</summary>
public static class Texto
{
    /// <summary>"Pão  Francês" → "pao frances".</summary>
    public static string Normalizar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s.Trim().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
