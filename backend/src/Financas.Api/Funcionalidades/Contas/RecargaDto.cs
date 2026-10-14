using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>
/// Recarga do vale. Valor nulo ou zero = parar a recarga. Vencimento DiaFixo usa <c>Dia</c>;
/// DiaUtil usa <c>DiaUtilNumero</c> (ex.: 1º dia útil); UltimoDia/UltimoDiaUtil não precisam de dia.
/// </summary>
public record RecargaDto(decimal? Valor, RegraVencimento Vencimento, int? DiaUtilNumero, int? Dia);
