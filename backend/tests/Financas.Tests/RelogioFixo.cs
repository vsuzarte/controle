namespace Financas.Tests;

/// <summary>"Hoje" controlado pelo teste.</summary>
public class RelogioFixo(DateOnly hoje) : IRelogio
{
    public DateOnly Hoje { get; set; } = hoje;
}
