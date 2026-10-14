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
            _ when r.Vencimento is null or RegraVencimento.DiaFixo => r.DataInicio.AddMonths(passo),
            _ => DataNoMes(InicioMes(r.DataInicio).AddMonths(passo), r.Vencimento.Value, r.DiaUtilNumero),
        };
    }

    /// <summary>Data do vencimento dentro do mês, para as regras "N-ésimo dia útil", "último dia" e "último dia útil".</summary>
    public static DateOnly DataNoMes(DateOnly mes, RegraVencimento regra, int? diaUtil)
    {
        var inicio = InicioMes(mes);
        var fim = FimMes(mes);
        switch (regra)
        {
            case RegraVencimento.UltimoDia:
                return fim;
            case RegraVencimento.UltimoDiaUtil:
                var d = fim;
                while (!DiaUtil(d)) d = d.AddDays(-1);
                return d;
            case RegraVencimento.DiaUtil:
                var n = Math.Clamp(diaUtil ?? 1, 1, 23);
                var data = inicio.AddDays(-1);
                for (var contados = 0; contados < n;)
                {
                    data = data.AddDays(1);
                    if (DiaUtil(data)) contados++;
                }
                return data;
            default:
                return inicio;
        }
    }

    /// <summary>
    /// Até quando dá para pagar sem atraso: conta/boleto que vence em fim de semana ou feriado
    /// pode ser paga no próximo dia útil.
    /// </summary>
    public static DateOnly ProximoDiaUtil(DateOnly d)
    {
        while (!DiaUtil(d)) d = d.AddDays(1);
        return d;
    }

    /// <summary>Segunda a sexta, fora os feriados nacionais (dias em que os bancos não abrem).</summary>
    public static bool DiaUtil(DateOnly d) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Feriado(d);

    public static bool Feriado(DateOnly d)
    {
        if ((d.Month, d.Day) is (1, 1) or (4, 21) or (5, 1) or (9, 7) or (10, 12) or (11, 2) or (11, 15) or (11, 20) or (12, 25))
            return true;
        var pascoa = Pascoa(d.Year);
        // Móveis: Carnaval (segunda e terça), Sexta-feira Santa e Corpus Christi.
        return d == pascoa.AddDays(-48) || d == pascoa.AddDays(-47) || d == pascoa.AddDays(-2) || d == pascoa.AddDays(60);
    }

    /// <summary>Domingo de Páscoa (algoritmo de Meeus/Jones/Butcher).</summary>
    public static DateOnly Pascoa(int ano)
    {
        int a = ano % 19, b = ano / 100, c = ano % 100, d = b / 4, e = b % 4, f = (b + 8) / 25, g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30, i = c / 4, k = c % 4, l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451, mes = (h + l - 7 * m + 114) / 31, dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(ano, mes, dia);
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
