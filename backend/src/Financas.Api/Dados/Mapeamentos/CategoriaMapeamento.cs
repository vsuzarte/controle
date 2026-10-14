using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Dados.Mapeamentos;

public class CategoriaMapeamento : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> e)
    {
        e.Property(x => x.Nome).HasMaxLength(80);
        e.HasIndex(x => new { x.Nome, x.Tipo }).IsUnique();
    }
}
