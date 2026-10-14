namespace Financas.Api.Dominio;

/// <summary>Como achar o dia do vencimento em regras mensais. Nulo = dia fixo (o dia de DataInicio).</summary>
public enum RegraVencimento { DiaFixo, DiaUtil, UltimoDia, UltimoDiaUtil }
