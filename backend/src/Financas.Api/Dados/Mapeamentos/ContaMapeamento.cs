using Financas.Api.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Financas.Api.Dados.Mapeamentos;

public class ContaMapeamento : IEntityTypeConfiguration<Conta>
{
    public void Configure(EntityTypeBuilder<Conta> e)
    {
        e.Property(x => x.Nome).HasMaxLength(80);
        e.Ignore(x => x.EhCartao);
        e.Ignore(x => x.EhBeneficio);
        e.Ignore(x => x.EhDisponivel);
    }
}
