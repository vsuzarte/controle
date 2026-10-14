namespace Financas.Api.Funcionalidades.Previsao;

/// <summary>Configuração atual mais o que ajuda a entender o efeito: o padrão e até que data a janela vai hoje.</summary>
public record ConfigPrevisaoView(int? AteAntesDoPagamento, bool PularQuitado, int Padrao, int PagamentosPorMes, DateOnly? AteHoje);
