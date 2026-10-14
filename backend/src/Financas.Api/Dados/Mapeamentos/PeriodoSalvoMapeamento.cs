using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Dados.Mapeamentos;

public class PeriodoSalvoMapeamento : IEntityTypeConfiguration<PeriodoSalvo>
{
    public void Configure(EntityTypeBuilder<PeriodoSalvo> e)
    {
        e.Property(x => x.Nome).HasMaxLength(80);
    }
}
