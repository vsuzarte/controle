using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Dados.Mapeamentos;

public class CompraMapeamento : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> e)
    {
        e.Property(x => x.ChaveAcesso).HasMaxLength(44);
        e.HasIndex(x => x.ChaveAcesso).IsUnique();
        e.Property(x => x.Loja).HasMaxLength(200);
        e.Property(x => x.Cnpj).HasMaxLength(20);
        e.Property(x => x.FormaPagamentoNota).HasMaxLength(80);
        e.HasOne<Conta>().WithMany().HasForeignKey(x => x.ContaId).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Itens).WithOne(x => x.Compra).HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Restrict);
    }
}
