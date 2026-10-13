namespace Financas.Api.Dominio;

/// <summary>Fornece "hoje" no fuso do usuário. Abstraído para os testes controlarem a data.</summary>
public interface IRelogio
{
    DateOnly Hoje { get; }
}

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

public static class Calendario
{
    public static DateOnly InicioMes(DateOnly d) => new(d.Year, d.Month, 1);
    public static DateOnly FimMes(DateOnly d) => InicioMes(d).AddMonths(1).AddDays(-1);

    /// <summary>Dia do mês limitado ao último dia (ex.: dia 31 em fevereiro vira 28/29).</summary>
    public static DateOnly DiaNoMes(DateOnly mes, int dia) =>
        new(mes.Year, mes.Month, Math.Min(dia, DateTime.DaysInMonth(mes.Year, mes.Month)));

    public static DateOnly ParseMes(string mes) =>
        DateOnly.TryParseExact(mes + "-01", "yyyy-MM-dd", out var d)
            ? d
            : throw new RegraNegocioException($"Mês inválido: '{mes}'. Use o formato AAAA-MM.");

    /// <summary>Data da n-ésima ocorrência (1-based), sempre calculada a partir do início para não acumular desvios.</summary>
    public static DateOnly Ocorrencia(Regra r, int n)
    {
        var passo = (n - 1) * Math.Max(1, r.Intervalo);
        return r.Frequencia switch
        {
            Frequencia.Semanal => r.DataInicio.AddDays(7 * passo),
            Frequencia.Anual => r.DataInicio.AddYears(passo),
            _ => r.DataInicio.AddMonths(passo),
        };
    }

    // ---------- Cartão de crédito ----------
    // A fatura é identificada pelo mês do seu vencimento (dia 1 desse mês).
    // Compras feitas no dia do fechamento ou depois entram na fatura seguinte.

    public static DateOnly FaturaDaCompra(Conta cartao, DateOnly dataCompra)
    {
        var (fechamento, vencimento) = DiasCartao(cartao);
        var fechamentoNoMes = DiaNoMes(dataCompra, fechamento);
        var mesFechamento = dataCompra >= fechamentoNoMes
            ? InicioMes(dataCompra).AddMonths(1)
            : InicioMes(dataCompra);
        return vencimento > fechamento ? mesFechamento : mesFechamento.AddMonths(1);
    }

    public static DateOnly FechamentoDaFatura(Conta cartao, DateOnly faturaMes)
    {
        var (fechamento, vencimento) = DiasCartao(cartao);
        var mesFechamento = vencimento > fechamento ? faturaMes : faturaMes.AddMonths(-1);
        return DiaNoMes(mesFechamento, fechamento);
    }

    public static DateOnly VencimentoDaFatura(Conta cartao, DateOnly faturaMes) =>
        DiaNoMes(faturaMes, DiasCartao(cartao).vencimento);

    private static (int fechamento, int vencimento) DiasCartao(Conta c) =>
        c.DiaFechamento is { } f && c.DiaVencimento is { } v
            ? (f, v)
            : throw new RegraNegocioException($"O cartão '{c.Nome}' está sem dia de fechamento/vencimento.");
}
