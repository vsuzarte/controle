namespace Financas.Api.Funcionalidades.Compras;

/// <summary>O que foi lido da página da nota (NFC-e). Sem dados do consumidor.</summary>
public record NotaLida(
    string Chave,
    string Loja,
    string? Cnpj,
    DateTime Emissao,
    /// <summary>Soma dos itens (antes do desconto).</summary>
    decimal ValorTotal,
    decimal Desconto,
    decimal ValorAPagar,
    List<ItemNota> Itens,
    List<PagamentoNota> Pagamentos);
