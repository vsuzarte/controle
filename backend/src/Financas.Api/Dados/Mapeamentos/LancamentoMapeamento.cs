using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Dados.Mapeamentos;

public class LancamentoMapeamento : IEntityTypeConfiguration<Lancamento>
{
    public void Configure(EntityTypeBuilder<Lancamento> e)
    {
        e.Property(x => x.Descricao).HasMaxLength(200);
        e.Ignore(x => x.Valido);
        e.HasOne(x => x.Conta).WithMany().HasForeignKey(x => x.ContaId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ContaDestino).WithMany().HasForeignKey(x => x.ContaDestinoId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Regra).WithMany().HasForeignKey(x => x.RegraId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Lancamento>().WithMany().HasForeignKey(x => x.EstornoDeId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.Data);
        e.HasIndex(x => x.FaturaMes);
        // SQLite trata NULLs como distintos: lançamentos avulsos não colidem.
        e.HasIndex(x => new { x.RegraId, x.Sequencia }).IsUnique();
    }
}
