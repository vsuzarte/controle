namespace Financas.Api.Dominio;

/// <summary>Um item da nota: o que foi comprado, quanto e por quanto (com a parte do desconto já abatida).</summary>
public class ItemCompra
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompraId { get; set; }
    /// <summary>Posição do item na nota (1..N).</summary>
    public int Sequencia { get; set; }
    public string Descricao { get; set; } = "";
    /// <summary>Código do produto na loja ou código de barras (EAN).</summary>
    public string? Codigo { get; set; }
    /// <summary>Gravado em milésimos (pesáveis: 0,458 kg).</summary>
    public decimal Quantidade { get; set; }
    public string? Unidade { get; set; }
    /// <summary>Gravado em milésimos (preço por kg pode ter 3 casas).</summary>
    public decimal ValorUnitario { get; set; }
    /// <summary>Valor do item na nota.</summary>
    public decimal ValorTotal { get; set; }
    /// <summary>Valor do item com a parte proporcional do desconto da nota: a soma bate com o total pago.</summary>
    public decimal ValorPago { get; set; }
    public Guid CategoriaId { get; set; }
    /// <summary>Lançamento (da categoria do item) que contém este item.</summary>
    public Guid? LancamentoId { get; set; }

    public Compra? Compra { get; set; }
    public Categoria? Categoria { get; set; }
}
