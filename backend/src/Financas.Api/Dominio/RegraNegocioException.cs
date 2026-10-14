namespace Financas.Api.Dominio;

/// <summary>Erro de regra de negócio: a API responde 400 com a mensagem.</summary>
public class RegraNegocioException(string mensagem) : Exception(mensagem);
