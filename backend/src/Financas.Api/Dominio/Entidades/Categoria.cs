namespace Financas.Api.Dominio;

public class Categoria : IAuditavel
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Nome { get; set; } = "";
    public TipoCategoria Tipo { get; set; }
    public string? Grupo { get; set; }
    public string? Cor { get; set; }
    public string? Icone { get; set; }
    public bool Ativa { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
