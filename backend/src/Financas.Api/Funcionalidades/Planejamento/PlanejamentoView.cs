using Financas.Api.Funcionalidades.Lancamentos;

namespace Financas.Api.Funcionalidades.Planejamento;

public record PlanejamentoView(
    DateOnly Mes,
    List<LancamentoView> Receitas,
    List<LancamentoView> Compromissos,
    List<CategoriaPlanejada> Variaveis,
    decimal TotalReceitas,
    decimal ReceitasRecebidas,
    decimal TotalCompromissos,
    decimal CompromissosPagos,
    decimal TotalVariaveis,
    decimal GastoVariavel,
    decimal ResultadoPrevisto);
