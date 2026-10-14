using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Previsao;

/// <summary>Intervalos de datas salvos para a previsão. São preferência de tela: podem ser editados e excluídos.</summary>
public class PeriodoSalvoService(AppDbContext db)
{
    public Task<List<PeriodoSalvo>> Listar() =>
        db.PeriodosSalvos.OrderBy(p => p.De).ThenBy(p => p.Nome).ToListAsync();

    public async Task<PeriodoSalvo> Criar(PeriodoSalvoDto dto)
    {
        var p = new PeriodoSalvo();
        Aplicar(p, dto);
        db.PeriodosSalvos.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    public async Task<PeriodoSalvo> Editar(Guid id, PeriodoSalvoDto dto)
    {
        var p = await Obter(id);
        Aplicar(p, dto);
        await db.SaveChangesAsync();
        return p;
    }

    public async Task Excluir(Guid id)
    {
        db.PeriodosSalvos.Remove(await Obter(id));
        await db.SaveChangesAsync();
    }

    private async Task<PeriodoSalvo> Obter(Guid id) =>
        await db.PeriodosSalvos.FindAsync(id) ?? throw new RegraNegocioException("Período não encontrado.");

    private static void Aplicar(PeriodoSalvo p, PeriodoSalvoDto dto)
    {
        if (dto.Ate < dto.De) throw new RegraNegocioException("A data final do período deve ser depois da inicial.");
        if (dto.Ate > dto.De.AddYears(1)) throw new RegraNegocioException("Escolha um período de até um ano.");
        p.Nome = Validar.Texto(dto.Nome, "Nome");
        p.De = dto.De;
        p.Ate = dto.Ate;
    }
}
