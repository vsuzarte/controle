using Financas.Api.Dominio;

namespace Financas.Api.Funcionalidades.Categorias;

public record CategoriaDto(string Nome, TipoCategoria Tipo, string? Grupo, string? Cor, string? Icone, bool Ativa = true);
