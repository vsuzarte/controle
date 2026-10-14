using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Contas;

/// <summary>Recarga automática do vale: quanto entra, quando cai e a próxima data.</summary>
public record RecargaView(
    Guid RegraId,
    decimal Valor,
    RegraVencimento Vencimento,
    int? DiaUtilNumero,
    int Dia,
    DateOnly? Proxima);
