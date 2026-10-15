using Financas.Api.Funcionalidades.Lancamentos;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>
/// Uma linha do extrato: o lançamento, quanto ele mexe nesta conta (+ entra, − sai) e o saldo da conta depois dele.
/// Previstos e cancelados não mexem no saldo (Saldo nulo).
/// </summary>
public record ExtratoLinhaView(LancamentoView Lancamento, decimal Efeito, decimal? Saldo);
