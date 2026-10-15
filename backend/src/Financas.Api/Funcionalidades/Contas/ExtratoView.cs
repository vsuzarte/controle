using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>Extrato de uma conta no mês: saldo no início, o que entrou e saiu, saldo no fim e cada movimento.</summary>
public record ExtratoView(
    Guid ContaId,
    string Conta,
    TipoConta Tipo,
    DateOnly Mes,
    decimal SaldoInicio,
    decimal Entrou,
    decimal Saiu,
    decimal SaldoFim,
    /// <summary>Saldo no fim do mês contando também os previstos ainda não pagos/recebidos.</summary>
    decimal SaldoPrevisto,
    List<ExtratoLinhaView> Linhas);
