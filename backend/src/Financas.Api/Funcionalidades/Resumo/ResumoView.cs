using Financas.Api.Funcionalidades.Cartoes;
using Financas.Api.Funcionalidades.Planejamento;
using Financas.Api.Funcionalidades.Previsao;

namespace Financas.Api.Funcionalidades.Resumo;

public record ResumoView(
    DateOnly Hoje,
    decimal SaldoDisponivel,
    List<SaldoConta> Contas,
    decimal EntradasMes,
    decimal SaidasMes,
    List<Alerta> Alertas,
    List<FaturaView> FaturasAbertas,
    decimal ReceitasAReceber,
    decimal DespesasAPagar,
    decimal FaturasAPagar,
    decimal VariavelRestante,
    decimal SaldoProjetado,
    PlanejamentoView Planejamento,
    List<GastoCategoria> GastosPorCategoria,
    PrevisaoView Previsao,
    List<BeneficioView> Beneficios);
