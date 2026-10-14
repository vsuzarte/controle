using Financas.Api.Dados;
using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Financas.Api.Funcionalidades.Categorias;

/// <summary>Cadastro de categorias de receita e despesa. O nome é único por tipo.</summary>
public class CategoriaService(AppDbContext db)
{
    public Task<List<Categoria>> Listar() =>
        db.Categorias.OrderBy(c => c.Tipo).ThenBy(c => c.Grupo).ThenBy(c => c.Nome).ToListAsync();

    public async Task<Categoria> Criar(CategoriaDto dto)
    {
        var c = new Categoria();
        await Aplicar(c, dto);
        db.Categorias.Add(c);
        await db.SaveChangesAsync();
        return c;
    }

    public async Task<Categoria> Editar(Guid id, CategoriaDto dto)
    {
        var c = await db.Categorias.FindAsync(id) ?? throw new RegraNegocioException("Categoria não encontrada.");
        if (c.Tipo != dto.Tipo && await db.Lancamentos.AnyAsync(l => l.CategoriaId == id))
            throw new RegraNegocioException("Não é possível mudar o tipo de uma categoria já usada.");
        await Aplicar(c, dto);
        await db.SaveChangesAsync();
        return c;
    }

    private async Task Aplicar(Categoria c, CategoriaDto dto)
    {
        var nome = Validar.Texto(dto.Nome, "Nome");
        if (await db.Categorias.AnyAsync(x => x.Id != c.Id && x.Nome == nome && x.Tipo == dto.Tipo))
            throw new RegraNegocioException($"Já existe a categoria '{nome}'.");
        c.Nome = nome;
        c.Tipo = dto.Tipo;
        c.Grupo = Validar.Opcional(dto.Grupo);
        c.Cor = Validar.Opcional(dto.Cor);
        c.Icone = Validar.Opcional(dto.Icone);
        c.Ativa = dto.Ativa;
    }
}
