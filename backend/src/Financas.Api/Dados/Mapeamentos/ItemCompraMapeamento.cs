using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Dados.Mapeamentos;

public class ItemCompraMapeamento : IEntityTypeConfiguration<ItemCompra>
{
    public void Configure(EntityTypeBuilder<ItemCompra> e)
    {
        e.Property(x => x.Descricao).HasMaxLength(200);
        e.Property(x => x.Codigo).HasMaxLength(40);
        e.Property(x => x.Unidade).HasMaxLength(10);
        // Quantidade e preço unitário com 3 casas (pesáveis): gravados em milésimos, não em centavos.
        e.Property(x => x.Quantidade).HasConversion(v => (long)decimal.Round(v * 1000m, 0, MidpointRounding.AwayFromZero), v => v / 1000m);
        e.Property(x => x.ValorUnitario).HasConversion(v => (long)decimal.Round(v * 1000m, 0, MidpointRounding.AwayFromZero), v => v / 1000m);
        e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Lancamento>().WithMany().HasForeignKey(x => x.LancamentoId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.Descricao);
        e.HasIndex(x => new { x.CompraId, x.Sequencia }).IsUnique();
    }
}
