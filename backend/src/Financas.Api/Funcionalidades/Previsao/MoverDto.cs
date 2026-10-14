namespace Financas.Api.Funcionalidades.Previsao;

/// <summary>Mover no quadro: uma conta (LancamentoId) ou fatura (CartaoId + FaturaMes) para PagarEm; nulo = voltar ao automático.</summary>
public record MoverDto(Guid? LancamentoId, Guid? CartaoId, DateOnly? FaturaMes, DateOnly? PagarEm);
