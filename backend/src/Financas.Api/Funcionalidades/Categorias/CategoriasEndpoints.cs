namespace Financas.Api.Funcionalidades.Categorias;

public static class CategoriasEndpoints
{
    public static void MapearCategorias(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/categorias");
        g.MapGet("/", (CategoriaService s) => s.Listar());
        g.MapPost("/", (CategoriaDto dto, CategoriaService s) => s.Criar(dto));
        g.MapPut("/{id:guid}", (Guid id, CategoriaDto dto, CategoriaService s) => s.Editar(id, dto));
    }
}
