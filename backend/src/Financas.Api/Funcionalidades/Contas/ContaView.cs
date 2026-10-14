using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>Conta com o saldo de hoje; nos cartões, quanto do limite está em uso; nos vales, a recarga.</summary>
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
    decimal? LimiteUsado,
    /// <summary>Só vale: a recarga automática (nulo = sem recarga cadastrada).</summary>
    RecargaView? Recarga = null);
