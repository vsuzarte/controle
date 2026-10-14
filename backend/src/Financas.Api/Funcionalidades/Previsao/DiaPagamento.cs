namespace Financas.Api.Funcionalidades.Previsao;

/// <summary>Um dia em que entra dinheiro de pagamento (ex.: salário do 5º dia útil) e a regra que o gera.</summary>
public record DiaPagamento(DateOnly Data, string Nome, Guid RegraId);
