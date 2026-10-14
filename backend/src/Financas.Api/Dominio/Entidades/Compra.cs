namespace Financas.Api.Dominio;

/// <summary>
/// Compra importada de uma nota fiscal (NFC-e). Os itens ficam guardados para a busca "o que comprei";
/// o dinheiro entra como lançamentos (um por categoria), ligados a ela por <see cref="Lancamento.CompraId"/>.
/// Dados do consumidor (CPF, nome) não são guardados.
/// </summary>
public class Compra : IAuditavel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>Chave de acesso da nota (44 dígitos). Única: a mesma nota não entra duas vezes.</summary>
    public string ChaveAcesso { get; set; } = "";
    public string Loja { get; set; } = "";
    public string? Cnpj { get; set; }
    /// <summary>Data e hora de emissão da nota (horário local).</summary>
    public DateTime Emissao { get; set; }
    /// <summary>Valor pago (já com os descontos).</summary>
    public decimal Total { get; set; }
    public decimal Desconto { get; set; }
    /// <summary>Forma de pagamento como veio na nota (ex.: "Cartão de Débito").</summary>
    public string? FormaPagamentoNota { get; set; }
    public Guid ContaId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public List<ItemCompra> Itens { get; set; } = [];
}
