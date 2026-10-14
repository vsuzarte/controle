namespace Financas.Api.Funcionalidades.Compras;

/// <summary>Nota lida e pronta para conferir: loja, data, forma de pagamento, conta sugerida e itens com categoria.</summary>
public record LeituraNotaView(
    string Url,
    string Chave,
    /// <summary>Nome a usar no lançamento: o apelido usado da última vez para esta loja, ou o nome da nota.</summary>
    string Loja,
    string LojaNaNota,
    string? Cnpj,
    DateOnly Data,
    DateTime Emissao,
    decimal ValorTotal,
    decimal Desconto,
    decimal ValorAPagar,
    string? FormaPagamento,
    Guid? ContaSugeridaId,
    Guid? CategoriaPadraoId,
    List<ItemLidoView> Itens,
    /// <summary>Preenchido se esta nota já foi importada (não entra duas vezes).</summary>
    Guid? CompraExistenteId);
