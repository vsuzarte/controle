namespace Financas.Api.Funcionalidades.Compras;

/// <summary>Uma compra com todos os itens (abre a partir do lançamento).</summary>
public record CompraView(Guid Id, string Loja, DateTime Emissao, decimal Total, decimal Desconto, string? FormaPagamentoNota,
    Guid ContaId, string Conta, List<ItemCompraView> Itens);
