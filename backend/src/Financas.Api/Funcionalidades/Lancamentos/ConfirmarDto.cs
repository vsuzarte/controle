using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Lancamentos;

/// <summary>Juros: quanto do valor pago foi juros/multa por atraso.</summary>
public record ConfirmarDto(DateOnly Data, decimal Valor, Guid? ContaId, FormaPagamento? FormaPagamento, decimal? Juros = null);
