namespace Financas.Tests;

/// <summary>Dias úteis e feriados nacionais.</summary>
public class CalendarioTests
{
    [Fact]
    public void Dias_uteis_pulam_fim_de_semana_e_feriados_nacionais()
    {
        Assert.Equal(D(2026, 4, 5), Calendario.Pascoa(2026));
        Assert.True(Calendario.Feriado(D(2026, 4, 3)));   // Sexta-feira Santa
        Assert.True(Calendario.Feriado(D(2026, 2, 16)));  // Carnaval
        Assert.True(Calendario.Feriado(D(2026, 11, 20))); // Consciência Negra
        // Novembro/2026: dia 1 é domingo e dia 2 é Finados -> 3,4,5,6,9.
        Assert.Equal(D(2026, 11, 9), Calendario.DataNoMes(D(2026, 11, 1), RegraVencimento.DiaUtil, 5));
        Assert.Equal(D(2026, 10, 7), Calendario.DataNoMes(D(2026, 10, 1), RegraVencimento.DiaUtil, 5));
        Assert.Equal(D(2026, 10, 30), Calendario.DataNoMes(D(2026, 10, 1), RegraVencimento.UltimoDiaUtil, null));
        Assert.Equal(D(2027, 2, 28), Calendario.DataNoMes(D(2027, 2, 1), RegraVencimento.UltimoDia, null));
    }
}
