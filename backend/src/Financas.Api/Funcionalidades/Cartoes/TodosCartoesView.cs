namespace Financas.Api.Funcionalidades.Cartoes;

public record TodosCartoesView(List<CartaoResumo> Cartoes, decimal TotalAPagar, decimal TotalEmAberto, decimal LimiteTotal, decimal LimiteUsadoTotal);
