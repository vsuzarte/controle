namespace Financas.Api.Dominio;

/// <summary>Configuração do usuário (chave/valor). Ex.: até quantos pagamentos à frente a previsão mostra.</summary>
public class Configuracao
{
    public string Chave { get; set; } = "";
    public string Valor { get; set; } = "";
}
