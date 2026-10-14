using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Previsao;

/// <summary>Preferências da janela da previsão, guardadas na tabela de configurações (chave/valor).</summary>
public class PreferenciasPrevisao(AppDbContext db)
{
    private const string ChaveAteAntes = "Previsao.AteAntesDoPagamento";
    private const string ChavePularQuitado = "Previsao.PularQuitado";

    public async Task<ConfigPrevisaoDto> Ler()
    {
        var c = await db.Configuracoes.Where(x => x.Chave.StartsWith("Previsao.")).ToDictionaryAsync(x => x.Chave, x => x.Valor);
        return new ConfigPrevisaoDto(
            c.TryGetValue(ChaveAteAntes, out var n) && int.TryParse(n, out var v) ? v : null,
            !c.TryGetValue(ChavePularQuitado, out var p) || p != "false");
    }

    public async Task Salvar(ConfigPrevisaoDto dto)
    {
        if (dto.AteAntesDoPagamento is not null and not (>= 1 and <= 24))
            throw new RegraNegocioException("Escolha de 1 a 24 pagamentos à frente.");
        await Gravar(ChaveAteAntes, dto.AteAntesDoPagamento?.ToString());
        await Gravar(ChavePularQuitado, dto.PularQuitado ? "true" : "false");
        await db.SaveChangesAsync();
    }

    private async Task Gravar(string chave, string? valor)
    {
        var c = await db.Configuracoes.FindAsync(chave);
        if (valor is null) { if (c is not null) db.Configuracoes.Remove(c); return; }
        if (c is null) db.Configuracoes.Add(new Configuracao { Chave = chave, Valor = valor });
        else c.Valor = valor;
    }
}
