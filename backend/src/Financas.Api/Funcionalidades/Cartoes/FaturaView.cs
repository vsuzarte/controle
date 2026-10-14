using Financas.Api.Funcionalidades.Lancamentos;

namespace Financas.Api.Funcionalidades.Cartoes;

public record FaturaView(
    Guid CartaoId,
    string Cartao,
    DateOnly Mes,
    DateOnly Fechamento,
    DateOnly Vencimento,
    decimal Total,
    decimal Pago,
    decimal Restante,
    StatusFatura Status,
    List<LancamentoView> Lancamentos,
    List<LancamentoView> Pagamentos);
