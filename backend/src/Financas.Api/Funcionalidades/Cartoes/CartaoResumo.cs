namespace Financas.Api.Funcionalidades.Cartoes;

public record CartaoResumo(Guid Id, string Nome, string? Cor, int DiaFechamento, int DiaVencimento,
    decimal? Limite, decimal LimiteUsado, decimal? LimiteDisponivel, List<FaturaView> APagar, FaturaView? Aberta);
