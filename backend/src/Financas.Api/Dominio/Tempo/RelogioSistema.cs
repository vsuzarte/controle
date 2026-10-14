namespace Financas.Api.Dominio;

public class RelogioSistema(IConfiguration config) : IRelogio
{
    private readonly TimeZoneInfo _fuso = ObterFuso(config["Fuso"] ?? "America/Sao_Paulo");

    /// <summary>Usado só no ambiente de testes E2E para os cenários serem determinísticos.</summary>
    private readonly DateOnly? _dataFixa = DateOnly.TryParse(config["DataFixa"], out var d) ? d : null;

    public DateOnly Hoje => _dataFixa ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _fuso));

    private static TimeZoneInfo ObterFuso(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
    }
}
