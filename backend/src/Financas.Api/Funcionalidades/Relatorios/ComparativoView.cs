using Financas.Api.Funcionalidades.Lancamentos;

namespace Financas.Api.Funcionalidades.Relatorios;

public record ComparativoView(
    List<DateOnly> Meses,
    SerieMensal Total,
    List<SerieMensal> Categorias,
    List<SerieMensal> ContasFixas,
    List<SerieMensal> Cartoes,
    SerieMensal Juros,
    List<LancamentoView> JurosDetalhe);
