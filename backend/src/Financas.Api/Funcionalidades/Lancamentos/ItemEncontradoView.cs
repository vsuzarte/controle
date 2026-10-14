namespace Financas.Api.Funcionalidades.Lancamentos;

/// <summary>Item de nota fiscal que bateu com a busca, mostrado embaixo do lançamento.</summary>
public record ItemEncontradoView(string Descricao, decimal Quantidade, string? Unidade, decimal ValorPago);
