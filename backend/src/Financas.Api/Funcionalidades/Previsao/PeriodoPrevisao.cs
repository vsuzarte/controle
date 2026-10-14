namespace Financas.Api.Funcionalidades.Previsao;

public record PeriodoPrevisao(
    DateOnly Inicio,
    DateOnly Fim,
    string Titulo,
    bool Atual,
    bool Encerrado,
    decimal SaldoInicio,
    decimal SaldoFim,
    decimal Entradas,
    decimal AReceber,
    decimal Contas,
    decimal ContasAPagar,
    decimal Faturas,
    decimal FaturasAPagar,
    decimal DiaADia,
    int DiasRestantes,
    decimal? PorDia,
    List<ItemPrevisao> Itens,
    /// <summary>Período de hoje sem nada pendente: aparece resumido e a previsão já mostra o seguinte.</summary>
    bool Quitado = false);
