namespace Financas.Api.Dominio;

/// <summary>Valor orçado para uma categoria de despesa variável num mês.</summary>
public class OrcamentoItem : IAuditavel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateOnly Mes { get; set; }
    public Guid CategoriaId { get; set; }
    public decimal Valor { get; set; }
    /// <summary>Valor calculado pelo sistema no momento da sugestão (referência).</summary>
    public decimal ValorSugerido { get; set; }
    public bool Editado { get; set; }
    /// <summary>Removido da previsão deste mês pelo usuário. Não reaparece sozinho.</summary>
    public bool Dispensado { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Categoria? Categoria { get; set; }
}
