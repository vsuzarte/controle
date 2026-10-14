using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>Conta com o saldo de hoje e, nos cartões, quanto do limite está em uso.</summary>
public record ContaView(
    Guid Id,
    string Nome,
    TipoConta Tipo,
    decimal SaldoInicial,
    string? Cor,
    int? DiaFechamento,
    int? DiaVencimento,
    bool Ativa,
    decimal? Limite,
    decimal Saldo,
    decimal? LimiteUsado);
