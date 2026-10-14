using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Dados.Mapeamentos;

public class RegraMapeamento : IEntityTypeConfiguration<Regra>
{
    public void Configure(EntityTypeBuilder<Regra> e)
    {
        e.Property(x => x.Descricao).HasMaxLength(200);
        e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Conta).WithMany().HasForeignKey(x => x.ContaId).OnDelete(DeleteBehavior.Restrict);
    }
}
