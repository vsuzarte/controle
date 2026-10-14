namespace Financas.Api.Dominio;

/// <summary>Beneficio = vale-refeição/alimentação: tem saldo, mas só serve para compras (não paga contas).</summary>
public enum TipoConta { Corrente, Poupanca, Dinheiro, CarteiraDigital, CartaoCredito, Beneficio }
