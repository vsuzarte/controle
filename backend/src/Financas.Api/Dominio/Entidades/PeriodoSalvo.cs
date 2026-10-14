namespace Financas.Api.Dominio;

/// <summary>Intervalo de datas que o usuário salvou para ver na previsão (preferência de tela, pode ser excluído).</summary>
public class PeriodoSalvo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Nome { get; set; } = "";
    public DateOnly De { get; set; }
    public DateOnly Ate { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
