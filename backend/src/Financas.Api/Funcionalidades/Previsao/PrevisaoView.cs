namespace Financas.Api.Funcionalidades.Previsao;

public record PrevisaoView(
    DateOnly Hoje,
    DateOnly Inicio,
    DateOnly Fim,
    /// <summary>false = sem salário cadastrado: o ciclo vira o mês do calendário.</summary>
    bool PorSalario,
    List<PeriodoPrevisao> Periodos,
    decimal SaldoInicio,
    decimal SaldoFim,
    decimal Entradas,
    decimal Contas,
    decimal Faturas,
    decimal DiaADia,
    /// <summary>Intervalo escolhido pelo usuário (em vez da janela automática).</summary>
    bool Personalizado = false);
