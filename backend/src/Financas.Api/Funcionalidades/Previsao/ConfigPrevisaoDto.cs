namespace Financas.Api.Funcionalidades.Previsao;

/// <summary>
/// Regra da janela da previsão e dos vencimentos da tela inicial.
/// AteAntesDoPagamento: mostrar tudo que vence até antes do N-ésimo próximo pagamento (nulo = padrão: um mês de pagamentos).
/// </summary>
public record ConfigPrevisaoDto(int? AteAntesDoPagamento, bool PularQuitado);
