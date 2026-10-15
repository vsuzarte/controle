using Financas.Api.Funcionalidades.Cartoes;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>Resumo do cartão no extrato: limite, quanto está usado, quanto está livre e a fatura em aberto.</summary>
public record CartaoNoExtratoView(decimal? Limite, decimal LimiteUsado, decimal? Disponivel, FaturaView? FaturaAberta, FaturaView? FaturaAPagar);
