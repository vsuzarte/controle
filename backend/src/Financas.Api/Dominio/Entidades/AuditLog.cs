namespace Financas.Api.Dominio;

public class AuditLog
{
    public long Id { get; set; }
    public string Entidade { get; set; } = "";
    public Guid EntidadeId { get; set; }
    public string Operacao { get; set; } = "";
    public string? Antes { get; set; }
    public string? Depois { get; set; }
    public string? Motivo { get; set; }
    public DateTime DataHora { get; set; } = DateTime.UtcNow;
}
