using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Dados.Mapeamentos;

public class PagamentoPlanejadoMapeamento : IEntityTypeConfiguration<PagamentoPlanejado>
{
    public void Configure(EntityTypeBuilder<PagamentoPlanejado> e)
    {
        e.HasIndex(x => x.LancamentoId).IsUnique();
        e.HasIndex(x => new { x.CartaoId, x.FaturaMes }).IsUnique();
    }
}
