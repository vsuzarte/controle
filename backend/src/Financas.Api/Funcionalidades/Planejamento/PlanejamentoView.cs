using Financas.Api.Funcionalidades.Lancamentos;

namespace Financas.Api.Funcionalidades.Planejamento;

public record PlanejamentoView(
    DateOnly Mes,
    List<LancamentoView> Receitas,
    List<LancamentoView> Compromissos,
    List<CategoriaPlanejada> Variaveis,
    /// <summary>Receitas em dinheiro que paga contas (sem o vale).</summary>
    decimal TotalReceitas,
    decimal ReceitasRecebidas,
    decimal TotalCompromissos,
    decimal CompromissosPagos,
    decimal TotalVariaveis,
    decimal GastoVariavel,
    /// <summary>Considera o vale: ele cobre as compras do dia a dia feitas com ele.</summary>
    decimal ResultadoPrevisto,
    /// <summary>Recargas do vale no mês: aparecem nas receitas, mas somadas à parte (só servem para compras).</summary>
    decimal ReceitasVale = 0);
