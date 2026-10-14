namespace Financas.Api.Dominio;

/// <summary>Fornece "hoje" no fuso do usuário. Abstraído para os testes controlarem a data.</summary>
public interface IRelogio
{
    DateOnly Hoje { get; }
}
