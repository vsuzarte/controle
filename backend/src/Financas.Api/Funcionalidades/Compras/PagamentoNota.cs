namespace Financas.Api.Funcionalidades.Compras;

/// <summary>Forma de pagamento como veio na nota (ex.: "Cartão de Débito").</summary>
public record PagamentoNota(string Forma, decimal Valor);
