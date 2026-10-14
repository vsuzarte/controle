using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Contas;

public record ContaDto(string Nome, TipoConta Tipo, decimal SaldoInicial, string? Cor, int? DiaFechamento, int? DiaVencimento, bool Ativa = true, decimal? Limite = null);
