namespace Financas.Api.Funcionalidades.Previsao;

/// <summary>Tela "Configurar" da previsão: as preferências e o efeito delas hoje (o padrão e até que data a janela vai).</summary>
public class ConfiguracaoPrevisaoService(PreferenciasPrevisao preferencias, DiasDePagamento dias, PrevisaoService previsao)
{
    public async Task<ConfigPrevisaoView> Ver()
    {
        var c = await preferencias.Ler();
        var porMes = await dias.PorMes();
        var hoje = await previsao.Obter();
        return new ConfigPrevisaoView(c.AteAntesDoPagamento, c.PularQuitado, porMes + 1, porMes, hoje.PorSalario ? hoje.Fim : null);
    }

    public async Task<ConfigPrevisaoView> Salvar(ConfigPrevisaoDto dto)
    {
        await preferencias.Salvar(dto);
        return await Ver();
    }
}
