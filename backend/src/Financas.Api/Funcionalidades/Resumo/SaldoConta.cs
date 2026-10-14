using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Resumo;

public record SaldoConta(Guid Id, string Nome, TipoConta Tipo, string? Cor, decimal Saldo);
